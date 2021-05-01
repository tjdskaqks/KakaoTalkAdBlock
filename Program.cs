using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace KakaoTalkAdBlock
{
    class Program
    {
        #region WinAPI
        [DllImport("user32.dll")]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        static extern bool EnumChildWindows(IntPtr WindowHandle, EnumWindowProcess Callback, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr ihwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = false)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = false)]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        static class SetWindowPosFlags
        {
            public const int SWP_NOMOVE = 0x0002;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        delegate bool EnumWindowProcess(IntPtr Handle, IntPtr Parameter);

        static bool EnumWindow(IntPtr Handle, IntPtr Parameter)
        {
            List<IntPtr> target = (List<IntPtr>)GCHandle.FromIntPtr(Parameter).Target;
            if (target == null)
                throw new Exception("GCHandle Target could not be cast as List(Of IntPtr)");
            target.Add(Handle);
            return true;
        }
        #endregion

        #region Global Variables
        static string[] KAKAOTALK_TITLE_STRING = { "카카오톡", "Kakaotalk", "カカオトーク" };

        static volatile List<IntPtr> hwnd = new List<IntPtr>();
        static IntPtr popUpHwnd = IntPtr.Zero;
        static Container container = new Container();

        static readonly object hwndLock = new object();

        const int UPDATE_RATE = 100;

        static uint WM_CLOSE = 0x10;
        #endregion

    
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new TextWriterTraceListener("kakaotalkadblock-logs.txt"));
            Trace.AutoFlush = true;

            watchProcess();
            removeAd();
        }

        static void watchProcess()
        {
            // hwnd must not be changed while removing ad
            lock (hwndLock)
            {
                hwnd.Clear();

                // find kakaotalk window
                foreach (string title in KAKAOTALK_TITLE_STRING)
                {
                    IntPtr tmpHwnd = IntPtr.Zero;

                    while ((tmpHwnd = FindWindowEx(IntPtr.Zero, tmpHwnd, null, title)) != IntPtr.Zero)
                    {
                        Trace.TraceInformation(String.Format("Window Found: Title: '{0}', handle: '{1}'", title, tmpHwnd));
                        hwnd.Add(tmpHwnd);
                    }
                }
            }

            var processes = Process.GetProcessesByName("kakaotalk");
            foreach (Process proc in processes)
            {
                Trace.TraceInformation(String.Format("Window Found by GetProcessesByName: Title: '{0}', handle: '{1}', MainWindowHandle '{2}'", proc.MainWindowTitle, proc.Handle, proc.MainWindowHandle));
            }
        }

        static void removeAd()
        {
            Trace.TraceInformation("=================removeAd()=================");
            var localHwnd = new List<IntPtr>();
            var childHwnds = new List<IntPtr>();
            var windowClass = new StringBuilder(256);
            var windowCaption = new StringBuilder(256);
            var windowParentCaption = new StringBuilder(256);

            // hwnd must not be changed while removing ad
            lock (hwndLock)
            {
                foreach (IntPtr wnd in hwnd)
                {
                    Trace.TraceInformation(String.Format("KakaoTalk Handle: {0}", wnd));

                    childHwnds.Clear();
                    var gcHandle = GCHandle.Alloc(childHwnds);

                    // get handles from child windows
                    try
                    {
                        EnumChildWindows(wnd, new EnumWindowProcess(EnumWindow), GCHandle.ToIntPtr(gcHandle));
                    }
                    finally
                    {
                        if (gcHandle.IsAllocated) gcHandle.Free();
                    }

                    // get rect of kakaotalk
                    RECT rectKakaoTalk = new RECT();
                    GetWindowRect(wnd, out rectKakaoTalk);

                    // iterate all child windows of kakaotalk
                    foreach (var childHwnd in childHwnds)
                    {
                        GetClassName(childHwnd, windowClass, windowClass.Capacity);
                        GetWindowText(childHwnd, windowCaption, windowCaption.Capacity);

                        Trace.TraceInformation(String.Format("ChildWindowHandle: {0}", childHwnd));
                        Trace.TraceInformation(String.Format("ChildWindowClass: {0}", windowClass));
                        Trace.TraceInformation(String.Format("ChildWindowCaption: {0}", windowCaption));

                        // hide ad
                        if (windowClass.ToString().Equals("EVA_Window"))
                        {
                            Trace.TraceInformation(String.Format("EVA_Window Found: {0}", childHwnd));
                            GetWindowText(GetParent(childHwnd), windowParentCaption, windowParentCaption.Capacity);

                            Trace.TraceInformation(String.Format("EVA_Window ParentCaption: {0}", windowParentCaption));

                            Trace.TraceInformation(String.Format("getParent ({0}) == wnd {1} : {2}", GetParent(childHwnd), wnd, GetParent(childHwnd) == wnd));
                            Trace.TraceInformation(String.Format("ParentCaption sartswith LockModeView", windowParentCaption.ToString().StartsWith("LockModeView")));


                            if (GetParent(childHwnd) == wnd || windowParentCaption.ToString().StartsWith("LockModeView"))
                            {
                                ShowWindow(childHwnd, 0);
                                SetWindowPos(childHwnd, IntPtr.Zero, 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE);
                                Trace.TraceInformation("[HIDE AD]");

                            }
                        }

                        // remove white area
                        Trace.TraceInformation(String.Format("ParentCaption sartswith OnlineMainView", windowCaption.ToString().StartsWith("OnlineMainView")));
                        Trace.TraceInformation(String.Format("getParent ({0}) == wnd {1} : {2}", GetParent(childHwnd), wnd, GetParent(childHwnd) == wnd));
                        if (windowCaption.ToString().StartsWith("OnlineMainView") && GetParent(childHwnd) == wnd)
                        {
                            var width = rectKakaoTalk.Right - rectKakaoTalk.Left;
                            var height = rectKakaoTalk.Bottom - rectKakaoTalk.Top - 31; // 31; there might be dragon. don't touch it
                            Trace.TraceInformation(String.Format("[OnlineMainView] Adjusted size : Width {0}, Height {1}", width, height));
                            UpdateWindow(wnd);
                            SetWindowPos(childHwnd, IntPtr.Zero, 0, 0, width, height, SetWindowPosFlags.SWP_NOMOVE);
                        }

                        Trace.TraceInformation(String.Format("ParentCaption sartswith LockModeView", windowCaption.ToString().StartsWith("LockModeView")));
                        Trace.TraceInformation(String.Format("getParent ({0}) == wnd {1} : {2}", GetParent(childHwnd), wnd, GetParent(childHwnd) == wnd));
                        if (windowCaption.ToString().StartsWith("LockModeView") && GetParent(childHwnd) == wnd)
                        {
                            var width = rectKakaoTalk.Right - rectKakaoTalk.Left;
                            var height = (rectKakaoTalk.Bottom - rectKakaoTalk.Top); // 38; there might be dragon. don't touch it.
                            Trace.TraceInformation(String.Format("[OnlineMainView] Adjusted size : Width {0}, Height {1}", width, height));
                            UpdateWindow(wnd);
                            SetWindowPos(childHwnd, IntPtr.Zero, 0, 0, width, height, SetWindowPosFlags.SWP_NOMOVE);
                        }
                        Trace.TraceInformation("-------------------------");
                    }
                    Trace.TraceInformation("=============================================");
                }

                // close popup ad
                popUpHwnd = IntPtr.Zero;

                while ((popUpHwnd = FindWindowEx(IntPtr.Zero, popUpHwnd, null, "")) != IntPtr.Zero)
                {
                    // popup ad does not have any parent
                    if (GetParent(popUpHwnd) != IntPtr.Zero) continue;

                    // get class name of blank title
                    var classNameSb = new StringBuilder(256);
                    GetClassName(popUpHwnd, classNameSb, classNameSb.Capacity);
                    string className = classNameSb.ToString();

                    if (!className.Contains("EVA_Window_Dblclk")) continue;

                    // get rect of popup ad
                    RECT rectPopup = new RECT();
                    GetWindowRect(popUpHwnd, out rectPopup);

                    var width = rectPopup.Right - rectPopup.Left;
                    var height = rectPopup.Bottom - rectPopup.Top;

                    if (width.Equals(300) && height.Equals(150))
                    {
                        SendMessage(popUpHwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    }
                }
            }
        }
    }
}
