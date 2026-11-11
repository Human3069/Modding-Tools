@echo off
cd /d "%~dp0"
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:WireframePacker.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll WireframePacker.cs
pause
