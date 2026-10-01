@echo off
setlocal
cd /d "%~dp0"
echo Uygulama ikonlari yeniden uretiliyor...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Scripts\RegenerateAppIcon.ps1"
if errorlevel 1 goto :fail

call :publish win-x64 x64
if errorlevel 1 goto :fail
call :publish win-x86 x86
if errorlevel 1 goto :fail

echo.
echo Hazir:
echo   %~dp0publish\win-x64\DirectoryManagement-x64.exe
echo   %~dp0publish\win-x86\DirectoryManagement-x86.exe
echo 64 bit Windows icin x64, 32 bit Windows icin x86 exe'yi kopyalayip calistirin.
goto :done

:publish
set "RID=%~1"
set "BITS=%~2"
set "OUT=%~dp0publish\%RID%"
set "EXE_NAME=DirectoryManagement-%BITS%"
echo.
echo Calisan portable kopya varsa kapatiliyor (%BITS%)...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Scripts\UnlockPublishOutput.ps1" -PublishDir "%OUT%" -ExeName "%EXE_NAME%.exe"
if errorlevel 1 exit /b 1
echo Portable %RID% derleniyor (.NET 10, %BITS%)...
dotnet publish DirectoryManagement.csproj -c Release -r %RID% --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:AssemblyName=%EXE_NAME% ^
  -o "%OUT%"
if errorlevel 1 exit /b 1
exit /b 0

:fail
echo.
echo HATA: Portable derleme basarisiz oldu. Yukaridaki hata mesajini inceleyin.
if /I "%~1"=="/nopause" exit /b 1
pause
exit /b 1

:done
if /I not "%~1"=="/nopause" pause
exit /b 0
