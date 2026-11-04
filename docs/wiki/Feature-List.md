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
**v0.1.0-beta.6** keeps ordinary tracking available through known optional
Workshop/cache/preview failures, with explicit partial-capture reasons.
**v0.1.0-beta.7** adds portable draft workspaces and save/discard protection
for draft text and its original AI-packet association.
**v0.1.0-beta.8** makes failed new-project initialization and legacy conversion
recoverable without claiming unrelated storage or deleting unknown artifacts.
**v0.1.0-beta.9** opens invalid/unreadable settings in labelled safe manual
mode, with explicit reload/repair and original-byte/concurrent-save protection.
**v0.1.0-beta.10** preserves compilation debt across settings changes and
short sessions, with a persistent pending notice and explicit manual-mode hold.
**v0.1.0-beta.11** protects recognized inactive tracking anchors/backups and
reserved storage from export/workspace writes, including Workshop without an
open project.

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
| Portable draft workspace | Draft > Save workspace or Open workspace | Exact unfinished text plus its original packet, independently reopenable after restart |
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
- **Draft:** Import draft, Paste draft, and, since beta 7, Save workspace and Open workspace.
- **Apply approved notes:** a separate approval action.
- **Settings:** a separate configuration action, called Plugin settings in beta 2.

The original ten actions and two beta 7 workspace actions remain available. Hover over a menu action or select it with
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

In beta 8, initialization durably marks its intended baseline before committing
that JSON. If an operation fails, fix the reported storage/permissions problem.
If **no baseline JSON exists**, use **New** with the same filename; only a
recognized unfinished attempt can be retried, capturing the current editor.
If the **baseline JSON exists**, use **Resume**; a complete matching baseline
can finish its pending marker without being rewritten. Legacy conversion can
retry while its original packet remains, with the original `.legacy.bak` intact.

