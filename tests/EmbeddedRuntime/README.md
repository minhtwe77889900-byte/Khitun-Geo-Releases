# Embedded JavaScript compatibility gate

This harness verifies the shared browser-free host used by the opt-in native preview.
It pins Jint 4.16.2 and loads the exact deployed JavaScript files. The application now pins the same Jint dependency and reuses this tested host.
The regular startup screen remains unchanged. Windows build and native UI
inspection remain required before replacing that production screen.

From the repository root on Windows with .NET 8 and Node installed:

```powershell
./tools/verify-native-runtime.ps1
```

The script runs the catalogue freshness check, JavaScript controls, portable
native tests, the real Jint harness and a Windows compilation. It fails on any
restore, build or test error. It does not publish or create an installer. The
draft validation workflow invokes the same script when uploaded by the user.

The harness checks 46 frozen original-core controls (small floating-point
rounding tolerance), 1,095 independent PROJ controls (planar error below 2 mm),
381 resolved catalogue definitions, C# request → Jint → validated C# response,
negative/null heights, Cyrillic metadata, quoted data, atomic undo and cancellation.
A reused engine is confined to the sequential numerical test loop. The shared
host creates a fresh engine for every conversion and never exposes CLR services.
Cancellation during execution and statement/memory/timeout constraints are tested.
These checks do not measure overall process RAM or native UI responsiveness.

Local verification completed with official Jint 4.16.2 and Acornima 1.7.0
net8.0 assemblies, compiled with the installed Roslyn compiler and executed on
.NET 8. The direct path is available because the local SDK CLI aborts in
Process.StartTime before restore. Pass the SDK root and extracted assembly paths:

```bash
bash tests/EmbeddedRuntime/verify-direct.sh /path/to/dotnet-sdk /path/to/Jint.dll /path/to/Acornima.dll
```

All 46 frozen and 1,095 PROJ checks passed; maximum planar error was
0.0011988564498252407 m. Frozen controls permit an absolute rounding tolerance
of 1e-8 or relative 2e-12, whichever is larger; Jint/V8 differed by 1.86e-9 m
in an inverse datum height. No mathematical source or parameter changed.
NuGet access recovered during this stage. SDK-project restore/build, the
PowerShell script, Windows application compilation and UI inspection remain
pending; direct portable engine checks do not certify those steps.
