# OpenConstructionSet integration

**Status: planned, not shipped.** OCS is not currently a plugin dependency and
does not change the existing offline/manual FCS documentation workflow.

## Why consider it?

[OpenConstructionSet](https://github.com/lmaydev/OpenConstructionSet) is a C#
Kenshi modding SDK with saved-file readers/writers, base/active mod contexts,
typed records/references/instances, comparisons, and installation discovery.
Those are useful building blocks for an optional saved-data backend and future
wiki exports.

The inspected revision is
[`c7ce7a752328a31687627fed65609fe8585c091c`](https://github.com/lmaydev/OpenConstructionSet/tree/c7ce7a752328a31687627fed65609fe8585c091c).
Its project targets .NET 9 and declares MIT licensing.

## Boundaries

The inspected context builder reads files from disk. It does not provide our
FCS live-model capture, unsaved-control handling, notes insertion, or dirty-state
integration. Its flattened context does not inherently establish per-field
source provenance or a user's explicit session baseline.

The current FCS plugin targets .NET Framework 4.8 and cannot directly consume
the inspected .NET 9 build. An optional out-of-process helper is the proposed
direction; selecting a compatible older release requires evidence rather than
an assumption. No separate desktop application is required.

OCS is supplementary. It does not override BFrizzleFoShizzle's authoritative
documentation or verified FCS behavior.

## Planned delivery

1. Pin and validate package/runtime/license requirements, choose the process
   boundary, and define a versioned snapshot protocol and visible error model.
2. Add optional explicit Steam/GOG/Workshop installation selection and a
   **read-only, saved-only** snapshot adapter for the common core.
3. Cross-check results against FCS after saving/reopening identical disposable
   inputs with explicit dependency/load order.
4. Add provenance tracing only where the underlying layer reads supply enough
   evidence; do not infer origins from the final merged record.
5. Use the validated saved-data/provenance model for later wiki-ready exports.

The live adapter stays responsible for unsaved edits and approved notes
insertion. Saved/offline snapshots must be distinguishable from live ones in
baseline identity, exports, warnings, and staleness handling.

## Acceptance requirements

OCS's exact merge/load-order, missing-dependency, type/default, deletion,
reference-removal, and instance behavior must be compared with FCS. Any
mismatch is investigated and surfaced rather than silently resolved in OCS's
favor. Original file checksums must remain unchanged.

The planned helper will not call OCS save APIs or use it to update open mods or
editor notes. The existing plugin must remain usable without OCS or a modern
.NET runtime installed.

The complete actionable checklist is in the
[Copilot plan](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md).
