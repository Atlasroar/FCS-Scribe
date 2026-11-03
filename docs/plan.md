# FCS-Scribe Copilot plan

This is the working feature plan and acceptance checklist. Keep it current as
work lands: check an item only when its stated scope is implemented and verified,
and record remaining limitations rather than treating an experimental feature
as release-ready. Unchecked items are planned work, not implemented promises.

## Goal and constraints

Build an FCS Extended plugin that lets a user edit a Kenshi mod normally,
explicitly track changes, export evidence for AI-written documentation, review
the draft, and append approved notes through FCS. Keep extraction/comparison
reusable for saved-data analysis and eventual wiki generation.

- [x] Use FCS Extended as the editor integration base; do not build a replacement editor or separate desktop application.
- [x] Treat BFrizzleFoShizzle's [FCS Extended](https://github.com/BFrizzleFoShizzle/FCS_extended), [RE_Kenshi](https://github.com/BFrizzleFoShizzle/RE_Kenshi), and [KenshiLib](https://github.com/BFrizzleFoShizzle/KenshiLib) documentation as authoritative unless explicitly instructed otherwise.
- [x] Keep native game-runtime APIs distinct from managed FCS editor APIs; do not invent integration contracts.
- [x] Keep the initial workflow offline/manual: no AI account, credentials, subscription, or provider connection required.
- [x] Never write directly to a `.mod` open in FCS, silently save, overwrite original mods during investigation, or discard unknown fields.
- [x] Use disposable copies for runtime investigation; keep proprietary binaries out of the repository.
- [x] Avoid a custom binary writer as a workaround; surface unsupported versions, types, unresolved data, and partial extraction.
- [ ] Preserve these constraints when adding OCS or other future backends.

## Feature coverage

| Priority | Feature | Current status and remaining gate |
| --- | --- | --- |
| First | Integration feasibility check | Report complete for one guarded Steam FCS build; interactive acceptance and broader stack coverage remain |
| MVP | Explicit tracking baseline | Implemented and core/runtime checked; verify actual editor focus behavior |
| MVP | Structured change catalogue | Implemented for the documented captured model; partial coverage remains explicit |
| MVP | Human-readable references | IDs/names and unresolved warnings implemented; field-level dependency provenance remains unresolved |
| MVP | AI handoff | Versioned JSON and instructions implemented; real manual handoff acceptance remains |
| MVP | Documentation preview/import | Editable JSON draft, file import, and paste implemented; interactive acceptance remains |
| MVP | Safe notes insertion | Copy-based runtime checks pass; real FCS Extended approval/save workflow remains |
| Next | Session history | Planned; no cross-restart baseline/history persistence yet |
| Next | Markdown export | Planned; use the existing verified catalogue rather than a second diff engine |
| Next, staged | OCS integration | Repository assessment complete; optional read-only saved-data backend planned, not installed or implemented |
| Later | Wiki-ready export | Planned after dependency/provenance validation |
| Optional | Direct AI connection | Planned only after the manual workflow is reliable |

## 1. Integration feasibility and compatibility

Evidence: [feasibility report](feasibility.md), `src\FcsAdapter.cs`, and
`scripts\Inspect-Fcs.ps1`.

- [x] Verify `IPlugin.Init(Assembly)` and upstream extension manifest/discovery conventions.
- [x] Establish initialization timing and add UI on FCS's WinForms thread without introducing hooks.
- [x] Identify the writable active mod, actual loaded-mod list, declared dependencies, and locked mods.
- [x] Read loaded records and unsaved edits already committed to FCS's model.
- [x] Inspect deletion, reference-removal, and nested-instance semantics.
- [x] Determine the notes location and scope: record-ID-keyed Notes/TODO entries in editor-side `data\__TODO__<user>.txt`, not embedded `.mod` notes.
- [x] Distinguish that facility from Workshop `ModData.changeNotes`; do not silently substitute one for the other.
- [x] Verify append, inherited text preservation, dirty-state handling, normal save, and reopen on disposable copies.
- [x] Isolate reflection and guard the exact inspected Steam FCS 2.14.2 executable hash with actionable errors.
- [x] Document unsupported builds, partial extraction, uncommitted controls, and nontransactional multi-record application.
- [ ] Validate startup and the complete loader/UI workflow in a real FCS Extended run.
- [ ] Expand the compatibility matrix only after inspecting and testing each additional build; never bypass the hash guard.
- [ ] Check representative real dependency stacks and interactions with other FCS Extended extensions.

