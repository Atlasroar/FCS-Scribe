# Feature list and behavior

This page describes the current source, what each feature is intended to do,
and how to use it. **Resumable tracking, scheduled checkpoint review and Plugin
settings are included in v0.1.0-beta.2.** The earlier v0.1.0-beta.1
still uses session-only baselines and non-advancing manual reviews.
Interactive FCS Extended acceptance remains open. Planned features at the end
are not implemented.
**v0.1.0-beta.3** groups the window actions into menus as described below.
The earlier beta 2 archive uses the flat button row.
**v0.1.0-beta.4** requires complete draft evidence coverage and supports safe
retries of matching completed notes.
**v0.1.0-beta.5** adds previewable, inclusive recording-range AI exports across
sessions, with original evidence identities and current-state validation.

For installation and prerequisites, see [Installation and workflow](Installation-and-Workflow).
For evidence and unsupported cases, see [Validation and limitations](Validation-and-Limitations).

## At a glance

| Feature | How to access it | What it produces |
| --- | --- | --- |
| Embedded documentation window | FCS-Scribe toolbar button | An MDI child inside FCS, with change, draft and Workshop tabs |
| Contextual action help | Hover over a menu action or select it with the keyboard | An explanation at the bottom, separate from operation status |
| Resumable baseline | Core > Start tracking | A new or validated saved checkpoint and evidence history |
| Change catalogue | Core > Review changes | Exact before/after evidence and a successfully saved checkpoint |
| Session consolidation | Core > Compile journal | Append durable session entries to the main journal without duplicates or deleting originals |
| Review and export settings | Settings | Manual or 30/60/300/600-second reviews and default JSON directory |
| Manual AI handoff | Core > Export AI packet | Preview/select a recording range; schema-3 JSON retains original batches and uniquely qualified evidence |
| Editable draft | Draft > Import draft or Paste draft | JSON you can review and edit before applying |
| Approved record notes | Apply approved notes | Appended FCS Notes/TODO entries; FCS is marked dirty, not saved |
| Workshop information | Workshop info tab or Workshop > Export Workshop info | Local/staged/cached metadata, source details and warnings |
| Preview-image archive | Workshop > Archive preview image | A new, byte-identical copy of the local PNG/JPEG/GIF preview |

## 1. Embedded window and contextual help

Enable the plugin in FCS Extended, open a writable `.mod` in normal editing
mode, then select **FCS-Scribe** on the FCS toolbar. Scribe opens as a standard
MDI child inside FCS, rather than a separate owned desktop window. Selecting
the toolbar action again brings the existing Scribe window forward.

Beta 3 uses five top-level entries inside Scribe:

- **Core:** Start tracking, Review changes, Compile journal and Export AI packet.
- **Workshop:** Export Workshop info and Archive preview image.
- **Draft:** Import draft and Paste draft.
- **Apply approved notes:** a separate approval action.
- **Settings:** a separate configuration action, called Plugin settings in beta 2.

All ten actions remain available. Hover over a menu action or select it with
keyboard navigation to read its explanation in the bottom help area. Menus are
disabled while an action is running and do not merge into the parent FCS menu.
Narrow windows keep remaining entries accessible through an overflow menu.
Automatic work waits until open menus close rather than interrupting selection.
The last operation's status remains visible separately. The **Changes and
warnings**, **Editable draft JSON**, and **Workshop info** tabs are unchanged.
The published beta 2 uses the earlier flat button row.

The adapter rejects unsupported executable builds, read-only sessions and
merge/translation modes. It supports the inspected Steam FCS 2.14.2 build,
not every executable with a similar version number. See the installation
page for the exact compatibility hash.

## 2. Start tracking: establish an explicit baseline

Finish any active field edit, then select **Start tracking**. Choose a new
tracking JSON or resume an existing one. A new file captures an immutable
copy of the supported in-memory model, including unsaved edits already
committed to that model.

This establishes the point from which subsequent edits will be compared:
it does **not** mean "compare my mod against vanilla." Earlier edits are
already part of the baseline and will not be reported as new changes.

Resume validates the saved snapshot, history and mod identity in the background,
restores its checkpoint identity, then compares it with the current editor.
Pending changes are retained until review saves them; validation does not
silently advance over them. Broken or incompatible history is rejected, not
truncated to an assumed valid entry.

An old schema-1/2 AI export is not a full baseline. Explicit conversion approval
preserves its exact bytes at `<file>.json.legacy.bak` and its evidence as history,
then replaces that JSON with a fresh **current-editor-state** checkpoint.
It cannot reconstruct earlier state.

