@echo off

call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"

@echo on
dotnet publish BedOwnershipTools.slnx -c Release
@echo off

pause
