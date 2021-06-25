# KakaotalkPC_Adblock_Service
카카오톡 PC 광고 삭제   

|   |   |
|---|---|
|언어|C#|
|SDK|.NET 5|
|IDE|Visual Studio 16.9.0|
|Project|WorkerService|
|Nuget Package|Microsoft.Extensions.Hosting.WindowsServices 5.0.1|
|메인 프로젝트|https://github.com/blurfx/KakaoTalkAdBlock|





# KakaoTalkAdBlock

Removes ads from KakaoTalk PC client.

## Download

- Download [publish/setup.exe](https://github.com/blurfx/KakaoTalkAdBlock/blob/master/publish/setup.exe)
- Run `setup.exe` to install
- After installation, run `KakaoTalkAdBlock` from the Start menu or desktop 

### Requirements

- [.NET Framework 4.6.2 Runtime](https://dotnet.microsoft.com/download/dotnet-framework/net462)

### When uninstallable on Windows 10 due to security issues

#### Automatic fix
- Download [win10-security-fix.reg](https://github.com/blurfx/KakaoTalkAdBlock/blob/master/win10-security-fix.reg)
- Run `win10-security-fix.reg` to fix registry automatically

#### Manual fix
- Run Registry Editor by typing regedit in the run menu.
- Move to "\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\\\.NETFramework\Security\TrustManager\PromptingLevel"
- Change "Internet, Localintranet, MyComputer" to Enabled

## Update History

- Go [Releases](https://github.com/blurfx/KakaoTalkAdBlock/releases) page 

## At a glance

![](https://raw.githubusercontent.com/blurfx/KakaoTalkAdBlock/master/kakaotalk.png)

This program runs in the tray.

![](https://raw.githubusercontent.com/blurfx/KakaoTalkAdBlock/master/tray.png)