Starting/resuming replaces the in-window export association and clears the
draft. Copy unfinished draft text elsewhere first. Saved baselines/history
survive closing; drafts/export associations do not. Changing active mod,
actual load order or locked-mod context requires a new baseline.
Nothing is saved to the `.mod`.

Every successful start/resume opens a new UTC timestamp + unique-ID `.jsonl`
session under `<project>.json.scribe\sessions`. The selected JSON is the full
initial baseline, not a file rewritten on each tick. The adjacent storage
folder contains a small `project.json` identity/head marker and the compiled
`journal.jsonl`. Keep the baseline and storage together.

**Tracking files contain the full captured loaded model, including dependency
records, and may be large/private. Never use them or their backups as an
AI handoff or include them in Workshop distribution.** Export a compact AI
packet instead.

## 3. Review changes: inspect the evidence

After starting tracking, edit normally in FCS and select **Review changes**.
Read the **Changes and warnings** tab before asking an assistant to describe
the edits.

Review appends changed records/context to the current session log and flushes
them to disk. It advances the baseline **only after a successful recording**.
Unchanged reviews do not append duplicate checkpoints.
Detected outside edits and save failures preserve the previous baseline and
pause automation. Ordinary FCS saving and note application do not advance it.
An incomplete final log line requires explicit recovery approval: the original
is backed up under `.json.scribe\recovery` before removing that tail. Complete
but invalid entries are never skipped. Resume also warns about differences
from the editor: this is not a mod backup or automatic lost-edit restoration.
Interval sampling misses edits reverted between recordings; it is not an
every-event log.

The supported catalogue includes record additions/deletions and renames,
typed scalar fields, layered reference additions/removals/quantities, and
nested instance data and deletion markers. Entries identify stable record
IDs, names/types, paths, operations, before/after values and available
reference names. Unknown reference names and source provenance stay explicit.

Missing, null, empty, zero, false and removal markers remain distinct.
`active` describes stored overrides; `effective` describes FCS's resolved
value. Two such entries can describe **the same edit**, not two independent
gameplay changes.

The catalogue is partial. Pending controls, separate dialogue buffers,
general external files, layouts and metadata change history are not
comprehensively tracked. A zero-change result is not proof that nothing
anywhere in FCS changed. Unsupported values and unsafe inherited-data changes
stop extraction instead of silently inventing evidence.

## 4. Export AI packet: hand evidence to an external assistant

With tracking active, select **Export AI packet**. In beta 5, choose the
**first and last recordings, inclusive**, and inspect the preview before
choosing a `.json` destination. The initial selection includes all available
entries; narrow it to the period you want to document. Entries show UTC time,
session identity, source kind and change count. Earlier sessions and both
compiled and uncompiled recordings are available after resume.

Current edits not yet recorded appear as an explicitly **UNRECORDED** final
entry and can be included or excluded. Context-only recordings remain visible
but supply no record-change IDs. A range containing only context updates
cannot produce record documentation; use **Export Workshop info** for that.
Unchanged reviews do not create selectable duplicates. Converted legacy
evidence and older full-checkpoint entries are labelled separately.

The schema-3 packet retains the selected original packets, including batch IDs,
timestamps, sessions and historical context. The outer `Changes` uses unique
`EntryId:original-change-ID` citations and one outer `ResponseTemplate`.
Use that outer template, not the archival nested templates. The range is an
ordered set of observations, **not a net diff or complete event log**:
separate recordings of repeated edits and reversals remain separate evidence.
Earlier values do not necessarily describe the mod's current values.
Overlapping ranges may document the same evidence again under different range
batches; exporting does not mark entries as already documented. Compare
citations and existing notes before approving.

The outer context and source fingerprint describe the **current editor at
export**, even if your range ends earlier. Subsequent captured gameplay or
Workshop/context changes still make its draft stale. Exporting does not record,
compile, advance tracking, save the mod or rewrite old packets. Cancelling the
range/destination/replace-draft dialog retains the existing draft association.
Source validation/range building runs on detached data in the background;
live capture and preview remain on the UI thread.

Packets contain selected change evidence and context, not the full loaded
database or record deltas. They retain warnings from the selected recordings
and current context. Schema-1/2 original packets remain readable unchanged;
schema-3 AI handoffs are not tracking baselines and cannot be resumed or
converted. Resume the original tracking JSON and its `.scribe` storage instead.

