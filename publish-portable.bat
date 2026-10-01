@echo off
setlocal
cd /d "%~dp0"
echo Uygulama ikonlari yeniden uretiliyor...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Scripts\RegenerateAppIcon.ps1"
if errorlevel 1 goto :fail
echo Calisan portable kopya varsa kapatiliyor...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Scripts\UnlockPublishOutput.ps1" -PublishDir "%~dp0publish\win-x64"
if errorlevel 1 goto :fail
echo Portable win-x64 derleniyor (.NET 10)...
dotnet publish DirectoryManagement.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o "%~dp0publish\win-x64"
if errorlevel 1 goto :fail
echo.
echo Hazir: %~dp0publish\win-x64\DirectoryManagement.exe
echo Bu exe'yi istediginiz klasore kopyalayip calistirin (portable).
goto :done

:fail
echo.
echo HATA: Portable derleme basarisiz oldu. Yukaridaki hata mesajini inceleyin.
if /I "%~1"=="/nopause" exit /b 1
pause
exit /b 1

:done
if /I not "%~1"=="/nopause" pause
exit /b 0
