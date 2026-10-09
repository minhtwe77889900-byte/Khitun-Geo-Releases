#!/usr/bin/env bash
# Compile native WinForms sources against real Windows reference assemblies.
# This does not build the SDK project or run a Windows UI.
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
task_dotnet_root="${1:?Pass .NET SDK root}"
task_windows_ref="${2:?Pass WindowsDesktop 8.0.20 ref/net8.0 directory}"
task_jint="${3:?Pass Jint 4.16.2 net8.0 DLL}"
task_acornima="${4:?Pass Acornima 1.7.0 net8.0 DLL}"
task_acad="${5:?Pass ACadSharp 3.7.16 DLL}"
task_pdf="${6:?Pass directory containing actual PDFsharp 6.2.4 and dependency DLLs}"
task_output="$(mktemp -d)"
trap 'rm -rf "$task_output"' EXIT
references=()
for reference in "$task_dotnet_root/packs/Microsoft.NETCore.App.Ref/8.0.20/ref/net8.0/"*.dll; do
    if [[ ! -f "$task_windows_ref/$(basename "$reference")" ]]; then references+=("-r:$reference"); fi
done
for reference in "$task_windows_ref/"*.dll; do references+=("-r:$reference"); done
for reference in "$task_pdf/"*.dll; do references+=("-r:$reference"); done
cd "$repo_root"
"$task_dotnet_root/dotnet" "$task_dotnet_root/sdk/8.0.414/Roslyn/bincore/csc.dll" -nologo -target:library -nullable:enable "-out:$task_output/NativeWindows.dll" "${references[@]}" "-r:$task_jint" "-r:$task_acornima" "-r:$task_acad" tests/NativeConversion/WindowsCompilerUsings.cs src/KhitunGeo/IntegrityVerifier.cs src/KhitunGeo/Native/*.cs
printf '%s\n' 'PASS native WinForms source compilation against Windows reference assemblies (UI execution pending)'