## 2. Baselines and deterministic change detection

Evidence: `src\Core.cs`, `src\FcsAdapter.cs`, `tests\CoreTests.cs`, and
`tests\IntegrationTests.cs`.

- [x] Capture an immutable baseline only when the user explicitly selects Start tracking.
- [x] Explain that edits made before tracking begins are outside the baseline.
- [x] Compare against the tracking baseline, not just vanilla/base data.
- [x] Use stable IDs rather than names for record identity.
- [x] Detect added/deleted records, renamed records, modified fields, flags, and exact quantities.
- [x] Detect reference additions, removals, quantity changes, and explicit removal markers.
- [x] Capture nested instance data, presence, and deletion tombstones.
- [x] Preserve zero, false, missing, null, empty, inherited, and explicitly removed distinctions in the supported model.
- [x] Distinguish stored active overrides, effective values, and edits since the tracking baseline.
- [x] Avoid reporting pre-existing inherited data as new edits; reject unsafe inherited-value changes.
- [x] Include loaded dependency context and unresolved-provenance warnings without claiming full dependency resolution.
- [x] Exclude generated notes and tracking metadata from gameplay change detection.
- [x] Keep comparison/baseline advancement explicit; notes application and ordinary saving do not reset tracking.
- [x] Account for FCS refreshing dependency declarations during normal save.
- [x] Use deterministic ordering and streaming fingerprints suitable for the 32-bit editor.
- [ ] Verify control commit/focus behavior in the interactive editor; retain the warning until verified.
- [ ] Profile real large mod stacks and document practical capture latency/memory limits.
- [ ] Extend extraction only where additional subsystem semantics are verified; keep omissions visible until then.

## 3. Structured export and AI documentation handoff

- [x] Export schema-versioned JSON with active mod identity, baseline identity/time, and export identity/time.
- [x] Include dependency/load-order context, record type/ID/name, operation, field/reference path, exact before/after values, and layer/source labels.
- [x] Include referenced IDs, resolved names where available, and explicit unresolved/partial-data warnings.
- [x] Export relevant changes/context rather than the entire loaded database.
- [x] Include an editable response template and instructions requiring evidence/change IDs.
- [x] Treat record names, descriptions, dialogue, and other mod content as untrusted data, never AI instructions.
- [x] Require concise factual notes, exact quantities/conditions, separation of interpretation, and no invented design intent or gameplay effects.
- [x] Require unresolved-reference and partial-catalogue caveats; do not claim complete extraction.
- [x] Provide a fully manual export/import workflow with no network calls by the plugin.
- [x] Warn users to inspect potentially private mod content before sharing it externally.
- [ ] Exercise a real exported packet through an external assistant and verify the returned draft against its evidence.
- [ ] Review the usability/readability of the packet and editable JSON draft with realistic documentation batches.

## 4. Draft review and approved notes application

- [x] Support draft-file import and paste into an editable preview.
- [x] Validate batch identity, source fingerprint, valid evidence IDs, nonempty text, and unique surviving targets.
- [x] Require explicit user approval before insertion; no automatically trusted AI output.
- [x] Preserve existing active notes, inherited notes, and existing TODO categories.
- [x] Identify batches and reject duplicate application to the same target.
- [x] Block stale drafts when captured data/context changed after export.
- [x] Require an explicitly chosen surviving record target for deleted-record documentation.
- [x] Apply through verified FCS mechanisms, mark dirty, and leave saving to the user.
- [x] Report partial in-memory application on failure; do not claim transactional safety.
- [ ] Manually verify import, paste, editing, validation errors, cancellation, and approval dialogs in FCS Extended.
- [ ] Manually verify existing/inherited notes, normal Save, editor restart, and reopen with no unrelated gameplay changes.
- [ ] Verify stale/duplicate handling and context changes across the actual interactive workflow.

