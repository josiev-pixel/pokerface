#!/usr/bin/env pwsh
# Run the test suite. We deliberately use `dotnet run` (the xUnit v3 / Microsoft.Testing
# .Platform self-hosted runner) rather than `dotnet test`: this machine has Windows Smart
# App Control enforcing, which blocks the legacy vstest reflection host. See
# docs/DEVELOPMENT.md for the full story.
param([string]$Configuration = "Debug")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
dotnet run --project (Join-Path $root "tests/PokerEngine.Tests") -c $Configuration -- @args
