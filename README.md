# FCS-Scribe

An experimental **FCS Extended plugin**, not a replacement editor, for tracking
Kenshi mod edits and reviewing AI-written documentation before appending it to
FCS's existing per-record Notes/TODO facility.

## Purpose and intended scope

FCS-Scribe is a documentation companion for mod authors. Its purpose is to turn
the changes you actually make in FCS into an auditable catalogue, help an AI
assistant explain those changes without inventing intent, and let you approve
the resulting notes before anything is inserted into the editor.

The long-term goal is reusable, evidence-backed mod documentation: session
history, changelogs, and eventually cross-linked wiki data. It is not intended
to replace FCS, autonomously edit gameplay, or make an AI provider mandatory.

| Area | Available in the experimental plugin | Intended expansion |
| --- | --- | --- |
| Tracking | One project baseline, timestamped delta session logs and resumable journal history | History browsing and multi-batch documentation workflows |
| Review scheduling | Manual or opt-in 30-second/1/5/10-minute recordings; manual/30-minute compilation | Broader interactive acceptance |
| Change evidence | Stable IDs, names, exact field/reference values, layers, instances, and unresolved-data warnings | Verified dependency and field-level provenance |
| AI documentation | Manual JSON export/import, editable drafts, evidence IDs, and explicit approval | Optional direct provider connection |
| FCS notes | Append through the existing Notes/TODO facility; preserve prior notes and use normal FCS Save | Improved workflow after interactive acceptance |
| Workshop information | Read-only local/staged metadata export and lossless preview-image archiving | Published Steam state/history requires a separately verified integration |
| Saved-data analysis | Not implemented | Optional read-only OpenConstructionSet backend and installation discovery |
| Published documentation | Project documentation and wiki | Markdown changelogs and dependency-resolved wiki-ready exports |

The **v0.1.0-beta.2 prerelease remains experimental**, restricted to one inspected FCS build.
Interactive FCS Extended acceptance and broader mod-stack validation remain
open; planned features are not included simply because they appear above.
See the [beta release notes](docs/releases/v0.1.0-beta.2.md) for resumable session
journals, recording/compilation scheduling and export-folder settings added
since beta 1.

**Important:** these notes are stored by FCS in editor-side
`data\__TODO__<user>.txt` files, **not inside the `.mod` file**. They are keyed by
record ID and can span mods. FCS-Scribe does not edit Steam Workshop change
notes. See the [integration feasibility report](docs/feasibility.md) for the
verified boundaries.

The [Copilot plan and feature checklists](docs/plan.md) track implemented work,
remaining acceptance checks, OCS integration, and future documentation features.