## 5. MVP acceptance gate

The experimental implementation is not declared release-ready until this gate
is complete. Use disposable mods and notes files for manual checks.

- [x] Run deterministic core checks for additions, modifications, deletions, renames, references, unchanged/inherited data, typed values, draft validation, and serialization.
- [x] Exercise real unsaved edits and normal FCS save/reopen methods on copied vanilla data and a representative mod.
- [x] Verify notes-only changes leave the captured gameplay model unchanged.
- [x] Verify active/inherited notes, TODO category, dirty state, duplicate blocking, and no save during apply.
- [x] Verify the saved/reopened captured gameplay records match the expected edited state.
- [x] Provide build/install/workflow instructions and a verified/uncertain/unsupported feasibility report.
- [ ] Follow the README installation steps in a disposable FCS Extended environment; confirm extension discovery and toolbar placement.
- [ ] Complete the interactive checks in sections 1-4 and record the results and any remaining exceptions.
- [ ] Test unsupported-build, read-only, merge/translation-mode, malformed-draft, and notes-save failure behavior visibly.
- [ ] Re-run relevant automated checks after fixes, update documentation, and explicitly decide whether the MVP is ready for normal use.

Recorded implementation evidence: 41 core checks, 30 disposable vanilla
integration checks, and 29 representative-mod integration checks passed. The
mod fixture had no instances and explicitly skipped that mutation; vanilla
coverage includes it. These results do not replace interactive acceptance.

## 6. OpenConstructionSet integration

