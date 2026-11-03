# Installation and workflow

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

Close FCS. Extract the release archive, then copy its `FCS-Scribe` folder into
`<Kenshi>\mods\FCS-Scribe`. Launch FCS Extended normally, enable FCS-Scribe in
the extension selector, and open a writable `.mod` in normal editing mode.
The plugin adds an FCS-Scribe toolbar button.

Install FCS Extended from its upstream distribution; this project's archive
does not include or replace upstream binaries. Start with disposable mods and
back up the editor's notes files. See [Validation and limitations](Validation-and-Limitations)
before using the experimental plugin on important work.

## Build from source

Run in Windows PowerShell 5.1 from the repository root:

```powershell
.\scripts\Build.ps1 -FcsDirectory '<Kenshi>'
```

The build uses the installed FCS Extended plugin contract and the Framework
C# compiler; it needs no .NET SDK or provider credentials. Output is written
under `build\FCS-Scribe` without installing anything.

## Document a batch

1. Finish any active field edit and select **Start tracking**. Earlier edits
   are deliberately outside the captured baseline.
2. Edit normally and select **Review changes**. Read the warnings as well as
   the exact before/after evidence.
3. **Export AI packet** to JSON. Inspect it before sharing mod content with
   an external assistant; the plugin sends nothing itself.
4. Ask the assistant to follow the packet instructions and return the response
   template as JSON. Import or paste it into the editable draft.
5. Verify target record IDs, evidence IDs, quantities, conditions, and prose.
   Select **Apply approved notes** and explicitly approve.
6. Inspect FCS Notes, then use FCS's ordinary **Save** action when ready.

Existing active and inherited notes are preserved. An existing TODO stays a
TODO rather than being silently reclassified. Applying notes marks the editor
dirty but does not save or advance the tracking baseline.

## Recovery and lifecycle

A deleted record cannot receive a new note: explicitly choose a surviving
record to hold its documentation. Combine entries if they share a target.
Stale drafts and repeat application of a batch to the same record are rejected.

Closing the plugin window/editor loses the current baseline/export association;
copy unfinished draft text elsewhere before restarting. A changed active mod
or actual loaded/locked-mod context requires a new baseline. Normal saving does
not reset tracking, although dependency-header refreshes may require re-export
before applying an older draft.

Multi-record application is not transactional. If an error reports partial
notes, inspect them before retrying or saving. Do not infer safety from a zero
completed-entry count: an interrupted FCS operation may have made partial
in-memory changes.
