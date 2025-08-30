@echo off

call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"

set "RW_MANAGED_ROOT=C:\Games\RimworldDev\Rimworld1.5.4409"
set "HMY_MANAGED_ROOT=C:\Games\RimworldDev\Harmony2.4.1"

set "CSC_REFERENCES=%RW_MANAGED_ROOT%\Assembly-CSharp.dll"
set "CSC_REFERENCES=%CSC_REFERENCES%,%RW_MANAGED_ROOT%\UnityEngine.CoreModule.dll"
set "CSC_REFERENCES=%CSC_REFERENCES%,%RW_MANAGED_ROOT%\UnityEngine.IMGUIModule.dll"
set "CSC_REFERENCES=%CSC_REFERENCES%,%RW_MANAGED_ROOT%\UnityEngine.TextRenderingModule.dll"
set "CSC_REFERENCES=%CSC_REFERENCES%,%RW_MANAGED_ROOT%\UnityEngine.InputLegacyModule.dll"
set "CSC_REFERENCES=%CSC_REFERENCES%,%HMY_MANAGED_ROOT%\0Harmony.dll"

set "CSC_INPUTS=Source\Properties\AssemblyInfo.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\BedOwnershipTools.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\ModSettingsImpl.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\RuntimeHandleProvider.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\HarmonyPatches.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\HarmonyPatches\UIPatches.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\HarmonyPatches\CommunalBeds.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\HarmonyPatches\BedAssignmentPinning.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\HarmonyPatches\BedAssignmentGroups.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\CompBuilding_BedXAttrs.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\CompPawnXAttrs.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\CATPBGroupAssignmentOverlayAdapter.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\CATPBAndPOMethodReplacements.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\AssignmentGroup.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\GameComponent_AssignmentGroupManager.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\Command_SetAssignmentGroup.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\Dialog_EditAssignmentGroups.cs"
set "CSC_INPUTS=%CSC_INPUTS% Source\Dialog_RenameAssignmentGroup.cs"

@echo on
cd ..\..
csc -target:library -out:1.5\Assemblies\BedOwnershipTools.dll -reference:%CSC_REFERENCES% %CSC_INPUTS%
@echo off

pause
