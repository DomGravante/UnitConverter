# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build (Debug)
dotnet build UnitConverter.sln

# Build specific platform
dotnet build UnitConverter.sln -p:Platform=x64
dotnet build UnitConverter.sln -p:Platform=ARM64

# Publish (Release, trimmed) — required for MSIX sideloading
dotnet publish UnitConverter/UnitConverter.csproj -c Release -p:Platform=x64
```

There are no automated tests in this project.

To install/test the extension, the MSIX package must be sideloaded via Visual Studio's "Package and Publish" menu or by building the MSIX and running the installer.

## Architecture

This is a **Microsoft PowerToys Command Palette extension** built with `Microsoft.CommandPalette.Extensions`. It surfaces a unit converter (inches ↔ mm) directly inside the PowerToys Command Palette, aimed at mechanical engineers who frequently switch between imperial and metric units.

**Extension lifecycle** (defined by the CommandPalette SDK):
1. Command Palette discovers the extension via the MSIX `com.microsoft.commandpalette` app extension declared in `Package.appxmanifest`.
2. Command Palette launches `UnitConverter.exe -RegisterProcessAsComServer`.
3. `Program.cs` registers `UnitConverter` (COM GUID `b36eeab6-...`) as a COM out-of-process server via `Shmuelie.WinRTServer` and blocks until disposed.
4. Command Palette calls back via COM to get `IExtension` → `GetProvider(ProviderType.Commands)` → `UnitConverterCommandsProvider` → `TopLevelCommands()` → a single `CommandItem` wrapping `UnitConverterPage`.
5. `UnitConverterPage` (a `DynamicListPage`) handles live search: each keystroke calls `UpdateSearchText` → `BuildItems` → `RaiseItemsChanged`.

**Key files**:
- `UnitConverter/UnitConverter.cs` — COM-registered `IExtension` root class
- `UnitConverter/UnitConverterCommandsProvider.cs` — `CommandProvider` that exposes the top-level command list
- `UnitConverter/Pages/UnitConverterPage.cs` — all conversion logic and UI list items; pressing Enter on a result copies the converted value to the clipboard via `CopyTextCommand`
- `UnitConverter/Pages/GaugeTable.cs` — Manufacturer's Standard Gauge (MSG) lookup table for steel sheet (gauges 3–30), implemented as a switch expression

**Supported input formats** (all handled in `UnitConverterPage.BuildItems`):
- Decimal with unit: `25.4 mm`, `1 in`, `100mm`
- Pure fraction (inches): `3/8 in`, `3/8`
- Mixed fraction (inches): `1 3/4 in`, `2 1/2`
- Gauge lookup: `18g`, `18 ga`, `18 gauge`
- Bare number: shows both in→mm and mm→in

**Result format**: every conversion shows two list items — a rounded result (F2 for mm, F3 for in) and an exact result (G6 significant figures). Gauge results show four items (rounded + exact for each unit).

## AOT / Trimming Constraints

The project is AOT-compatible (`IsAotCompatible=true`). Avoid reflection, `dynamic`, or any APIs that require runtime type discovery. In Debug builds, trim/AOT analyzers run but trimming is disabled. In Release builds, trimming is fully enabled and trim warnings are treated as errors.

## Package Management

Package versions are centrally managed in `Directory.Packages.props`. Add new packages there with a `<PackageVersion>` entry, then reference them in the `.csproj` with `<PackageReference>` (no `Version` attribute).

## Supported Platforms

x64 and ARM64 only (`Directory.Build.props`). The solution file also lists x86 configurations but the project does not actually support it.
