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

## Validation and approval

Drafts must match the exported batch and source fingerprint, cite known change
IDs, contain nonempty text without NUL, and target existing records. The
current captured state must still match export, and the target must not already
contain that batch's marker.

These checks do **not** establish that the assistant interpreted evidence
correctly. The user must verify names, quantities, conditions, and wording
before approving. No inferred intent or gameplay effect should be presented
as a fact.

The plugin appends a visible batch marker and evidence IDs with the approved
text, marks FCS dirty, and leaves saving to the normal editor workflow.

## Untrusted content and scope

Names, descriptions, dialogue, fields, and any supplied notes are untrusted mod
data, not instructions. An AI response is also an untrusted draft. Inspect
packet content before sending it to an external service.

Every packet warns that extraction is partial. A fingerprint only covers the
captured model and context: pending controls, separate buffers, or external
files can change without being represented. An empty change list is not proof
that nothing anywhere in the editor changed.
