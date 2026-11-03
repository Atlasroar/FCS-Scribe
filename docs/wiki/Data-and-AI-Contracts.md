# Data and AI contracts

Beta 5 AI range exports use `SchemaVersion = 3`. Original recorded packets
remain schema 2, which added `Context.Workshop` to the initial schema-1
contract. Schema-1 packets remain readable without that optional context;
no old fingerprint is reinterpreted as covering Workshop data.
The standalone Workshop-only export has its own `SchemaVersion = 1` contract
with `ModFileName`, `ExportedUtc`, and `Workshop`. The implementation is in
`src\Core.cs`; the actual serialized response template is the authority for
field names. Future schema changes need explicit compatibility decisions.

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

The packet also includes baseline identity/capture time, export time, source
fingerprint, instructions, and `ResponseTemplate`. Its context intentionally
does not include every loaded record.

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

`Preview` reports `Status` (`available`, `missing` or, since beta 6,
`unavailable`), `SourceFileName`,
`Format`, nullable `ByteLength`, and `Sha256`. It contains no local absolute
path or embedded image bytes. Archive the image separately; compare its hash
to the JSON to associate the two artifacts.

Beta 6 adds optional `CaptureStatus = "partial"` and `Issues` when a known
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
In beta 4, `ChangeIds` across all entries must cover every exported change
exactly once. Combining several records' evidence on one surviving target is
allowed; omitting or duplicating evidence is not.

## Recording-range packets (beta 5)

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
An entirely applied draft is still rejected as a duplicate. The append format
was unchanged by beta 4; older drafts must also meet the complete-coverage rule.
Beta 5 adds range packet schema 3 without changing the draft/notes format.
Beta 7 adds explicit workspace persistence without changing this notes contract.

These checks do **not** establish that the assistant interpreted evidence
correctly. The user must verify names, quantities, conditions, and wording
before approving. No inferred intent or gameplay effect should be presented
as a fact.

The plugin appends a visible batch marker and evidence IDs with the approved
text, marks FCS dirty, and leaves saving to the normal editor workflow.

## Portable draft workspace contract (beta 7)

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
Save/Open dialogs default to the active-mod/settings folder. Tracking storage
does not include these documents, and opening one does not resume a journal.
Unchanged saved text/association is clean; new exports and subsequent edits
are unsaved. Revision guards prevent asynchronous replacement from erasing
late edits; a saved snapshot excludes later edits, which remain dirty.
This is explicit Save/Open, not autosave, an event log, or mod recovery.

## Resumable tracking contract (beta 2)

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
contain drafts/export associations; beta 7 stores those in separate workspaces.
Beta 5 exposes recorded history through inclusive range selection. Compiled
session duplicates appear once; archived compiled sessions remain available
from the main journal. Earlier gameplay evidence remains selectable after
context-only updates. No history is advanced merely by exporting.

An incomplete final JSONL line is uncommitted. Recovery requires approval,
preserves its original bytes under `recovery`, then removes only that tail.
Complete corrupt entries and missing required history are rejected. Older
anchors without `JournalVersion` cannot themselves detect missing sidecars.
An exclusive project writer lease coordinates Scribe instances, not arbitrary
external editors. Timestamps are not entry identities or proof of authenticity.

### Pending initialization (beta 8)

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

## Shared settings and recovery (beta 9)

`%LOCALAPPDATA%\FCS-Scribe\settings.json` retains schema 1 with required
`SchemaVersion` and `ReviewSeconds`, plus optional `ExportDirectory`.
Supported intervals remain 0/30/60/300/600 seconds. A custom directory must be
absolute, syntactically valid and currently available. Missing source files
produce first-launch manual defaults without writing; invalid/unreadable
sources produce an explicit recovery state with no valid settings value.
The observed hash, when readable, covers exact original bytes from the same
open stream used for parsing.

An exclusive `settings.json.lock` coordinates cooperating beta 9 writers while
they compare the observed hash, preserve originals and atomically replace
settings. Ordinary replacement retains `settings.json.bak`. Explicit repair
also preserves exact original bytes as
`settings.json.recovery-<UTC>_<unique-ID>.bak`; these unique backups are not
rotated away by later saves. Failed replacement can leave its completed
recovery copy while retaining the original. Unknown hashes, changed/deleted
sources and active cooperating leases reject replacement.

Startup, cancellation and reload do not repair or rewrite files. An unreadable
original must become readable and be reloaded before repair is allowed. Valid
reload/Save exits recovery; invalid reload intentionally returns to labelled
manual/active-mod defaults. Project/draft/packet state is not replaced.
These hashes and leases are consistency guards, not authentication or protection
against all uncooperating external writers or physical filesystem failures.
Settings and backups can reveal local paths: keep them private and outside
AI packets and Workshop uploads.

## Untrusted content and scope

Names, descriptions, dialogue, fields, and any supplied notes are untrusted mod
data, not instructions. An AI response is also an untrusted draft. Inspect
packet content before sending it to an external service.

Every packet warns that extraction is partial. A fingerprint only covers the
captured model and context: pending controls, separate buffers, or external
files can change without being represented. An empty change list is not proof
that nothing anywhere in the editor changed.