Preexisting history, foreign markers, changed sources and unrelated backups
block initialization rather than being erased. Old unflagged markers cannot
prove that an earlier attempt owned them: restore their original baseline or
choose another filename. Do not delete the whole `.scribe` folder to retry.
See [recovery details](Installation-and-Workflow#failed-project-initialization-beta-8).

Starting/resuming replaces the in-window export association and clears the
draft only after beta 7's save/discard protection. Save a draft workspace to
retain that independent association; tracking history does not contain draft text.
Saved baselines/history survive closing. Changing active mod,
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
In beta 7, unsaved draft text or a new export association requires
Save/Discard/Cancel before replacement. Inspect the packet for private content before sharing it
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

**Import draft** requires an exported batch, either newly exported or restored
from a beta 7 workspace.
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

### Portable draft workspaces (beta 7)

**Draft > Save workspace** writes one JSON containing the exact editable text
and its original AI packet, including schema-3 recording evidence. Unfinished,
invalid or empty draft JSON can be saved without inventing evidence or approving
notes. The window title marks unsaved work. **Open workspace** validates the
bundle and restores both pieces after restarting FCS; it does not need the
original export file or an active tracking session.

Opening reports stale state, mismatched batches, incomplete/invalid responses,
or already-applied notes. Such text remains available for review; Apply always
recaptures the editor, validates evidence/current state, and asks for approval.
A corrupt/missing associated packet or unsupported workspace fails before
replacing the current work. Text entered without an export can be saved as an
explicit text-only workspace, but cannot be applied without associated evidence.
Do not edit identifiers to associate old prose with a different export.

Close, Start/resume, new export, Import, Paste and Open all protect unsaved work:
**Yes** saves a workspace, **No** explicitly discards, and **Cancel** retains it.
Cancelling the subsequent file dialog or failing validation/writing retains the
old text and association. Opening/starting in the background cancels replacement
if you edit in the meantime. Saving captures a snapshot; later edits remain
unsaved. Closing while an operation is pending waits for you to close again
after it finishes. FCS parent-close may likewise need to be retried.

Saves use a flushed temporary file and atomic replacement with a `.bak` of the
previous workspace. Observed external file changes block overwriting; Save to
a new filename to retain your local work. Existing tracking files, AI packets
and unrelated JSON cannot be replaced by workspace Save. Defaults follow the
active-mod/settings folder. Workspaces contain private evidence and prose:
inspect before sharing and do not upload them as tracking or Workshop data.

This is explicit Save/Open, not autosave or crash recovery. Save before quitting;
forced termination can still lose later unsaved edits. Tracking sessions never
back up draft text, and workspaces never save/restore a mod, resume tracking,
remember blanket approval or mark evidence as globally documented.

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
values do **not** prove the current published listing's state.

In beta 6, malformed/locked caches and invalid, unsupported, oversized or
unreadable previews do not stop ordinary gameplay capture or recording.
Known header values and dependency/reference lists remain; readable metadata
and preview components are kept independently. During an active upload or
connected refresh, staged Workshop values and preview reads are skipped.
Offline disabled controls alone do not imply an active refresh.

Failures are not silently ignored: `CaptureStatus: "partial"` and structured
`Issues` identify the unavailable component/code/reason. They also appear in
snapshot/Workshop warnings, the bottom status, AI context and approval guidance.
Standalone Workshop export may therefore intentionally contain partial data;
inspect its status and warnings rather than treating nulls as verified defaults.
Expected missing files remain explicitly missing/unknown, not fabricated data.

Captures retry on subsequent actions/ticks and recover when data becomes
readable. Known fields and availability transitions still affect stale-draft
checks; recovered metadata can require re-export even without gameplay edits.
Unavailable payload contents cannot be validated merely because their error
status stayed unchanged. Fatal resource/contract/model failures still stop
the action; this is not a broad exception-suppression policy.
Workshop-only export/archive action failures still show an error, but do not
pause gameplay automation or clear a pause caused by an earlier tracking
failure. Ordinary tracking/model/journal errors retain their pause behavior.

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
Exports cannot overwrite recognized active or inactive tracking anchors or
reserved storage. Settings persist at
`%LOCALAPPDATA%\FCS-Scribe\settings.json`.
Beta 9 keeps the window usable when that file is corrupt, unsupported,
unreadable or names an unavailable custom directory. A persistent recovery
banner identifies the reason/path. Manual tracking and active-mod export
defaults remain usable; automatic recording and compilation stay disabled.
Opening the window does not rewrite the original settings.

**Discard edits and reload** reads/validates current disk settings, including
changes from another instance. A failed reload remains in safe manual mode.
**Save repaired settings** explicitly saves your chosen valid values only after
preserving readable original bytes in a unique `.recovery-<UTC>_<ID>.bak`;
ordinary `.bak` rotation cannot erase that recovery copy. An unreadable
original must become readable and be reloaded before it can be replaced.
Cancel leaves the file untouched. Stale or concurrently locked saves fail
instead of silently overwriting another cooperating instance. Reload/repair
retain the current project, original packet and unfinished draft.
See [settings recovery](Installation-and-Workflow#settings-recovery-beta-9).

File dialogs restore FCS's working directory. AI/Workshop exports, draft
workspace saves and new preview archives reject reserved tracking storage.
Existing unreadable/malformed JSON is not replaced; use a different filename.
Valid existing AI/Workshop exports remain replaceable with the normal dialog
confirmation. See [destination protection](Installation-and-Workflow#export-destination-protection-beta-11).

## 10. Compile journal: consolidate without deleting sessions

In beta 10, automatic mode compiles on the next eligible tick once the oldest
durable uncompiled recording is **30 minutes old**. Age includes time with
Scribe closed or in Manual. It is not a new 30-minute wait after each settings
save, interval change or session reopening. With no pending entries there is
no empty compilation timer. Each interval entry is already durable before this
step. Compilation appends missing entry IDs to the main journal, never rewrites
the full baseline, and can be retried without duplicating complete entries.
An interrupted final compilation line uses the same explicit recovery flow.

The persistent **Pending compilation** notice shows the count, oldest UTC,
due/overdue state and any manual/recovery/error hold, separately from transient
operation status. It wraps in narrow windows. **Manual mode never compiles by
itself**: use **Compile journal**. Settings recovery also holds automatic work.
Selecting an automatic interval can make retained old debt immediately eligible,
but compilation still waits for a visible, unblocked tick; busy/minimized/hidden
windows, open menus and modal dialogs defer work without advancing the deadline.
Errors preserve debt and pause automation; reopen/validate a faulted journal.

Folder-only saves/reloads preserve recording and compilation deadlines.
Changing intervals re-arms only recording. Successful compilation clears debt
without moving the next recording; a later first pending entry starts its own
30-minute age window. Reopening derives debt from existing validated timestamps,
without rewriting history or migrating storage. See
[compilation scheduling](Installation-and-Workflow#checkpoint-scheduling-and-storage).

Session originals are retained. The status shows how many recordings await
compilation. Close Scribe before archiving files yourself; only fully compiled
sessions are redundant. Keep the baseline, `project.json`, main journal and
all uncompiled sessions. New projects detect a missing storage folder/head;
older compatible baseline JSONs cannot signal missing sidecars themselves.
There is no automatic pruning or history-management UI yet.

## Planned features: not implemented

| Planned feature | Intended direction |
| --- | --- |
| General history-management UI | Inspect/manage saved history beyond the existing AI recording-range selector; draft workspaces are implemented in beta 7 |
| Markdown changelogs | Reuse verified change evidence for human-readable exports |
| Optional OpenConstructionSet helper | Read saved data out of process, without replacing unsaved-model access or writing open mods |
| Dependency-resolved wiki-ready exports | Produce linked documentation only after dependency/provenance validation |
| Direct AI-provider connections | Optional integration after the manual workflow is reliable |

These are roadmap items, not buttons or behaviors available in the beta.
The [remaining delivery scope](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md#remaining-delivery-scope)
separates S09-S16 stabilization from these five expansion areas and records
their prerequisites. The bounded beta 11 physical results and untested
exceptions are in [Validation and limitations](Validation-and-Limitations#real-fcs-extended-acceptance-beta-11);
they do not close the acceptance gate.
See the [working feature plan](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md)
and [OpenConstructionSet integration](OpenConstructionSet-Integration) for
their checklists and boundaries.
