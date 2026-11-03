# Building and releases

For normal installation and use, see [Installation and workflow](Installation-and-Workflow).
This page covers source builds, reproducible validation and release packaging.

## Build requirements

- Windows, Windows PowerShell 5.1 and .NET Framework 4.8.
- [FCS Extended](https://github.com/BFrizzleFoShizzle/FCS_extended), installed
  from its upstream distribution, including its managed plugin contract.
- The supported Steam FCS installation described in
  [Installation and workflow](Installation-and-Workflow#prerequisites).

No .NET SDK, NuGet restore, AI subscription, provider credentials or network
access is required for these local build/validation commands once the required
FCS/FCS Extended files are installed. The plugin itself does not connect to an
AI provider; exporting and sharing a packet is a separate manual action.

## Build from source

Run in Windows PowerShell 5.1 from the repository root:

```powershell
.\scripts\Build.ps1 -FcsDirectory 'D:\SteamLibrary\steamapps\common\Kenshi'
```

Replace the example with your actual Kenshi directory. The script uses the
Framework C# compiler and the installed FCS Extended contract. It produces
`build\FCS-Scribe\FCS-Scribe.dll` and `FCS_extended.json`.

Building does **not** install the plugin or modify the Kenshi installation.
Follow [Installation and workflow](Installation-and-Workflow#install-a-release)
to install the resulting `FCS-Scribe` folder with FCS closed. Do not copy or
replace FCS Extended's own contract DLL.

## Run checks and inspect the supported build

Run from the repository root, replacing the placeholder paths:

```powershell
.\scripts\Test.ps1 -FcsDirectory '<Kenshi>'
.\scripts\Integration-Test.ps1 -FcsDirectory '<Kenshi>' -FixtureMod '<record-bearing.mod>'
.\scripts\Integration-Test.ps1 -FcsDirectory '<Kenshi>' -FixtureMod '<Kenshi>\data\gamedata.base'
.\scripts\Inspect-Fcs.ps1 -FcsDirectory '<Kenshi>'
```

`Test.ps1` runs dependency-free core checks and optionally compiles the plugin.
Omit `-FcsDirectory` to run only the core checks.

`Integration-Test.ps1` copies the FCS executable, required libraries, definitions
and specified data into an isolated ignored `build\integration-fixtures`
directory. A 32-bit test host initializes actual FCS forms under a generic
WinForms message loop without launching FCS's complete startup/mod-selector
workflow. It exercises in-memory edits and normal save/reopen on disposable
output, not the source installation or original mod. Fixtures remain available
for inspection and can be deleted afterward.
Settings recovery and compilation scheduling each run in a fresh 32-bit process
to avoid retaining earlier large-model fixture allocations. Add `-SettingsOnly`
or `-ScheduleOnly` for one targeted suite (not both). PDB-only symbols retain
useful failure locations without
full-debug JIT lifetime changes; this does not remove production memory limits.

`Inspect-Fcs.ps1` supports inspection of the locally installed build. Obtain
source binaries from an appropriate installed FCS/FCS Extended distribution;
this repository does not redistribute them.

See [Validation and limitations](Validation-and-Limitations) for recorded
results, coverage boundaries and the manual interactive acceptance gate.
Passing the standalone checks does not establish full FCS Extended acceptance.

## Package a release

Start from a **clean, committed checkout** and run:

```powershell
.\scripts\Package.ps1 -FcsDirectory '<Kenshi>'
```

The script rebuilds the plugin and generates
`build\release\FCS-Scribe-v<version>.zip` and its `.zip.sha256` checksum.
The version comes from the plugin's assembly metadata, and the package records
the exact source commit. Packaging does not publish or install anything.

Release archives contain the `FCS-Scribe` folder with this project's plugin,
`FCS_extended.json`, README, documentation, GPL-3.0 license and `release.json`.
No Kenshi, FCS or upstream FCS Extended binaries are bundled.

Download published assets from
[GitHub releases](https://github.com/Atlasroar/FCS-Scribe/releases).
Compare the ZIP's SHA-256 with its matching `.sha256` asset before installing.
The matching Git tag/source archives supply corresponding source;
`release.json` identifies the package's version, source commit, supported FCS
hash and experimental status.

## Documentation sources and project checklists

The wiki is the detailed user guide; the README is a short feature/wiki index.
Reviewed wiki source pages live under
[`docs/wiki`](https://github.com/Atlasroar/FCS-Scribe/tree/main/docs/wiki).
Updating those files does not automatically publish the separate GitHub wiki;
keep the source and published pages in sync.

The [working feature plan](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md)
tracks implemented work, acceptance checklists, OCS integration and future
documentation features. The
[feasibility report](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/feasibility.md)
records integration findings and compatibility evidence. Version-specific
release notes live under
[`docs/releases`](https://github.com/Atlasroar/FCS-Scribe/tree/main/docs/releases).
