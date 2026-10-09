#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
task_dotnet_root="${1:?Pass the .NET SDK directory}"
task_sdk="${2:-8.0.414}"
task_runtime="${3:-8.0.20}"
task_output="$(mktemp -d)"
trap 'rm -rf "$task_output"' EXIT
references=()
for reference in "$task_dotnet_root/packs/Microsoft.NETCore.App.Ref/$task_runtime/ref/net8.0/"*.dll; do
    references+=("-r:$reference")
done
cd "$repo_root"
"$task_dotnet_root/dotnet" "$task_dotnet_root/sdk/$task_sdk/Roslyn/bincore/csc.dll" \
    -nologo -target:exe -nullable:enable "-out:$task_output/NativeWorkspace.dll" \
    "${references[@]}" tests/NativeWorkspace/CompilerUsings.cs tests/NativeWorkspace/Program.cs \
    src/KhitunGeo/Native/SurveyPoint.cs src/KhitunGeo/Native/PointWorkspace.cs \
    src/KhitunGeo/Native/HeightCalculator.cs src/KhitunGeo/Native/TabularPaste.cs \
    src/KhitunGeo/Native/PointFileService.cs src/KhitunGeo/Native/ExcelXlsxReader.cs
"$task_dotnet_root/dotnet" exec --runtimeconfig tests/NativeWorkspace/NativeWorkspace.runtimeconfig.json \
    "$task_output/NativeWorkspace.dll"
