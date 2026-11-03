# Installation and workflow

Resumable tracking, automatic checkpoint reviews and Plugin settings below
are included in **v0.1.0-beta.2**. The earlier beta 1 uses session-only baselines.

## Prerequisites

Use Windows, .NET Framework 4.8, an installed FCS Extended distribution, and
the inspected Steam FCS 2.14.2 executable. Compatibility is guarded by this
exact SHA-256, not just its display version:

```text
3688c4d8421d51c2d30c2ff0338874779d91eff150c6a02bf415bc1cdd9c7796
```

Unknown builds are rejected. Do not remove the guard to try another build:
inspect and validate its integration first. GOG compatibility is not claimed.

## Install a release

Download a release from [GitHub releases](https://github.com/Atlasroar/FCS-Scribe/releases)
and verify its ZIP against the matching SHA-256 checksum. Close FCS.
Extract `FCS-Scribe-v<version>.zip`, then copy its `FCS-Scribe` folder into
`<Kenshi>\mods\FCS-Scribe`. Launch FCS Extended normally, enable FCS-Scribe in
the extension selector, and open a writable `.mod` in normal editing mode.
The plugin adds an FCS-Scribe toolbar button and opens as an embedded MDI child.
Hover over action buttons, or focus them with Tab, to see descriptions at the
bottom; operation status stays visible separately.
In **v0.1.0-beta.3**, actions are grouped into **Core**, **Workshop**
and **Draft** menus, with separate **Apply approved notes** and **Settings**
entries. See the [feature list](Feature-List#1-embedded-window-and-contextual-help)
for the menu layout and keyboard help; the published beta 2 keeps its button row.

Install FCS Extended from its upstream distribution; this project's archive
does not include or replace upstream binaries. Start with disposable mods and
back up the editor's notes files. See [Validation and limitations](Validation-and-Limitations)
before using the experimental plugin on important work.

## Build from source

See [Building and releases](Building-and-Releases) for requirements, the build
command, output files, validation and packaging. Building does not install
anything or modify your game. Do not replace FCS Extended's own contract DLL.

## Document a batch

1. Finish any active field edit and select **Start tracking**. Create a tracking
   JSON or resume an existing one after background validation. Earlier edits
   are deliberately outside a new file's captured baseline.
2. Edit normally and select **Review changes**. Read the warnings as well as
   the exact before/after evidence. Review flushes changed records/context to
   the current timestamped session log, then advances its baseline.
3. **Export AI packet**: in beta 5, select an inclusive first/last recording
   range and inspect its preview before choosing the JSON destination.
   Earlier sessions remain selectable after resume. Current unrecorded edits
   are explicitly labelled; including them does not create a recording.
   Inspect the packet before sharing it with an external assistant; the plugin
   sends nothing itself.
4. Ask the assistant to follow the packet instructions and return the response
   **outer** template as JSON, using its qualified change IDs rather than
   nested archival templates. Import or paste it into the editable draft.
5. Verify target record IDs, quantities, conditions, and prose. In beta 4,
   cover every exported change ID exactly once across the complete draft.
   Select **Apply approved notes** and explicitly approve.
6. Inspect FCS Notes, then use FCS's ordinary **Save** action when ready.

Existing active and inherited notes are preserved. An existing TODO stays a
TODO rather than being silently reclassified. Applying notes marks the editor
dirty but does not save or advance the tracking baseline.

## Checkpoint scheduling and storage

**Plugin settings** offers Manual (default), 30 seconds, 1 minute, 5 minutes
and 10 minutes. Automatic reviews durably append session deltas while the window
is visible, not minimized/busy or blocked by visible modal forms; they never
save the mod or apply AI notes. In beta 10, compilation becomes due when the
oldest durable uncompiled recording is 30 minutes old, including time closed or
manual. It runs on the next eligible automatic tick, not inside settings Save
or Resume. Manual mode uses **Compile journal** explicitly.
Errors pause automation; reopen/validate after a journal write failure.

The persistent **Pending compilation** notice shows the count, oldest UTC,
due/overdue state and any manual/recovery/error hold. Settings saves/reloads and
short sessions retain this debt. A folder-only edit does not reset recording
either; an interval change re-arms recording without moving compilation.
Hidden/minimized/busy windows, open menus and modal dialogs defer work without
acknowledging the deadline. After successful compilation there is no empty
timer: the next first pending recording starts a fresh age window.

JSON save dialogs default to the active mod's actual directory. Settings can
choose another existing default; dialogs still allow individual locations.
Changing settings does not move an existing tracking file. The settings file
is `%LOCALAPPDATA%\FCS-Scribe\settings.json`.

### Settings recovery (beta 9)

If settings are invalid, unsupported, unreadable or reference an unavailable
custom directory, Scribe opens with a persistent **SETTINGS RECOVERY** banner.
It names the file and reason. Safe manual mode uses active-mod export defaults;
automatic recording and compilation are disabled. Start/resume, manual Review
and Compile, Workshop actions and draft work remain available. No original
settings are rewritten just by opening Scribe.

Open **Settings**. **Discard edits and reload** discards only the dialog's
unsaved configuration choices and validates the current disk file; it does not
discard the working draft or project. Fix access/directory problems and reload,
or choose valid values and explicitly **Save repaired settings**. Repair
preserves readable original bytes in a unique
`settings.json.recovery-<UTC>_<unique-ID>.bak` before atomic replacement.
Later saves never rotate away that original. Cancel writes nothing.

An unreadable original cannot be safely replaced until access is restored and
Reload obtains its bytes. If another instance changes or locks shared settings,
Save fails explicitly; reload after it finishes before choosing and saving
values again. Valid reload/Save exits recovery and adopts its selected interval.
The current tracking project, original AI packet and exact draft text remain
in place. Beta 10 preserves compilation debt through these settings actions;
recovery never enables automatic work until a valid reload or explicit repair.

### Project storage

The selected JSON is a full baseline, not a per-tick rewrite. Adjacent
`<file>.json.scribe\project.json` tracks identity/head, `sessions` holds one UTC
timestamp + unique-ID JSONL log per start/resume, and `journal.jsonl` receives
compiled entries. Compilation is idempotent and keeps session originals.
For example:

```text
fcs-scribe-tracking.json                  Initial full captured baseline
fcs-scribe-tracking.json.scribe\
  project.json                           Project identity and durable head
  journal.jsonl                          Compiled change entries
  sessions\<UTC timestamp>_<ID>.jsonl     One append-only log per session
  recovery\<ID>.bak                       Originals retained after explicit recovery
```

Each recording stores changed complete records and current context, not another
full loaded snapshot. Added/deleted records, provenance and context-only changes
remain replayable. Unchanged state adds no entry. Each recording is flushed
before its baseline is adopted; a crash before compilation does not lose
already completed recordings.

Recording and ordinary button actions do not postpone the compilation deadline.
Busy work defers one due operation, not a backlog of overlapping ticks.
An exclusive project lease coordinates Scribe writers, and hash/chain checks
detect observed external edits; these are not signatures or a universal lock
against external tools. Closing stops new ticks; an in-flight write may finish
before its writer lease is released.

Back up this entire storage with the baseline. Close Scribe before manually
archiving fully compiled sessions; uncompiled logs remain required. No pruning
or history-management UI is implemented.

An interrupted last line requires recovery approval; the original is preserved
under `.json.scribe\recovery` before removing only that tail. Complete corrupt
entries are rejected. New roots flag missing storage/head; older compatible
full-state roots cannot themselves detect missing sidecars.

Loading a legacy AI packet requires conversion approval: retain original bytes at
`<file>.json.legacy.bak` and its evidence as history, then establish a **fresh
current-editor baseline** in that same JSON. Old packets cannot reconstruct
their missing baseline. Detected external edits, corruption and incompatible
mod context fail explicitly rather than being overwritten or truncated.
Only original schema-1/2 AI packets support this conversion. A beta 5 schema-3
recording-range handoff is not a baseline; resume the original tracking JSON
with its `.scribe` folder instead.

**Tracking JSONs/backups contain the full captured loaded model, may be large
and private, and must not go to AI or Workshop.** Use the compact **Export AI
packet** action, which exports only the chosen recording range, original
compact evidence and relevant context, not full loaded snapshots or deltas.
The outer fingerprint describes the current editor at export even for an
older range; subsequent captured changes make its draft stale. Exporting does
not advance tracking or compile history. Ranges preserve repeated edits and
reversals rather than calculating a net diff. Overlapping ranges may document
the same observations again; check existing notes and citations before approval.

## Capture Workshop information and the preview

Open the active mod's FCS **Steam Workshop** window to make its staged values
and change-notes draft available. Scribe's **Workshop info** tab shows the
current local metadata, source and warnings. **Export Workshop info** writes
JSON without requiring a tracking baseline or gameplay edits. AI packets also
include Workshop context, but do not treat it as new record changes.

If that dialog has not been opened for the active mod, header fields and any
saved `.info` cache remain readable; staged change notes are unknown. No Steam
query, refresh, save or upload occurs. Title/author/version/description,
required and referenced mods, tags, change-notes draft, Steam ID, cached update
time, visibility and preview details are all represented, including unknowns.

Use **Archive preview image** for a separate, byte-identical image copy.
Choose a new PNG/JPEG/GIF filename outside Kenshi; existing archives are not
overwritten. The preview hash in JSON associates it with the copied image.
Missing images and unsupported/oversized encodings fail explicitly. Local
metadata is not proof of the current published Steam listing or its changelog.

In beta 6, known optional Workshop failures no longer stop Start tracking,
Review changes, automatic recording or AI range export. The Workshop tab/JSON
reports `CaptureStatus: "partial"` with component-specific `Issues`; tracking
warnings and bottom status repeat the notice. Header fields and any readable
component remain available. Expected absent files stay explicitly unknown.
During an active upload/refresh, staged metadata and preview reads wait while
gameplay capture continues; Scribe itself never invokes those operations.

Repair the source/cache/image or wait for the Workshop operation to finish,
then capture/review again. Recovery is recorded as context and can stale an
existing draft, requiring re-export. Scribe does not repair or replace source
files. Preview archiving remains strict: unavailable/unsupported images cannot
be archived, and an upload/refresh beginning during file selection blocks it.
Unavailable payloads are not validated; unchanged error status does not prove
their bytes stayed unchanged. Unexpected model/contract and fatal resource
errors still stop the action.
Workshop-only export/archive action errors are reported without pausing
gameplay recording; they do not re-enable already-paused automation. Ordinary
tracking/model/journal failures still pause it.

## Recovery and lifecycle

### Failed project initialization (beta 8)

New projects and approved legacy conversions use a pending initialization marker
before committing the baseline. Ordinary errors retain recognizable pending
state rather than leaving an unexplained "project already exists" blocker.

1. Fix the reported write, permissions, sharing or destination-path problem.
2. If a new project's baseline JSON **does not exist**, choose **Start tracking >
   New** with the same filename. Only recognized pending metadata with no
   history can be reused. This captures a fresh current-editor baseline; it does
   not restore the failed attempt's in-memory snapshot.
3. If the baseline JSON **exists**, choose **Resume existing JSON**. Resume
   validates exact baseline bytes, project identity, empty initial history and
   any required legacy backup before finalizing a pending marker. It does not
   rewrite the committed baseline. Normal current-editor difference confirmation
   still applies.
4. If the file is still an original schema-1/2 AI packet, Resume offers the
   ordinary explicit conversion again. Retry checks the same source hash and
   intact `.legacy.bak`; after baseline replacement, use normal Resume instead.

Initialization never claims preexisting session/journal/recovery data, a foreign
marker, unrelated baseline/marker backups or a changed legacy source. Missing
or changed required data fails explicitly. Restore the matching files from
backup or choose a separate filename; preserve unrelated files before doing
manual moves. Earlier beta markers without pending metadata cannot establish
ownership, so they are not automatically deleted or repaired. A missing marker
with only a backup also requires restoration, not automatic claiming.

Do not delete the whole `.scribe` directory to get past an error. No recursive
cleanup is part of recovery. Ordinary failed atomic writes remove only their
own temporary file; forced termination may leave temporary files, which are
not automatically swept. Source snapshots and backups may contain private data.
This recovery covers initialization, not missing recorded history, mod
restoration or arbitrary filesystem/power-loss guarantees.

### Drafts, notes and tracking

A deleted record cannot receive a new note: explicitly choose a surviving
record to hold its documentation. Combine entries if they share a target.
Stale drafts and repeat application of a batch to the same record are rejected.

In beta 7, **Draft > Save workspace** preserves exact unfinished text with its
original packet. **Open workspace** restores it after restart, independently
of tracking; it reports stale/invalid responses and never applies them.
Unsaved work is protected before close, Start/resume, Export, Import, Paste and
Open with Yes (save), No (discard), or Cancel. A failed/cancelled save retains
the current draft. Later edits during saving remain unsaved; edits during
background Open/Start cancel replacement. Closing during work must be retried
when it finishes. Save explicitly before quitting: forced termination is not
covered by these prompts or an autosave system.

Workspaces are separate JSON documents, not tracking baselines or AI responses.
They include private evidence/prose and keep a previous-file `.bak` on replacement.
Observed external edits block overwrite; save to a new filename instead.
Neither tracking journals nor a workspace restore the mod or confer approval.
Resume tracking separately using its JSON after reopening. A changed active mod
or actual loaded/locked-mod context requires a new baseline. Normal saving does
not reset tracking, although dependency-header refreshes may require re-export
before applying an older draft.

Resume replays the main journal and uncompiled sessions. Differences from the
current editor require confirmation because they may be lost/discarded unsaved
edits. Tracking is **not a `.mod` backup** and does not restore lost mod changes.

Multi-record application is not transactional. If an error reports partial
notes, inspect them before retrying or saving. Do not infer safety from a zero
completed-entry count: an interrupted FCS operation may have made partial
in-memory changes.
Beta 4 retries require the original complete draft and unchanged captured
state. Matching completed notes are skipped; only pending targets are appended.
Conflicting text/evidence, partial blocks or multiple same-batch markers stop
the operation for inspection. Do not delete completed entries from the draft.
In beta 7, reopen the saved workspace containing the original complete draft
and packet for a retry; fresh current-state/existing-notes checks still apply.
