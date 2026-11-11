@echo off
cd /d "%~dp0"
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TblCompiler.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll TblCompiler.cs
if errorlevel 1 exit /b 1
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /main:CompilerTests /out:CompilerTests.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll TblCompiler.cs CompilerTests.cs
if errorlevel 1 exit /b 1
CompilerTests.exe
pause
