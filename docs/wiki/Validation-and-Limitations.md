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

## S11 partial capture and sampling: beta 13

**598 core checks**, the plugin build, and the isolated `-SamplingOnly` suite
with **22 checks each on copied vanilla and representative test-mod data**
passed. The existing representative `-RecoveryOnly` suite also passed its
94 checks after the presentation change. The
[supported/omitted-state matrix](Data-and-AI-Contracts#supported-and-omitted-state-beta-13)
is the support boundary, not a promise of complete extraction.

The sampling suite binds the real native `ObjectPropertyBox`/PropertyGrid Name
editor to the loaded FCS record and initializes its native undo/list context.
It edits the actual focused textbox, raises its wired WinForms Enter/Escape
key events, and performs a real focus transition with an observed LostFocus
event. It does not substitute direct model setters for those commit tests:

| Route | Observed boundary |
| --- | --- |
| Pending text, then serialized Scribe Review without changing focus | Text remains pending; model/fingerprint/history remain unchanged. Review neither commits nor fabricates evidence for it. |
| Escape textbox event | Pending text is discarded; captured state remains unchanged. |
| Enter textbox event | Native control commits the Name and marks the editor dirty; subsequent Review records it. |
| Actual textbox-to-button focus transition | Native lost-focus handling commits Name; Review records the reversal as a second observation. Native focus handling may move focus again while destroying the editor. |
| Forward/reverse commits between reviews | No additional evidence/entry when both committed values return to the sampled state before the next Review. |
| Sample forward and reverse separately | Both observations survive range export; core tests additionally compile/reopen them without collapsing history. |
| Results and historical display | Baseline, zero-evidence review and range preview retain partial/sampling warnings. Displaying older/restored packets adds guidance without modifying their serialized evidence. |

Mod bytes, existing notes and native dirty state are checked: this workflow
does not save or clear edits. Synthetic journal checks verify no-op session
hashes, unchanged state fingerprints and exact retained original packets.
No snapshot/fingerprint/storage schema is changed for the new explanatory text.

These are native controls under the disposable WinForms host, **not physical
keyboard input through the complete FCS Extended workflow**. Only this bound
Name text editor's routes are established here. Numeric/dropdown/reference/
dialogue editors, separate modal buffers and every possible focus route are
not newly verified. Earlier S09 user evidence covers one real field-to-Scribe
route, not all controls. External assets/layouts, complete event history and
saved-state verification remain omitted. At beta 13 publication, S09 and
S12-S16 remained open; later scoped evidence is recorded below.

## S12 performance measurements: partial beta 14

**At beta 14 publication, S12 remained open.** The user selected publishing bounded history/profiling
improvements while retaining full-capture memory work and a sustained real
FCS Extended soak. Beta 15 adds the bounded memory improvement described below;
the beta 14 measurements remain historical comparison evidence.
The measurements below are local Windows/.NET Framework
x86 test-host observations, not universal support limits or a production SLA.
Runs used fresh processes and copied/synthetic data; some independent jobs
ran concurrently, so small timing differences are not isolated CPU benchmarks.

### History costs and measured regression envelopes

The paired synthetic workload has 2,000 records, 16,992 cells, 100 recordings,
10 retained sessions and a 1,000-cell changed record. Every observation renames
that record, so its complete record delta is stored even though only its name
changed. Baseline/session/journal storage was about 26.3 MiB. Medians:

| Operation | Beta 13 code | Beta 14 code |
| --- | --- | --- |
| Changed recording | 77.9 ms | 64.4 ms |
| Compilation with pending entries | 1,421.4 ms | 257.0 ms |
| Repeated no-op compilation | 2,417.7 ms | 40.3 ms |
| Resume with compiled session originals retained | 7,021.1 ms | 6,425.3 ms |

These are whole-operation times, including existing validation/I/O. Changed
recording includes comparison, full session hash checks, delta/packet validation,
durable append and marker update; it is not just disk-write time.
The large difference in no-op compilation is removal of redundant parsing,
not removal of integrity checks. Resume still hashes/replays the full model
and history and remains proportional to accumulated data.

Workload-specific **local regression ceilings** were checked against every
measured sample, not just medians. These conservative rounded ceilings are
reproduction/review budgets on this setup, **not enforced runtime caps**:

| Synthetic workload | Changed recording max budget | Pending compilation max budget | No-op compilation max budget | Retained-history resume max budget |
| --- | --- | --- | --- | --- |
| 100 entries / 10 sessions / 1,000-cell changed record | 250 ms | 500 ms | 250 ms | 10 seconds |
| 300 entries / 30 sessions / 1,000-cell changed record | 250 ms | 750 ms | 500 ms | 30 seconds |
| 20 entries / 5 sessions / 20,000-cell changed record | 500 ms | 3 seconds | 500 ms | 30 seconds |

All samples met those local ceilings and the synthetic **128 MiB peak working
set** envelope. The growing workload used about 74.8 MiB of storage, peaked
at 49.6 MiB working set and resumed in at most 18.75 seconds. The large-record
workload used about 100.2 MiB, peaked at 84.3 MiB and resumed in at most
19.37 seconds. No history was automatically archived, pruned or rewritten.
Full-file byte hashing still scales with session/journal size; fully compiled
session contents skip JSON/packet parsing only after matching the observed
whole-file hash. Recording again invalidates that completed-session status.
Missing/modified files still fail explicitly, including empty-session headers.

### Native capture and the unresolved memory boundary

The smaller copied `gamedata.base` case contains 9,399 records / 322,017 cells.
Three baseline measurements took about 1.10-1.16 seconds per native capture,
0.69-0.70 seconds per unchanged comparison, 11.94 seconds for baseline creation
and 9.06 seconds for resume. Process lifetime peak working set reached 399.8 MiB.

The larger case combines copied `gamedata.base`, `Newwworld.mod`, `Dialogue.mod`
and `rebirth.mod`, with `gamedata.base` also used as the disposable active
fixture. It contains **55,130 records / 1,339,047 cells**; this is a stress
configuration of official data, not a representative custom mod/extension
stack. In an accelerated **30-cycle** x86 run:

| Measurement | Observed |
| --- | --- |
| Native capture on the UI thread | Median 4.15 seconds; range 3.34-5.50 seconds |
| Unchanged review after capture | Median 2.41 seconds; maximum 2.79 seconds |
| Create full baseline / resume | 44.95 seconds / 30.81 seconds |
| Process lifetime peak working set | 1,662.8 MiB |

Capture and comparison are separate measurements; actual Review includes both.
The baseline alone is about 215 MiB on disk. Working set is **not** a precise
measure of remaining address space; fragmentation, native FCS, other extensions
and allocations between samples can exhaust the 32-bit process earlier.
Managed/private-memory endpoints are also recorded, without forced collection
or a production GC/memory workaround. The 30 cycles completed, but this is
not a multi-hour editor soak or proof of leak-free/unbounded operation.

For now, use Manual on large/slow stacks, finish operations before starting
another and keep separate mod/notes backups. A longer interval reduces how
often capture pauses occur, not their UI-thread duration or peak allocation.
There is **no verified safe record-count or memory ceiling**. If memory or
pauses are problematic, save normally and reopen the editor/project when safe;
do not bypass validation, drop uncompiled files, force collection or assume
archival fixes full-capture allocation. Performance optimization does not
change the partial-capture contract.

### Beta 15: snapshot-local string sharing

Equal immutable cell strings now share storage within one native capture or
validated loaded baseline. This includes field paths, layer/source labels,
reference IDs, kinds and value text. Cells and their mutable `Value` objects
remain separate. The pool is local to one capture/load, not global interning
or a retained adapter cache. No records are omitted, values truncated, forced
collection introduced or evidence/schema/fingerprint contracts changed.

The same 55,130-record / 1,339,047-cell official-data stress configuration,
in fresh x86 processes over **30 capture/review cycles**, measured:

| Measurement | Beta 14 code | Beta 15 code |
| --- | --- | --- |
| Process lifetime peak working set | 1,662.8 MiB | 896.7 MiB |
| Native capture median; range | 4.15 s; 3.34-5.50 s | 4.44 s; 4.04-4.97 s |
| Unchanged review median; maximum | 2.41 s; 2.79 s | 2.41 s; 3.49 s |
| Create full baseline / resume | 44.95 s / 30.81 s | 45.11 s / 30.88 s |
| Managed / private bytes after final unchanged review | 1,122.7 / 1,162.9 MiB | 367.5 / 404.3 MiB |

Peak working set fell about **46%** in these runs, not to a universal memory
ceiling. Capture's median was about 7% slower in this comparison; this is a
memory improvement, not a capture-speed fix. Managed/private values are
endpoints, not isolated peaks. Natural GC timing affects all these results,
and native/editor/extension allocations and fragmentation still matter.

All repeated captures retained the initial record/cell counts and produced
zero recorded changes with matching fingerprints. A full baseline saved by
the pre-change beta 14 host was separately validated and compacted without
changing its state fingerprint, serialized-state hash or source-file bytes.
616 core checks include exact serialization and distinct mutable ownership.
The complete copied-vanilla integration matrix passed again (269 general,
16 settings, 24 scheduling, 24 export, 95 recovery and 22 sampling checks).
All samples from reruns of the three synthetic history workloads stayed within
the published local timing/128 MiB envelopes, including retained replay.

This accelerated native test is **not a multi-hour interactive FCS Extended
soak**, nor a representative custom dependency/extension stack. The actual-editor
soak below is separate evidence for one configuration; custom-stack coverage
and practical headroom/latency remain unverified. Use Manual
where necessary and keep ordinary mod/notes backups; no automatic Save,
pruning, memory threshold or safe-stack-size promise is added.

### Beta 15: two-hour actual-editor soak

On November 4, 2026, the user exercised **FCS Extended with FCS-Scribe beta 15**
and `Scibe Test Mod`. No additional dependencies or other extensions were
reported. The loaded baseline contains **54,952 records / 1,326,402 cells**
and is 223,524,698 bytes. This is one configuration, not custom-stack coverage.

A read-only external sampler pinned the editor's PID and process start time,
without injecting code, forcing GC or changing the mod, notes or tracking set.
It observed the same process from **18:53:04 to 20:53:08 UTC**, with 1,428
samples over 7,204.15 seconds. The nominal interval was five seconds; the
largest observed gap was 5.09 seconds.

| Memory observation | Start | End | Maximum sampled |
| --- | --- | --- | --- |
| Private bytes | 398.7 MiB | 342.8 MiB | 437.3 MiB |
| Working set | 465.1 MiB | 321.0 MiB | 497.1 MiB |

The OS-reported **process lifetime peak working set was 775.1 MiB**, already
reached during setup before the timed window, and did not rise during it.
Sampled maxima can miss between-sample allocations; neither these counters
nor the lower final endpoint prove leak-free operation or remaining address
space. No per-operation latency was measured in this physical run.

Scribe stayed in **Manual** mode. The durable journal establishes **eight
exact forward/reverse Name-edit cycles during the timed window**: 16 changed
observations, plus two setup observations before the window. All 18 recordings
have unique IDs, are compiled and remain in the original retained session.
Counts come from recorded times and paired values, not retrospective estimates.
The last changed observation was at 20:34:34 UTC, so the sampled window includes
idle periods, not two hours of continuous editing or automated stress.

The user reported no errors or sustained unresponsiveness and successful
midpoint/end compilation. After an explicit normal Save, a cold editor restart
was independently established by the new process identity. The user reported
Resume completing without error/discrepancy, then an unchanged Review and two
compilations with nothing new. The resumed session descends from the current
marker. Independent SHA-256 checks confirmed the original baseline and
compiled journal remained byte-for-byte unchanged, with all 18 unique
recordings retained; a read-only full-baseline validation also passed.

Post-Resume memory belongs to a **separate process epoch**: one endpoint was
607.8 MiB private / 670.9 MiB working set, with a 693.5 MiB lifetime peak.
Those are not extra samples or a new maximum from the two-hour process.
No AI notes application or Workshop upload was part of this test, and it
does not establish native notes retention or full S09 acceptance.

**The observed soak/restart case passed. The owner explicitly accepted S12
for this tested scope and authorized S13, not universal support.**
Additional intended custom dependency/extension stacks, practical headroom
and measured UI latency remain unverified. Raw baselines/native files must
remain private; the figures above are aggregate evidence only.

### Beta 14 integrity and acceptance boundaries

608 core checks passed, including changed/empty/missing fully compiled
sessions, invalidation after a new recording, reopened retained history and
byte-preserving rejection. Each replayed line still has identity/hash/packet
validation; full snapshot fingerprints and local/global chains remain checked.
The extra session-line hash/packet validation during Resume was duplicate work,
not a separate protection. Schema versions and original evidence are unchanged.

The complete copied-vanilla integration runner passed **269 general + 16
settings + 24 scheduling + 24 export + 95 recovery + 22 sampling checks** in
separate hosts. Representative isolated results were **16 + 24 + 24 + 94 + 22**.
A final native profiling smoke test with the official stack and the small
representative active fixture loaded 54,952 records / 1,326,402 cells. Its
single capture took 4.21 seconds; one sample is not sustained-memory evidence.

The representative general suite currently cannot select a numeric-field
record in its fixture (`Sequence contains no matching element`). The same
failure reproduced with the unchanged beta 13 test host; it is not counted as
a passing general run or silently repaired here. Its isolated settings,
scheduling, export, recovery and sampling suites pass separately. The later
beta 15 actual-editor soak covers one configuration only; representative
custom stacks, broader memory/latency headroom and the previously untested
S09 boundaries remain open.

## S13 storage safety (beta 16)

639 core checks passed, including 23 added complete-set assertions on closed
disposable tracking projects. They verify byte-identical copies with baseline
backups and complete storage, compiled/pending replay, original evidence and
fingerprints, and idempotent compilation. A separate closed-copy archival
case retains complete evidence in the main journal after removing a fully
compiled original.

Missing latest/middle pending sessions, missing marker/baseline and foreign
marker/journal cases reject without changing the remaining file bytes.
Restoring exact missing files permits replay. With all originals intact, a
missing main journal reconstructs as pending history and can be compiled.
With required originals archived too, Resume rejects the incomplete chain;
restoring the exact journal permits replay. The source set stays unchanged.
These checks do not claim preservation of empty directories, access times or
the absence of an empty writer-lock file after a failed Open.

Copied official-data and representative-mod recovery runs each passed 98
checks, including shared main-window/chooser notices, default chooser button
visibility and narrow-window warning wrapping. Native recovery behavior
remains covered on disposable copies; no live mod, notes or tracking data
was mutated by these automated checks.

The [manual backup/restore, archive and pre-upload procedures](Installation-and-Workflow#complete-tracking-sets-and-restore-failures)
retain the active-mod-folder default. No schema, identity, replay, hash,
flush or retention rule changes; no automatic pruning or backup/archive
wizard. Baselines include full loaded/dependency data. Scribe does not inspect,
filter or block native Workshop uploads, and no Steam upload was tested.
Older compatible roots cannot prove missing sidecars; coherent old backups
cannot prove newer history exists. Tracking-copy replay under the same mod
identity is not S14 mod relocation or complete mod/notes restoration.
Physical disk/power/access failures and every third-party sync/storage tool
remain outside this bounded evidence.

## S14 portability and divergent history (beta 17)

657 core checks passed, including 18 new assertions. Independently valid
same-project branches remain replayable alone. Unioning their sibling session
files, concatenating a valid alternate journal suffix, or reordering intact
journal lines rejects specifically as divergent/reordered history, preserving
all remaining file bytes. A sibling marker with absent history also rejects.
Mod identity/name, reordered loaded context and changed locked context cannot
rebind old history. A fresh filename creates an independent current-state
baseline with no transplanted recordings; the common source stays unchanged.

Copied official-data and representative-mod recovery suites each passed
**112 checks**. Additional native cases create two compiled reversible
observations, close writers, then move/rename the complete tracking set.
All moved bytes initially match; Resume under the unchanged active-mod path
retains identity, fingerprint and both recordings. Unchanged Review/Compile
adds no history.

The native tests then load a byte-identical same-filename mod copy from a
different directory. Its path identity changes; old-project Resume rejects
before discrepancy approval and leaves the old files unchanged. Explicit New
at a fresh filename captures the relocated native state without importing old
history. Neither saved mod copy nor notes/dirty state is changed by Scribe.
Only disposable fixtures were used; no live data was moved or saved.

This verifies the [fresh-baseline route](Installation-and-Workflow#fresh-baseline-after-moving-a-mod-or-changing-machines),
not transparent relocation/history transplantation. It is a same-host
supported-build test, not physical cross-machine installation, all dependency
versions, path aliases, network shares or distributed locking. An initial
overlong fixture destination exposed the existing .NET Framework path limit;
shorter fixture paths passed. No long-path support or silent path fallback was
added. Equal path strings on another machine do not prove equal data. Native
identity, loaded/locked guards, hashes, replay, schemas and draft approval
remain unchanged; only failure guidance is clearer. S09 full acceptance and
S15/S16 remain open.

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
| Performance and restoration | The beta 11 physical baseline alone was not large-stack/history profiling or proof of mod restoration. Later S12 profiling/soak evidence has an explicitly accepted tested scope; broader configurations and mod restoration remain unverified. |

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
