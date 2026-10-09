# Compile and test only. Does not publish, package, upload or enable native calculation.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    foreach ($test in @('tools/export-native-crs.cjs', 'tests/CoordinateCore/core.cjs', 'tests/CoordinateCore/adapter.cjs')) {
        if ($test -eq 'tools/export-native-crs.cjs') { & node $test --check }
        else { & node $test }
        if ($LASTEXITCODE -ne 0) { throw "JavaScript check failed: $test" }
    }
    foreach ($project in @('NativeWorkspace', 'NativeExport', 'NativePassport', 'CoordinateBridge', 'NativeConversion', 'NativeCatalogue', 'NativeDrawing', 'EmbeddedRuntime')) {
        if ($project -eq 'NativeCatalogue') {
            & dotnet run --project "tests/$project/$project.csproj" -c Release -- src/KhitunGeo/wwwroot/crs/native-systems.json
        }
        else { & dotnet run --project "tests/$project/$project.csproj" -c Release }
        if ($LASTEXITCODE -ne 0) { throw "Native check failed: $project (restore, compilation or execution)" }
    }
    & dotnet run --project tests/NativeStartupSmoke/NativeStartupSmoke.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Native WinForms startup smoke test failed' }
    & dotnet build src/KhitunGeo/KhitunGeo.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Windows compilation failed' }
    Write-Host 'PASS native migration and embedded-runtime candidate checks. Windows UI inspection remains required.'
}
finally { Pop-Location }
