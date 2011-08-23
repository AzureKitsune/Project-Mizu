@echo off
REM  Set us to the Current Directory
cd %~dp0
SET WinSDK6=C:\Program Files\Microsoft SDKs\Windows\v6.0A\bin
echo Compiling Mizu...
REM Build the solution
%windir%\Microsoft.NET\Framework\V4.0.30319\msbuild "Mizu.sln" /p:Configuration=Release
mkdir bin
mkdir bin\Scripts\
copy Mizu\bin\Debug\Mizu.exe bin\Mizu.exe
copy Mizu\bin\Debug\Mizu.Lib.Evaluator.dll bin\Mizu.Lib.Evaluator.dll
copy Mizu\bin\Debug\Mizu.Parser.dll bin\Mizu.Parser.dll
copy Mizu\Scripts\ bin\Scripts\
echo Done
start bin