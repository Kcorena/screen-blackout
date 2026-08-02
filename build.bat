@echo off
rem Build ScreenBlackout.exe with the .NET Framework compiler (bundled with Windows).
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find csc.exe. .NET Framework 4.x is required.
  exit /b 1
)
"%CSC%" /nologo /codepage:65001 /win32icon:ScreenBlackout.ico /target:winexe /out:ScreenBlackout.exe ScreenBlackout.cs MsiKb.cs
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)
echo Built ScreenBlackout.exe
