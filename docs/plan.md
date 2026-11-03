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
| Requested follow-up | Workshop metadata and preview archive | Implemented and copy-tested; cached/local provenance explicit; interactive acceptance remains |
| Requested follow-up | Embedded window and contextual help | Native MDI child and per-button bottom help implemented; construction/event checks pass; interactive acceptance remains |
| Beta 2 | Resumable tracking and session journals | Full initial baseline, timestamped delta logs and durable replay implemented; history UI/draft persistence remain planned |
| Beta 2 | Recording, compilation and export settings | Manual/30/60/300/600-second recording, explicit/30-minute compilation and active-mod defaults implemented |
| Beta 3 stabilization | Grouped window menus | Core, Workshop and Draft dropdowns with standalone approval/settings implemented and disposable-copy checked; real FCS Extended UI acceptance remains |
| Next | Markdown export | Planned; use the existing verified catalogue rather than a second diff engine |
| Next, staged | OCS integration | Repository assessment complete; optional read-only saved-data backend planned, not installed or implemented |
| Later | Wiki-ready export | Planned after dependency/provenance validation |
| Optional | Direct AI connection | Planned only after the manual workflow is reliable |

## Stabilization gate: before further feature work

The current user-directed UI cleanup is the first work item. After that, address
S01-S16 **one issue at a time**, in the order below, before starting new feature
expansion. In particular, OCS implementation, Markdown/wiki exports and direct
AI connections stay on hold. Existing checkmarks elsewhere describe their
original scope; they do not close these newly identified gaps.

Each issue needs a focused change or an explicitly accepted support boundary,
regression evidence, accurate user-facing guidance and a recorded outcome before
moving on. Known limitations do not imply a promise to build a mod-restoration
system, capture every editor event, support every FCS version or query Steam.
Do not silently reclassify unresolved defects as accepted limitations.
Each addressed issue gets its own PR, merge and beta release before proceeding
to the next issue.

### UI cleanup: grouped actions

- [x] Replace the flat button row with Core (Start tracking, Review changes, Compile journal, Export AI packet), Workshop (Export Workshop info, Archive preview image) and Draft (Import draft, Paste draft) dropdowns.
- [x] Keep Apply approved notes and Settings as separate top-level actions inside Scribe, without merging menus into the parent FCS window.
- [x] Preserve every action's existing dispatch, explicit approval, busy/error handling and independent bottom help/status areas.
- [x] Cover exact menu grouping, hover/keyboard-selection help, operation gating, narrow-window overflow, menu-based review and deferral of automatic work while menus are open on disposable FCS copies.
- [x] Update wiki-source navigation guidance and distinguish beta 3 menus from the beta 2 button row.
- [ ] Verify menu opening, keyboard navigation, small-window layout and FCS MDI interaction in the real FCS Extended acceptance pass (S09).

Beta 3 checks passed: 138 core, 136 disposable vanilla FCS and 135
representative-mod FCS checks, plus the plugin build. The representative mod
has no instances; vanilla covers that mutation. Beta 3 contains this UI cleanup;
beta 2 assets remain unchanged. The real-editor acceptance gate remains open.

### S01. High: incomplete evidence coverage can pass draft validation

The user selected **complete coverage of every exported change**. Beta 4
enforces exactly-once citations, not implicitly approved partial documentation.

- [x] Reject omitted/duplicate evidence before mutation and align the wiki with the full-coverage guarantee.
- [x] Retain the complete original draft for retries, skip only one matching completed block per target, and reject conflicts or a fully applied batch.
- [x] Cover omitted records/change IDs, duplicate citations, deleted-record targets, line-ending normalization, conflicting blocks, stale state and partial-application retries; preserve the append format and update the contract/wiki.

S01 is addressed in beta 4. Safe retry is not transactional rollback or draft
persistence; broader S04/S15 work and real FCS Extended acceptance remain open.
Recorded checks: 157 core, 141 disposable vanilla FCS and 140 representative-mod
FCS checks passed. The representative mod has no instances; vanilla covers that
mutation. S02 is the next open stabilization issue.

