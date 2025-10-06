@echo off

set /p TAGVERSION="Version: "

git tag -a "v%TAGVERSION%" -m "%TAGVERSION% release"

pause