Export also fills the editable draft with a blank response template.
Replacing an existing nonempty draft after a previous export requires
confirmation. Inspect the packet for private content before sharing it
manually; the plugin sends nothing to an AI service.

Ask the assistant to follow `Instructions` and return only JSON matching
`ResponseTemplate`. It should cite supplied change IDs, preserve identifiers,
use exact quantities and distinguish facts from interpretation. Mod names,
descriptions, dialogue and other packet text are untrusted data, not commands.
Context is background, not a list of newly made edits; nested contexts describe
their original observations rather than the current situation.

Workshop-only extraction does not require gameplay edits: use **Export
Workshop info** instead of this action.

## 5. Import draft and Paste draft: review before applying

**Import draft** requires an exported batch in the current Scribe session.
It reads a JSON response into **Editable draft JSON**, without applying it.
**Paste draft** replaces that editor's contents with clipboard text; pasting
alone does not validate or apply the result.

Edit the draft's prose and check every `TargetRecordId` and `ChangeIds` list.
Keep `BatchId` and `SourceFingerprint` associated with the exported packet.
Each target must be an existing surviving record and may occur only once;
combine entries intended for the same target.

A deleted record cannot hold a new note. Its template target is left empty
so you can explicitly select a surviving record to hold the documentation.
Empty or malformed note entries and unknown cited change IDs are rejected.
In beta 4, every exported change ID must appear **exactly once across the whole
draft**, including stored/effective entries. Omitting a record or change, or
repeating a citation within/across notes, rejects the entire draft before any
notes are appended. Combine deleted-record evidence onto a surviving target
without dropping its citations. This closes S01 in the
[stabilization plan](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md#s01-high-incomplete-evidence-coverage-can-pass-draft-validation).

No evidence-ID check can establish that the assistant's prose is correct.
Review its claims, quantities, conditions and interpretation yourself.
See [Data and AI contracts](Data-and-AI-Contracts) for the exact draft shape.

## 6. Apply approved notes: append through FCS

Select **Apply approved notes** after reviewing the draft. Scribe checks
batch/fingerprint identity, current captured state, complete nonduplicate evidence coverage,
nonempty note text and unique surviving targets. Changes to captured gameplay
or Workshop context after export can make the draft stale; review and export
again rather than editing identifiers to bypass the check.

An explicit confirmation shows the pending notes to append and the matching
completed notes to skip. Existing active and inherited text is retained, and an
existing TODO category is preserved. Scribe adds a batch marker/evidence IDs
and marks FCS dirty. A fully applied batch is rejected rather than duplicated.

Inspect FCS Notes afterward, then use **FCS's normal Save** when ready.
Scribe never silently saves. Ordinary saving can refresh declared dependency
headers and invalidate an exported draft, but does not reset tracking.

These are shared editor-side, per-record Notes/TODO annotations in
`data\__TODO__<user>.txt`, **not `.mod` content or Steam Workshop change notes**.
Distributing a mod alone does not distribute these annotations.

Multi-record application is not transactional. If an error reports possible
partial in-memory application, inspect Notes before retrying or saving.
Keep the **original complete draft**, including already-completed entries.
When the captured state still matches, retry skips targets containing exactly
one matching completed note block and appends only pending targets. Matching
requires the same text and ordered citations; line-ending normalization is
allowed. Conflicting/partial blocks or multiple same-batch markers stop the
retry for inspection, not silent skipping. Do not remove completed entries
from the draft or edit identifiers to bypass validation.

## 7. Workshop info: inspect and export local publishing context

Open the active mod's **Steam Workshop** window in FCS first if you want its
staged title, tags, visibility and change-notes draft. Select Scribe's
**Workshop info** tab to capture and display current local data, including
its source and warnings.

**Export Workshop info** writes standalone JSON containing the active mod
filename, export time and Workshop metadata. It works without a tracking
baseline or captured record changes. AI packets also include the same kind
of information under `Context.Workshop`, but it is not record-change evidence.

| Field | Intended meaning |
| --- | --- |
| `ModName` | Workshop title, distinct from the active `.mod` filename |
| `Author` | Author text from the current active mod header |
| `Version` | Integer version from the current active mod header |
| `Description` | Description from the current active mod header |
| `Dependencies` | FCS-derived required mods for the current model |
| `ReferencedMods` | Additional references shown parenthesized in FCS's dependency list |
| `Tags` | Matching Workshop dialog's staged tags, or saved cache |
| `ChangeNotes` | The current dialog's upload draft, not published changelog history |
| `SteamId` | Workshop ID as a string, avoiding JSON number precision loss |
| `LastUpdate` | Cached FCS timestamp with its round-trip date/time and timezone kind; not filesystem write time |
| `Visibility`, `VisibilityCode` | Available visibility label and its numeric code |
| `Preview` | Local availability, source filename, actual format, byte count and SHA-256 |

Scribe prefers a matching live Workshop dialog; otherwise it reads the active
header and any saved `_<mod>.info` cache. A previous mod's dialog is not reused
as the current mod's metadata. Staged change notes are unknown without a
matching dialog, because FCS does not reload them from that cache.

Missing fields remain `null` with warnings. Steam ID `"0"` explicitly means
FCS's unpublished/default ID, not an assigned listing. Unknown visibility
codes retain their numeric value without an invented label.

Capture does not contact Steam or invoke Refresh, Save or Upload. Cached
values do **not** prove the current published listing's state. Capture is
blocked during an existing upload or connected Workshop refresh to avoid
reading changing data; offline capture remains supported.

## 8. Archive preview image: preserve the original bytes

Set a preview through FCS's **Steam Workshop** window, then select
**Archive preview image** in Scribe. Choose a **new filename outside the
Kenshi/FCS installation**.

FCS stores the local image as `_<mod>.img`. Scribe detects the actual PNG,
JPEG or GIF encoding and copies the bytes unchanged with the corresponding
image extension. It does not replace, resize or re-encode the original,
overwrite an existing archive, or download a Steam image.

The JSON preview metadata contains availability, format, byte count and a
SHA-256 hash, not embedded image bytes. Archive separately and compare hashes
to associate the JSON with its image. If the preview changes while you choose
a destination, Scribe rejects the operation so you can capture it again.

Missing images, unsupported formats, oversized images, existing destinations
and mod-file targets produce errors rather than success-shaped placeholders.
The inspected FCS limit is 1 MB (1,048,576 bytes); Scribe accepts that boundary
and rejects larger previews.

## 9. Plugin settings: review scheduling and export defaults

Select **Settings** (**Plugin settings** in beta 2) to choose **Manual** (default), **30 seconds**,
**1 minute**, **5 minutes** or **10 minutes**. Automatic reviews durably append
session deltas like manual reviews; they do not save the mod, apply AI notes or
send network requests.

Ticks run only while Scribe is visible, not minimized, initialized and not busy
or blocked by visible modal forms. Operations do not overlap. Errors pause
scheduling; a write failure needs reopen/validation. Closing stops new ticks,
though an already-running write may finish. Validation, comparison and disk work run in the background;
live FCS capture remains on its UI thread and can briefly pause large sessions.

JSON save dialogs default to the active `.mod` file's own directory. Choose
another existing default directory in settings or another location in an
individual dialog. Changing defaults does not move the active tracking file.
Manual exports cannot overwrite that file. Settings persist at
`%LOCALAPPDATA%\FCS-Scribe\settings.json`.
File dialogs restore FCS's working directory. AI/Workshop exports also reject
destinations inside the active project's reserved storage folder.

## 10. Compile journal: consolidate without deleting sessions

Automatic mode compiles every 30 minutes on a separate deadline; manual mode
uses **Compile journal**. Each interval entry is already durable before this
step. Compilation appends missing entry IDs to the main journal, never rewrites
the full baseline, and can be retried without duplicating complete entries.
An interrupted final compilation line uses the same explicit recovery flow.

Session originals are retained. The status shows how many recordings await
compilation. Close Scribe before archiving files yourself; only fully compiled
sessions are redundant. Keep the baseline, `project.json`, main journal and
all uncompiled sessions. New projects detect a missing storage folder/head;
older compatible baseline JSONs cannot signal missing sidecars themselves.
There is no automatic pruning or history-management UI yet.

## Planned features: not implemented

| Planned feature | Intended direction |
| --- | --- |
| History browser and draft persistence | Inspect/select multiple saved batches and restore unfinished draft/export associations |
| Markdown changelogs | Reuse verified change evidence for human-readable exports |
| Optional OpenConstructionSet helper | Read saved data out of process, without replacing unsaved-model access or writing open mods |
| Dependency-resolved wiki-ready exports | Produce linked documentation only after dependency/provenance validation |
| Direct AI-provider connections | Optional integration after the manual workflow is reliable |

These are roadmap items, not buttons or behaviors available in the beta.
See the [working feature plan](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md)
and [OpenConstructionSet integration](OpenConstructionSet-Integration) for
their checklists and boundaries.