The [project wiki](https://github.com/Atlasroar/FCS-Scribe/wiki) explains the
workflow, architecture, coding decisions, data contracts, validation limits,
and [references and knowledge credits](https://github.com/Atlasroar/FCS-Scribe/wiki/References-and-Knowledge-Credits).
Its reviewed source pages live in [`docs/wiki`](docs/wiki).

## Requirements and build

- Windows, Windows PowerShell 5.1, and .NET Framework 4.8.
- [FCS Extended](https://github.com/BFrizzleFoShizzle/FCS_extended), installed
  using its upstream distribution.
- The inspected Steam FCS **2.14.2** executable. Compatibility is guarded by its
  exact SHA-256, not just its version string. GOG and other builds are not yet
  supported.

No .NET SDK, AI subscription, provider credentials, NuGet restore, or network
access is required to build or use the plugin. Build against the installed
upstream plugin contract:

```powershell
.\scripts\Build.ps1 -FcsDirectory 'D:\SteamLibrary\steamapps\common\Kenshi'
```

This creates `build\FCS-Scribe\FCS-Scribe.dll` and `FCS_extended.json`. It does
**not** install anything or modify the Kenshi installation.

With FCS closed, copy that **FCS-Scribe directory** to
`<Kenshi>\mods\FCS-Scribe`. Launch FCS Extended normally, enable FCS-Scribe in
its extension-selection window, and open a writable `.mod`. The plugin adds an
**FCS-Scribe** toolbar button. Do not copy or replace FCS Extended's own plugin
contract DLL. Begin with disposable mod copies and back up your FCS notes files.
Scribe opens inside FCS as a normal MDI child window. Hover over any action
button, or focus it with Tab, for an explanation at the bottom of the window;
the last operation's status remains visible separately.

For a packaged release, extract `FCS-Scribe-v<version>.zip` and copy its
`FCS-Scribe` folder to the same location. Release archives contain this project's
plugin, manifest, documentation, license, and source-commit metadata; they do not
bundle Kenshi, FCS, or upstream FCS Extended binaries.

## Workflow

1. Finish any active field edit and select **Start tracking**. Choose a new
   tracking JSON or resume an existing one after background validation. A new
   file starts from the current model; earlier edits are outside its baseline.
2. Edit the mod normally. **Review changes** compares against that baseline,
   including unsaved edits already committed to FCS's model, flushes new changes
   to the session log and advances the baseline only after a successful write.
3. **Export AI packet** saves schema-2 JSON containing exact before/after
   values, IDs, dependency/load-order and Workshop context, warnings, AI instructions, and
   a `ResponseTemplate`. Inspect the packet before sharing mod content with
   an external AI assistant. The plugin never sends it anywhere.
4. Ask the assistant to follow `Instructions` and return JSON matching
   `ResponseTemplate`. **Import draft** or **Paste draft**, then edit the JSON
   in the draft tab. Keep `BatchId`, `SourceFingerprint`, and valid `ChangeIds`.
   Fill in each `Text` and check its `TargetRecordId`.
5. Select **Apply approved notes** and explicitly approve the confirmation.
   Existing active and inherited notes are preserved. Existing TODO categories
   are retained. The plugin marks the editor dirty but does **not** save.
   Inspect FCS Notes, then use FCS's normal Save action.

A deleted record cannot hold a new note. Its response-template target is left
empty: choose a surviving record ID yourself, combining entries if necessary.
Each target can appear only once in a draft. An incomplete draft is rejected
rather than partially accepted.

FCS saving and applying notes do not advance the baseline. Starting a new file
or successfully saving a reviewed checkpoint does. A changed active mod,
actual load order, or locked-mod list requires
a new baseline. A save-time refresh of declared dependencies does not reset
tracking, but can invalidate an already-exported draft. Gameplay changes after
export block stale drafts. Batch markers detect repeat application to the same
record. Resume restores the saved baseline and history, but editable drafts
and their in-window export associations are not persisted; copy unfinished
drafts elsewhere before restarting.

## Tracking files and review settings

**Start tracking** offers **New tracking JSON** and **Resume existing JSON**.
The selected JSON is a full initial baseline, written once rather than on every
tick. Every start/resume creates a distinct UTC timestamp + unique-ID session
log. Parsing, replay/validation, comparison and disk work run in the background.
Live FCS capture remains on its UI thread and can pause large sessions.

For `fcs-scribe-tracking.json`, keep this storage together:

```text
fcs-scribe-tracking.json                  Initial full captured baseline
fcs-scribe-tracking.json.scribe\
  project.json                           Project identity and durable head
  journal.jsonl                          Compiled change entries
  sessions\<UTC timestamp>_<ID>.jsonl     One append-only log per session
  recovery\<ID>.bak                       Originals retained after explicit recovery
```

Each recording stores changed records and current context, not another full
loaded snapshot. Added/deleted records, provenance and context-only changes
remain replayable. Unchanged state adds no entry. Each entry is flushed to disk
before its baseline is adopted; a crash before compilation does not lose
already completed recordings.

**Plugin settings** selects **Manual** (default), **30 seconds**, **1 minute**,
**5 minutes** or **10 minutes**. Automatic mode compiles durable session entries
into the main journal every **30 minutes**; manual mode compiles only through
**Compile journal**. Compilation appends without duplicate entry IDs and never
deletes session files or rewrites the full baseline.

Automatic work runs while Scribe is visible and not minimized, busy or blocked
by a visible modal form. Deadlines are independent: recording and ordinary
button actions do not postpone compilation. Busy work defers one due operation,
not a backlog of overlapping ticks. Failures pause automation; reopen/validate
after a journal write failure. Closing stops new ticks; an in-flight write may
finish before its writer lease is released.
No automatic review saves a `.mod`, applies AI notes or sends network requests.

An exclusive project lease prevents cooperating Scribe instances from writing
the same project. Hash/chain checks reject detected outside edits and invalid
history; these are not signatures or a universal lock against external tools.
An interrupted final JSONL line requires explicit recovery approval: preserve
the original bytes in `recovery`, then remove only that incomplete tail.
Complete but corrupt entries are rejected, never skipped.

Resume replays the main journal and uncompiled sessions. Differences from the
current editor require confirmation, because they may be lost/discarded unsaved
edits. **Tracking is not a `.mod` backup and does not restore lost mod changes.**
New roots detect a missing storage folder/head. Older full-state tracking JSONs
remain readable unchanged, but cannot themselves signal a missing sidecar.

Close Scribe before manually archiving session logs. Only fully compiled
sessions are redundant; keep the baseline, `project.json`, main journal and
all uncompiled sessions. The UI shows the number awaiting compilation. No
history is automatically pruned. Back up the entire project, not just its JSON.

Old schema-1/2 **AI packets are not baselines**. Loading one asks for conversion:
preserve the exact original at `<file>.json.legacy.bak`, retain its evidence as
history, and establish a fresh checkpoint from the **current editor state**.
It does not reconstruct earlier state. Subsequent ticks append session entries;
the original legacy backup is not rotated away.

All JSON save dialogs default to the **active `.mod` file's own directory**.
Settings can choose another existing default directory; individual dialogs
still allow another location. Changing settings does not move an active
tracking file. Settings persist in `%LOCALAPPDATA%\FCS-Scribe\settings.json`.
File dialogs restore FCS's working directory, and manual exports cannot
overwrite the currently active tracking JSON.

**The baseline contains the full captured loaded model, including dependency
records; session logs also contain potentially private mod data. Do not send
tracking storage/backups to AI or include them in Workshop uploads.** Use **Export AI packet** for the compact
handoff. It exports pending changes, or the most recent checkpoint with record
evidence if its fingerprint still matches the current model. Older batches
remain in tracking history; a history browser/combined AI export is not yet
implemented.

## Workshop information and preview archives

The **Workshop info** tab reads the same local sources used by FCS's Steam
Workshop window. **Export Workshop info** writes a standalone JSON file even
without a tracking baseline or gameplay edits. AI packets also include this
information as `Context.Workshop`, not as evidence of new gameplay changes.

| Export field | Source and meaning |
| --- | --- |
| `ModName` | Workshop title, distinct from the active `.mod` filename |
| `Author`, `Version`, `Description` | Current active mod header, including committed unsaved edits |
| `Dependencies`, `ReferencedMods` | FCS-derived required mods and the additional parenthesized references in its dependency list |
| `Tags`, `SteamId`, `Visibility`, `VisibilityCode` | Matching Workshop dialog state, or its saved local `.info` cache |
| `ChangeNotes` | Current Workshop upload draft text; not published changelog history or record Notes/TODO |
| `LastUpdate` | Cached FCS timestamp, preserving its round-trip date/time and timezone kind; not filesystem modification time |
| `Preview` | Local preview availability, source filename, actual format, byte count and SHA-256 |

Open the active mod's **Steam Workshop** window first to capture its staged
title/tags/visibility and change-notes draft. Without a matching dialog, Scribe
reads the saved cache when present. Unknown fields stay `null` with warnings;
Steam ID `"0"` remains an explicit unpublished/default value. Steam IDs are
strings to avoid JSON number precision loss. No Steam request, Refresh, Save
or Upload is triggered. A cached visibility or timestamp is **not** a claim
about the current published listing. Metadata changes can invalidate exported
drafts, but do not produce record change IDs.

**Archive preview image** copies FCS's local `_<mod>.img` file byte-for-byte
to a new PNG, JPEG or GIF file using its actual encoding. Choose a new filename
outside the Kenshi/FCS installation; existing archives are never overwritten.
The source is neither replaced nor re-encoded. Missing, unsupported or oversized
images are reported explicitly (the inspected FCS limit is 1 MB). Preview
details in JSON do not embed the image: use this action to archive it separately.

## Catalogue and limitations

The reusable core is independent of FCS, WinForms, and AI providers. It compares
stable record IDs, names, typed scalar fields, layered reference values and
removal markers, and nested instance data. It preserves missing/null,
zero/false/empty, numeric types, and stored versus effective values.
JSON-pointer-style paths escape `~` and `/` in field names and IDs.

`active` means a stored override; `effective` means FCS's currently resolved
value. These may describe the **same edit**, not separate gameplay changes.
`base` and `locked` preserve available layer data; their individual source mods
are explicitly unresolved. This is not a dependency-resolved wiki exporter.
Unsupported scalar types stop extraction with an error.

Every packet is labeled **partial extraction**. General external files,
metadata change history, layouts, dialogue editor buffers, and pending control
edits are outside the catalogue. Current Workshop context and the local preview
are the documented exceptions, not comprehensive external-file tracking.
Do not infer completeness from a zero-change result. Changes wholly
outside the captured model cannot be used for stale-draft detection. Large
catalogues run synchronously on FCS's UI thread and can pause the editor.

Notes are ordinary editable text, not guaranteed-correct AI output. Evidence
IDs are checked, but factual correctness still requires human review.
Multi-record notes application is **not transactional**: errors report that
partial in-memory notes may exist; inspect them before retrying or saving.
No custom binary writer, silent mod save, mod overwrite,
Markdown/wiki export, or direct AI-provider integration is implemented.

## Validation and inspection

```powershell
.\scripts\Test.ps1 -FcsDirectory '<Kenshi>'
.\scripts\Integration-Test.ps1 -FcsDirectory '<Kenshi>' -FixtureMod '<path-to-a-record-bearing.mod>'
.\scripts\Integration-Test.ps1 -FcsDirectory '<Kenshi>' -FixtureMod '<Kenshi>\data\gamedata.base'
.\scripts\Inspect-Fcs.ps1 -FcsDirectory '<Kenshi>'
```

`Test.ps1` runs dependency-free core checks and optionally compiles the plugin.
`Integration-Test.ps1` copies the FCS executable, required libraries, definitions,
and specified data into an isolated directory under `build\integration-fixtures`.
It initializes actual FCS forms without launching the interactive editor or its
mod-selection dialog, exercises in-memory edits and the normal save handler,
and reopens the disposable output. It does not write to the source installation
or original mod. Fixtures are retained for inspection and may be deleted afterward.
The host is explicitly 32-bit, like this FCS build.

The standalone host does not replace a manual FCS Extended acceptance pass:
verify extension selection, toolbar placement, field-focus behavior, draft
editing/approval dialogs, and notes inspection in the real interactive editor.

To package a clean, committed checkout for release:

```powershell
.\scripts\Package.ps1 -FcsDirectory '<Kenshi>'
```

The script builds the plugin and generates a versioned archive and SHA-256
checksum under `build\release`. It does not publish or install the package.

## Authoritative upstream references

BFrizzleFoShizzle's documentation is authoritative for integration decisions
and must not be overridden without explicit instruction:

- [FCS Extended](https://github.com/BFrizzleFoShizzle/FCS_extended):
  managed editor plugin contract and loader.
- [RE_Kenshi](https://github.com/BFrizzleFoShizzle/RE_Kenshi):
  game-runtime extension and installation guidance.
- [KenshiLib](https://github.com/BFrizzleFoShizzle/KenshiLib):
  native game-runtime API and plugin-development best practices.

RE_Kenshi/KenshiLib's native game interfaces are not assumed to be managed FCS
editor APIs. This plugin uses FCS Extended's contract and WinForms's UI thread;
it adds no hooks or third-party detouring system.

Licensed under [GPL-3.0](LICENSE).
