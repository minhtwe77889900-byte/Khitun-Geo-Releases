#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
task_mode="${1:?Pass model or quality or workflow}"
task_dotnet_root="${2:?Pass SDK root}"
task_output="$(mktemp -d)"
trap 'rm -rf "$task_output"' EXIT
references=()
for reference in "$task_dotnet_root/packs/Microsoft.NETCore.App.Ref/8.0.20/ref/net8.0/"*.dll; do references+=("-r:$reference"); done
sources=(tests/NativeWorkspace/CompilerUsings.cs tests/NativePassport/Program.cs tests/NativePassport/ModelTests.cs src/KhitunGeo/Native/SurveyPoint.cs src/KhitunGeo/Native/PointWorkspace.cs src/KhitunGeo/Native/ConversionPassport.cs src/KhitunGeo/Native/DeferredCloseRequest.cs)
options=()
arguments=()
case "$task_mode" in
  model) ;;
  quality)
    task_jint="${3:?Pass Jint DLL}"; task_acornima="${4:?Pass Acornima DLL}"
    references+=("-r:$task_jint" "-r:$task_acornima")
    sources+=(tests/NativePassport/QualityTests.cs src/KhitunGeo/Native/NativeQualityRuntime.cs)
    options+=(-define:QUALITY)
    cp "$task_jint" "$task_output/Jint.dll"
    cp "$task_acornima" "$task_output/Acornima.dll"
    cp "$repo_root/src/KhitunGeo/wwwroot/workspace-core.js" "$task_output/"
    ;;
  workflow)
    sources+=(tests/NativePassport/PassportWorkflowTests.cs src/KhitunGeo/Native/PassportConversion.cs src/KhitunGeo/Native/CoordinateConversion.cs src/KhitunGeo/Native/CoordinateBridge.cs)
    options+=(-define:WORKFLOW)
    ;;
  pdf)
    task_pdf="${3:?Pass directory containing actual PDF dependency DLLs}"
    for reference in "$task_pdf/"*.dll; do references+=("-r:$reference"); cp "$reference" "$task_output/"; done
    sources+=(tests/NativePassport/PdfTests.cs src/KhitunGeo/Native/PassportFontResolver.cs src/KhitunGeo/Native/PassportPdfWriter.cs)
    options+=(-define:PDF)
    arguments=("${4:?Pass regular font}" "${5:?Pass bold font}" "${6:?Pass output directory}")
    ;;
  *) printf '%s\n' 'Unsupported test mode' >&2; exit 2 ;;
esac
cd "$repo_root"
"$task_dotnet_root/dotnet" "$task_dotnet_root/sdk/8.0.414/Roslyn/bincore/csc.dll" -nologo -target:exe -nullable:enable "-out:$task_output/NativePassport.dll" "${references[@]}" "${options[@]}" "${sources[@]}"
"$task_dotnet_root/dotnet" exec --runtimeconfig tests/NativeWorkspace/NativeWorkspace.runtimeconfig.json "$task_output/NativePassport.dll" "${arguments[@]}"
