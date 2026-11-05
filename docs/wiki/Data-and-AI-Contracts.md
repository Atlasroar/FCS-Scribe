# Data and AI contracts

AI range exports use `SchemaVersion = 3`. Original recorded packets
remain schema 2, which added `Context.Workshop` to the initial schema-1
contract. Schema-1 packets remain readable without that optional context;
no old fingerprint is reinterpreted as covering Workshop data.
The standalone Workshop-only export has its own `SchemaVersion = 1` contract
with `ModFileName`, `ExportedUtc`, and `Workshop`. The implementation is in
`src\Core.cs`; the actual serialized response template is the authority for
field names. Future schema changes need explicit compatibility decisions.

## Markdown export contract

Markdown is a derived human-readable output, not a new AI response, tracking
or workspace schema. Existing JSON packets and evidence identities are
unchanged. `src\MarkdownExport.cs` validates the packet and renders deterministic
UTF-8/LF text for identical packet/draft input; it does not capture data or
implement a second diff engine.

Recording order, original packet/session/entry identities, current-at-export
context and qualified evidence IDs remain explicit. Record/path grouping keeps
stored/effective views together without dropping citations or collapsing
observed reversals. Values show their captured kind and quoted payload text;
missing/null/removal kinds, empty strings, false/zero, precision and literal
control escapes stay distinct. Current and every selected recording's warnings
remain visible. Unresolved references/ownership stay unresolved.

Reviewed export adds a separate prose section only after the shared
batch/fingerprint/target/exactly-once checks against a fresh live snapshot.
It uses an empty notes lookup, not native completed-block state: exporting a
draft is neither application nor proof it was applied. Explicit numeric
evidence anchors link each prose citation to its factual row. Untrusted text
is escaped, including literal URLs that would otherwise become GFM auto-links.

The first line is `<!-- FCS-Scribe Markdown export v1: <body SHA-256> -->`,
followed by LF and the exact UTF-8 body. The body hash allows replacement only
of intact generated files; it is not authorship/signing. The displayed packet
serialization hash uses the repository's JSON serializer, not the byte hash
of a separately pretty-printed `.json` file. Keep that original packet and
workspace for audit/reuse; Markdown cannot resume tracking or restore a draft.
`ExportStorage` applies reserved-location, observed-hash and precommit
classification guards before the shared flushed atomic write.

## Snapshots and changes

Snapshots identify the active mod, loaded-mod order, declared dependencies,
locked mods, capture scope, records, and warnings. Each record has a stable ID,
type, display name, and typed cells with paths and layers.
JSON-pointer-style path segments escape `~` as `~0` and `/` as `~1`.
Missing/null, zero/false/empty, numeric types and removal markers remain
distinct; unsupported scalar types stop extraction rather than invent values.

| Layer | Meaning |
| --- | --- |
| `base` | Available inherited/base storage; exact source mod may be unresolved |
| `active` | Storage in the editable active mod |
| `locked` | Available locked layer; exact source mod may be unresolved |
| `effective` | The value exposed by FCS's current model |

Change entries identify the record, operation, path/layer, exact before/after
values, available source labels, referenced ID, and resolved reference name.
An unresolved target remains unresolved; a display name is not an identifier.

