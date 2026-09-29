@echo off
rem Builds SurvivalChaosFfx.dll into Assets\Plugins\FidelityFX\x86_64.
rem
rem Needs Visual Studio's C++ tools, found with vswhere, and the Unity editor
rem named in ProjectSettings\ProjectVersion.txt, for its plugin headers.
rem
rem Unity keeps a native plugin loaded until the editor quits, so once the
rem editor has used FSR 3 the copy into Assets fails until it is closed.
setlocal

set "ROOT=%~dp0"
set "PROJECT=%ROOT%..\.."
set "OBJ=%ROOT%build"
set "OUT=%PROJECT%\Assets\Plugins\FidelityFX\x86_64"

for /f "tokens=2" %%v in ('findstr /b /c:"m_EditorVersion:" "%PROJECT%\ProjectSettings\ProjectVersion.txt"') do set "UNITY_VERSION=%%v"
set "UNITY_API=%ProgramFiles%\Unity\Hub\Editor\%UNITY_VERSION%\Editor\Data\PluginAPI"
if not exist "%UNITY_API%\IUnityGraphicsD3D12.h" (
    echo Unity plugin headers not found in "%UNITY_API%".
    exit /b 1
)

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VS=%%i"
if not defined VS (
    echo Visual Studio's C++ tools were not found.
    exit /b 1
)
call "%VS%\VC\Auxiliary\Build\vcvars64.bat" >nul || exit /b 1

if not exist "%OBJ%" mkdir "%OBJ%"
if not exist "%OUT%" mkdir "%OUT%"

cl /nologo /LD /O2 /MT /EHsc /std:c++17 /W4 /DNDEBUG /Zi ^
   /I"%UNITY_API%" /Fo"%OBJ%\\" /Fd"%OBJ%\\" ^
   "%ROOT%src\SurvivalChaosFfx.cpp" ^
   /link /DLL /OUT:"%OBJ%\SurvivalChaosFfx.dll" /PDB:"%OBJ%\SurvivalChaosFfx.pdb" /IMPLIB:"%OBJ%\SurvivalChaosFfx.lib" /INCREMENTAL:NO || exit /b 1

copy /y "%OBJ%\SurvivalChaosFfx.dll" "%OUT%\SurvivalChaosFfx.dll" >nul || (
    echo Could not replace the plugin in Assets. Close the Unity editor and build again.
    exit /b 1
)
echo Built "%OUT%\SurvivalChaosFfx.dll"
