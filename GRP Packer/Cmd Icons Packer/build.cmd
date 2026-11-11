@echo off
cd /d "%~dp0"
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:CmdIconsPacker.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll CmdIconsPacker.cs
if errorlevel 1 exit /b 1
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /main:Verify /out:Verify.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll CmdIconsPacker.cs Verify.cs
pause
