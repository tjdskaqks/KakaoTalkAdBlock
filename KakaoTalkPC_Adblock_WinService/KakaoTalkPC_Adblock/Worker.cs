using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KakaoTalkPC_Adblock
{
   public class Worker : BackgroundService
   {
      private readonly ILogger<Worker> _logger;

      #region Global Variables
      static string[] KAKAOTALK_TITLE_STRING = { "카카오톡", "Kakaotalk" };

      static volatile List<IntPtr> hwnd = new List<IntPtr>();

      static uint WM_CLOSE = 0x10;
      #endregion

      public Worker(ILogger<Worker> logger)
      {
         _logger = logger;
      }

      public override Task StartAsync(CancellationToken cancellationToken)
      {
         _logger.LogInformation("Start Service");
         return base.StartAsync(cancellationToken);
      }

      public override Task StopAsync(CancellationToken cancellationToken)
      {
         _logger.LogInformation("Stop Service");
         return base.StopAsync(cancellationToken);
      }

      public override void Dispose()
      {
         base.Dispose();
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
         while (!stoppingToken.IsCancellationRequested)
         {
            hwnd.Clear();

            // find kakaotalk window
            foreach (string titleCandidate in KAKAOTALK_TITLE_STRING)
            {
               IntPtr tmpHwnd = IntPtr.Zero;

               while ((tmpHwnd = FindWindowEx(IntPtr.Zero, tmpHwnd, null, titleCandidate)) != IntPtr.Zero)
               {
                  hwnd.Add(tmpHwnd);
               }
            }

            List<IntPtr> childHwnds = new List<IntPtr>();
            StringBuilder windowClass = new StringBuilder(256);
            StringBuilder windowCaption = new StringBuilder(256);
            StringBuilder windowParentCaption = new StringBuilder(256);

            foreach (IntPtr wnd in hwnd)
            {
               childHwnds.Clear();
               GCHandle gcHandle = GCHandle.Alloc(childHwnds);

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

                  // remove white area
                  if (windowCaption.ToString().StartsWith("OnlineMainView") && GetParent(childHwnd) == wnd)
                  {
                     var width = rectKakaoTalk.Right - rectKakaoTalk.Left;
                     var height = (rectKakaoTalk.Bottom - rectKakaoTalk.Top) - 31; // 31; there might be dragon. don't touch it.
                     UpdateWindow(wnd);
                     SetWindowPos(childHwnd, IntPtr.Zero, 0, 0, width, height, SetWindowPosFlags.SWP_NOMOVE);
                  }

                  if (windowCaption.ToString().StartsWith("LockModeView") && GetParent(childHwnd) == wnd)
                  {
                     var width = rectKakaoTalk.Right - rectKakaoTalk.Left;
                     var height = (rectKakaoTalk.Bottom - rectKakaoTalk.Top); // 38; there might be dragon. don't touch it.
                     UpdateWindow(wnd);
                     SetWindowPos(childHwnd, IntPtr.Zero, 0, 0, width, height, SetWindowPosFlags.SWP_NOMOVE);
                  }
               }
            }

            // close popup ad
            IntPtr popUpHwnd = IntPtr.Zero;

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
            await Task.Delay(TimeSpan.FromMilliseconds(50), stoppingToken);
         }
      }

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
   }
}
