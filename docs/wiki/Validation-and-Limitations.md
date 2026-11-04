# Validation and limitations

The plugin is experimental. The purpose of validation is to establish exact
behavior within a stated scope, not to turn partial extraction into a claim of
complete editor compatibility.

## Automated and disposable-copy evidence

The recorded beta 11 checks passed. These retain session-journal, grouped-menu,
complete-coverage/safe-retry, recording-range, optional Workshop isolation and
portable draft, recoverable initialization and settings recovery checks while
adding inactive export-destination protection:

| Check | Result | Boundary |
| --- | --- | --- |
| Dependency-free core checks | 593 passed | Adds inactive/standalone/copied anchors, backup/storage guards, malformed/unreadable destinations, escaped/duplicate format fields, BOM encodings, concurrent changes and atomic export failure boundaries |
| Actual FCS methods on copied vanilla data | 266 existing + 16 settings + 24 scheduling + 24 export protection passed | Adds actual window AI/Workshop/workspace path writes, inactive storage with no open project, protected workspace backup rotation, ordinary existing destinations and preview storage guards in fresh hosts |
| Actual FCS methods on a representative copied mod | 265 existing + 16 settings + 24 scheduling + 24 export protection passed | Same coverage; no instances in this fixture, so that mutation was explicitly skipped and covered by vanilla |

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

Beta 9's settings matrix distinguishes unreadable originals from missing files,
hashes/parses the same stream and verifies exact bytes before/after repair.
Actual Windows sharing modes block reads and atomic replacement; a cooperating
lease and two simultaneous saves exercise serialization and stale conflicts.
The actual window records gameplay manually during recovery, gates timer and
automatic compilation, cancels its recovery dialog, reloads repaired settings
and retains the same project/packet and exact unfinished draft.
Settings checks run in a fresh 32-bit process; a combined run and full-debug
JIT lifetimes exhausted the host's address space. The final runner uses
PDB-only symbols and separate hosts, not a production memory fix. Large-model
and history performance remains S12. These cases do not establish exhaustive
ACL/disk-full/network-filesystem or physical-power-loss behavior.

Beta 10's scheduling checks use explicit UTC/monotonic clock inputs for exact
30-minute boundaries and synthesize an older valid session timestamp/hash in
disposable history to exercise repeated real Resume paths without a wall-clock
wait. Hidden/minimized/busy windows, menus and modal forms defer overdue work;
manual/recovery modes hold it; a journal conflict leaves its deadline intact.
Actual automatic work compiles it once, clears the notice and leaves recording
timing and originals intact. Future UTC timestamps do not create unbounded
resume delays; already-running monotonic countdowns survive clock adjustments.
These cases do not establish an authenticated clock or complete real-editor
acceptance.
A separate check generated compiled/uncompiled history using the **released
beta 9 binary**, then verified beta 10 replay/export retained exact evidence,
captured state, the original oldest pending timestamp and every source hash.

Beta 11 verifies complete inactive-project file hashes before/after rejected
window writes, including Workshop without an open project. Core tests inject
pre-commit write/flush/commit failures and destination conversions after
observation, with explicit errors and no temporary files left behind.
Existing schema-2/3 AI packets, Workshop exports and BOM-marked UTF-8/UTF-16
destinations remain replaceable; separate `.bak` files remain untouched by
AI/Workshop export replacement. Workspace rotation checks protect tracking
content at its backup path. These checks do not drive every native file dialog,
enumerate physical filesystem aliases, establish a universal race lock or
simulate physical power loss.

## S10 recorded-versus-saved boundary: beta 12

The existing **593 core checks** and plugin build passed. The isolated
`-RecoveryOnly` suite passed **94 checks on copied vanilla data and 94 on the
representative test mod**. These include the persistent notice, shared
Start/Resume guidance and narrow-window wrapping/menu help, plus:

- Record a real unsaved FCS rename, close tracking without saving, then reload
  the original saved mod through FCS's native data loader.
