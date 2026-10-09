#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
task_dotnet_root="${1:?Pass the .NET SDK directory}"
task_acad="${2:?Pass ACadSharp.dll}"
task_output="$(mktemp -d)"
trap 'rm -rf "$task_output"' EXIT
references=()
drawing_sources=()
for source in "$repo_root/src/KhitunGeo/Native/"Drawing*.cs; do
    if [[ "$source" != *Dialog.cs ]]; then drawing_sources+=("$source"); fi
done
for reference in "$task_dotnet_root/packs/Microsoft.NETCore.App.Ref/8.0.20/ref/net8.0/"*.dll; do references+=("-r:$reference"); done
cd "$repo_root"
"$task_dotnet_root/dotnet" "$task_dotnet_root/sdk/8.0.414/Roslyn/bincore/csc.dll" \
    -nologo -target:exe -nullable:enable "-out:$task_output/NativeDrawing.dll" "${references[@]}" "-r:$task_acad" \
    tests/NativeWorkspace/CompilerUsings.cs tests/NativeDrawing/*.cs \
    "${drawing_sources[@]}" src/KhitunGeo/Native/NativeDrawingReader.cs src/KhitunGeo/Native/SurveyPoint.cs
cp "$task_acad" "$task_output/ACadSharp.dll"
"$task_dotnet_root/dotnet" exec --runtimeconfig tests/NativeWorkspace/NativeWorkspace.runtimeconfig.json "$task_output/NativeDrawing.dll" "${@:3}"
