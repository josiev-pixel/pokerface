# Development notes

## Build & test

```pwsh
dotnet build PokerEngine.slnx        # build the engine + Cli
./test.ps1                           # run the test suite (Debug)
./test.ps1 Release                   # run in Release
dotnet run --project src/PokerEngine.Cli -- <command>   # use the CLI
```

If a fresh terminal can't find the .NET 10 SDK, refresh PATH:

```pwsh
$env:Path = [Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [Environment]::GetEnvironmentVariable("Path","User")
```

## Why we don't use `dotnet test` on this machine — Smart App Control

This dev machine runs **Windows Smart App Control (SAC) in enforcing mode**. SAC blocks
loading *unsigned, locally-built* assemblies in some load paths, which breaks the usual
.NET test loop. Concretely we hit two blocks (`0x800711C7`, "An Application Control policy
has blocked this file"):

1. The **legacy vstest host** (`Microsoft.NET.Test.Sdk`) is blocked outright when it
   reflection-loads the test assembly.
2. xUnit's custom **`AssemblyLoadContext`** is blocked when it loads an unsigned engine DLL
   via `LoadFromAssemblyPath` — *even though the default runtime loader admits the exact
   same DLL* (a plain `dotnet app.dll` that references the engine runs fine).

Turning SAC off is the only "real" fix, but it is irreversible without resetting Windows,
so we engineer around it instead:

- The test project (`tests/PokerEngine.Tests`) uses **xUnit v3 + Microsoft.Testing.Platform**
  (it builds to a console exe and runs itself via `dotnet run`), not the vstest host.
- The test project **compiles the engine source files directly** (`<Compile Include=
  "..\..\src\PokerEngine.Core\**\*.cs" />`) instead of taking a `ProjectReference`. That
  way the engine types live in the already-admitted test assembly and there is no separate
  DLL for xUnit's load context to be blocked on. As new engine libraries land
  (`Solver`, `Decision`, …), add their source globs to the test `.csproj` too — but never a
  project that defines `Main` (`Cli`, `Table`).

The shipping engine is still built as normal, independent class libraries; only the **test
assembly** folds the sources in, purely to dodge SAC. This decision is recorded in
[ADR-0005](adr/0005-testing-under-smart-app-control.md).

Run tests with `./test.ps1` (wraps `dotnet run --project tests/PokerEngine.Tests`).
