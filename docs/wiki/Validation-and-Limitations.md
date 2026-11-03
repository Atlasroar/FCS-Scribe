# Validation and limitations

The plugin is experimental. The purpose of validation is to establish exact
behavior within a stated scope, not to turn partial extraction into a claim of
complete editor compatibility.

## Automated and disposable-copy evidence

The recorded beta 6 checks passed. These retain session-journal, grouped-menu,
complete-coverage/safe-retry and recording-range checks while adding optional
Workshop failure isolation:

| Check | Result | Boundary |
| --- | --- | --- |
| Dependency-free core checks | 215 passed | Adds optional-field serialization, stable partial availability/reason fingerprints, known-field/gameplay/recovery stale checks, range preservation and AI unknown-value instructions |
| Actual FCS methods on copied vanilla data | 235 passed | Adds durable recording through busy upload/refresh, malformed/mismatched/locked cache, invalid/unsupported/oversized/locked preview, simultaneous failures, independent readable components, replay/export, visible UI warnings, automatic recording/recovery and separate action-error pause policies |
| Actual FCS methods on a representative copied mod | 234 passed | Same coverage; no instances in this fixture, so that mutation was explicitly skipped and covered by vanilla |

The integration host initializes actual FCS forms without launching the complete
interactive editor/mod-selector workflow. It exercises unsaved overrides,
active/inherited note preservation, existing TODO category preservation,
duplicate rejection, no save on apply, normal save/reopen, unchanged captured
gameplay records, and baseline retention.
The retry fixture seeds one completed note through FCS's actual API, then
retries the full draft and verifies that target is unchanged while the pending
target is appended and survives save/reload. This does not establish rollback
or exhaustively inject failures within FCS's nontransactional methods.
Workshop checks cover every requested field, staged values versus cached data,
unknown and unpublished values, current required/reference lists without header
mutation, malformed/partial XML and DTD rejection, PNG/JPEG/GIF byte preservation,
new-file/source protection, stale previews and the exact 1 MB boundary.
Beta 6 skips unstable Workshop fields during refresh/upload while gameplay
recording continues. Strict archiving still blocks busy/unreadable images;
offline capture remains available. Actual
FCS MDI parenting and every menu action's hover/keyboard help are exercised
under a generic WinForms message loop so async continuations stay on the UI
thread. This does not launch FCS's complete startup/mod-selector workflow.
Range checks use two real automatic gameplay ticks, preserving both active
and effective name evidence after resume. The modal range selector is
cancelled programmatically; native destination-file selection and the complete
real-editor export/import interaction still require manual acceptance.
A separate cross-version check created a baseline, compiled entry and
uncompiled entry using the released beta 4 binary, then reopened/exported them
with beta 5. Original packet serialization and all tracking-file hashes were
unchanged. This is compatibility evidence, not a broader performance test.
A separate beta 6 check used the released beta 5 binary to generate actual
compiled/uncompiled history containing Workshop/preview metadata. Beta 6
replay/export preserved exact original packet serialization and every tracking
source-file hash with the new optional availability fields omitted.

The optional-failure matrix records actual unsaved names, verifies stable
repeated captures and validates drafts against the known state. A real timer
tick records through corrupt cache data without pausing; repairing the cache
recovers metadata, stales the prior range draft and records context only.
Pause-policy checks preserve scheduling for Workshop-only errors and preserve
an existing tracking pause. This does not exhaustively fault-inject every
filesystem permission failure, force memory exhaustion or complete a real
interactive Steam upload/refresh.

Original game/mod files are only copied; test saves go to a fresh ignored
`build\integration-fixtures` directory. The Framework test host is explicitly
32-bit like this FCS build. Generated fixtures can be inspected and deleted.

## Reproduce

See [Building and releases](Building-and-Releases#run-checks-and-inspect-the-supported-build)
for the core/integration commands, inspection command and required local files.
Builds/inspection do not install the plugin or replace interactive acceptance.

## Remaining acceptance gate

The real FCS Extended workflow still needs a manual acceptance pass for
extension discovery, toolbar placement, field-focus/control commits, review,
export/import/paste, draft editing/cancellation/approval, stale/duplicate
messages, notes inspection, and normal save/restart.
Also check embedded resize/minimize/restore/close behavior, bottom help layout,
Workshop tab refresh, field agreement with a real Workshop dialog and preview
archive selection/cancellation.
New/resume/legacy-conversion/settings dialogs, every timer interval, long-running
validation/close and restart recovery also need interactive acceptance.
Also exercise the 30-minute schedule, discrepancy confirmation, recovery
approval/cancellation, two editor instances and manual session archiving.
Beta 3's grouped menus also require real-editor mouse/keyboard, small-window
layout and MDI acceptance; disposable-copy results do not close this gate.
Beta 5's range selection, preview scrolling, destination/cancellation and real
manual AI handoff also remain part of that interactive pass.
Beta 6's warning/status/approval wording, Workshop-only error dialogs and real
asynchronous Workshop operations also require interactive acceptance.

Broader dependency stacks, other extensions, unsupported/read-only modes, and
visible failure handling also need the checks recorded in the
[working plan](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md).
An experimental prerelease does not mark those checkboxes complete.

## Known limits

- One inspected Steam FCS 2.14.2 executable hash is supported; GOG and other
  builds are not automatically compatible.
- Pending controls, separate dialogue buffers, general external data files,
  layouts and metadata change history are outside comprehensive tracking.
- Workshop fields are local/staged/cached context, not a current Steam query or
  published changelog. Preview archiving supports only the local FCS image.
- Known optional Workshop failures are explicit partial capture, not verified
  payloads. Changing unreadable bytes without changing the reported availability
  state/reason is not detected by fingerprints. Unexpected adapter/model and
  fatal resource errors still stop actions.
- Base/locked field-level source mod provenance remains unresolved.
- Notes are shared editor-side record annotations, not `.mod` content or
  Workshop release notes.
- Multi-record note application has no verified transaction/rollback contract.
- Saved baselines/history can be resumed, but drafts/export associations remain
  window-local. The recording-range selector is not a general history editor,
  mod-restoration tool or persistent list of already documented evidence.
- Range exports preserve observations rather than a net diff. Overlapping
  ranges can document the same evidence under different batch markers; review
  citations and existing notes. Original and qualified catalogues both ship in
  the packet, so long selections are larger than a single-batch export.
- Capture runs synchronously on the editor thread; large stacks can pause UI.
- Interval sampling misses changes reverted between ticks. Record deltas still
  contain complete changed records; they are not a full editor event stream.
- History is unpruned and replay validation grows with it. Export source
  validation/range building uses workers, but live capture/fingerprinting and
  range previews still run on the UI thread. Holding/serializing original
  packets plus qualified catalogues increases memory use for large histories;
  32-bit memory/latency profiling remains S12, not a resolved performance claim.
- Full-state tracking files/backups may be large and private; they are not AI
  handoffs or Workshop distribution files. Fingerprint/hash checks detect
  self-consistency and observed external edits, not cryptographic authorship or
  an absolute cross-process transaction.
- Matching evidence IDs does not establish that AI prose is factually correct.
- OCS integration, Markdown exports, wiki-ready gameplay
  exports, and direct AI-provider connections are planned, not implemented.
