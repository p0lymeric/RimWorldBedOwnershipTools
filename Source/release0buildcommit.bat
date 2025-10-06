@echo off

git status --porcelain | findstr . && set DIRTY=1 || set DIRTY=0

if %DIRTY% equ 1 (
pause
exit
)

call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"

git checkout --detach

rmdir /s /q .vs Artifacts
rmdir /s /q ..\1.5 ..\1.6

dotnet build BedOwnershipTools.slnx /p:Configuration=Release /p:Platform="Any CPU"

git add -f ..\1.5\Assemblies\*
git add -f ..\1.6\Assemblies\*

for /f "delims=" %%i in ('git show -s --format^="'%%s' (%%h)" HEAD') do set COMMITLINE=%%i

git commit -m "Build %COMMITLINE%"

pause
