#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
task_dotnet_root="${1:?Pass the .NET SDK directory}"
task_output="$(mktemp -d)"
trap 'rm -rf "$task_output"' EXIT
references=()
for reference in "$task_dotnet_root/packs/Microsoft.NETCore.App.Ref/8.0.20/ref/net8.0/"*.dll; do references+=("-r:$reference"); done
cd "$repo_root"
node tools/export-native-crs.cjs --check
"$task_dotnet_root/dotnet" "$task_dotnet_root/sdk/8.0.414/Roslyn/bincore/csc.dll" -nologo -target:exe -nullable:enable "-out:$task_output/NativeCatalogue.dll" "${references[@]}" tests/NativeWorkspace/CompilerUsings.cs tests/NativeCatalogue/Program.cs src/KhitunGeo/Native/NativeCrsCatalogue.cs
"$task_dotnet_root/dotnet" exec --runtimeconfig tests/NativeWorkspace/NativeWorkspace.runtimeconfig.json "$task_output/NativeCatalogue.dll" src/KhitunGeo/wwwroot/crs/native-systems.json