- Exercise the actual asynchronous Resume path with injected No/Yes decisions:
  No keeps every history byte and creates no session; Yes leaves the saved
  editor unchanged until Review records the reversal. Compile retains both
  observations rather than rewriting the original.
- Explicitly save a different edit and note through normal FCS Save, reload,
  then Resume/Review/Compile against older compiled history without a further
  save, restoration, dirty-state change or mod/notes file rewrite.
- Copy the saved mod, native notes sidecars, full baseline and complete tracking
  folder byte-for-byte, then validate/replay the copied tracking set.

Native methods, persistence and window paths are real, but the post-crash
state is simulated by discarding the in-memory model and reloading saved files.
No live installation/test-mod/notes files were modified. This does not force
an actual process/power failure, drive every native dialog through the complete
FCS Extended application, test arbitrary backup restoration/relocation, or
close S09/S13/S14. Saved state is deliberately **not inferred** from tracking.

## Reproduce

See [Building and releases](Building-and-Releases#run-checks-and-inspect-the-supported-build)
for the core/integration commands, inspection command and required local files.
Builds/inspection do not install the plugin or replace interactive acceptance.

## Real FCS Extended acceptance: beta 11

A bounded physical pass was performed on November 3-4, 2026 using the supported
Steam FCS build, FCS Extended, FCS-Scribe beta 11 and a disposable active test
mod over the normal base-game stack. The user operated the editor. Supporting
files were inspected read-only; deliberately damaged journals were separate
copies, not the original project.

**This is partial acceptance evidence, not closure of S09 or approval for
normal production use.** UI/timing observations below are user-reported;
file counts, hashes, packet validation and byte-preservation claims were
independently checked where stated.

| Area | Observed result and boundary |
|---|---|
| Startup and embedded UI | User confirmed extension discovery, toolbar, grouped menus, keyboard/hover help, resizing, minimizing/restoring and close/reopen inside FCS. This covers the installed supported configuration only. |
| Baseline and real edits | A 54,951-record baseline was validated. Added record, scalar edit and same-ID rename were captured; repeated unchanged reviews added nothing. A direct field-to-Scribe focus route captured the edit, not every FCS control/buffer route. |
| Real AI handoff | Three recordings exported as a schema-3 packet with 71 qualified change citations. The actual manual assistant response, import/paste and complete coverage were checked. Export did not advance/compile history or save the mod. |
| Draft workspaces | Save/Open, exact completed text, an unsaved-text close cancellation, replacement/discard and restoration after closing Scribe were exercised. A stale restored draft was not treated as approval. |
| Approval and saved notes | Invalid JSON, stale state, approval No, approved append and duplicate rejection were exercised. Existing KEEP text and one generated block were independently verified after explicit FCS Save and an earlier cold restart. Apply had not saved the mod or notes sidecar before that explicit Save. The observed category token remained `N`; this is not physical verification of all TODO/inherited-note semantics. |
| Recording and compilation | User timed 30-second, 1-, 5- and 10-minute recording intervals, including early/late checks for the longer intervals. Minimized/menu-open deferral, restore catch-up, Manual hold and repeated Compile no-op were exercised. Real pending entries aged over 30 minutes while Manual/closed, then compiled on eligible automatic work; exact boundary arithmetic remains separately automated. |
| Workshop workflow | Export without tracking, default/unknown/unpublished values, native-dialog comparison and exact staged change notes were checked. A real 44,977-byte PNG archive matched source/export hashes. Cancel, existing-archive refusal and inside-install rejection were exercised without publishing. This does not cover every nonempty metadata field, encoding or real Steam operation. |
| Journal recovery | A complete project copy first hash-matched the original. Cancelling an incomplete-tail recovery retained its exact bytes with no backup. Approval restored all 11 complete entries and preserved the exact damaged bytes in a unique backup. A complete invalid line was rejected without truncation; baseline-only Resume rejected missing storage without inventing history. |
| Legacy conversion | A real schema-2 packet containing 67 changes was extracted to a new disposable file and validated. Cancel preserved it; approval retained its exact `.legacy.bak` bytes and original imported evidence in a valid fresh current-state project. Unchanged Review added no recording, and conversion did not save the mod. |
| Export guards | Corrected attempts to replace an inactive copied baseline and reserved marker were refused without changing either file. Malformed existing JSON was retained; a valid ordinary Workshop export was created/replaced successfully. An initial reported success was a different selected filename, not a protected-file overwrite. |
| Settings and busy close | Cancel/custom export defaults/discard-reload, invalid relative/missing directories, explicit malformed-settings recovery and repair/reload were exercised. Exact original malformed bytes were preserved in one unique repair backup; original valid settings were restored byte-for-byte. The project and unsaved draft survived repair/reload. Close was deferred during background validation and worked afterward. |
| Native read-only mode | A second FCS instance loaded the shared mod read-only. Clicking Scribe explicitly reported `Open a writable active mod before using FCS-Scribe.` This prevented reaching the separate project-writer exclusion test. |
| Final journal/restart | User confirmed the item name/ID and charges 790 persisted, Manual settings persisted, and original-project Resume/Review/Compile plus unchanged repeats worked. Files contain 12 distinct recordings, all compiled with exact original-line agreement; the final added recording is context-only and the original baseline is unchanged. |

### Final notes checkpoint correction

The final restart initially was reported as retaining the original KEEP text
and generated block, but file inspection did **not** confirm that claim.
The target instead contained `LOCAL ACCEPTANCE TEST - DO NOT PUBLISH`.
The user identified this as an earlier manual test-data replacement.
That exact replacement text is present in the saved target and was visible
after restart; there is no evidence attributing the replacement to Scribe.

The user chose to keep the current text and **not rerun final retention of the
original block**. No stale draft was reapplied. Earlier independently verified
KEEP/generated-block preservation remains evidence for its earlier checkpoint,
not proof that those same annotations remained through the final restart.

### Remaining acceptance gate

The following boundaries are explicitly untested or incomplete in this physical
pass. Existing automated coverage is not promoted to physical acceptance.

| Boundary | Remaining limitation |
|---|---|
| Broader compatibility | No additional non-vanilla dependency stack or independent FCS Extended extension was available. FCS Extended is the loader, not a second extension-coexistence case. The user accepted leaving this broader configuration untested. |
| Restricted modes/builds | Merge/translation and unsupported-build physical rejection were left untested by user choice/setup. No executable alteration or guard bypass was attempted. Native read-only rejection did pass. |
| Competing project writers | Native FCS read-only loading prevented opening Scribe in the second instance. The Scribe writer lease was therefore not physically contested. |
| Notes failure behavior | Deliberate native notes-save failures and multi-record partial-application failures were left untested by user choice; the shared editor-side notes file was not corrupted, removed or force-locked. |
| Final original-block retention | Not rerun after the acknowledged manual notes replacement. Current replacement-text persistence is not the same test. |
| Capture breadth | Deletions, reference/quantity/removal and nested-instance semantics remain automated/disposable-copy evidence, not additional physical mutations in this pass. One focus route does not establish all UI control commits. |
| Additional storage/settings failures | Physical power/disk/access failures, all initialization retry boundaries, unreadable/concurrent settings repair and manual compiled-session archival were not exhaustively exercised. |
| Workshop breadth | Real asynchronous Steam upload/refresh and every populated metadata/image-format boundary were not physically exercised. No publishing was performed for acceptance. |
| Performance and restoration | The large baseline is one observation, not large-stack/history profiling or proof of mod restoration. S12 and the remaining support-boundary work remain open. |

The [working plan](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md)
retains the overall gate. Recording these results/exceptions is not an implicit
decision to close S09, bypass remaining tests or publish a new beta.

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