### S02. High: AI handoff can omit accumulated recordings

- [ ] Provide an explicit, reviewable way to document accumulated recordings instead of only pending changes or the latest matching saved batch.
- [ ] Preserve batch/evidence identity and current-state validation; handle a Workshop/context-only recording making the previous gameplay batch ineligible.
- [ ] Cover several automatic ticks, multiple sessions, context-only changes, unchanged reviews and restart without advancing or rewriting history merely to export.

### S03. High: Workshop failures can stop ordinary tracking

- [ ] Separate gameplay capture availability from optional Workshop/cache/preview availability without silently hiding extraction errors.
- [ ] Represent unavailable metadata and its reason explicitly; preserve meaningful stale-draft checks and the no-Steam/no-save contract.
- [ ] Cover corrupt caches, unsupported/oversized previews, missing data and active upload/refresh states during ordinary recording.

### S04. Medium: editable drafts and export associations are not durable

- [ ] Preserve draft/packet associations or require explicit save/discard protection before close, session replacement, Import and Paste overwrite work.
- [ ] Validate restored drafts against their original evidence and current editor state; never imply that tracking history already backs up draft text.
- [ ] Cover cancellation, restart, unsaved edits during background work and stale or missing associated packets.

### S05. Medium: failed project creation can leave an orphaned marker

- [ ] Make baseline/marker initialization recoverable when marker creation succeeds but baseline creation fails.
- [ ] Clean up or repair only artifacts owned by that failed attempt; never damage existing storage or legacy backups.
- [ ] Inject permission/write/commit failures and verify that retry or recovery works without ambiguous "project already exists" errors.

### S06. Medium: invalid settings can block the entire window

- [ ] Offer an explicit settings recovery/reset path even when settings cannot be loaded during window construction.
- [ ] Report corrupt settings and preserve recoverable originals; avoid silent defaults or overwriting another instance's changes.
- [ ] Cover malformed/unsupported settings, missing custom directories, stale settings hashes and simultaneous editor instances.

### S07. Medium: compilation deadlines can be deferred indefinitely

- [ ] Stop unrelated settings changes, such as export-folder changes, from resetting an existing compilation deadline.
- [ ] Define the handling of pending compilation across short sessions and interval changes; make retained compilation debt visible without silently compiling in manual mode.
- [ ] Cover repeated settings saves, short-session restarts, manual/automatic transitions, busy deferral and independent recording deadlines.

### S08. Medium: exports can overwrite inactive tracking projects

- [ ] Protect recognized inactive project anchors and reserved storage, including Workshop export when no project is open.
- [ ] Explain the rejection and preserve ordinary user-selected export locations; do not rely solely on a generic overwrite confirmation.
- [ ] Cover another project's baseline, markers/backups and current storage alongside valid existing AI/Workshop export destinations.

### S09. High acceptance gate: complete the real FCS Extended workflow

- [ ] Run the interactive acceptance items in sections 1-5B on disposable mods/notes, including the new menu UI.
- [ ] Exercise startup/discovery, focus commits, dialogs, scheduling, resize/minimize/close/restart, other extensions and visible failure paths.
- [ ] Record actual results and remaining exceptions; standalone form/method checks do not close this gate.

### S10. Support boundary: tracking is not mod recovery

- [ ] Make recorded state versus saved mod state unmistakable in start/resume/status/recovery guidance.
- [ ] Exercise crash/discard discrepancies and confirm that continuing records differences rather than silently restoring or saving the mod.
- [ ] Document a practical mod-and-notes backup workflow without adding an unverified binary writer or restoration feature.

### S11. Support boundary: partial capture and interval sampling

- [ ] Keep a clear supported/omitted-state matrix, including pending controls, dialogue buffers and external files.
- [ ] Explain and verify that A-to-B-to-A edits between recordings are not an event log; zero evidence is not proof of no editor changes.
- [ ] Verify real control-commit behavior and retain explicit partial-extraction warnings wherever results are presented.

