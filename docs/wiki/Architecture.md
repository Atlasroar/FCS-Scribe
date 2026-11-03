# Architecture

The implementation keeps the editor boundary separate from the evidence model.
It does not replace FCS or load an AI provider.

| Component | Responsibility | Must not do |
| --- | --- | --- |
| `src\Core.cs` | Typed snapshots, immutable baseline copy, deterministic changes, fingerprints, packets, and draft validation | Depend on FCS, WinForms, or a provider SDK |
| `src\Tracking.cs` | Resumable documents, structural/history integrity checks, atomic checkpoint storage and settings | Read live FCS objects from worker threads or claim that legacy packets restore a full baseline |
| `src\Journal.cs` | Replayable record deltas, timestamped session logs, durable-head marker, writer lease, idempotent compilation, ownership-checked initialization/tail recovery and independent deadlines | Rewrite a full baseline each tick, claim unknown storage, discard uncompiled history or restore mod binaries |
| `src\RangeExport.cs` | Selected original packets, stable range identity and collision-free qualified evidence in schema-3 AI handoffs | Rewrite saved batches, collapse observed reversals or treat a range as a resumable baseline |
| `src\DraftWorkspace.cs` | Versioned portable original-packet/exact-text bundles, integrity validation and restored-draft readiness reporting | Invent missing evidence, approve restored text, save mods or treat draft text as tracking history |
| `src\SettingsStorage.cs` | Explicit load errors with same-stream hashes, cooperating save lease, stale-file checks and unique original-byte repair backups | Silently default invalid settings, overwrite an unreadable original or claim a universal filesystem lock |
| `src\ExportStorage.cs` | Active/inactive destination classification, reserved storage guards and observed-hash atomic JSON exports | Overwrite recognized tracking data, treat unclassifiable destinations as safe or claim universal filesystem exclusion |
| `src\SettingsDialog.cs` | New/resume choice, recording-range preview and manual/automatic review/export-directory settings | Automatically convert legacy evidence or export a range without confirmation |
| `src\FcsAdapter.cs` | Guard the FCS executable and read/update verified editor objects on the UI thread | Guess APIs, silently omit unsupported values, or write binary mods |
| `src\WorkshopArchive.cs` | Known-field XML cache contract and bounded, lossless local preview archiving | Contact Steam, overwrite prior archives, or re-encode/replace source images |
| `src\Plugin.cs` | Implement the upstream plugin interface, attach a toolbar action, present review/import/approval UI, and guard draft replacement/closing with revision-aware save/discard protection | Automatically trust/apply AI text or save the mod |
| `scripts` and `tests` | Build/package artifacts, inspect metadata, and exercise core/disposable-FCS behavior | Change the live installation during validation |

## Startup

FCS Extended discovers `FCS_extended.json` in enabled extensions and loads the
assembly listed in `FCS_Plugins`. Its `IPlugin.Init(Assembly)` call happens
before the FCS entry point. FCS-Scribe checks the supplied assembly, then waits
for WinForms `Application.Idle` to find the actual `baseForm` and attach UI.

No Harmony or native hook is added. RE_Kenshi/KenshiLib's native game-runtime
interfaces are not assumed to be managed editor APIs.
The Scribe form uses the actual `baseForm` as its standard WinForms `MdiParent`,
like native FCS windows. A separate bottom help label describes each action on
mouse hover or keyboard focus without replacing the persistent operation status.

Beta 9 keeps known settings read/parse/validation failures outside window
construction failure: a persistent recovery banner identifies the path/reason.
Temporary manual/active-folder values are explicitly labelled, with separate
guards disabling automatic recording and compilation. Settings can reload or
save a validated repair without replacing the current project or draft.
Unknown/programmer and fatal-resource errors are not converted into defaults.

Beta 2 tracking automation uses a UI-thread WinForms timer and a busy gate.
FCS snapshots are captured on that thread; only detached data is parsed,
validated, compared and written on workers. A successful durable session
recording precedes baseline adoption. Hidden/busy/minimized windows and visible
modal forms defer ticks; errors pause
automation, and closing prevents new ticks. Full-state tracking files are
local project state, not the compact AI handoff.
Beta 6 keeps known optional Workshop file/decode/busy-state failures separate
from ordinary model capture. The adapter reports partial availability and
explicit reasons while retaining known fields/readable components. These
states participate in context fingerprints, not gameplay change IDs. Fatal
resource errors and unsupported model/adapter contracts still fail explicitly.

The full starting baseline remains unchanged. Each reopening adds one session
log with a parent-checkpoint reference; recordings append changed records and
context. A small atomic marker records the durable head. Automatic compilation
appends missing entries once the oldest pending recording is 30 minutes old,
independently of recording deadlines; manual mode exposes Compile journal. Session originals
are retained and interrupted tails are never discarded without approval.

Beta 10 derives pending age from validated `RecordedUtc` values already present
in journal/session entries. Only in-memory stamps gain the timestamp; no stored
schema or marker changes. Replay restores the earliest uncompiled timestamp,
excluding compiled duplicates. A monotonic countdown runs within the window;
Resume restores elapsed age from UTC, including time closed/manual.
Configuration changes cannot re-anchor that countdown, and only successful
compilation can clear debt. A persistent notice is independent of operation
status and explains deferral/hold rather than silently compiling in manual mode.

## Evidence flow

```text
Live FCS model -> guarded adapter -> explicit baseline / current snapshot
                                      |
                                deterministic changes
                                      |
                              versioned JSON packet
                                      |
                              external AI assistant
                                      |
                         editable, validated draft + approval
                                      |
                        verified FCS Notes/TODO append + dirty flag
                                      |
                           user chooses normal FCS Save
```

Record identity uses stable IDs, not names. Base, active, locked, and effective
cells remain distinct. The exported packet includes change evidence and
context, not a full dump of the loaded record database.
Beta 5 range exports combine selected original packets and uniquely qualified
copied changes, not full snapshots/deltas. Source validation and range building
use detached data on workers; live capture and preview stay on the UI thread.
Export neither advances recording nor compiles sessions, and current-state
fingerprints still govern note application for historical ranges.

## Planned extension point

An optional OCS helper may later produce saved-only snapshots for the same core.
Its disk-backed context must remain distinguishable from the live FCS model.
It must not replace the live adapter for unsaved edits or notes insertion.
See [OpenConstructionSet integration](OpenConstructionSet-Integration).

## Source and release relationship

Wiki source lives in `docs\wiki` so decisions can be reviewed with code. The
published wiki has its own Git history. Release archives carry assembly
version information and a `release.json` source-commit identifier; the matching
tag/source archive supplies the corresponding source.
