# ADR 0005: Testing under Windows Smart App Control

- **Status:** Accepted
- **Date:** 2026-06-23
- **Deciders:** claude-cloud (build/infra call)

## Context
The dev machine runs **Windows Smart App Control (SAC) in enforcing mode**
(`HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy` → `VerifiedAndReputablePolicyState=1`).
SAC blocks loading *unsigned, locally-built* assemblies in two load paths that the standard
.NET test loop depends on (`FileLoadException 0x800711C7`):

1. The **legacy vstest host** (`Microsoft.NET.Test.Sdk`) — blocked when it reflection-loads
   the test assembly. `dotnet test` therefore fails before any test runs.
2. xUnit's custom **`AssemblyLoadContext`** — blocked when it loads an unsigned engine DLL
   via `LoadFromAssemblyPath`, *even though the default runtime loader admits the same DLL*
   (a plain `dotnet app.dll` referencing the engine runs fine; verified empirically).

Signing every build with a SAC-trusted certificate is not feasible, and disabling SAC is
irreversible without resetting Windows. We need a test loop that works as-is.

## Decision
1. Use **xUnit v3 + Microsoft.Testing.Platform** for the test project: it builds to a
   console executable and runs itself (`dotnet run`), avoiding the blocked vstest host.
2. Have the test project **compile the engine sources in-assembly** (`<Compile Include=
   "..\..\src\PokerEngine.Core\**\*.cs" />`) rather than taking a `ProjectReference`, so the
   engine types load as part of the already-admitted test assembly — there is no separate
   engine DLL for xUnit's load context to be blocked on. New engine libraries add their
   `**\*.cs` globs here too; projects that define `Main` (`Cli`, `Table`) are never folded in.
3. Standardize the command as `./test.ps1` → `dotnet run --project tests/PokerEngine.Tests`.

The production engine is still built as normal, independent class libraries (`Core` and the
later layers); only the **test assembly** folds sources in, purely to satisfy SAC.

## Consequences
- `dotnet test` is not used; CI/local both go through `./test.ps1`.
- Tests exercise the engine **sources** (recompiled into the test assembly), not the shipped
  DLLs. For pure logic that is equivalent; it does mean test builds don't inherit `Core`'s
  `TreatWarningsAsErrors`, so the library build remains the warnings gate.
- A little duplication in the test `.csproj` (one `Compile` glob per engine library), which
  is cheap and explicit.
- If this project moves to a machine without SAC, it can switch back to `ProjectReference`s
  and (optionally) `dotnet test` with no source changes — only the test `.csproj` differs.

## Alternatives considered
- **Disable SAC** — irreversible without an OS reset; rejected.
- **Sign assemblies with a trusted cert** — no such cert available; not worth the overhead.
- **Keep `ProjectReference` and run via `dotnet test`** — blocked by SAC; rejected.
