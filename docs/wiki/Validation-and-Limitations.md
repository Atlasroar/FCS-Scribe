# Validation and limitations

The plugin is experimental. The purpose of validation is to establish exact
behavior within a stated scope, not to turn partial extraction into a claim of
complete editor compatibility.

## Automated and disposable-copy evidence

The recorded beta 8 checks passed. These retain session-journal, grouped-menu,
complete-coverage/safe-retry, recording-range, optional Workshop isolation and
portable draft checks while adding recoverable initialization:

| Check | Result | Boundary |
| --- | --- | --- |
| Dependency-free core checks | 428 passed | Adds New/conversion interruptions at marker/baseline/final-marker write, flush and before/after commit; retry/Resume, repeated failures, backup/source/history ownership, actual sharing/target conflicts and source-byte preservation |
| Actual FCS methods on copied vanilla data | 266 passed | Adds actual Start retry after an uncommitted baseline write and background Resume after a committed baseline/final-marker failure, with unchanged baseline bytes and normal recording/session behavior |
| Actual FCS methods on a representative copied mod | 265 passed | Same coverage; no instances in this fixture, so that mutation was explicitly skipped and covered by vanilla |

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

Draft protection tests inject Discard/Cancel decisions into the window's
decision boundary and exercise actual Save/Open path helpers, background
storage/adoption and closing handlers. They do not operate every native
Save/Open/overwrite/approval dialog
through the full FCS Extended application; those interactions remain S09.
Explicit workspace persistence is not an autosave or forced-shutdown guarantee.

Beta 8's initialization matrix injects I/O/access-denied exceptions into scoped
atomic-write checkpoints, including immediately after completed commits.
Separate cases use actual Windows sharing violations and destination-directory
collisions, observed external file changes, missing legacy backups and
unrecognized history/markers/backups. Recovered projects subsequently record
and replay normally. These tests do not cut physical power, exhaust a volume
or exercise every real ACL/network-filesystem failure.
A separate check used the **released beta 7 binary** to generate compiled and
uncompiled history with Workshop context, then used beta 8 to replay/export it.
Original serialized evidence, captured state and every source-file hash matched.

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
- Saved baselines/history can be resumed; beta 7's explicit draft workspaces
  retain text/packet associations independently. They are not autosave or crash
  recovery: unsaved edits can be lost on forced termination. The recording-range selector is not a general history editor,
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
