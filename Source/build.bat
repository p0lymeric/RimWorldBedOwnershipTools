@echo off

call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"

@echo on
dotnet build BedOwnershipTools.slnx /p:Configuration=Release /p:Platform="Any CPU"
@echo off

pause
