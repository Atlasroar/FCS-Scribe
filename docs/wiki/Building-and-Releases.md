# Building and releases

For normal installation and use, see [Installation and workflow](Installation-and-Workflow).
This page covers source builds, reproducible validation and release packaging.
For compiler/test wrapper errors and their remedies, see
[Troubleshooting](Troubleshooting#build-and-validation-script-errors).

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

For task-table behavior, run non-showing WinForms checks with:

```powershell
.\scripts\Test.ps1 -FcsDirectory '<Kenshi>' -TodoViewOnly
```

This selector compiles all plugin source with the installed FCS Extended
contract and runs synthetic task controls in a 32-bit STA process. It creates
hidden control handles and drives date toggles/priority dropdowns without
showing forms, launching FCS, loading its executable or displaying modal
dialogs. It is not native-adapter or physical UI acceptance. Temporary
synthetic data is removed; the ignored test binary/contract copy remains.

`Integration-Test.ps1` copies the FCS executable, required libraries, definitions
and specified data into an isolated ignored `build\integration-fixtures`
directory. A 32-bit test host initializes actual FCS forms under a generic
WinForms message loop without launching FCS's complete startup/mod-selector
workflow. It exercises in-memory edits and normal save/reopen on disposable
output, not the source installation or original mod. Fixtures remain available
for inspection and can be deleted afterward.
Settings recovery, compilation scheduling, export protection, tracking/recovery and sampling boundaries each run in a fresh 32-bit process
to avoid retaining earlier large-model fixture allocations. Add `-SettingsOnly`
or `-ScheduleOnly`, `-ExportsOnly`, `-RecoveryOnly`, `-SamplingOnly`, `-NotesOnly`
or `-CompatibilityOnly`, `-ModWorkOnly` for one targeted suite (mutually exclusive). PDB-only symbols retain
useful failure locations without
full-debug JIT lifetime changes; this does not remove production memory limits.

`Inspect-Fcs.ps1` supports inspection of the locally installed build. Obtain
source binaries from an appropriate installed FCS/FCS Extended distribution;
this repository does not redistribute them.

`-ModWorkOnly` checks the real native record menu and multi-selection, opt-in
visibility, separate Scribe editors/files, manual/automatic/close persistence,
conflict/recovery controls, settings ownership, external-root retry and fresh
window discovery without changing native mod/notes bytes or dirty state.
The default combined run includes this host in its own fresh process too.
Dialogs are bypassed/programmatically driven at their boundaries; full
interactive FCS Extended startup/context-menu acceptance is still required.

See [Validation and limitations](Validation-and-Limitations) for recorded
results, coverage boundaries and the manual interactive acceptance gate.
Passing the standalone checks does not establish full FCS Extended acceptance.

`-RecoveryOnly` covers the persistent boundary UI, real unsaved edits discarded
by native saved-model reload, Resume No/Yes, new reversal recordings,
saved-but-unrecorded edits and exact private backup copies/replay. It injects
the discrepancy decision at the window boundary instead of clicking a native
dialog, and does not force a process crash or simulate physical power loss.

`-SamplingOnly` binds the real native Name property textbox and tests pending
text, wired Enter/Escape events, an actual LostFocus transition, sampled versus
unsampled reversals, no forced commit/save and retained UI/packet warnings.
Key events are raised programmatically, not sent as physical keyboard input;
other control types and complete interactive acceptance remain unverified.

`-NotesOnly` runs approved-note safety and then a separate cold-reopen host
process in the same disposable fixture. It uses two Name edits, creates native
fixture records when fewer than three exist, injects a completion-observer
failure after one real append, validates per-record/UI reporting and retries,
checks duplicate markers/inherited TODO/unrelated annotations, and explicitly
saves/reloads through native FCS. Gameplay is deliberately saved/reloaded before
export to settle dependency normalization; no fingerprint bypass is used.
The second process verifies saved blocks and duplicate rejection. This is not
physical FCS Extended acceptance or a simulated native disk/power failure.

`-CompatibilityOnly` runs executable/mode/session/context/source-label guards,
then the existing Workshop provenance/failure-isolation suite in a separate
fresh process. The default combined run also includes the compatibility host;
its normal first host already exercises Workshop.

```powershell
.\scripts\Integration-Test.ps1 -FcsDirectory '<Kenshi>' -FixtureMod '<Kenshi>\data\gamedata.base' -CompatibilityOnly
.\scripts\Integration-Test.ps1 -FcsDirectory '<Kenshi>' -FixtureMod '<representative.mod>' -CompatibilityOnly -VanillaStack
```

Compatibility tests add a controlled copied overlay using native `LOCKED`
loading, not a third-party locked mod. Active/inherited/locked edits and
reported-context changes are deliberate in-memory fixture mutations, not
physical UI clicks or real hot-unloading/reordering. A differing test-host
assembly is the negative hash case, not an inspected second FCS build.
Workshop runs without the four-file stack even when `VanillaStack` is selected;
do not combine its provenance count with a claim of full-stack Workshop testing.
Expected injected cache/preview failures log their causes; the host must still
finish with PASS and a successful exit. No Steam Refresh/Upload is invoked.

<a id="reproduce-performance-measurements-partial-s12"></a>

## Reproduce performance measurements

Run synthetic histories in a fresh x86 host without a game installation:

```powershell
.\scripts\Profile.ps1
.\scripts\Profile.ps1 -Entries 300 -Sessions 30
.\scripts\Profile.ps1 -Records 2000 -Entries 20 -Sessions 5 -LargeRecordCells 20000
```

Default dimensions are 2,000 records, 100 entries, 10 sessions and a 1,000-cell
changed record; other records have eight cells. Entries must divide evenly
across sessions. Every session is compiled and retained. The final history is
reopened three times with exact entry-count/fingerprint checks.

Use the isolated native profiling mode for locally installed official data:

```powershell
.\scripts\Integration-Test.ps1 -FcsDirectory '<Kenshi>' -FixtureMod '<Kenshi>\data\gamedata.base' -PerformanceOnly
.\scripts\Integration-Test.ps1 -FcsDirectory '<Kenshi>' -FixtureMod '<Kenshi>\data\gamedata.base' -PerformanceOnly -VanillaStack -ProfileIterations 30
```

`PerformanceOnly` is mutually exclusive with the targeted check suites and
is **not** run automatically by normal integration checks. `VanillaStack`
requires `PerformanceOnly` or `CompatibilityOnly` and copies official `gamedata.base`, `Newwworld.mod`,
`Dialogue.mod` and `rebirth.mod` into the disposable fixture, loading them
before the chosen copied active mod. It never uses the original files as
write targets. Profile iterations default to three (range 1-120).
Repeated native captures must retain record/cell counts and produce zero
recorded changes with matching fingerprints; these checks are included in
the unchanged-review measurement, not an extra capture fingerprint pass.

After `Profile.ps1` builds a host, check string-sharing compatibility against
a **copied** older full tracking baseline without opening/writing the project:

```powershell
& '.\build\performance-fixtures\<fixture>\PerformanceTests.exe' --verify-string-sharing '<copied-baseline.json>'
```

This validates the document, then compares its state fingerprint, serialized
state hash and original file hash before/after in-memory compaction. It does
not rewrite the baseline or validate the matching journal/session set; normal
Resume remains necessary for project validation.

Reports are JSON under ignored `build\performance-fixtures` or
`build\integration-fixtures`. They include source revision/working-tree label,
host executable hash, runtime/bitness, workload sizes, whole-operation elapsed
times, managed/private-memory endpoints and cumulative process peak working
set. No forced GC is performed. Console min/median/max summaries are descriptive;
profiling failures exit nonzero, but local timing budgets are not universal
CI/runtime assertions. See the exact
[measured envelopes](Validation-History#s12-performance-measurements-partial-beta-14)
and [current performance limits](Validation-and-Limitations#performance-and-long-sessions).

**Do not publish the fixture directories:** copied native files and full
tracking snapshots can contain private/dependency data and upstream binaries.
Share only reviewed/redacted aggregate reports. The native test host is not
the full interactive editor; its memory/timing result does not establish
multi-hour FCS Extended safety.

For an actual-editor soak, keep observations separate from the disposable
native host: pin the live editor's PID **and process start time**, sample
timestamped private bytes/working set/lifetime peak, and do not combine memory
epochs across a restart. Five-second endpoints can miss transient allocations;
the lifetime peak includes setup before sampling and is not remaining address
space. A timer completing establishes duration, not successful interactions.
Keep Scribe in Manual for this bounded procedure and derive edit/restore
counts from durable recording times/values, not a retrospective estimate.
Record human error/usability/compilation/Resume observations separately.
See the [archived two-hour actual-editor evidence](Validation-History#beta-15-two-hour-actual-editor-soak).
Keep full baselines, native files and private fixture contents out of public
reports; publish only reviewed aggregate results.

## Repository security automation

GitHub CodeQL **default setup** analyzes C# with the extended security suite,
using build mode `none` on GitHub-managed runners. It runs without a local
Kenshi installation or proprietary FCS binaries. It is not a plugin build or
the Windows/32-bit integration suite, and incomplete external type context can
limit analysis. Review run status and findings in the repository's Security
and Actions views; an enabled scanner is not a claim of no vulnerabilities.

Dependabot vulnerability alerts, secret scanning/push protection and private
vulnerability reporting are enabled in repository settings. There is no
package-manager manifest today, so dependency alerts do not inventory the
externally installed Framework/FCS libraries. Reassess coverage if OCS or other
packaged dependencies are introduced. No dependency manifest or runtime
dependency is added merely to enable alerts.

See the [security policy](https://github.com/Atlasroar/FCS-Scribe/blob/main/SECURITY.md)
for private reporting and scope. Use the
[test-result form](https://github.com/Atlasroar/FCS-Scribe/issues/new?template=05-test-result.yml)
to distinguish automated results, physical observations and untested boundaries.

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
`FCS_extended.json`, README, security policy, documentation, GPL-3.0 license and `release.json`.
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
documentation features. The [self-guided testing checklist](Testing-Checklist)
lists current-release user checks without claiming they have passed. The
[feasibility report](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/feasibility.md)
records integration findings and compatibility evidence. Version-specific
release notes live under
[`docs/releases`](https://github.com/Atlasroar/FCS-Scribe/tree/main/docs/releases).

### Keep each page focused

| Page | Owns | Keep elsewhere |
|---|---|---|
| Home | Orientation, navigation and brief current status | Release chronology and detailed test reports |
| Feature list | Implemented capabilities, controls and essential boundaries | Release history and full operational/recovery procedures |
| Installation and workflow | Setup, use, saving, backup and recovery procedures | Historical results and schema field reference |
| Troubleshooting | Symptoms, quoted messages and safe remedies | Changelog narration and acceptance claims |
| Validation and limitations | Current support boundary and remaining gates | Historical measurements/test logs |
| Testing checklist | Self-guided current-release checks and expected behavior | Historical results or unobserved Pass claims |
| Release history | Version index, original notes and upgrade differences | Current feature instructions |
| Validation history | Release/fixture-scoped results and dated observations | Blanket current-release acceptance |
| Architecture / Coding decisions | Components/data flow / design rationale | Changelogs |
| Data and AI contracts | Formats, invariants and required compatibility details | A release-by-release feature timeline |
| OCS integration / References | Explicitly unshipped proposal / sources and credits | Implemented-feature claims for planned work |

Keep old published anchors working when moving sections, update navigation,
and publish reviewed source changes to the separate wiki. Documentation-only
cleanup does not require a plugin version bump or replacing a released ZIP.
