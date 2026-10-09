#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
task_dotnet_root="${1:?Pass the .NET SDK directory}"
task_output="$(mktemp -d)"
trap 'rm -rf "$task_output"' EXIT
references=()
for reference in "$task_dotnet_root/packs/Microsoft.NETCore.App.Ref/8.0.20/ref/net8.0/"*.dll; do references+=("-r:$reference"); done
cd "$repo_root"
"$task_dotnet_root/dotnet" "$task_dotnet_root/sdk/8.0.414/Roslyn/bincore/csc.dll" -nologo -target:exe -nullable:enable "-out:$task_output/CoordinateBridge.dll" "${references[@]}" \
 tests/NativeWorkspace/CompilerUsings.cs tests/CoordinateBridge/Program.cs src/KhitunGeo/Native/SurveyPoint.cs src/KhitunGeo/Native/PointWorkspace.cs src/KhitunGeo/Native/CoordinateBridge.cs
runtime_config="tests/NativeWorkspace/NativeWorkspace.runtimeconfig.json"
"$task_dotnet_root/dotnet" exec --runtimeconfig "$runtime_config" "$task_output/CoordinateBridge.dll"
"$task_dotnet_root/dotnet" exec --runtimeconfig "$runtime_config" "$task_output/CoordinateBridge.dll" --request > "$task_output/request.json"
node - "$task_output/request.json" "$task_output/response.json" <<'JS'
const fs=require('node:fs'),adapter=require('./src/KhitunGeo/wwwroot/coordinate-adapter.js');
fs.writeFileSync(process.argv[3],adapter.convertJson(fs.readFileSync(process.argv[2],'utf8')));
JS
"$task_dotnet_root/dotnet" exec --runtimeconfig "$runtime_config" "$task_output/CoordinateBridge.dll" --response "$task_output/response.json"
