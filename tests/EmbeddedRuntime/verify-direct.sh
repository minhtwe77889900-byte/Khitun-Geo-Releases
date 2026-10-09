#!/usr/bin/env bash
# Real packages only; caller supplies extracted NuGet net8.0 assembly paths.
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
task_dotnet_root="${1:?Pass the .NET SDK directory}"
task_jint="${2:?Pass the Jint 4.16.2 net8.0 DLL}"
task_acornima="${3:?Pass the Acornima 1.7.0 net8.0 DLL}"
task_output="$(mktemp -d)"
trap 'rm -rf "$task_output"' EXIT
references=()
for reference in "$task_dotnet_root/packs/Microsoft.NETCore.App.Ref/8.0.20/ref/net8.0/"*.dll; do references+=("-r:$reference"); done
cd "$repo_root"
"$task_dotnet_root/dotnet" "$task_dotnet_root/sdk/8.0.414/Roslyn/bincore/csc.dll" -nologo -target:exe -nullable:enable "-out:$task_output/EmbeddedRuntime.dll" "${references[@]}" "-r:$task_jint" "-r:$task_acornima" tests/NativeWorkspace/CompilerUsings.cs tests/EmbeddedRuntime/Program.cs src/KhitunGeo/Native/EmbeddedCoordinateRuntime.cs src/KhitunGeo/Native/SurveyPoint.cs src/KhitunGeo/Native/PointWorkspace.cs src/KhitunGeo/Native/CoordinateBridge.cs src/KhitunGeo/Native/CoordinateConversion.cs
cp "$task_jint" "$task_output/Jint.dll"
cp "$task_acornima" "$task_output/Acornima.dll"
cp src/KhitunGeo/wwwroot/coordinate-core.js src/KhitunGeo/wwwroot/coordinate-adapter.js src/KhitunGeo/wwwroot/crs/native-systems.json tests/CoordinateCore/controls.json tests/crs-proj-controls.json "$task_output/"
"$task_dotnet_root/dotnet" exec --runtimeconfig tests/NativeWorkspace/NativeWorkspace.runtimeconfig.json "$task_output/EmbeddedRuntime.dll"