### S12. Performance: full capture and growing history

- [ ] Profile capture, comparisons, resume, per-recording session hashes and compilation scans on realistic large stacks/histories.
- [ ] Establish measured latency/memory budgets, then reduce repeated full-file/full-history work without dropping integrity checks.
- [ ] Cover retained compiled sessions, large changed records and long-running 32-bit editor sessions; document practical limits.

### S13. Storage safety: incomplete backups and accidental distribution

- [ ] Make the anchor/marker/journal/uncompiled-session dependency clear and validate restore/copy workflows.
- [ ] Provide practical safe-archival and pre-Workshop-upload guidance while retaining the requested active-mod-folder default.
- [ ] Cover missing or wrongly archived files and privacy warnings for full loaded/dependency data; never silently prune required history.

### S14. Portability: path identity and divergent histories

- [ ] Define a verified relocation/new-baseline workflow for moved mods or another machine without bypassing identity checks.
- [ ] Explain linear history, branch divergence and load-order incompatibility before users attempt version-control merges.
- [ ] Cover moved/copied paths and divergent chains; automatic journal merging remains outside the verified contract.

### S15. Notes safety: partial application and sidecar distribution

- [ ] Improve per-record partial-application reporting and verified retry guidance without promising rollback or reapplying completed notes.
- [ ] Make shared per-record notes sidecars and their separate backup/distribution requirements explicit in the approval workflow.
- [ ] Exercise failures between records, duplicate markers, inherited notes, normal save and restart on disposable copies.

### S16. Compatibility and provenance boundaries

- [ ] Retain the exact executable guard and document a verified compatibility matrix; do not treat unsupported builds as compatible.
- [ ] Exercise representative dependency stacks and keep unresolved base/locked ownership visible rather than inferring source mods.
- [ ] Keep cached/staged Workshop values distinct from published Steam truth; BFrizzleFoShizzle's documentation remains authoritative.

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

- [x] Capture an immutable starting baseline through Start tracking; advance it only after successful checkpoint saves.
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
- [x] Inspect the user's first real schema-1 export for its catalogue/template shape without committing private mod content.
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

Recorded beta 2 implementation evidence: 138 core checks, 116 disposable vanilla
integration checks, and 115 representative-mod integration checks passed. The
mod fixture had no instances and explicitly skipped that mutation; vanilla
coverage includes it. These results do not replace interactive acceptance.

### 5A. First-export follow-up: Workshop and window workflow

- [x] Inspect the guarded FCS Workshop dialog's data lifecycle, sidecar paths, preview handling, dependencies, and MDI hosting.
- [x] Capture Workshop mod title, author, integer version, description, required/reference dependency lists, tags, staged change notes, Steam ID, cached last update, and visibility.
- [x] Read a matching live dialog or saved XML cache; identify source and unknowns, preserve precision, and never query Steam or invoke refresh/save/upload.
- [x] Read staged change notes from the control rather than stale `ModData.changeNotes`; keep them separate from record Notes/TODO and published history.
- [x] Add schema-2 AI packet context while retaining schema-1 readability; metadata is background, not new gameplay evidence.
- [x] Export Workshop-only JSON without a baseline or record changes and show it in a read-only review tab.
- [x] Archive the local preview byte-for-byte, detect PNG/JPEG/GIF encoding despite `.img`, report size/hash, and preserve the original.
- [x] Require a new archive outside Kenshi, reject overwrite/mod targets/stale previews, and exercise the exact 1 MB size boundary.
- [x] Embed Scribe as a native MDI child of FCS rather than an owned desktop window.
- [x] Give all ten actions bottom-of-window hover and keyboard descriptions without erasing operation status; the current UI follow-up moves them into menus.
- [x] Exercise missing/partial/corrupt caches, stale dialogs, explicit unpublished/unknown values, dependency reads without mutation, upload/refresh blocking without disabling offline capture, image formats, archive failures, MDI parenting and help events on disposable FCS copies.
- [ ] Manually verify embedded resizing, minimize/restore, close/reopen, hover/focus help, and Workshop tab behavior in FCS Extended.
- [ ] Manually compare all fields with the actual Workshop window, archive a real preview, and verify offline/cache/unknown warnings.

