@echo off
setlocal

echo ====================================
echo  Building AVSOverlay with NativeAOT
echo ====================================

set PROJECT="AVS Overlay.csproj"
set RUNTIME=win-x64
set CONFIG=Release
set OUTPUT=bin\%CONFIG%\net8.0\%RUNTIME%\publish

dotnet publish %PROJECT% ^
 -c %CONFIG% ^
 -r %RUNTIME% ^
 /p:PublishAot=true ^
 /p:SelfContained=true ^
 /p:PublishTrimmed=true ^
 /p:StripSymbols=true ^
 /p:InvariantGlobalization=true ^
 /p:EnableCompressionInSingleFile=true

IF %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build failed.
    pause
    exit /b 1
)

echo.
echo Native AOT build complete!
echo Output: %OUTPUT%
explorer "%OUTPUT%"
pause
