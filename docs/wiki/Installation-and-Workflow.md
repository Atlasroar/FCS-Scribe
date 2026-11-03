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
save the mod or apply AI notes. A separate 30-minute deadline compiles entries
into the main journal. Manual mode uses **Compile journal** explicitly.
Errors pause automation; reopen/validate after a journal write failure.

JSON save dialogs default to the active mod's actual directory. Settings can
choose another existing default; dialogs still allow individual locations.
Changing settings does not move an existing tracking file. The settings file
is `%LOCALAPPDATA%\FCS-Scribe\settings.json`.

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

## Recovery and lifecycle

A deleted record cannot receive a new note: explicitly choose a surviving
record to hold its documentation. Combine entries if they share a target.
Stale drafts and repeat application of a batch to the same record are rejected.

Closing the plugin window/editor loses editable drafts/export associations,
not the saved checkpoint. Resume its JSON after reopening; copy unfinished
draft text elsewhere before restarting. A changed active mod
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
Draft/export associations still do not survive closing Scribe.
