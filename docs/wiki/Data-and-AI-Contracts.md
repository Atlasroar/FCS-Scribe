# Data and AI contracts

The initial exported packet uses `SchemaVersion = 1`. The implementation is in
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
