# Validation and limitations

This page describes the current support boundary and remaining acceptance
work. Detailed test counts, measurements and dated observations belong in
[Validation history](Validation-History), not the feature guide.

## Supported environment

| Area | Current boundary |
|---|---|
| Platform | Windows, .NET Framework 4.8 and installed FCS Extended |
| FCS executable | Inspected Steam file/product 2.14.2, assembly 2.1.0.0, guarded by the exact SHA-256 below |
| Editing mode | A writable active `.mod` in normal editing mode; read-only, merge and translation sessions are rejected |
| Dependencies and extensions | Specific copied official-data/representative configurations have evidence; arbitrary custom stacks and other extensions are not broadly accepted |
| GOG and other builds | Not verified/supported by the current executable adapter |
| FCS Extended startup and physical UI | Scoped beta 21 guided observations and an owner-reported passing feature checklist exist for one test setup; broader configurations and detailed failure cases remain unverified |

Supported executable SHA-256:

```text
3688c4d8421d51c2d30c2ff0338874779d91eff150c6a02bf415bc1cdd9c7796
```

The guard is the hash, not the display version. Do not remove it or replace
upstream binaries to force compatibility. See the archived
[exact configuration matrix](Validation-History#exact-build-and-configuration)
for the inspected interface dependency and copied-stack scopes. That interface
hash identifies a test dependency, not an additional runtime whitelist.

## What the evidence establishes

| Evidence | What it establishes | What it does not establish |
|---|---|---|
| Core checks | Deterministic evidence, storage/replay rules, validation and controlled failures | Native UI behavior or physical disk/power-failure guarantees |
| Disposable native hosts | Actual FCS methods/forms on copied inputs, including guarded capture, menu selection, persistence and recovery cases | The complete FCS Extended startup/mod-selector workflow or every physical interaction |
| Historical physical acceptance | The specifically recorded editor/version/configuration and user observations | Blanket acceptance of the current release or other stacks |
| Performance profiles and soak | Measured workloads and one accepted bounded actual-editor soak | Universal record-count, memory, latency or leak-free guarantees |
| CodeQL | Static analysis within its available context | A Windows plugin build, native acceptance or proof of no vulnerabilities |
| Markdown exports | Deterministic typed/evidence-linked output, synthetic GFM rendering and guarded copied-native export paths | A new physical beta 22 acceptance pass, complete gameplay interpretation, vanilla comparisons or a tracking/workspace backup |
| Recording-history browser | Detached original catalogue, session/search filters, complete inclusive range preview and non-writing browser behavior | A physical beta 23 acceptance pass, saved/applied state, an event log or retention/deletion guarantees |
| Task table | Strict metadata/legacy storage, non-writing filters/sorting and synthetic non-showing WinForms editing/layout checks | A physical beta 24 acceptance pass or independently rerun native record-selection/type integration |

The current notes/task feature has copied-native evidence on official-data and
representative fixtures. Its detailed scope is recorded under
[Scribe notes and TODOs](Validation-History#scribe-notes-and-todos-beta-20).
Use [Building and releases](Building-and-Releases#run-checks-and-inspect-the-supported-build)
to reproduce checks; passing a runner is not physical acceptance.

The [beta 21 real-editor follow-up](Validation-History#real-fcs-extended-follow-up-november-5-2026)
adds user-operated footer/menu/tab checks and Scribe text/settings persistence
through a full FCS restart. Installed DLL bytes matched the release archive;
test notes/tasks/settings and `.mod` bytes were independently unchanged across
that restart. It does not establish complete native-annotation or tracking/AI
workflow acceptance.

The later [tracking/AI/workspace follow-up](Validation-History#tracking-ai-handoff-and-workspace-follow-up-november-5-2026)
adds independently checked recording/range/workspace contracts and user-observed
missing-evidence, stale-state and approval-cancellation behavior.
The [owner checklist decision](Validation-History#owner-checklist-decision-november-5-2026)
marks all fourteen feature areas Pass and authorizes further feature work.
Those statuses are owner-reported, not independently observed executions.
A saved test `.mod` byte change across the later restart remains unexplained
despite the reported native discard; the seven checked Scribe files remained
unchanged. The earlier byte-identical restart and this later discrepancy are
separate observations.

Current Markdown exports have [beta 22 core/rendering/copied-native evidence](Validation-History#markdown-changelog-exports-beta-22).
Facts and reviewed prose remain separate, with exact values and evidence links.
The owner's beta 21 checklist does not establish physical acceptance of these
new actions or resolve the earlier saved-mod discrepancy.

The [beta 23 history browser evidence](Validation-History#recording-history-browser-beta-23)
covers read-only filtering, original context/identity, pending/legacy/empty
states and reuse of existing export guards on a small copied fixture. It is
not new physical FCS Extended acceptance. Retention/pruning, full metadata
history and prior-note application status remain outside this feature.

The [beta 24 task-table evidence](Validation-History#task-table-beta-24) covers
813 core checks/build and 34 synthetic non-showing task-view cases. The updated
native integration suite compiled but was not executed; no FCS executable or
visible forms were opened. Captured task names/types are not live record labels,
and unknown legacy metadata is not inferred. The earlier checklist does not
certify this new task UI.

## Capture and provenance limits

Capture is a partial observation of supported committed in-memory state, not
every editor event or a comparison against vanilla. Pending controls, separate
dialogue buffers, general external files/assets/layouts and comprehensive
metadata history are omitted. Zero evidence is not an all-clear, and edits
reverted between observations may be absent.

Base/locked field ownership remains unresolved. Loaded order, declared
dependencies and locked context are not interchangeable source attribution.
Workshop fields are local/staged/cached context, not a live Steam query or
published changelog. Unavailable payload bytes are not fingerprinted; matching
availability reasons do not prove those bytes stayed unchanged.

See the [supported/omitted-state contract](Data-and-AI-Contracts#supported-and-omitted-state-beta-13).

## Persistence, privacy and approval limits

- Tracking records observed state, including unsaved edits; it neither saves
  nor restores `.mod`, assets or native annotations. Resume validates and
  compares history rather than recovering gameplay.
- Scribe `notes.txt`/`todo.txt`, native shared `data\__TODO__<user>.txt`,
  tracking sets and AI draft workspaces are different persistence surfaces.
  Scribe text files commit independently. Root switching is not migration or
  a multi-file transaction.
- Native multi-record annotation application has no verified rollback
  contract. Inspect partial results and retain the original complete draft
  for a guarded retry. Matching evidence IDs do not prove prose correctness.
- Explicit workspace Save/Open preserves draft text and its packet; it is
  not autosave or forced-termination recovery. History browsing/range selection
  is not a history editor or a list of already documented evidence.
- Hashes/leases detect self-consistency and observed conflicts, not authorship,
  universal aliases, all uncooperating-writer races or physical failures.
  Tracking histories are linear and must not be merged as text.
- Full baselines, journals, workspaces, settings/locators and backups may
  contain private/dependency data. Scribe does not inspect or block native
  Workshop uploads.

Follow the [backup and recovery procedures](Installation-and-Workflow#recorded-state-saved-mods-and-backups-beta-12)
and [safe note retry procedure](Installation-and-Workflow#per-record-notes-outcomes-and-safe-retries-beta-18).

## Performance and long sessions

Live capture runs on the editor thread and can pause large sessions.
History is unpruned, validation costs grow with it, and long range exports
retain original packets plus qualified catalogues in a 32-bit process.
Use Manual for large stacks and retain normal mod/notes saves and backups.
A longer interval reduces frequency, not each capture's memory/latency cost.

The owner accepted the bounded performance/soak scope recorded in
[historical measurements](Validation-History#s12-performance-measurements-partial-beta-14);
custom-stack headroom and broader latency remain unverified. Profiling commands
belong in [Building and releases](Building-and-Releases#reproduce-performance-measurements).

## Remaining interactive acceptance

Use the [self-guided testing checklist](Testing-Checklist) for current
history/Markdown actions and related regressions. Its items start Not run;
publishing the checklist is not a new physical acceptance pass.

The owner accepted beta 21's feature checklist for the tested configuration
and explicitly directed feature work to continue. Remaining S09 support/test
boundaries are not silently marked passed: broader workflow/control coverage,
final original annotation-block retention, additional dependency/extension
configurations, competing project writers, physical save/access failures and
real asynchronous Steam behavior remain outside the established evidence.
The later saved-mod byte discrepancy also remains unresolved; the owner's
checklist decision does not turn it into an independently passed no-save check.

The archived [remaining physical acceptance table](Validation-History#remaining-acceptance-gate)
preserves exactly what was left untested and why, including the
[final notes checkpoint correction](Validation-History#final-notes-checkpoint-correction).
The [working plan](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md)
tracks scope and decisions. Owner-reported acceptance is distinguished from
independent verification; untested configurations are not passed.

## Not implemented

History retention/editing controls, OCS saved-data integration, wiki-ready gameplay
exports, direct AI-provider connections, native notes-button redirection and
plain-text vanilla comparisons are not current features.

## Historical evidence links

<!-- Preserve previously published bookmarks; detailed results moved intact. -->
<a id="automated-and-disposable-copy-evidence"></a>
<a id="scribe-notes-and-todos-beta-20"></a>
<a id="historical-baseline-evidence-beta-11"></a>
<a id="s10-recorded-versus-saved-boundary-beta-12"></a>
<a id="s11-partial-capture-and-sampling-beta-13"></a>
<a id="s12-performance-measurements-partial-beta-14"></a>
<a id="history-costs-and-measured-regression-envelopes"></a>
<a id="native-capture-and-the-unresolved-memory-boundary"></a>
<a id="beta-15-snapshot-local-string-sharing"></a>
<a id="beta-15-two-hour-actual-editor-soak"></a>
<a id="beta-14-integrity-and-acceptance-boundaries"></a>
<a id="s13-storage-safety-beta-16"></a>
<a id="s14-portability-and-divergent-history-beta-17"></a>
<a id="s15-partial-notes-and-shared-sidecars-beta-18"></a>
<a id="s16-compatibility-and-provenance-matrix-beta-19"></a>
<a id="exact-build-and-configuration"></a>
<a id="dependency-and-ownership-evidence"></a>
<a id="workshop-truth-and-authoritative-integration"></a>
<a id="reproduce"></a>
<a id="real-fcs-extended-acceptance-beta-11"></a>
<a id="final-notes-checkpoint-correction"></a>
<a id="remaining-acceptance-gate"></a>
<a id="known-limits"></a>

Older bookmarks land here rather than breaking. Open [Validation history](Validation-History)
for the preserved release-specific results, configuration tables, measurements
and acceptance observations.