### 5B. Resumable tracking and scheduled checkpoint review (beta 2)

- [x] Offer new-file and existing-JSON choices from Start tracking.
- [x] Store one full starting state and versioned identity/fingerprints; retain existing full-state roots as compatible anchors.
- [x] Validate saved state/history in the background, retain baseline identity across resume, and reject incompatible mod/load-order context.
- [x] Preserve legacy AI exports as evidence and exact `.legacy.bak` bytes after explicit conversion approval, starting a fresh current-state baseline.
- [x] Append durable changed-record/context deltas to one UTC timestamp + unique-ID session log per start/resume instead of rewriting full baselines.
- [x] Persist a small atomic project/head marker and hold a project writer lease; detect missing required history and observed external edits.
- [x] Compile missing entry IDs into the main append-only journal every 30 automatic minutes; provide explicit Compile journal in manual mode.
- [x] Keep compilation independent of recording/button activity, defer busy/modal/minimized work and never run a catch-up backlog.
- [x] Retain session originals; require all uncompiled logs and permit manual archival of compiled logs with the plugin closed.
- [x] Recover only incomplete final lines after approval and durable original-byte backup; reject complete corruption without truncation.
- [x] Confirm editor-versus-recorded-state discrepancies and explicitly distinguish tracking from mod backup/restoration.
- [x] Advance the in-memory baseline only after a successful save; retain it and stop automatic review after errors.
- [x] Offer Manual (default), 30 seconds, 1 minute, 5 minutes and 10 minutes in persisted Plugin settings.
- [x] Serialize automatic actions, skip hidden/busy windows, stop on close and keep all live FCS reads on its UI thread.
- [x] Default JSON dialogs to the active mod's actual directory; support custom defaults/individual locations without moving the active tracking file.
- [x] Keep compact AI export available for pending evidence or the latest matching saved batch, without resetting imported drafts on each tick.
- [x] Warn that full-state tracking files are large/private and must not be used for AI handoff or Workshop distribution.
- [x] Exercise background start/resume, actual timer dispatch, independent interval/30-minute deadlines, idempotent compilation, crash-tail recovery, missing storage/head, legacy compatibility, writer exclusion and copied-data defaults.
- [ ] Manually exercise new/resume/conversion/settings dialogs, long-running validation/close, all intervals and restart recovery in FCS Extended.
- [ ] Add history browsing, combined batch export and persisted draft/export associations.

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

Remaining feature expansion follows the stabilization gate and MVP acceptance.
The beta 2 checkpoint foundation is already implemented. History/export changes
needed to resolve S02 are stabilization work; broader history UI, OCS and new
output formats remain deferred.

### Session history

- [x] Persist the baseline, identities/timestamps, dependency context and evidence across editor restarts using an anchor plus durable session/main journals.
- [x] Restore history only after verifying active mod/context identity; report restored checkpoints/pending changes and reject incompatible sessions.
- [ ] Preserve duplicate detection and draft-to-export associations across restarts without resetting the baseline on save/apply.
- [ ] Add history browsing, batch selection and retention controls beyond new/resume/review.
- [x] Define versioned storage/legacy conversion, explicit corruption failures, rotating small-marker backup, permanent legacy/recovery backups and unpruned session/main history.
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

1. Finish the requested menu cleanup without changing existing action semantics.
2. Address S01-S16 one at a time, recording regression evidence and any explicitly accepted support boundaries.
3. Close the real FCS Extended acceptance gate and any issues it exposes before new feature expansion.
4. Only then resume OCS design/implementation, broader session history and Markdown exports as independently scoped work.
5. Validate dependency provenance before wiki-ready exports; a direct AI connection remains optional and explicitly prioritized.
