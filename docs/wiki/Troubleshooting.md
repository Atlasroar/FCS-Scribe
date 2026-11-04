# Troubleshooting

## Installation troubleshooting

Start here if FCS-Scribe is missing, will not open, or fails immediately.
Use disposable mods while diagnosing this experimental plugin.

| Symptom | Check and solution |
|---|---|
| FCS Extended is not installed or its extension selector does not appear | Install and launch [FCS Extended](https://github.com/BFrizzleFoShizzle/FCS_extended) using its upstream instructions. FCS-Scribe does not include or replace FCS Extended. RE_Kenshi and KenshiLib are not substitutes for the managed editor loader. BFrizzleFoShizzle's documentation remains authoritative. |
| FCS-Scribe is absent from the extension selector | Close FCS and check the extracted folder layout below. Copy the release's `FCS-Scribe` folder, not the ZIP or an extra outer release folder, into `<Kenshi>\mods`. Confirm the DLL and manifest are present. |
| The extension is listed but there is no FCS-Scribe toolbar button | Enable it in the extension selector and finish opening the editor. Check the FCS Extended output for initialization errors, especially an unsupported executable hash or a DLL-loading error. Restart FCS after changing plugin files; replacing a DLL does not update an already loaded instance. |
| Clicking the toolbar button reports a writable-mod error | Open a writable active `.mod` in normal editing mode. No active mod, native read-only loading, translation and merge workflows are not supported. Another FCS instance may have caused the mod to open read-only. Do not remove locks or force a second writer. |
| The button exists but the Scribe window seems missing | Scribe is an embedded FCS MDI child, not a separate desktop app. Click the toolbar button to activate it and check inside the FCS workspace for a minimized child. Maximize/restore it; use menu overflow in a narrow window. |
| A release archive is damaged, incomplete, or differs from its checksum | Download the ZIP and its matching `.sha256` asset from the same [release](https://github.com/Atlasroar/FCS-Scribe/releases). Verify before installing. Do not mix DLLs and manifests from different copies or install a source-code ZIP as a built plugin. |
| Windows refuses to load the DLL | Confirm Windows and .NET Framework 4.8 are installed, the release extracted successfully, and FCS Extended is the loader. If Windows marked downloaded files as blocked, verify their source/checksum before using the file Properties unblock option. Inspect security-software/quarantine messages; do not disable protection or run everything as administrator as a blanket fix. |
| It worked before an FCS update, or another store/version fails | Compare the actual executable SHA-256 with the supported hash below. A matching display version is insufficient. Unsupported builds, including unverified GOG builds, need adapter inspection and validation; do not bypass the guard or replace upstream binaries with arbitrary downloads. |
| Exporting or starting tracking immediately fails | Use an existing writable local directory, a separate `.json` filename, and enough free space. A full baseline can be hundreds of MB and atomic replacement can need additional temporary/backup space. Settings may point at a disconnected or deleted custom folder. |
| Notes were applied but are missing from the distributed `.mod` | Record Notes/TODO live in FCS's editor-side `data\__TODO__<user>.txt`, not inside the mod or Workshop change notes. Applying does not save. Inspect FCS Notes and use normal FCS Save deliberately; consult the notes section before retrying. |

Expected minimum release layout:

```text
<Kenshi>\mods\FCS-Scribe\
  FCS-Scribe.dll
  FCS_extended.json
```

The manifest contains `"FCS_Plugins": ["FCS-Scribe.dll"]`. Keep the other
release files as supplied. Do not copy this project's files over
`FCS_extended_plugin.dll`, the FCS executable, or other upstream libraries.

The inspected Steam FCS 2.14.2 executable must have this exact SHA-256:

```text
3688c4d8421d51c2d30c2ff0338874779d91eff150c6a02bf415bc1cdd9c7796
```

Use `Get-FileHash -Algorithm SHA256 -LiteralPath '<actual FCS executable>'`
in PowerShell to check the file you launch. See
[Installation and workflow](Installation-and-Workflow) for the full setup.

## Large stacks and profiling (partial S12, betas 14-15)

Faster compilation does not make capture memory-safe for every 32-bit session.
If Review pauses for seconds or memory approaches the process limit, use Manual,
save the mod and notes normally, preserve the complete tracking set and reopen
when safe. Do not delete uncompiled logs or bypass hash checks. See
[measured workloads, profiling and unresolved memory limits](Validation-and-Limitations#s12-performance-measurements-partial-beta-14).

Beta 15 shares immutable strings within individual captures/loaded baselines.
The copied official-data 30-cycle profile peaked at 896.7 MiB rather than
beta 14's 1,662.8 MiB, but Review still pauses for seconds. This is not a safe
memory threshold or leak-free guarantee. A later
[two-hour actual-editor soak](Validation-and-Limitations#beta-15-two-hour-actual-editor-soak)
passed one test-mod configuration without reported errors or sustained
unresponsiveness; additional dependency/extension stacks remain unverified.

Profiling-only errors: `Entries must divide evenly across Sessions.` requires
positive evenly divisible dimensions; `VanillaStack is restricted to
PerformanceOnly profiling.` requires both flags. `Performance host compilation
failed.`, `Performance run failed; fixture retained at ...` and `Native performance
run failed; fixture retained at ...` are failed measurements, not passing
budgets: inspect the reported compiler/exception output, retain the fixture
privately and fix the input/tool/resource problem before rerunning.

## Finding a message

Use **Ctrl+F** for the first sentence of your error. Values shown as
`<id>`, `<path>`, `<hash>`, `<count>` or `<details>` vary by project.
Tables quote complete messages or their identifying prefixes; longer errors
can include further instructions and an inner exception/stack trace.

- [Warning flags and non-error notices](#warning-flags-and-non-error-notices)
- [Startup and compatibility errors](#startup-and-compatibility-errors)
- [Tracking, storage and recovery errors](#tracking-storage-and-recovery-errors)
- [AI packet and recording-range errors](#ai-packet-and-recording-range-errors)
- [Draft validation and notes errors](#draft-validation-and-notes-errors)
- [Portable workspace errors](#portable-workspace-errors)
- [Settings errors](#settings-errors)
- [Workshop metadata and preview errors](#workshop-metadata-and-preview-errors)
- [Export destination errors](#export-destination-errors)
- [Windows and .NET errors](#windows-and-net-errors)
- [Build and validation script errors](#build-and-validation-script-errors)

## Before attempting a repair

**A tracking project is not a mod backup.** Review, Compile, export and Resume
do not save or restore the `.mod`. Applying approved notes changes editor
memory and marks it dirty; saving remains an explicit FCS action.
For a complete mod/notes/tracking backup, use the
[private backup workflow](Installation-and-Workflow#practical-private-backup-workflow)
with all FCS instances closed. Journal recovery repairs tracking, not lost
unsaved gameplay or notes.

1. Stop retrying an operation that can write. Record the full message and
   which file/action it names. Use Manual settings while diagnosing, when
   available; a failed action can already have paused automatic work.
2. Preserve unsaved draft text with **Draft > Save workspace**. Tracking logs
   do not preserve editable drafts. Preserve important unsaved editor work
   through FCS's normal workflow only after inspecting its state.
3. Before copying/restoring journal files, close Scribe and wait for pending
   operations to finish. Back up the baseline **and its entire matching
   `.json.scribe` folder**, including sessions, marker and recovery backups.
   Never mix files from different copies or histories.
4. Work on a disposable copy for corruption/recovery tests. Do not delete
   `writer.lock` to break an active lease, manually truncate journal lines,
   edit hashes/IDs to make a file pass validation, or delete backups to force
   initialization. The presence of a lock file alone does not mean it is held.
5. After resolving the cause, Resume and validate the matching project.
   A faulted journal must be reopened; saving Settings alone cannot repair it.

There is no promised automatic rollback for multi-record note application or
for every I/O/power failure. Preserve evidence before taking corrective action.

## Warning flags and non-error notices

### Extraction and evidence warnings

These are coverage/interpretation warnings, not instructions to reinstall.
Most cannot be "cleared" without changing the supported extraction model.

| Message or flag | Meaning and solution |
|---|---|
| `PRIVATE TRACKING SET:` | Beta 16's persistent main-window/chooser notice. Keep the baseline and complete matching `.json.scribe` storage/backups together, with writers closed before copying or archiving. Baselines include full loaded/dependency data: never send them to AI. Keep tracking, backups and draft workspaces out of Workshop content. Scribe does not inspect or block native uploads. Follow the [complete-set failure matrix](Installation-and-Workflow#complete-tracking-sets-and-restore-failures), [compiled-session archival](Installation-and-Workflow#safely-archiving-compiled-session-originals) and [pre-upload safeguards](Installation-and-Workflow#before-a-workshop-upload). |
| `PARTIAL EXTRACTION - review all warnings.` | The catalogue is intentionally incomplete. Inspect every warning and before/after value; do not describe it as a complete record of editor activity. |
| `PARTIAL EXTRACTION - inspect names, values and all warnings before sharing.` | Recording-range preview uses the same partial model. Check selected recordings before sending a compact packet to an external assistant. |
| `PARTIAL EXTRACTION - baseline captured; review all warnings.` (beta 13) / `PARTIAL EXTRACTION - baseline captured with Workshop availability warnings.` (earlier) | Baseline creation succeeded, not complete extraction. Beta 13 always displays capture warnings, even without optional Workshop failures. Inspect any component issues and repair only the affected local metadata/image source. |
| `PARTIAL CAPTURE - recordings sample committed model state, not editor events.` | The accompanying notice explains A-to-B-to-A omissions, zero evidence and pending/omitted data. Finish edits normally, inspect evidence and record intermediate states explicitly when needed; no recording interval guarantees a complete event log. See the [capture matrix](Data-and-AI-Contracts#supported-and-omitted-state-beta-13). |
| `Experimental in-memory extraction, not a complete catalogue of all FCS editor state.` | Only the inspected in-memory model is captured. Retain the caveat in documentation; external assets and separate editing buffers require independent review. |
| `Scope: "In-memory, partial extraction"` | Machine-readable scope label for the same limitation. It does not claim full saved-file or editor-buffer coverage. |
| `Base and locked field-level source mod provenance is unresolved; load order is reported as editor context.` | Load order does not prove which dependency supplied a particular field. Do not infer field ownership or remove the warning because the mod list looks correct. |
| `Earlier edits made before Start tracking are outside this baseline.` | New tracking starts from what the editor contains now. To document earlier edits, use an existing matching project/history or establish a deliberate before/after test; a new baseline cannot recover them. |
| `Uncommitted UI controls may not yet be in the model. Finish editing the field before reviewing changes.` | A text box or separate editor may not have committed its value. Finish the edit using the normal control workflow, change focus and review again. One working focus route does not establish all controls are safe. |
| `Names, scalar fields, layered references and instances are captured; external files, layouts, dialogue editor buffers and metadata change history are outside this catalogue. Workshop metadata is current context, not a list of new edits.` | Review excluded assets/buffers separately. Do not treat captured Workshop context as a historical list of gameplay edits. |
| `Missing/invalid record state: <id>.` | FCS reported a missing/invalid state for that record. Inspect the record and dependency setup in FCS; retain the warning and do not invent missing values. If unexpected, preserve a minimal reproducible fixture. |
| `Unresolved reference <id> from <record/path>.` / `UNRESOLVED` | The referenced ID was absent or deleted in the captured catalogue. Check the reference and required mods in FCS. Do not silently substitute a similarly named record or claim the dependency is resolved. |
| `base:unresolved`, `locked:unresolved` | Field source labels acknowledge unresolved base/locked provenance. They are not file paths or errors to fix by editing JSON. |
| `WARNING:` / `RECORDING WARNING:` | The former labels current context warnings; the latter preserves warnings from selected earlier recordings. Current recovery does not erase historical uncertainty. Review both. |
| `RECORDING RANGE - chronological observations, not a net diff. Current-state validation is from export time.` | Repeated edits/reversals remain separate observations. Use qualified outer change IDs and the outer response template; do not call an earlier recorded value the current value. |
| Recording kind `pending` | The selected observation is current, **unrecorded** editor state. Exporting it does not make it durable history. Use Review when you intend to record it. |
| Stored/active and effective entries describe the same edit | They describe different layers, not necessarily two user actions. Cite all required evidence but avoid double-counting the gameplay change. |

### Workshop warning flags

| Exact warning or identifying prefix | Meaning and solution |
|---|---|
| `Workshop values are local/staged or cached, not a fresh Steam query. No Save, Refresh or Upload was invoked.` | Compare with FCS's local dialog/cache, not assumed live Steam truth. Scribe does not contact Steam or publish for you. |
| `Dependencies are FCS's current required mods; ReferencedMods are the additional parenthesized entries shown by FCS.` | Keep the two lists distinct. Check dependency declarations in FCS rather than merging or reinterpreting the lists. |
| `ChangeNotes is the dialog's current upload draft, not published Workshop changelog history.` | Review staged prose before any separate deliberate publishing workflow. It is not proof of what Steam already displays and is unrelated to record Notes/TODO. |
| `Open the active mod's Steam Workshop window to capture staged title/tags/visibility and its change-notes draft.` | Open the matching active mod's native Workshop dialog, then recapture. Opening it does not require Upload or Refresh. |
| `ChangeNotes is unavailable without a matching Workshop dialog; FCS does not reload it from the .info cache.` | A cache alone cannot recover that draft. Open the correct dialog and inspect its current text; unknown is not the same as an empty published changelog. |
| `No saved Workshop .info cache exists; Workshop-only fields are unknown.` | Expected for a new/unconfigured local mod. Use the native dialog for staged values; do not fabricate cache fields. |
| `No saved Workshop cache directory exists; Workshop-only fields are unknown.` | The local cache location is absent. Check the selected mod/location; leave unavailable fields unknown. |
| `Unrecognized Workshop visibility code: <code>.` | FCS's value does not map to the inspected visibility choices. Do not relabel it Public/Private by guessing; inspect the native dialog and report the code/build. |
| `No assigned Steam Workshop ID is known; zero is FCS's unpublished/default ID.` | Expected for unpublished/default local metadata. Do not treat `0` as a valid published item or publish solely to remove this warning. |
| `No last-update timestamp is known. Null does not mean the mod has never been updated.` | No usable cached timestamp was available. Leave it unknown rather than substituting file modification time. |
| `LastUpdate preserves FCS's cached round-trip date/time and timezone kind; it is not the local file modification time.` | Preserve the timestamp/kind as reported; do not reinterpret it as a fresh query or local `.info` write time. |
| `No local Workshop preview image exists; no image was downloaded from Steam.` | Use FCS's local image-selection control if you want a preview. Scribe will not download one. |
| `PARTIAL Workshop capture; unavailable values are not validated. Review warnings.` / `CaptureStatus: "partial"` | At least one optional component could not be captured. Read every `Issues` entry and repair the named source. Ordinary gameplay capture may continue; unavailable bytes are not fingerprinted. |
| `Workshop <component> unavailable [<code>]: <message>` | Structured component failure, also repeated in capture warnings. Use the code table below and the accompanying inner error; do not treat unavailable values as empty/default/verified. |
| Preview `Status: "missing"` | No local preview file. Expected absence, not image corruption. |
| Preview `Status: "unavailable"` / `UnavailableReason` | A busy operation or read/decode problem prevented capture. Remedy the named reason before archiving; stale/unknown preview bytes are not a safe fallback. |
| Preview `Status: "available"` | Local bytes were read and identified. This does not verify a published Steam image; compare its hash with your archive. |

`CaptureStatus` and `Issues` are optional; an absent partial flag is not an
independent guarantee that every Workshop field is known. Recovery from partial
availability can change the fingerprint and make an older AI draft stale.

| `Issues.Code` | Component / explanation | Solution |
|---|---|---|
| `upload-in-progress` | `dialog`: `Workshop upload is in progress; staged Workshop values and preview were not captured.` | Let the user-started operation finish, then recapture. Scribe does not start, cancel or complete uploads. |
| `refresh-in-progress` | `dialog`: `Workshop refresh is in progress; staged Workshop values and preview were not captured.` | Wait for the native operation to finish. Do not start a refresh merely to troubleshoot Scribe. |
| `invalid-data` | `cache` or `preview`: content/decoding validation failed. | Preserve the offending source and inspect the specific cache/image error below. Repair via the owning editor/image workflow, then retry. |
| `unsupported` | `cache` or `preview`: unsupported size, format or operation. | Use the supported local format/limits; never disable validation. |
| `access-denied` | `Access to the Workshop <component> was denied. Check permissions and retry.` | Restore appropriate read permission/unblock access to the named local source. Do not grant broad administrator access as a default remedy. |
| `read-failed` | `The Workshop <component> could not be read; it may be locked or changing. Check the FCS-Scribe log and retry.` | Wait for the owning operation to finish, inspect FCS Extended output and retry. Preserve repeated failures for a report. |

### State, scheduling and confirmation notices

| Message or flag | Meaning and solution |
|---|---|
| `SETTINGS RECOVERY - automatic recording disabled.` | Invalid/unreadable settings triggered safe Manual mode and active-mod export defaults. Follow [Settings errors](#settings-errors); merely opening Scribe does not overwrite the original. Manual project/draft/Workshop work remains available. |
| `Settings could not be loaded. Original file retained; safe manual recovery mode remains active.` | Reload still failed. Restore access or choose valid settings and explicitly repair only when original bytes can be preserved. |
| `Action failed; automatic review paused. The baseline advances only after a successful checkpoint save.` | Read the associated error before resuming automation. For journal faults, close/reopen and validate the project first; do not simply re-enable the timer. |
| `Automatic review paused after an error; resume or save settings to re-enable.` | Resolve the underlying cause first. Valid settings Save/Reload or successful Resume can re-enable eligible automatic work; these do not repair damaged history. |
| `Workshop action failed; gameplay tracking was not changed.` | A Workshop-only action failed without introducing a gameplay pause. Any earlier pause remains; solve the Workshop error separately. |
| `Pending compilation: <count> recordings; oldest <UTC>; due ...` | Durable entries are not yet copied into the main journal. They remain in session logs. Use Compile journal in Manual mode or wait for an eligible automatic tick; do not delete the sessions. |
| `due now/overdue` | Thirty minutes have elapsed since the oldest pending recording, including time closed or Manual. It is not a promise to run while blocked/minimized. |
| `Settings recovery: automatic compilation held; use Compile journal.` | Automatic work is gated by settings recovery. Manual compilation remains explicit; repairing settings is a separate operation. |
| `Manual mode: use Compile journal; debt is retained.` | Expected in Manual. Review/Save/Resume do not automatically compile pending entries. |
| `Automatic work paused; debt is retained. Compile manually or resolve the error.` | A prior error paused automatic work. Resolve it before writes; retained debt is not lost history. |
| `Automatic compilation waits for the next eligible visible, unblocked tick.` | Restore Scribe and leave menus/modal dialogs closed. Automatic work also waits for busy actions, a minimized FCS parent, valid settings and an open project. |
| `Timestamp ahead of local UTC: treated as due now.` | A recording timestamp is ahead of the current clock. Debt becomes due rather than waiting indefinitely. Check system time; do not edit recorded timestamps. |
| `Local UTC moved behind the recording; the existing countdown is retained.` | The wall clock moved backward after scheduling. The existing monotonic countdown is retained; check the clock without rewriting history. |
| `Tracking is NOT a saved-mod backup.` (beta 11 and earlier) / `Tracking records captured editor state, including UNSAVED edits.` (beta 12) | Journals catalogue captured state, not recoverable `.mod`/asset backups. Beta 12 keeps the boundary notice visible even when operation/help text changes. Back up the actual mod and separate notes files. |
| `Last recorded UTC: <UTC>. Mod/notes save state is NOT verified.` | This is the last observed tracking state, not proof of normal FCS Save. A recording can be ahead of or behind the actual saved mod. Verify/save through FCS explicitly if intended. |
| `Confirm resumed state` / `The editor differs from the last recorded state (<count> evidence entries; context-only differences can have zero).` | Resume validated history but the editor fingerprint differs. **No** declines this session; **Yes** keeps current editor state and lets the next manual or eligible automatic Review record differences, including reversals of discarded edits. Neither choice restores or saves mod/notes. Older versions use the shorter count-only message. |
| `This repairs tracking history only; it cannot recover unsaved mod/notes edits or save those files.` | The interrupted-tail prompt is about tracking storage only. Preserve/repair the tracking log as instructed; recover actual mod/notes files only from an independent matching backup. |
| `Convert legacy export` / `This AI export requires explicit baseline conversion.` | Only original schema-1/2 packets support conversion. Cancel preserves the packet; approval preserves exact `.legacy.bak` bytes and starts from the current editor. A packet cannot reconstruct its old baseline. |
| `Recover interrupted journal` / `An interrupted final journal line requires explicit recovery: <path>` | Back up the project first. Cancel leaves bytes untouched. Approve only the intended copy/project: Scribe preserves original bytes in `.scribe\recovery\<id>.bak`, then removes the incomplete tail. Complete invalid entries must not be skipped. |
| `Unsaved draft workspace` / `* unsaved draft workspace` | Save preserves the current exact draft and original packet; No discards this window's unsaved work; Cancel keeps working. A workspace does not save the mod. |
| `An operation is still running. Draft work is retained; close again after it completes.` | Close was deferred during a busy operation. Wait and close again; do not terminate FCS to force the window shut. |
| `Draft changed during the operation. Replacement cancelled; the latest text and original packet were retained.` | A late edit invalidated asynchronous replacement. Review the retained text, save it and retry intentionally. |
| `The saved snapshot does not include edits made during saving; the latest text remains unsaved.` | The workspace saved the earlier snapshot, not the later edit. Save again to preserve the latest draft. |
| `Pasted as unassociated text; it cannot be applied without an AI packet.` | Text can be kept as a text-only workspace, but applying requires the matching exported packet. Export/Open workspace first when preparing applicable notes. |
| `Text-only workspace: no associated AI packet. It cannot be applied; export a packet to document changes.` | Successful text restoration, not an applicable AI draft. Do not invent a packet identity. |
| `Draft is unfinished or invalid JSON: <details>` | Workspace text was restored faithfully, including unfinished JSON. Fix the text; workspace saving is allowed before a response is valid. |
| `Draft cannot be applied: <details>` | Restoration succeeded but validation failed. Follow the specific draft/stale/duplicate remedy; restoring is not approval. |
| `<count> pending notes pass evidence/current-state checks. Review the prose; approval is still required.` | Validation is not factual review or authorization. Inspect prose and explicitly approve only intended appends. |
| `Approve notes` / multi-record application is not transactional | No cancels application. Yes appends pending approved notes, skips matching completed blocks, marks FCS dirty and does not save. Inspect Notes before normal FCS Save. |

## Startup and compatibility errors

| Message / exception | Cause and solution |
|---|---|
| `Unsupported FCS executable (SHA-256 <hash>)` | Only the inspected Steam executable is supported. Use the supported installation or request inspection of the exact build; do not patch out the hash guard. |
| `Open a writable active mod before using FCS-Scribe.` | No writable active mod, or FCS opened it read-only. Open a normal writable test `.mod`; close competing editor instances normally. This is an intentional readiness guard. |
| `Only normal .mod editing is supported, not translation or merge mode.` | The active file/mode is unsupported. Return to normal mod editing without forcing the current operation through Scribe. |
| `Only normal mod editing is supported; merge, translation and other file modes are not.` | FCS is not in the supported `USER` mode. Complete/exit that workflow normally and reopen a supported session. |
| `The editor data session changed. Reopen FCS-Scribe and start tracking again.` | FCS replaced its data model after the window attached. Preserve the draft, reopen Scribe and select an appropriate matching project or new baseline. |
| `FCS access must run on its UI thread.` | Integration/threading contract failure, not a JSON problem. Stop the action and report the stack/build/other extensions; do not retry from a script or background thread. |
| `The FCS main window is not an MDI container.` | The expected host UI contract is absent. Check the supported build/loader and report; do not change window internals to force attachment. |
| `FCS object is not initialized while reading <name>.` / `FCS object is not initialized while calling <name>.` | The editor object graph is not ready or differs from the inspected contract. Wait for normal editor load, reopen Scribe, then report recurring errors with the stack. |
| `MissingFieldException`, `MissingMemberException`, `TypeLoadException` / `Expected one compatible FCS method: <name>` | A reflected API/member/type differs or is missing. Confirm exact supported binaries and extension versions. This needs adapter investigation, not removing validation or editing mod files. |
| `TargetInvocationException` | A reflected FCS call threw. Read the inner exception and relevant notes/storage remedy; include both layers in a report. |
| `Nested instance depth exceeds supported extraction at <record/path>` | Nesting exceeded the extraction limit of 8. Capture stopped rather than truncating silently. Preserve a minimal fixture and request support; do not describe the incomplete capture as complete. |
| `Unsupported FCS value type <type>` | A value has no verified conversion. Extraction stops instead of omitting it. Report the exact type/path/build; do not stringify/edit data to hide the failure. |

## Tracking, storage and recovery errors

For integrity failures, close Scribe and preserve the whole project. Restore a
known matching backup set, not selected lines/files from divergent histories.
If recovery is impossible, a separate new baseline can document **future**
edits only; it does not repair or recover missing earlier evidence.

| Message / identifying prefix | Cause and solution |
|---|---|
| `Select Start tracking first.` | Review/Compile/AI export has no active project. Use Start tracking > New or Resume. Closing/reopening Scribe alone does not resume a project. Workshop export is separate and needs no baseline. |
| `Begin a session before recording.` | Internal project lifecycle was not initialized. Reopen/Resume through the UI; report if a normal action reaches this error. |
| `The active mod or dependency/load-order context changed. Start tracking again.` | Current mod/path, load order or locked context differs. Beta 17 adds: `Restore the original mod path and loaded/locked context, or choose New tracking JSON with a new filename. Existing history is not relocated or merged.` Restore the original context to Resume, or follow the [fresh-baseline workflow](Installation-and-Workflow#fresh-baseline-after-moving-a-mod-or-changing-machines). The tracking JSON path alone is not the active-mod identity. |
| `Record type changed for stable ID <id>. Start tracking again.` | A supposedly stable ID now has another type. Verify loaded data/context; preserve the evidence and use a separate baseline only after understanding the mismatch. |
| `Inherited/locked data changed for <id>` | Changes cannot safely be attributed to active-mod edits. Check dependency/load-order changes and use a matching context or a deliberately new baseline; do not relabel inherited evidence as active. |
| `The JSON belongs to a different active mod or project path. Nothing was overwritten.` | Active-mod identity includes its path, not just its display name. Load the original mod at its matching location or start a separate project. Moving tracking files does not authorize changing mod identity. |
| `The tracking JSON destination is a directory. Choose a file path.` | The chosen target is a directory. Select a separate `.json` file inside an existing directory. |
| `The tracking JSON already exists. Use Start tracking > Resume existing JSON.` | New cannot overwrite an existing baseline. Use Resume, including after an initialization that committed the baseline before failing. |
| `This JSON already exists. Resume it or choose a new filename.` | A supposedly new target appeared or already exists. Preserve it; select Resume or a truly new filename. |
| `Choose a .json file; mod and game files are never tracking targets.` | Wrong extension. Pick a separate `.json`; never use a `.mod`, game executable or assets as storage. |
| `The JSON is empty.` | Input deserialized as no document. Restore the correct nonempty project/packet; do not create an empty JSON object as a repair. |
| `Unknown tracking document format.` | This is another format or unsupported data. Select the intended tracking baseline, not a workspace, Workshop export or arbitrary JSON. |
| `The tracking document has an unsupported format or incomplete checkpoint.` | Required schema/identity/state/timestamps are missing or invalid. Restore a valid matching baseline; do not fabricate required fields. |
| `The saved snapshot does not match its checkpoint fingerprint.` | The baseline's state no longer matches its saved hash. Preserve it and restore a verified matching backup; do not recalculate hashes to legitimize modified content. |
| `The tracking history has a broken checkpoint chain.` | Embedded checkpoint sequence is inconsistent. Restore matching complete data; manually reordering entries is not supported. |
| `The last validated entry does not match the saved snapshot.` | Embedded history and final state disagree. Restore the matching project version. |
| `The review does not belong to this checkpoint.` | Review evidence belongs to another baseline/current state. Reopen and validate the intended project; report recurring normal-UI failures. |
| `An active mod identity is required.` | Captured/loaded state lacks the mod identity. Open the correct supported writable mod; restore an intact project if the error comes from saved data. Report a live-capture reproduction. |
| `Snapshot collections must not be missing.` | Required snapshot lists are absent. Restore the complete valid document, or report a live-capture contract failure; do not insert empty lists to hide lost state. |
| `Missing or duplicate stable record ID.` | Captured/loaded records have missing or repeated identities. Inspect the selected data/context and restore valid originals; do not invent replacement IDs. |
| `Missing value or duplicate field path in <id>` | Record cells lack required typed values/paths or repeat a layer/path. Restore intact stored data or report the affected live record/build; do not drop duplicate cells to force validation. |
| `Session storage exists without its tracking baseline.` | For a recognized uncommitted New attempt, retry New at the same filename. Otherwise restore its original baseline or use a new filename; do not delete unknown storage. |
| `The project's session storage is missing or incomplete. Restore its matching .json.scribe folder, including project.json.` | A modern baseline requires its matching marker/storage. Restore the complete coherent set with writers closed; an empty folder/lock or foreign marker is not restored history. See the [failure matrix](Installation-and-Workflow#complete-tracking-sets-and-restore-failures). |
| `The project's session storage is missing. Restore its .json.scribe folder.` (through beta 15) | Earlier wording for the same missing-marker condition. Restore the complete matching folder, including `project.json`, not just the baseline. |
| `The session storage marker belongs to another project or is invalid.` | Marker identity/schema/head is wrong. Restore the marker and history from the same backup set. |
| `A session depends on missing history. Restore its required session files.` | A session's parent cannot be found. Restore required logs/main journal; do not rename a later session to make it look like the parent. |
| `Required recorded history is missing. Restore uncompiled sessions or the main journal.` | The marker's durable head is unavailable or mismatched. Restore all required matching history. |
| `The session has a broken local entry chain.` | A session's entry/session/previous-state sequence is inconsistent. Restore the intact original session, not edited lines. |
| `A session changed outside Scribe; reopen and validate the project.` | Compilation detected external session edits. Stop competing writers/sync tools, preserve both versions and reopen for validation before continuing. |
| `The JSON changed or disappeared outside Scribe. Automatic review is paused; reload it before saving.` | An observed storage/workspace/settings file changed externally. Preserve competing versions and reload the appropriate surface. For projects, close/reopen and validate the whole set. |
| `Duplicate or conflicting journal entry.` | Duplicate compiled IDs or conflicting copies were detected. Restore one coherent history; do not concatenate independently modified journals. |
| `The project contains divergent or reordered history.` | Previous-entry/fingerprint ordering does not match. Beta 17 adds: `Restore one coherent branch; automatic journal merging is not supported.` Preserve both complete branches; do not union session folders or concatenate intact journal suffixes, even if Git reports no text conflict. Follow [divergent-branch recovery](Installation-and-Workflow#divergent-branches-and-version-control); use a separate new baseline if the gameplay state was independently reconciled. |
| `The replayed delta does not match its saved checkpoint.` | Replayed state/context differs from recorded evidence. Treat as corruption or inconsistent files; restore a verified matching backup. |
| `Invalid or incompatible session header.` | Session format, identity, parent or timestamp is invalid. Restore the matching original session. |
| `Unsupported journal entry.` | A complete line is not a supported journal-entry object. Scribe rejects it; incomplete-tail recovery must not be used to skip complete corruption. |
| `Invalid journal entry identity, time or hash.` | Record identifiers, UTC time, delta or content hash failed validation. Restore intact originals; do not edit hashes/times. |
| `Journal evidence belongs to another baseline.` | Entry packet and previous checkpoint disagree. Restore the correct journal, not evidence from another project. |
| `Empty journal line.` | A complete blank line is invalid history. Preserve it and restore a known-good copy; do not use general-purpose text editors to normalize journals. |
| `Incomplete session delta.` | A stored context/update/removal structure is incomplete. Restore the intact entry/project. |
| `The session delta changes project or loaded/locked context.` | Replay would cross unsupported project/context boundaries. Restore the correct data set. |
| `Invalid or duplicate removed record in session delta.` | Removal IDs repeat, are invalid, or do not refer to existing state. Restore intact history. |
| `Invalid or duplicate updated record in session delta.` | Updated records/IDs repeat or are invalid. Restore intact history. |
| `Recovery is restricted to this project's journal files.` | A recovery request targets another location/type. Stop and report; do not move arbitrary files into the recovery path to bypass the restriction. |
| `The interrupted log changed before recovery.` | The file changed after the recovery prompt was generated. Cancel, stop external writers and reopen to validate the current bytes before approving again. |
| `A journal write failed. Reopen the project and validate its logs before recording again.` | This instance is faulted after a write/compile failure. Resolve disk/access issues, preserve the project, then close/reopen and validate. Re-enabling settings alone is insufficient. |

### Initialization and legacy-backup safeguards

| Message / identifying prefix | Cause and solution |
|---|---|
| `The interrupted initialization does not match this baseline.` | Pending marker, baseline bytes or identity disagree. Restore matching files; do not approve/rewrite unrelated state. |
| `Unfinished initialization belongs to a different source.` | Retry is using a different legacy source. Reload the original JSON or select a separate new filename. |
| `Existing session storage is not a recognized unfinished initialization.` | New cannot safely claim that storage. Restore its original baseline or choose a new filename; do not delete storage to silence the check. |
| `Existing session history cannot be claimed by project initialization.` | Sessions/journal/recovery files already exist. Resume the matching project or choose a separate location. |
| `An unrelated baseline/legacy backup already exists.` | The destination's backups are not owned by this attempt. Preserve them and choose a different copy/filename or restore their matching project. |
| `The preserved legacy backup is missing or changed.` | Conversion's original packet backup is unavailable or altered. Restore its exact bytes before resuming. |
| `The session marker backup path is a directory.` | A directory occupies the expected backup filename. Choose another project filename or investigate the existing contents; do not recursively delete it. |
| `A session marker backup exists without its marker.` | A backup cannot be claimed as a new project. Restore the matching project or choose a separate filename. |
| `The existing session marker backup belongs to another source.` | Marker backup source identity differs. Preserve it and choose a matching original or separate filename. |
| `A different legacy backup already exists; choose a different tracking file.` | Existing `.legacy.bak` is not this packet. Preserve both; convert a separate copy. |
| `The legacy export changed while it was being preserved.` | Concurrent mutation invalidated conversion's original-byte preservation. Stop competing edits, preserve versions and retry from a verified separate copy. |

## AI packet and recording-range errors

Do not repair evidence by editing batch IDs, fingerprints, original packets or
change IDs. Re-export from a validated project or restore the original packet.
Only the **outer** schema-3 response template is the response contract.

| Message | Cause and solution |
|---|---|
| `No recorded or pending changes to document.` | No observations are available. Make/commit an intended edit and Review, or Resume the project containing the desired earlier recordings. An unchanged baseline has no new evidence. |
| `Choose a valid inclusive recording range.` | First/last selection is out of bounds or reversed. Choose an available chronological first through last recording. |
| `This range contains only context updates, not record-change evidence. Choose a range containing record changes.` | Workshop/context-only entries contain no record evidence for notes. Include an appropriate gameplay recording instead. |
| `The editor changed while selecting the range. Export again to review the current recordings and pending edits.` | Current state changed between capture and destination selection. Retry export and inspect the fresh selection; the prior packet cannot silently be rebound. |
| `The exported AI packet is incomplete or unsupported.` | Missing required packet context/evidence/template or unsupported schema. Restore/re-export the original packet with the matching plugin. |
| `The packet contains incomplete or duplicate change evidence.` | Evidence fields/IDs are missing or duplicated. Re-export; do not hand-edit immutable evidence. |
| `The packet's batch or response-template evidence is inconsistent.` | Batch hash, template identity or citations do not match. Restore the original packet; put response prose in the response draft, not the evidence packet. |
| `The response template does not cover each change exactly once.` | Packet template itself is inconsistent. Restore/re-export, rather than deleting changes to match it. |
| `Recording ranges require AI packet schema 3.` | A schema-1/2 packet contains range-only fields. Use an unmodified original or current range export. |
| `The recording range has no valid project identity.` | Range project identity is missing/invalid. Re-export from the validated tracking project. |
| `The range does not match its original recording evidence.` | Copied changes/order/pending identity no longer matches original packets. Restore the unmodified range export. |
| `The range has no original recordings.` | Range archive is empty/missing. Re-export the selected recordings. |
| `Invalid, duplicate or unsupported original recording.` | Invalid identity/kind/schema/session/UTC time or duplicates in a range. Restore/re-export; do not manually combine packet internals. |
| `The recording belongs to another mod or project path.` | An original packet has another active-mod identity. Select the correct project; do not combine separate mod paths as one range. |
| `A recording-range AI export is not a tracking baseline. Resume the original tracking JSON with its .scribe folder.` | A schema-3 handoff cannot be resumed/legacy-converted. Choose the actual project baseline plus storage, or Open its saved draft workspace for draft work. |

## Draft validation and notes errors

| Message / identifying prefix | Cause and solution |
|---|---|
| `Export an AI packet or Open workspace with its original packet first.` | Import has no associated evidence. Export the intended packet or restore the matching workspace first. |
| `Export an AI packet or Open workspace with its original packet before applying notes.` | Text alone is not applicable evidence. Restore its actual packet; do not insert guessed IDs. |
| `The response draft is empty.` | Import produced no draft object, for example JSON `null`. Use a real outer response object. |
| `The draft does not match the exported batch.` | Wrong `BatchId`/`SourceFingerprint` or no draft object. Use the response template from the associated export, not another batch or a nested archival template. |
| `The draft is stale: tracked data changed after export. Review and export again.` | Captured gameplay or known Workshop/context/availability changed. Preserve prose in a workspace, review/re-export and reconcile it against new evidence. Do not copy a fresh fingerprint into old prose to bypass validation. |
| `The draft contains no notes.` | `Entries` is absent/empty. Complete the template with factual notes covering all exported evidence. |
| `Each note needs a unique surviving TargetRecordId. Combine notes for the same target.` | Target is absent/deleted/blank or duplicated. Choose a surviving record, including for deleted-record documentation, and combine entries per target while preserving all citations. |
| `Note text must be nonempty and cannot contain NUL.` | Empty/whitespace text or a NUL character is invalid. Write factual nonempty text and remove the invalid character. |
| `Each note must cite valid change IDs from this batch.` | A note has no citations or cites IDs outside this export. Use exact outer IDs; schema-3 citations include `EntryId:original-change-ID`. |
| `Change ID <id> is cited more than once. Cover every exported change exactly once.` | A citation repeats within/across notes. Remove only duplicate citations, keeping complete coverage. |
| `The draft must cover every exported change exactly once. Missing change IDs: <ids>.` | Some exported evidence was omitted. Add all missing IDs to appropriate factual notes; do not shorten the exported packet to evade coverage. |
| `This documentation batch is already fully present. No notes were appended.` | Duplicate prevention succeeded. Inspect existing notes instead of reapplying or deleting markers. |
| `The batch marker on <id> does not identify one matching completed note.` | Existing marker/text/citations are modified, ambiguous or duplicated. Inspect FCS Notes against the **original complete draft** before any retry; do not delete markers just to force another append. |
| `FCS has not initialized the active notes file. Open FCS Notes, then try again.` | Native notes storage is not ready. Open FCS's Notes facility, then retry validation/approval. |
| `Record no longer exists: <id>` | A target disappeared. Review current state and choose a surviving target through a fresh valid draft. |
| `Notes application stopped after <count> newly completed record(s).` | Nontransactional application may have appended some notes in memory. Inspect before saving/retrying. Retain the original **complete** draft; matching completed blocks are skipped on retry. Do not submit a reduced remainder draft or assume nothing changed. Scribe saved nothing automatically. |
| Notes vanish after restart or normal Save fails | Check the native notes save error, correct `data\__TODO__<user>.txt`, permissions and any second editor. Preserve in-memory notes/draft before closing. Scribe cannot make FCS's normal save transactional or infer success from Apply. Do not externally rewrite a notes file while FCS owns it. |

Malformed draft syntax is reported by the .NET JSON reader, so exact wording
varies. Remove Markdown fences/commentary, use valid JSON escaping/commas and
the expected object/array shapes. Preserve `BatchId`, `SourceFingerprint`,
targets and citations from the correct template; review prose independently.

## Portable workspace errors

| Message | Cause and solution |
|---|---|
| `This is not a supported draft workspace.` | Wrong format/schema or missing exact draft text. Open a workspace, not a response/AI packet/Workshop JSON. Save to a new name rather than overwriting unrelated JSON. |
| `The workspace's associated AI packet is missing.` | A packet hash exists without its packet. Restore the intact bundle; do not associate an arbitrary replacement packet. |
| `The workspace's original AI packet changed or is incomplete.` | Embedded evidence does not match its hash. Restore a known intact workspace or reconstruct a new workspace from the actual original export and preserved draft. |
| `The draft workspace is empty.` | No workspace object. Select the nonempty saved file or a verified matching backup. |

A valid bundle can contain invalid/stale/text-only draft text: see
[state notices](#state-scheduling-and-confirmation-notices).
Save workspace protects both its target and `.bak` from tracking-storage
replacement. It only replaces an existing valid workspace, not arbitrary
otherwise-valid JSON.

## Settings errors

Settings live at `%LOCALAPPDATA%\FCS-Scribe\settings.json`. A genuinely absent
settings file uses Manual/active-mod defaults. An existing invalid or unreadable
file instead enters explicit recovery; these are different cases.

**Safe recovery:** Cancel writes nothing. Restore access/a valid original and
choose **Discard edits and reload**, or choose valid values and explicitly
**Save repaired settings**. Readable originals receive a unique
`settings.json.recovery-<UTC>_<id>.bak` before replacement. Keep those backups.
Reload discards only unsaved settings-dialog choices, not the project/draft.

| Message | Cause and solution |
|---|---|
| `Plugin settings are empty.` | Existing settings deserialize as no object. Reload a valid original or explicitly repair after its bytes can be preserved. |
| `Unsupported settings schema. Expected schema 1; reload or explicitly save repaired settings.` | Settings version/required data is not supported. Use a matching supported configuration or explicit repair; do not silently relabel a future schema. |
| `Choose Manual, 30 seconds, 1 minute, 5 minutes or 10 minutes.` | Serialized interval is unsupported. Choose one supported interval in Settings. |
| `Choose a review interval.` | The dialog has no selection. Select a supported interval before Save. |
| `The custom export directory must be an absolute path.` | A relative or drive-relative path was supplied. Choose an existing absolute folder such as `C:\Exports`, or active-mod defaults. |
| `The custom export directory is invalid.` | Path syntax contains invalid characters/components. Use the actual existing directory path. |
| `The custom export directory is unsupported.` | Windows/.NET cannot interpret the path form. Use a standard absolute filesystem folder. |
| `The custom export directory is too long.` | Framework/Windows path limit exceeded. Use a shorter local folder; do not change global policies as a first remedy. |
| `The custom export directory is missing or unavailable: <path>` | Saved/custom directory no longer exists or is inaccessible. Reconnect/restore access, Reload, or explicitly repair to an existing folder/active-mod defaults. |
| `Choose an existing custom export directory.` | Empty or nonexistent custom-folder selection. Select an existing folder or re-enable active-mod defaults. |
| `The export directory does not exist: <path>` | A dialog's configured folder disappeared since validation. Restore it or choose valid settings before retrying. |
| `The original settings could not be read safely. Restore access and Reload from disk before saving a repair.` | No observed source bytes/hash are available, so safe preservation is impossible. Restore access, Reload and only then Save repaired settings. |
| `Settings could not be locked for saving. Another instance may be saving; reload and retry after it finishes.` | Shared settings lease is held. Let the other save finish/close its owner normally, then Reload before retrying. Do not delete `.lock` to force a write. |
| `Settings changed while preserving the original. Reload before saving; the settings were not overwritten.` | External change raced the repair backup. Preserve both versions, stop competing edits, Reload and choose settings again. |
| `The settings loader returned neither settings nor a recovery error.` | Internal invariant failure. Report the stack/version; manually resetting files cannot establish that the loader is correct. |

Settings can also report the generic external-change, JSON-reader, access,
sharing and disk errors elsewhere on this page. A valid Save/Reload does not
move existing tracking files or repair a faulted journal.

## Workshop metadata and preview errors

Optional cache/preview failures are usually represented as partial Workshop
capture while gameplay recording continues. Strict **Archive preview image**
still refuses unavailable bytes. A Workshop-only action error does not create
a gameplay pause or clear an earlier pause.

| Message / identifying prefix | Cause and solution |
|---|---|
| `The Workshop .info cache has an unsupported root element.` | Cache XML is not the expected `ModData` root/namespace. Preserve it, verify the correct active mod/cache and repair through an appropriate native workflow; do not substitute unrelated XML. |
| `The Workshop .info cache is empty or identifies a different mod.` | Empty/mismatched cache. Check the selected mod and cache provenance; leave unknown values unknown until corrected. |
| `The Workshop .info cache XML is invalid or prohibited.` | Invalid XML, prohibited DTD/external constructs, or the document limit was exceeded. Preserve the cache and regenerate/repair appropriate local data; do not enable XML external entities to load it. |
| `The Workshop .info cache XML or field values are invalid.` | XML field parsing failed, for example an invalid numeric/date value. Preserve the source and inspect the named inner error; do not guess a Steam ID or timestamp. |
| `No local Workshop preview exists. Set an image in FCS's Steam Workshop window first.` | No image is available to archive. Select a supported local image through the native dialog; no download occurs. |
| `The Workshop preview exceeds FCS's supported 1 MB image size.` | More than **1,048,576 bytes**. Prepare a supported image at or below the limit and deliberately select it in FCS. Renaming does not reduce bytes. |
| `The Workshop preview could not be read completely.` | Source changed/truncated during read. Let the image write finish and retry; inspect repeated failures. |
| `The Workshop preview is not a supported PNG, JPEG or GIF image.` | Decoded format is unsupported. Convert a separate source image properly; changing the extension is not conversion. |
| `The Workshop preview image could not be decoded.` | Invalid/corrupt image bytes. Test the original in an image tool and deliberately select a valid replacement in FCS. Preserve evidence before replacing source data. |
| `The Workshop preview image decoder failed.` | Image library failed to decode. Preserve the source, try a supported re-exported image and report repeated failures with format/size. |
| `The preview changed while choosing an archive location. Review it and try again.` | Source hash changed during file selection. Recapture and inspect the latest preview, then retry a new archive filename. |
| `Workshop upload is in progress; staged Workshop values and preview were not captured. Wait before archiving.` | User-started upload is active. Wait for it to finish; do not archive stale bytes. |
| `Workshop refresh is in progress; staged Workshop values and preview were not captured. Wait before archiving.` | Native refresh is active. Wait and recapture; Scribe does not invoke it. |
| `Choose an archive location outside the Kenshi/FCS installation.` | Archive target is inside the installation. Choose a new file in a separate backup/export directory outside Kenshi. |
| `Archive the preview with its original .<format> image extension.` | Target extension does not match detected bytes. Use `.png`, `.gif`, or `.jpg`/`.jpeg` as appropriate, even if the native source is `.img`. |
| Archive destination already exists / `IOException` | Archives are **new-file-only**, with no overwrite. Choose another filename; do not delete a valuable archive just to repeat the operation. |

## Export destination errors

Keep compact exports, workspaces and preview archives separate from the
baseline and `.json.scribe` storage. Guards recognize inactive anchors,
adjacent storage, recognizable standalone tracking formats and backups too.
A native Windows overwrite prompt is **not** export success: read Scribe's
subsequent status/error and verify the actual filename.

| Message | Cause and solution |
|---|---|
| `Exports and draft workspaces cannot replace tracking anchors, backups or reserved .json.scribe storage.` | A target or workspace backup would damage recognized/reserved storage. Choose a separate filename **outside** the storage folder. Do not rename/delete project data to evade the guard. |
| `Choose a standard file path, not an alternate data stream.` | NTFS stream-style path was supplied. Use an ordinary file path. |
| `Choose a file path without trailing dots or spaces in its components.` | Ambiguous Windows path components could resolve unexpectedly. Choose a clean filename/folder without those endings. |
| `The existing destination is not a JSON object. Choose a new export filename.` | Existing content is an array/scalar/other value, not safely replaceable object JSON. Preserve it and choose a new filename. |
| `The existing destination cannot be safely classified as JSON.` | Parsing failed; the target may be damaged tracking data. Do not overwrite or "repair" it for an export. Preserve it and select a new filename. |
| `The existing destination cannot be safely classified as a single JSON object.` | Empty, incomplete, invalidly encoded or multiple-object content cannot be safely classified. Use a new filename; one seemingly valid first object is not enough. |
| `Workshop export requires a .json filename.` | Use a separate `.json` metadata target. |
| `Export requires a .json filename; mod files are never export targets.` | AI packet export needs `.json`, never `.mod` or other game files. |

Valid existing AI/Workshop object exports remain replaceable after normal
confirmation and observed-hash checks. Workspace replacement is stricter:
existing content must be a valid workspace. Access/read failures are not
treated as proof that a destination is empty/new.

## Windows and .NET errors

These can originate from the framework, FCS, filesystem or reflected calls.
Their exact text is OS/runtime/localization dependent; there is no finite
plugin-defined list of every possible external exception. Use the named path,
inner exception and operation to distinguish them from the explicit messages
above.

| Exception / symptom | Safe response |
|---|---|
| `FileNotFoundException`, `DirectoryNotFoundException` | Confirm the exact selected path and file extension, including hidden `.txt` suffixes, the correct drive and complete copied project. Missing optional Workshop sources have explicit unknown/missing states instead. |
| `UnauthorizedAccessException`, `SecurityException`, access denied | Restore appropriate access to the specific file/folder or use a writable export directory. Check read-only/security controls; do not disable security globally or run everything elevated. |
| `IOException`: file in use/sharing violation | Close the owning Scribe instance normally and let busy work finish. An active project holds `writer.lock` exclusively and keeps its baseline open; copying may fail while it is open. A second native FCS can instead load the mod read-only and be rejected before project locking. Do not kill unrelated processes or delete locks. |
| `IOException`: disk full, device unavailable, network/sync failure, replacement failure | Stop writes, restore space/device availability and preserve existing/temporary/backup files. Reopen and validate after a failed journal write. Use a stable local test directory; atomic replacement is not a universal filesystem/power-loss guarantee. |
| `SerializationException`, `XmlException`, invalid JSON/member/type errors | Select the correct document type and use valid JSON. For response drafts, fix syntax against the original template; for immutable project/evidence files, restore originals rather than editing identities/hashes. |
| `ArgumentException`, `NotSupportedException`, `PathTooLongException` from paths | Use a shorter ordinary absolute Windows path and correct extension. Check for invalid characters, unsupported device/stream syntax and inaccessible roots. |
| `BadImageFormatException`, `FileLoadException`, assembly/type load failures | Verify the correct built plugin and upstream loader/runtime files, supported editor and complete installation. Do not copy random framework/FCS DLLs into the plugin folder. Preserve full loader output. |
| `OutOfMemoryException` / very slow capture or validation | FCS is a 32-bit process; loaded stacks and full baselines can be large. Wait for busy validation rather than repeatedly launching operations. Preserve work normally, reduce a disposable reproduction's stack and report record count/size/operation. Do not claim streaming removes all memory limits. |
| `ObjectDisposedException` naming `JournalProject` | An operation used a closed project. Reopen/Resume normally; report if ordinary UI actions reproduce it. |
| `ArgumentOutOfRangeException` naming `intervalSeconds` / `Compilation scheduling requires UTC timestamps.` | Internal scheduling inputs are invalid. Use supported Settings choices; do not edit recorded times. Report a normal-UI reproduction with clock/settings details. |
| `ArgumentException: Recordings are required.` | Internal range dialog opened without available recordings. Reopen the intended project; report if the UI offers a range with no sources. |
| `ArgumentNullException` naming `value` during settings Save | Internal caller supplied no settings object. Report the UI sequence/stack; do not fabricate files to mask it. |

## Build and validation script errors

These apply to source contributors, not normal release installation.
Use [Building and releases](Building-and-Releases) for command arguments.
The detailed compiler/test output immediately before the final error is the
diagnostic evidence; the final wrapper line alone is not the root cause.

| Message / identifying prefix | Solution |
|---|---|
| `FCS Extended's plugin contract was not found at <path>. Install FCS Extended first.` | Install upstream FCS Extended and pass its actual Kenshi directory to `-FcsDirectory`. Do not replace its contract with this project's DLL. |
| `Run this script in Windows PowerShell 5.1 with .NET Framework 4.8 installed.` | Use the supported Framework host/compiler, not a .NET SDK assumption or unsupported shell/runtime. |
| `Plugin compilation failed.` | Read the compiler errors, correct the source/environment, then rebuild. A failed build is not installed. |
| `Test compilation failed.` | Resolve the preceding core-test compiler errors; do not interpret this as a test pass/fail result. |
| `Core tests failed.` | Inspect the preceding assertion/exception and fix the regression before packaging. |
| `Choose at most one targeted integration suite.` | Use only one of `-SettingsOnly`, `-ScheduleOnly`, `-ExportsOnly`, or omit all for the default combined run. |
| `Integration test compilation failed.` | Inspect compiler output and verify the supported FCS/contract paths. |
| `Integration tests failed; disposable fixture retained at <path>` | Inspect the retained copy and full output. Do not run mutation fixtures on the original mod or delete evidence before diagnosis. |
| `Settings recovery tests failed; disposable fixture retained at <path>` | Inspect the settings suite's assertions/output in the retained fixture before changing recovery behavior. |
| `Scheduling tests failed; disposable fixture retained at <path>` | Inspect the scheduling assertion/time/debt state; retain independent recording/compilation semantics. |
| `Export protection tests failed; disposable fixture retained at <path>` | Preserve targets/fixture and investigate byte-preservation/classification failure; do not disable guards to pass the suite. |
| `Tracking recovery boundary tests failed; disposable fixture retained at <path>` | Inspect the retained disposable fixture and failed assertion. The suite checks discarded/saved-state discrepancies, Resume decisions, no silent save/restoration and complete byte-identical backups; never run destructive diagnostics against live notes/mods. |
| `Could not inspect source checkout.` | Run packaging in a valid Git checkout with Git available; inspect the preceding Git error. |
| `Release packaging requires a clean, committed checkout.` | Review and commit intended source changes first; preserve unrelated work. Do not reset/delete changes simply to make packaging run. |
| `Could not determine source commit.` | Resolve the Git checkout/HEAD error before producing a release with unverified provenance. |
| `Invalid release version in plugin metadata: <version>` | Correct intended assembly informational-version metadata to the package script's supported form, commit and package again. |

## Reporting an unresolved problem

Open a [focused issue form](https://github.com/Atlasroar/FCS-Scribe/issues/new/choose):
installation/UI, tracking/storage, drafts/notes, Workshop, test results, or a
feature/documentation request. Each has an optional upload field for reviewed
screenshots, recordings and minimal diagnostic examples. Public upload size
and file-type limits are controlled by GitHub; do not work around them by
posting private full projects elsewhere.

For suspected security vulnerabilities, use
[private reporting](https://github.com/Atlasroar/FCS-Scribe/security/advisories/new),
not a public issue or attachment. See the
[security policy](https://github.com/Atlasroar/FCS-Scribe/blob/main/SECURITY.md).

Include the plugin/release version, FCS executable hash, FCS Extended version,
other enabled extensions, normal/read-only/merge/translation mode, action
sequence, exact selected filename and full error including inner exceptions.
For I/O failures, include disk/access/competing-instance information. Capture
relevant FCS Extended output: this plugin writes `FCS-Scribe:` errors,
`FCS-Scribe settings recovery:` and `FCS-Scribe Workshop <component> [<code>]:`
diagnostics to standard error; a dedicated Scribe log filename is not promised.

**Inspect/redact before sharing.** Full baselines and storage can contain the
loaded dependency model and private mod content; notes files can contain
unrelated annotations. Prefer a minimal disposable reproduction and the
relevant error excerpt. Do not upload entire real projects/notes by default.
Preserve unmodified originals locally: editing a copy to redact content can
invalidate its hashes, so a synthetic minimal reproduction is preferable for
testing integrity/recovery behavior. Record manual edits and test mistakes
explicitly rather than attributing every changed note/file to the plugin.

## Catalogue scope and maintenance

This catalogue covers the explicit runtime errors, warning strings, structured
Workshop issue codes, recovery/approval notices and script failure messages in
**v0.1.0-beta.11**. It is a troubleshooting reference, **not a claim that every
failure path has passed physical acceptance**. A bounded real-editor pass is
documented, but the full S09 acceptance gate remains open; see
[Validation and limitations](Validation-and-Limitations).

Message definitions and guards are maintained in
[`src`](https://github.com/Atlasroar/FCS-Scribe/tree/main/src):
`Plugin.cs`, `FcsAdapter.cs`, `Core.cs`, `Tracking.cs`, `Journal.cs`,
`RangeExport.cs`, `DraftWorkspace.cs`, `SettingsStorage.cs`,
`SettingsDialog.cs`, `ExportStorage.cs` and `WorkshopArchive.cs`.
Contributor commands live in
[`scripts`](https://github.com/Atlasroar/FCS-Scribe/tree/main/scripts).
Update this page when adding/changing a message, flag or recovery policy;
never recommend bypassing a safety check to clear its warning.
