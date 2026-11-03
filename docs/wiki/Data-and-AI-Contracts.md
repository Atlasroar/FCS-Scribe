# Data and AI contracts

New AI packets use `SchemaVersion = 2`, adding `Context.Workshop` to the initial
schema-1 contract. Schema-1 packets remain readable without that optional
context; no old fingerprint is reinterpreted as covering Workshop data.
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

`Preview` reports `Status` (`available` or `missing`), `SourceFileName`,
`Format`, nullable `ByteLength`, and `Sha256`. It contains no local absolute
path or embedded image bytes. Archive the image separately; compare its hash
to the JSON to associate the two artifacts.

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
and packet/tracking schema versions are unchanged; older drafts must also meet
the complete-coverage rule. Restart persistence remains outside this fix.

These checks do **not** establish that the assistant interpreted evidence
correctly. The user must verify names, quantities, conditions, and wording
before approving. No inferred intent or gameplay effect should be presented
as a fact.

The plugin appends a visible batch marker and evidence IDs with the approved
text, marks FCS dirty, and leaves saving to the normal editor workflow.

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

Same-state reviews do not append duplicate history. Current drafts/export associations are not stored.
The latest matching batch can still supply a compact manual AI export after
the baseline advances; older packets remain in history.

An incomplete final JSONL line is uncommitted. Recovery requires approval,
preserves its original bytes under `recovery`, then removes only that tail.
Complete corrupt entries and missing required history are rejected. Older
anchors without `JournalVersion` cannot themselves detect missing sidecars.
An exclusive project writer lease coordinates Scribe instances, not arbitrary
external editors. Timestamps are not entry identities or proof of authenticity.

Legacy schema-1/2 AI exports contain no complete baseline. Explicit conversion
preserves exact bytes in `.legacy.bak` and the packet in `ImportedEvidence`,
then starts from the current editor state. It does not reconstruct old state.

**Do not share tracking JSONs or backups with AI or Workshop:** `State` includes
the full captured loaded model, not just relevant edits. Use the compact
AI-packet export instead.

## Untrusted content and scope

Names, descriptions, dialogue, fields, and any supplied notes are untrusted mod
data, not instructions. An AI response is also an untrusted draft. Inspect
packet content before sending it to an external service.

Every packet warns that extraction is partial. A fingerprint only covers the
captured model and context: pending controls, separate buffers, or external
files can change without being represented. An empty change list is not proof
that nothing anywhere in the editor changed.