Use [lmaydev/OpenConstructionSet](https://github.com/lmaydev/OpenConstructionSet)
as a supplementary saved-data backend/reference, not a replacement editor or an
authority overriding BFrizzleFoShizzle's guidance.

### 6A. Assessment and design gate

- [x] Inspect the README, loader, context options, record comparison model, framework target, and license.
- [x] Establish useful capabilities: saved-file reading, base/active mod contexts, typed records/references/instances, comparisons, and Steam/GOG/Workshop discovery.
- [x] Establish the live-edit boundary: its inspected context builder reads disk and does not provide our FCS unsaved-model/notes integration.
- [x] Record compatibility: inspected revision `c7ce7a752328a31687627fed65609fe8585c091c` targets .NET 9 and is MIT-licensed; the current plugin targets .NET Framework 4.8.
- [x] Identify that merged context data is not automatically complete per-field source provenance or an explicit user-session baseline.
- [ ] Pin and validate the actual package/revision to use, including supported file versions and applicable license notices.
- [ ] Decide the integration boundary before implementation: prefer an optional out-of-process helper for the modern runtime; evaluate an older compatible package only with evidence, not an assumed framework match.
- [ ] Define a versioned request/response contract that maps saved snapshots into the reusable core without introducing OCS dependencies into the live editor adapter.
- [ ] Specify installation selection, runtime prerequisites, helper lifecycle, cancellation/error reporting, and protocol/version mismatch behavior.
- [ ] Keep the existing manual plugin workflow usable without OCS or its runtime installed; no separate desktop UI is required.

### 6B. Read-only saved-data backend

Implementation follows the design decision and MVP acceptance gate.

- [ ] Add optional Steam/GOG/Workshop installation discovery; require explicit selection when installations or mod locations are ambiguous.
- [ ] Load saved mod copies and explicitly selected dependency/load-order context using OCS.
- [ ] Label the backend and its results saved-only; never silently replace a live baseline with disk state.
- [ ] Map stable IDs, names, exact typed fields, references, deletion markers, and instances into the common snapshot model.
- [ ] Preserve explicit baseline capture/advancement for offline comparisons rather than treating all active-versus-base differences as recent user edits.
- [ ] Make missing dependencies, unresolved IDs, unsupported formats, and partial reads explicit; do not rely on permissive loader defaults.
- [ ] Review and test exact load-order/merge behavior against FCS rather than assuming equivalent results.
- [ ] Keep the helper read-only: do not call OCS save APIs or use it to update open mods or FCS notes.
- [ ] Preserve the existing live FCS adapter as the source for unsaved edits and approved notes insertion.
- [ ] Keep saved/offline and live source identities distinct in exports, warnings, and draft staleness checks.

### 6C. Cross-validation and provenance gate

- [ ] Compare OCS snapshots with the live adapter after normal FCS save/reopen on identical disposable inputs and load order.
- [ ] Cover additions, deletions, renames, field types/defaults, reference quantities/removal, instances, dependency overrides, and unresolved data.
- [ ] Investigate/report mismatches; do not use OCS results to silently override authoritative guidance or verified FCS behavior.
- [ ] Verify repeatable results under different explicit load orders and missing/duplicate dependency cases.
- [ ] Verify original file checksums remain unchanged and helper errors cannot trigger writes or false success.
- [ ] Record field/reference provenance during layer application where needed; validate that the selected OCS APIs expose enough information.
- [ ] If provenance requires lower-level reads/additional tracing, assess that work explicitly; do not infer origins from the flattened final context.
- [ ] Document supported OCS/runtime versions, saved-only limits, installation, and fallback to the current plugin workflow.

## 7. Next features

These features follow MVP acceptance. OCS design research can proceed
independently, but its implementation must not block stabilizing the live plugin.

### Session history

- [ ] Persist baseline snapshots, identities/timestamps, dependency context, and documentation batches across editor restarts.
- [ ] Restore history only after verifying active mod/context identity; clearly distinguish restored, stale, and incompatible sessions.
- [ ] Preserve duplicate detection and draft-to-export associations across restarts without resetting the baseline on save/apply.
- [ ] Add explicit user controls for selecting/advancing baselines and managing history.
- [ ] Define storage/version migration, corruption/error behavior, and retention before implementation.
- [ ] Test restart recovery, stale drafts, changed mods/dependencies, and notes-only changes.

### Markdown changelogs/documentation

- [ ] Export readable Markdown from the same verified catalogue and reviewed notes.
- [ ] Retain stable IDs/evidence links, exact quantities, source/layer labels, unresolved references, and partial-extraction warnings.
- [ ] Keep factual changes separate from interpretation and avoid duplicating stored/effective entries as separate edits.
- [ ] Test deterministic output and correct escaping of untrusted names/text.

## 8. Later: wiki-ready exports

Depends on verified dependency interpretation and provenance; use the OCS
saved-data backend where it has passed cross-validation.

- [ ] Export effective records, relationships, and source provenance under an explicit mod/load-order context.
- [ ] Distinguish stored facts, effective values, and inferred behavior in the output schema.
- [ ] Build cross-linked research, item, building, and crafting documentation with stable identifiers.
- [ ] Carry unresolved references, missing dependencies, and extraction limitations into generated pages.
- [ ] Preserve enough evidence to audit generated pages against their source records and fields.
- [ ] Validate cross-links, override/load-order cases, and exact values against representative FCS/OCS fixtures before claiming correctness.

## 9. Optional: direct AI-provider connection

- [ ] Choose provider/configuration scope explicitly; keep manual export/import fully functional.
- [ ] Define credential handling, consent, data-sharing scope, and actionable network/provider errors before implementation.
- [ ] Send only the reviewed structured packet and treat all included mod text as untrusted data.
- [ ] Validate provider responses with the same batch/evidence/staleness checks as manual drafts.
- [ ] Keep generated notes editable and require explicit approval; never automatically apply or save.
- [ ] Test cancellation, unavailable providers, malformed responses, stale results, and accidental repeated requests.

## Working order

1. Finish the real FCS Extended acceptance gate and fix any issues it exposes.
2. Complete OCS design decisions, then implement/test the optional read-only backend.
3. Add session history and Markdown exports as independently scoped follow-ups.
4. Validate dependency provenance before building wiki-ready exports.
5. Add a direct AI connection only if explicitly prioritized; it is not an MVP prerequisite.