The [verified configuration matrix](Validation-History#s16-compatibility-and-provenance-matrix-beta-19)
does not expand field ownership. `base:unresolved` and `locked:unresolved`
remain literal source labels, including on effective fields/references selected
from those layers. An active cell may name the writable mod; a dependency's
position/name, matching record ID or header declaration does not prove which
mod supplied a flattened inherited value. Loaded order, declared dependencies
and native locked-file context remain separate sequences in their native forms.
Changing loaded/locked context requires a fresh baseline; a declaration-only
Save normalization is not evidence that a source mod changed.

The packet also includes baseline identity/capture time, export time, source
fingerprint, instructions, and `ResponseTemplate`. Its context intentionally
does not include every loaded record.

<a id="supported-and-omitted-state-beta-13"></a>

## Supported and omitted state

"Committed" below means a control has updated FCS's in-memory model, **not**
that FCS has saved the mod. This matrix describes the guarded executable,
not every editor build, record subsystem or control.

| State | Capture contract and boundary |
| --- | --- |
| Record identity and presence | Stable ID, type, effective name and available layered names; additions/removals relative to the preceding captured state. Missing/invalid states produce warnings; this is not the native undo/event history. |
| Scalar fields and flags | Supported typed base/active/locked storage and effective values; missing, null, empty, zero and false remain distinct. Unsupported scalar types fail explicitly. A stored override and its effective value can describe the same edit. |
| References | Available layered links, quantities, explicit removals and effective values; target IDs and resolvable names. Unresolved targets remain unresolved. |
| Nested instances | Supported recursive instance fields, presence and deletion tombstones, within the extraction depth limit. This does not capture external layouts or independent editor buffers. |
| Mod/dependency context | Active path-derived identity, filename, loaded order, declared dependencies and locked-mod list. Flattened base/locked storage does not establish exact source-mod ownership. |
| Workshop | Known local header/derived dependencies and matching staged/cached metadata, preview hash and availability reasons. Not a Steam query, published history or new gameplay evidence. Unavailable payload bytes are not validated. |
| Pending property controls | Not captured until their normal editor workflow commits to the model. Review does not force a commit, press Enter, move focus or save. Opening/clicking another window can itself change focus before Review runs. |
| Dialogue and separate editor buffers | Uncommitted dialogue/text/dialog buffers are omitted. Supported values already stored in the captured record model may be included; this is not complete dialogue-editor extraction. |
| External files, assets and layouts | No general content catalogue or validation. A captured path string is not a hash of the referenced file; preview-image metadata is the specifically documented exception. |
| Native/Scribe notes, TODOs and tracking metadata | Excluded from gameplay evidence. Text files, native sidecars, workspaces and journals have separate persistence and backup contracts. |
| Saved mod state and editor events | No saved-state verification, binary backup/restoration, keystroke log, undo history, complete metadata history or observations of intermediate states between samples. |

### Sampling and zero evidence

Manual Save Changes and eligible automatic recordings compare supported captured state
with the **last durable recording**, not with every user action. A-to-B-to-A
edits between observations can produce no change IDs and no new session entry.
Recording B and then recording the reversal to A retains both observations;
range export does not collapse them just because their final net effect is zero.
An unchanged capture appends nothing; changed context can append a recording
with no record-change IDs. Neither case proves no editor activity occurred.

A matching fingerprint validates only the supported captured representation,
not pending controls, omitted buffers or arbitrary external file contents.
Finish edits normally and verify the resulting before/after evidence; do not
treat a zero count as an all-clear or shorten the interval as a completeness
guarantee. Scheduling can defer observations while hidden, busy or modal.

Sampling guidance appears in new packet instructions and UI presentation,
including new baselines, zero-evidence reviews, range previews and restored
packet display. Snapshot warnings/scope and fingerprint formats are unchanged;
old recorded/nested packets are not rewritten to add wording.
See [scoped native-control evidence](Validation-History#s11-partial-capture-and-sampling-beta-13).

## Workshop context

`Workshop` contains `ModName` (the Workshop title), `Author`, integer `Version`,
`Description`, `Dependencies`, `ReferencedMods`, `Tags`, `ChangeNotes`, string
`SteamId`, `LastUpdate`, `Visibility`, `VisibilityCode`, `Preview`, `Source`,
and `Warnings`. The outer mod filename is not a fallback Workshop title.

Header fields come from the active model. Required/reference lists use FCS's
read-only derivation methods without refreshing its header. Workshop fields
come from the matching dialog or its local XML cache; the source is explicit.
Missing fields are `null`, whereas `"0"` is an explicit default Steam ID.
Unknown visibility codes remain numeric with no invented label. `LastUpdate`
preserves FCS's round-trip timestamp and timezone kind, not file write time.
The current change-notes control is an upload draft, not published history;
it is unknown if the matching dialog has not been opened.

`Preview` reports `Status` (`available`, `missing` or `unavailable`), `SourceFileName`,
`Format`, nullable `ByteLength`, and `Sha256`. It contains no local absolute
path or embedded image bytes. Archive the image separately; compare its hash
to the JSON to associate the two artifacts.

Optional `CaptureStatus = "partial"` and `Issues` are present when a known
optional component fails or the matching Workshop dialog is busy. Each issue
has `Component` (`cache`, `preview` or `dialog`), `Code` and a human-readable
`Message`. Codes distinguish invalid data, unsupported encoding/size, access
denial, read failure and upload/refresh in progress. Known header fields and
FCS-derived dependency/reference lists remain available. Cache failure does
not erase a readable preview; preview failure does not erase readable metadata.
Unavailable values remain unknown rather than being replaced by old/default
values. An unavailable preview also carries `UnavailableReason`.

Expected absent files retain their existing missing/unknown warnings, without
claiming a capture error. No-issue captures omit the new optional fields,
preserving old metadata serialization. Schema-1/2 original packets, schema-3
range exports and tracking/session storage keep their existing versions.
New captures may legitimately differ from pre-beta-6 fingerprints where the
older version could not represent a component failure.

Issues appear in Workshop and snapshot warnings, UI status, exported context
and AI instructions. File/decode errors are logged locally; metadata capture
does not repair, truncate, fetch or overwrite the failing source. Busy captures
skip staged dialog values and preview reads, not gameplay capture. Explicit
preview archiving still requires a readable supported image and waits for
an active upload/refresh to finish.

Known fields, availability states/reasons and captured gameplay participate in
the current-state fingerprint. Losing/recovering availability or changing a
known value makes an existing draft stale. **Unreadable payloads themselves
are not validated or fingerprinted:** one invalid/unreadable payload can change
to another with the same reported state/reason without a new fingerprint.
Partial capture is not verification of the unavailable data.
Unexpected reflection/contract failures, unsupported editor modes, ordinary
model extraction errors and fatal resource errors are not silently downgraded.

Workshop context is background, not new-edit evidence. It is included in
schema-2 stale-state fingerprints, but does not generate record change IDs or
alter documentation-batch identity on its own. Its strings remain untrusted
mod data. Nothing fetches or verifies the current Steam listing.

## Draft shape

An assistant returns the packet's response template filled with prose:

```json
{
  "BatchId": "<copy from ResponseTemplate>",
  "SourceFingerprint": "<copy from ResponseTemplate>",
  "Entries": [
    {
      "TargetRecordId": "<surviving stable record ID>",
      "ChangeIds": ["<valid change ID from this packet>"],
      "Text": "<reviewable factual documentation>"
    }
  ]
}
```

The placeholders above are illustrative, not valid evidence for a real batch.
Each target must appear only once. If documentation is about a deleted record,
the user must choose a surviving target instead of inventing a global note.
`ChangeIds` across all entries must cover every exported change
exactly once. Combining several records' evidence on one surviving target is
allowed; omitting or duplicating evidence is not.

<a id="recording-range-packets-beta-5"></a>

## Recording-range packets

Schema 3 adds `ProjectId` and ordered `Recordings` alongside the existing
packet fields. Each recording has `EntryId`, nullable `SessionId`, UTC
`RecordedUtc`, `Kind` and its unchanged original `Packet`. Kinds are
`recorded` (durable session entry), `pending` (current unrecorded edits),
`checkpoint` (older full-state history) and `imported` (converted legacy
evidence). These are selected in recorded/history order, not re-sorted by
wall-clock timestamps. Only a final pending entry is allowed.

The outer `Changes` concatenates the selected original change catalogues,
replacing only each copied ID with `EntryId:original-change-ID`. Original
batch IDs, change IDs and packets remain intact inside `Recordings`.
Context-only recordings contribute no change IDs. Repeated edits, reversals
and distinct stored/effective observations are not collapsed into a net diff.
An all-context range cannot be exported as record-change documentation.

Range baseline identity hashes a versioned prefix, project ID and ordered
entry/batch identities. The outer batch hash uses that identity and the
qualified change catalogue. Identical selections retain identity across
compilation and resume. Pending identities also bind to the durable head
and pending state fingerprint; repeated same-state exports remain stable.
Validation checks original schema-1/2 packets, unique entry identities, project
compatibility, exact original-to-qualified evidence correspondence and the
outer template's complete coverage. Nested schema-3 ranges are rejected.

Use **only the outer `ResponseTemplate`** and qualified IDs for a response.
It groups evidence onto unique current surviving targets; a historical target
that no longer exists is blank until the user chooses a survivor. The outer
`SourceFingerprint` and `Context` describe the current editor state at export,
not the last selected observation. `BaselineCapturedUtc` comes from the first
selected packet's original baseline time; recording times live in `Recordings`.
Warnings in original contexts remain visible and relevant.

Selecting/exporting does not append entries, advance the durable head, compile
sessions or rewrite any saved packet/hash. It checks the source files against
their observed hashes. Schema 3 is an AI-only handoff, not a resumable baseline:
Start tracking rejects it with instructions to use the original project JSON.
The tracking/session schemas, schema-1/2 serialization and note format are
unchanged. These integrity checks are not signatures or proof of authenticity.

## Validation and approval

Drafts must match the exported batch and source fingerprint, cite known change
IDs with complete, nonduplicate coverage, contain nonempty text without NUL,
and target unique existing records. Coverage validation finishes before any
notes are appended. The current captured state must still match export.

For a partial-application retry, retain the original complete draft. A target
is skipped only when its existing notes contain one matching completed block
for this batch, including the original text and ordered change IDs. Line
endings are normalized for comparison; conflicting/incomplete blocks and
multiple same-batch markers are rejected. Only remaining targets are appended.
An entirely applied draft is still rejected as a duplicate. Older drafts must
also meet the complete-coverage rule. Range packet schema 3 and workspace
persistence do not create an alternate draft/native-note format.

These checks do **not** establish that the assistant interpreted evidence
correctly. The user must verify names, quantities, conditions, and wording
before approving. No inferred intent or gameplay effect should be presented
as a fact.

The plugin appends a visible batch marker and evidence IDs with the approved
text, marks FCS dirty, and leaves saving to the normal editor workflow.

<a id="portable-draft-workspace-contract-beta-7"></a>

## Portable draft workspace contract

A separate JSON uses `Format: "FCS-Scribe draft workspace"` and
`SchemaVersion: 1`. It embeds `Packet`, `PacketHash` (the stream SHA-256 of
that packet's serialized contract), and exact `DraftText`. Packet schemas
1/2/3 are retained, including schema-3 originals and qualified evidence.
An explicitly text-only workspace has both packet fields null and cannot
apply notes. A missing packet with a declared hash, changed/malformed packet,
unknown format/version or missing text fails loading before UI replacement.
Hashes establish self-consistency, not authenticity or protection against a
deliberate edit that recomputes them.

Draft text is intentionally opaque on disk: invalid/incomplete JSON and
mismatched responses are recoverable without rebinding to a different export.
Open validates the bundle, then reports draft validation against original
evidence, current editor state and existing notes. It does not apply or approve
anything. Apply repeats the ordinary complete-coverage/current-state/retry
checks; saved validation results never confer later permission.

Saves reuse flushed atomic JSON replacement and an observed-file hash; the
previous workspace is retained at `.bak`. Replacing a different existing
destination requires it to be a valid workspace, not a tracking root or packet.
Save/Open dialogs follow the project/export-directory defaults. Tracking storage
does not include these documents, and opening one does not resume a journal.
Unchanged saved text/association is clean; new exports and subsequent edits
are unsaved. Revision guards prevent asynchronous replacement from erasing
late edits; a saved snapshot excludes later edits, which remain dirty.
This is explicit Save/Open, not autosave, an event log, or mod recovery.

<a id="note-application-outcomes-beta-18"></a>

## Note application outcomes

The on-disk packet/draft/workspace schemas and note block format are unchanged.
Complete nonduplicate coverage and current-state validation run before writes.
The adapter records confirmed IDs only after native append and exact unique
block readback using the same normalized-line comparison as retry validation.
It marks the editor dirty before each attempt to keep potentially partial
mutations visible to the native save workflow.

`NoteApplyReport` is an in-memory immutable-list snapshot of newly completed,
previously completed/skipped and unconfirmed IDs plus the active notes file.
`NoteApplyException` retains that report, last target, stage and original cause.
Unconfirmed is not synonymous with untouched or safe-to-append. Completion is
not disk persistence. Refresh/progress failure can occur after some or all
appends; the original complete draft/packet remains associated and retry checks
are repeated rather than trusting a stored report.

No automatic rollback, save, marker repair, partial-coverage exception or
stale-state bypass is added. Native Save/reload can normalize declaration
metadata and aggregated note formatting; retry requires the original fingerprint
and exact unique completed blocks, not byte-identical native aggregate formatting.
The [safe retry procedure](Installation-and-Workflow#per-record-notes-outcomes-and-safe-retries-beta-18)
keeps shared Notes/TODO sidecars separate from mod/tracking backup and distribution.

<a id="resumable-tracking-contract-beta-2"></a>

## Resumable tracking contract

A tracking file is distinct from an AI packet: `Format` is
`FCS-Scribe tracking project` and its own `SchemaVersion` is 1. It stores
`InitialFingerprint`, `StateFingerprint`, `BaselineId`, UTC `ValidatedUtc`,
the full captured `State`, ordered `Entries` and `ImportedEvidence`.
Each entry links its `PreviousFingerprint` to a packet's resulting state.
These fields describe the immutable starting anchor, including any older
checkpoint history. New roots also declare `JournalVersion = 1`, requiring
their adjacent session storage. Previous roots remain readable unchanged.

Resume checks required fields, state fingerprints, history continuity,
supported packet versions, batch hashes, template evidence and mod identity.
These are self-consistency checks, not signatures or proof that hand-edited
history is truthful. Broken documents are rejected, not partially salvaged.
Loaded/locked context and inherited changes are also checked against FCS
before tracking resumes.

Session headers use format `FCS-Scribe session`, schema 1, project/session IDs,
UTC start time and parent entry ID/fingerprint. A timestamp + unique-ID `.jsonl`
file is created on every start/resume. Subsequent lines use format
`FCS-Scribe journal entry`, schema 1, with a stream-hashed `JournalRecord`.
The record identifies its parent, UTC recording time, original compact AI
packet and a `SnapshotDelta`: current context, changed complete records and
removed record IDs. Unchanged loaded records are not serialized again.

The small `project.json` marker stores identity and durable head; it is
atomically replaced with a previous-marker backup. The full baseline is not
rewritten by recordings. Flushed session entries precede marker/baseline
advancement. Replay verifies chain, hashes and resulting snapshot fingerprints.
The main `journal.jsonl` receives missing entries at compilation, deduplicated
by identity and hash. Compiled session files are redundant; uncompiled files
remain required. Sessions are not automatically deleted.

Same-state reviews do not append duplicate history. Tracking storage does not
contain drafts/export associations; those use separate workspaces.
Recorded history is exposed through inclusive range selection. Compiled
session duplicates appear once; archived compiled sessions remain available
from the main journal. Earlier gameplay evidence remains selectable after
context-only updates. No history is advanced merely by exporting.

An incomplete final JSONL line is uncommitted. Recovery requires approval,
preserves its original bytes under `recovery`, then removes only that tail.
Complete corrupt entries and missing required history are rejected. Older
anchors without `JournalVersion` cannot themselves detect missing sidecars.
An exclusive project writer lease coordinates Scribe instances, not arbitrary
external editors. Timestamps are not entry identities or proof of authenticity.

<a id="complete-set-copying-and-archival-beta-16"></a>

### Complete-set copying and archival

The private unit of backup is the baseline plus its entire matching `.scribe`
folder and associated baseline backups, copied with all writers closed.
Modern journal-enabled roots require `project.json`; the durable head and
chain expose missing required pending sessions. Foreign markers/journals are
rejected. Failed validation does not repair missing history or make an empty
folder sufficient.

Fully compiled originals may be archived only with writers closed and the
main journal retained. If the main journal is missing but **all originals**
remain, replay can recover the entries as pending and Compile can reconstruct
it. Missing journal plus required archived originals is not recoverable from
the remaining working set. Restore exact files from a coherent private backup.
No automatic pruning, merge, schema change or weakened hash check is introduced.

Older roots without `JournalVersion` cannot prove sidecars are missing; a
coherent old set cannot prove newer history exists elsewhere. Copying tracking
storage under the same mod identity is not verified relocation of the mod.
The [copy/archive/upload procedures](Installation-and-Workflow#complete-tracking-sets-and-restore-failures)
retain those limits. Baselines include full loaded/dependency data; storage,
backups and draft workspaces must stay outside public upload content. The
persistent warning does not inspect or block native Workshop uploads.

<a id="path-identity-and-linear-branches-beta-17"></a>

### Path identity and linear branches

The native adapter derives `ModId` from the uppercase full active-mod path,
not the tracking JSON path or a mod-content hash. `ModName` is compared exactly.
Moving/renaming a complete closed tracking set can therefore retain its project
and entry identities while the mod remains at its original location. Copying
the `.mod` elsewhere does not transplant that identity; use an explicit fresh
baseline at a new filename and retain the old project separately.

Resume/record guards compare mod identity, loaded-mod sequence and locked-mod
sequence. Declaration metadata alone can be a context-only observation; the
guard does not hash every dependency binary or identify a machine. Inherited
data changes remain subject to the existing attribution checks.

Every new entry must extend the current head/fingerprint. A known identical
entry in retained session originals can deduplicate against the compiled
journal; two distinct successors to a common parent cannot. Combining valid
sibling sessions or journal suffixes rejects without rewriting remaining file
bytes. No automatic merge, rebase, re-ID, path rewrite or historical import is
added. [Portability procedures](Installation-and-Workflow#portability-and-linear-history-beta-17)
separate tracking-only moves, fresh baselines and divergent-history recovery.
Existing draft associations are not rebound or approved by these actions.

<a id="compilation-debt-beta-10"></a>

### Compilation debt

No new field or schema is written. Each validated session/journal entry already
has `RecordedUtc`. Replay tracks the earliest timestamp among uncompiled
entries; entries recovered from the compiled journal, including session
duplicates, are excluded. Context-only recordings count as pending too.

Compilation is due at oldest pending age 30 minutes, including time closed or
manual. Start/Resume reconstructs the remaining age, then uses a monotonic
in-window deadline. Settings changes do not reset it. Manual/recovery/error
holds and UI deferral do not clear it. Successful compilation empties pending
debt without re-arming recording; a later first entry creates a new age window.
No pending entries means no automatic compilation timer. Existing serialized
packets, timestamps, markers and history remain untouched merely by replay.

Scheduling requires UTC. A future pending timestamp on Resume/new debt is
treated as due now in automatic mode, never silently compiled in manual mode.
An already-running countdown is retained through UTC clock changes. These are
best-effort local scheduling rules, not authenticated timestamps or a trusted
cross-restart clock.

<a id="pending-initialization-beta-8"></a>

### Pending initialization

During New/legacy conversion, marker schema 1 may contain optional
`Initialization: { BaselineHash, SourceHash }`. `BaselineHash` is the stream
SHA-256 of the intended full tracking document. `SourceHash` is null for a new
file or the exact original legacy-file hash for conversion. Pending
`ProjectId` and `HeadId` must match, and the marker fingerprint identifies
the initial state. The pending marker is flushed before baseline commit.

Before that commit, a retry requires a recognized pending marker with the same
source identity, no existing baseline for New, and no session/journal/recovery
history. It may replace only recognizable prior initialization metadata, never
unknown backups. Legacy bytes are preserved/verified before conversion.
After baseline commit, Resume requires matching root/project/fingerprint,
exact baseline bytes, an empty initial history and the intact legacy backup
where applicable, then atomically writes the ordinary complete marker.

Completed markers omit `Initialization`, preserving the existing normal
serialization. Tracking, session, packet and workspace schemas are unchanged.
No history is reset, no committed baseline is rewritten during finalization,
and no unknown artifacts are deleted. Earlier unflagged orphan markers and
backup-only storage cannot safely establish ownership and require matching-file
restoration or another filename. These are consistency/observed-file checks
under the cooperating writer lease, not authentication or a universal lock.

Legacy schema-1/2 AI exports contain no complete baseline. Explicit conversion
preserves exact bytes in `.legacy.bak` and the packet in `ImportedEvidence`,
then starts from the current editor state. It does not reconstruct old state.
Schema-3 recording-range exports are rejected as baseline inputs rather than
silently converting the selected history into a new tracking project.

**Do not share tracking JSONs or backups with AI or Workshop:** `State` includes
the full captured loaded model, not just relevant edits. Use the compact
AI-packet export instead.

<a id="shared-settings-and-recovery-beta-9"></a>

## Project settings and recovery

Settings use `<project folder>\FCS-Scribe.settings.json`. The legacy user-wide
`%LOCALAPPDATA%\FCS-Scribe\settings.json` is preserved, not imported implicitly;
see [upgrade notes](Release-History#upgrade-notes). Schema 1 has required `SchemaVersion`
and `ReviewSeconds`, plus optional `ExportDirectory`,
`ModNotesEnabled`, `ModTodoEnabled`, `ProjectDirectory` and `ProjectModId`.
Settings lacking notes/task/project fields default to disabled text features
and a mod-local project directory. New settings bind to the path-derived active
mod ID; another mod's settings require explicit repair or a separate folder.
Supported intervals remain 0/30/60/300/600 seconds. A custom directory must be
absolute, syntactically valid and currently available. Missing source files
produce first-launch manual defaults without writing; invalid/unreadable
sources produce an explicit recovery state with no valid settings value.
The observed hash, when readable, covers exact original bytes from the same
open stream used for parsing.

An exclusive `<settings filename>.lock` coordinates cooperating writers while
they compare the observed hash, preserve originals and atomically replace
settings. Ordinary replacement retains `<settings filename>.bak`. Explicit repair
also preserves exact original bytes as
`<settings filename>.recovery-<UTC>_<unique-ID>.bak`; these unique backups are not
rotated away by later saves. Failed replacement can leave its completed
recovery copy while retaining the original. Unknown hashes, changed/deleted
sources and active cooperating leases reject replacement.

Opening/cancelling does not repair the settings source. Reload revalidates it
without repairing malformed/unreadable originals; valid local Scribe work is
saved before reload. An unreadable original must become readable and be
reloaded before repair is allowed. Valid
reload/Save exits recovery; invalid reload intentionally returns to labelled
Manual/project-folder defaults. Project/draft/packet state is not replaced.
These hashes and leases are consistency guards, not authentication or protection
against all uncooperating external writers or physical filesystem failures.
Settings and backups can reveal local paths: keep them private and outside
AI packets and Workshop uploads.

<a id="scribe-text-files-and-project-locator-beta-20"></a>

## Scribe text files and project locator

`notes.txt` is literal UTF-8 writing, with no JSON/header/markup. `todo.txt` is
separate UTF-8 plain text with this format (columns are actual tab characters):

```text
FCS-Scribe Mod To Do 1
Mod: example.mod
[ ]	0123456789abcdef0123456789abcdef	record-id	Record name	Task text
[x]	fedcba9876543210fedcba9876543210	other-record	Other name	Completed task
```

Each task has a unique GUID in `N` format, a stable native record ID, display
name and nonempty task text. `[ ]` is pending and `[x]` complete. The mod filename
and text columns escape backslash, tab, CR and LF as `\\`, `\t`, `\r`, `\n`.
Unknown/truncated escapes, malformed rows, duplicate GUIDs and another mod
filename reject loading rather than dropping content. This is Scribe's format,
not the native FCS sidecar format. No record or native annotation is written
when tasks are edited.

Disabled features do not read/write their text file. Opening reads exact bytes
and their hash through the same stream; invalid UTF-8 rejects loading. Saves
verify observed hashes under a cooperating `<filename>.lock`, flush a unique
same-folder temporary and move/replace atomically. Replacement keeps the old
bytes in `<filename>.backup-<unique-ID>`. Unchanged content is verified without
rewriting or normalizing BOM/newlines. External edits/removal reject a stale
save while retaining local text. Notes and TODOs are independent commits, not
a two-file transaction. These guards do not eliminate uncooperating-writer
races, physical failures or the need for backups.

Project defaults use the active mod folder. External mode writes
`FCS-Scribe.location.json` there, containing required `Format` =
`FCS-Scribe project location 1`, path-derived `ModId` and absolute `Directory`.
The directory must exist and must not be reserved tracking storage. Its
settings are `FCS-Scribe.settings.json`; their mod ID must match. Locator writes
use a cooperating lease, observed-hash guard and atomic JSON replacement.
Settings/root switching is not a multi-file transaction or data migration:
candidate settings may remain after a pointer-write failure, with the old
window association retained for retry. Unknown/foreign locators fail visibly
instead of redirecting to a default folder.

Existing tracking/workspaces remain at their explicit paths. The selected
project folder supplies new default destinations, unless the independent
export-directory override is set. Scribe settings/locator saves protect
recognized tracking anchors and their reserved storage/backup paths too.
These files are not included in gameplay fingerprints or AI evidence.
Text intended for public mod distribution must still be reviewed; private
settings, locators, baselines, workspaces and all backups stay out of uploads.

## Untrusted content and scope

Names, descriptions, dialogue, fields, and any supplied notes are untrusted mod
data, not instructions. An AI response is also an untrusted draft. Inspect
packet content before sending it to an external service.

Every packet warns that extraction is partial. A fingerprint only covers the
captured model and context: pending controls, separate buffers, or external
files can change without being represented. An empty change list is not proof
that nothing anywhere in the editor changed.
