@echo off
rem =====================================================================================
rem  ZSZ Match - budowanie i wdrozenie pluginu
rem
rem  Uzycie:
rem     build.bat                 - tylko kompilacja (wynik w src\ZSZMatch\bin\Release\net10.0)
rem     build.bat C:\cs2server    - kompilacja + skopiowanie pluginu na serwer
rem
rem  Wymaga .NET 10 SDK (sprawdz: dotnet --list-sdks).
rem =====================================================================================
setlocal

set "ROOT=%~dp0"
set "OUT=%ROOT%src\ZSZMatch\bin\Release\net10.0"

echo.
echo === [1/2] Kompilacja pluginu ===
pushd "%ROOT%src\ZSZMatch"
dotnet build -c Release
if errorlevel 1 (
    popd
    echo.
    echo BLAD: kompilacja nie powiodla sie.
    exit /b 1
)
popd

if "%~1"=="" (
    echo.
    echo Gotowe. Pliki do wdrozenia sa tutaj:
    echo    %OUT%
    echo.
    echo Skopiuj CALY folder jako:
    echo    ^<serwer^>\game\csgo\addons\counterstrikesharp\plugins\ZSZMatch\
    echo.
    echo Aby zrobic to automatycznie:
    echo    build.bat C:\cs2server
    exit /b 0
)

set "DEST=%~1\game\csgo\addons\counterstrikesharp\plugins\ZSZMatch"

if not exist "%~1\game\csgo\addons\counterstrikesharp\plugins" (
    echo.
    echo BLAD: nie znaleziono "%~1\game\csgo\addons\counterstrikesharp\plugins".
    echo Sprawdz, czy CounterStrikeSharp jest zainstalowany i czy sciezka jest poprawna.
    exit /b 1
)

echo.
echo === [2/2] Kopiowanie do %DEST% ===
if not exist "%DEST%" mkdir "%DEST%"

copy /Y "%OUT%\ZSZMatch.dll"            "%DEST%\ZSZMatch.dll"            >nul
copy /Y "%OUT%\ZSZMatch.example.json"   "%DEST%\ZSZMatch.example.json"   >nul
if exist "%OUT%\ZSZMatch.pdb" copy /Y "%OUT%\ZSZMatch.pdb" "%DEST%\ZSZMatch.pdb" >nul

echo Skopiowano:
echo    ZSZMatch.dll
echo    ZSZMatch.example.json
echo.
echo Uruchom serwer ponownie (albo wpisz "css_plugins reload ZSZMatch", jesli plugin
echo jest juz zaladowany), a nastepnie sprawdz logi w:
echo    %~1\game\csgo\addons\counterstrikesharp\logs\
echo.
endlocal
