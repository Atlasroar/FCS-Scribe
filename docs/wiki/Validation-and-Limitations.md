# Validation and limitations

The plugin is experimental. The purpose of validation is to establish exact
behavior within a stated scope, not to turn partial extraction into a claim of
complete editor compatibility.

## Automated and disposable-copy evidence

The recorded beta 3 checks passed. These retain beta 2's session-journal and
checkpoint coverage and add grouped-menu coverage:

| Check | Result | Boundary |
| --- | --- | --- |
| Dependency-free core checks | 138 passed | Includes session deltas/replay, idempotent compilation, explicit interrupted-tail recovery, complete-corruption rejection, missing history/storage, old-format compatibility, writer exclusion, target protection and independent deadlines |
| Actual FCS methods on copied vanilla data | 136 passed | Includes exact menu grouping, hover/keyboard help, overflow access, serialized menu dispatch and deferral while menus are open, alongside background start/resume, durable recording and compilation |
| Actual FCS methods on a representative copied mod | 135 passed | Same coverage; no instances in this fixture, so that mutation was explicitly skipped and covered by vanilla |

The integration host initializes actual FCS forms without launching the complete
interactive editor/mod-selector workflow. It exercises unsaved overrides,
active/inherited note preservation, existing TODO category preservation,
duplicate rejection, no save on apply, normal save/reopen, unchanged captured
gameplay records, and baseline retention.
Workshop checks cover every requested field, staged values versus cached data,
unknown and unpublished values, current required/reference lists without header
mutation, malformed/partial XML and DTD rejection, PNG/JPEG/GIF byte preservation,
new-file/source protection, stale previews, the exact 1 MB boundary and refresh/
upload blocking with offline capture preserved. Actual
FCS MDI parenting and every menu action's hover/keyboard help are exercised
under a generic WinForms message loop so async continuations stay on the UI
thread. This does not launch FCS's complete startup/mod-selector workflow.

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
- Base/locked field-level source mod provenance remains unresolved.
- Notes are shared editor-side record annotations, not `.mod` content or
  Workshop release notes.
- Multi-record note application has no verified transaction/rollback contract.
- Saved baselines/history can be resumed, but drafts/export associations remain
  window-local; there is no history browser or combined multi-batch AI export.
- Capture runs synchronously on the editor thread; large stacks can pause UI.
- Interval sampling misses changes reverted between ticks. Record deltas still
  contain complete changed records; they are not a full editor event stream.
- History is unpruned and replay validation grows with it. Manual AI export
  still compares on the UI thread. Very large stacks/histories remain a
  32-bit memory/latency risk despite fewer copies and smaller disk writes.
- Full-state tracking files/backups may be large and private; they are not AI
  handoffs or Workshop distribution files. Fingerprint/hash checks detect
  self-consistency and observed external edits, not cryptographic authorship or
  an absolute cross-process transaction.
- Matching evidence IDs does not establish that AI prose is factually correct.
- OCS integration, Markdown exports, wiki-ready gameplay
  exports, and direct AI-provider connections are planned, not implemented.
