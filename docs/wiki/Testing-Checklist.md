# Testing checklist

Self-guided acceptance checks for **v0.1.0-beta.25**: history browsing,
Markdown exports, the task table and guided history archive/restore.
The original checks **1-15** are retained; **16-21** cover tasks and **22-32**
cover history storage. Every item starts **Not run**; earlier owner-reported
Pass results and automated checks do not carry forward.
This is a test plan, not an automated or physical acceptance report.

## Preparation

Close FCS before installing the release. Verify the ZIP against its matching
`.sha256` asset and use the supported executable. Back up private mod/native
annotations, Scribe text/settings, complete tracking sets and draft workspaces;
see [installation and backups](Installation-and-Workflow).

Use a disposable test mod, not the original acceptance mod. Choose Manual
recording/compilation for these checks and a separate export folder outside
Kenshi/tracking storage. Create a few reversible record edits and use **Save
Changes** to record them; a restart and confirmed Resume can supply a second
session. Save the native mod only when explicitly preparing that fixture.
Scribe Save Changes is not a native mod save.

For task checks, enable **Mod To Do** and create several disposable tasks,
including two on the same record with different descriptions. Keep an untouched
legacy task-file copy only if one is available; do not downgrade the installed
plugin or replace real tasks to manufacture a fixture.

For archive checks, prepare multiple sessions before testing unsaved-work
boundaries: at least one closed compiled session, the current active session
and, if available, a closed uncompiled session. Session creation/Resume and
explicit Save Changes/Compile are setup actions, not no-save previews.
Keep a complete, verified private backup, including every original log.
Choose a separate private archive folder outside Kenshi and tracking storage;
another drive is optional.

Before a no-save check, record the relevant file hashes and recording identities,
then leave notes/tasks/draft text unsaved. Do not perform intervening explicit
saves or session replacement until comparing the results. Never use the original
acceptance mod, remove a real required log, disconnect a drive, force a power
cut or kill FCS to test recovery. Negative cases use disposable copies only.

## History browser

- [ ] **1. Open and close:** **Core > Browse history** opens from the real FCS
  Extended window without a handle-creation error. Close and reopen it once;
  the parent Scribe window remains usable.
- [ ] **2. Sessions and search:** selecting a session restricts its visible
  entries. Search a known record name/ID, field path or value, including
  different letter case. Clearing filters restores the original order;
  a no-match search shows an explicit empty state and disables exports.
- [ ] **3. Original evidence:** select recordings and inspect timestamps,
  entry/session/batch/baseline identities, typed before/after values, source
  labels and captured context/Workshop warnings. The browser does not invent
  ownership, flatten reversals or treat zero evidence as an all-clear.
- [ ] **4. Pending edits:** make a supported in-memory edit without Save Changes,
  then open history. It appears explicitly as **UNRECORDED**, not as a durable
  recording; browsing does not record, compile or save that edit.
- [ ] **5. Snapshot refresh:** after closing history and making another edit,
  reopening shows the updated catalogue. The browser describes itself as a
  snapshot, not a live event log or saved-mod view.
- [ ] **6. Empty and special entries:** a fresh baseline with no observations
  reports no recordings and disables exports. If available, inspect a legacy
  or context-only entry: its original identity/warnings remain explicit, and
  context-only evidence cannot alone generate record documentation.
- [ ] **7. Complete ranges:** use Ctrl/Shift to select separated entries, hiding
  at least one intervening entry with a search/session filter. Try both
  **Export AI range...** and **Export Markdown range...**. Their first/last
  preview includes every intervening recording, not just the visible selection.
- [ ] **8. Cancellation and no-save browsing:** leave enabled Scribe notes/tasks
  and an AI draft unsaved. Cancel each nested range selector, then close the
  browser. Selection cancellation returns to browsing; text and draft/packet
  association remain intact. No export, recording, compilation, native
  annotation or mod save occurs, and Scribe text files are not implicitly saved.

## Exports

- [ ] **9. AI export and draft protection:** explicitly accept a history range
  and export JSON to a new safe filename. It contains the original ordered
  recordings and any labelled pending evidence. An existing dirty draft gets
  save/discard/cancel protection; cancelling replacement retains that draft
  and its packet. Nothing is sent to an AI service automatically.
- [ ] **10. Markdown facts:** export through history and **Core > Export
  Markdown**. Exact typed values, identities, source labels, reversals and
  warnings agree with the chosen range. Export does not replace the editable
  draft or apply native notes.
- [ ] **11. Reviewed Markdown:** with a valid, complete reviewed draft and its
  original packet, use **Draft > Export reviewed Markdown**. Prose and captured
  facts remain separate, citations resolve to evidence, and literal URL/HTML
  text is not rendered as injected links/content. A disposable missing-evidence
  or stale draft is rejected without native writes or draft replacement.
- [ ] **12. Destinations and explicit-save boundary:** in disposable files only,
  confirm wrong extensions, tracking/reserved targets and unrelated or edited
  Markdown files are rejected. An intact generated Markdown export can be
  replaced. Cancelling the final file picker writes no export, but an explicitly
  accepted export action may already have saved enabled Scribe notes/tasks;
  unlike browsing, it is not a no-save action.

## Related regressions

- [ ] **13. Compilation and Resume:** compile, close/restart using deliberate
  native save/discard decisions, then confirm Resume. Original recordings remain
  ordered and unduplicated across sessions. Resume does not restore gameplay
  or certify that the recorded observations were saved to the mod.
- [ ] **14. Notes and tasks:** enabled notes/tasks still save to separate
  `notes.txt`/`todo.txt` files through Save Changes. **Add Mod To Do** remains
  below native **Add Todo Item** and creates a Scribe task, not a native task.
  Disabling either feature leaves its setting explanation on its own tab.
- [ ] **15. Menus and footer:** history/Markdown actions retain contextual help
  in the shared bottom warnings/help box. Mouse exit/menu close restores normal
  information, while recording/export/compilation status remains separate.
  No stack of top-of-window warnings has returned.

## Task table

- [ ] **16. Visibility and captured metadata:** with Mod To Do enabled, inspect
  **Done, Item name, String ID, Item type, Priority, Created date and
  Description**. Newly added tasks use the selected record's name/ID/type,
  Normal priority and a creation date. These are captured labels, not a promise
  of live record updates. Turn Mod To Do off: the table, search, filters and
  task actions disappear inside that tab. Re-enable it without losing tasks
  or altering the native TODO window.
- [ ] **17. Search and combined filters:** search all fields, then a selected
  field, with different letter case. Combine completion, item type and priority
  filters. A no-match query shows no tasks; clearing filters restores the full
  catalogue. Filtering does not save notes/tasks or reorder `todo.txt`.
- [ ] **18. Sorting and task identity:** sort each relevant heading and reverse
  it. Edit a visible task after filtering/sorting, including one of the two
  tasks linked to the same record. Only that task changes; its description,
  completion and priority do not become attached to another row.
- [ ] **19. Inline edits and persistence:** change Done and priority
  (**Low / Normal / High / Unspecified**) and edit a multiline description.
  Under a filter that excludes the new value, the row disappears only after
  committing the edit. Save deliberately, then reload/restart the disposable
  fixture: values and task identities persist. Native annotations are unchanged.
- [ ] **20. Date controls:** From/To use inclusive local-calendar dates.
  Exercise each bound independently, both together and a reversed range.
  Removing a bound immediately refreshes rows; Clear filters removes both.
  Unknown legacy creation dates are excluded only while a date filter is active.
  Dates describe task creation, not FCS record creation or subsequent edits.
- [ ] **21. Legacy/recovery cases, if available:** on private copied fixtures,
  legacy tasks show unknown type/date and Unspecified priority. Opening or an
  unchanged save preserves their original bytes; a real edit writes format 2
  with an original-byte backup. A malformed/conflicting task file fails visibly,
  disables task mutation and is not overwritten; separate unsaved notes survive.
  Record unavailable legacy/recovery subcases as Not run or Blocked.

## Guided history archive and restore

- [ ] **22. Workspace entry and inventory:** **Workspace > Manage History**
  opens from the real FCS Extended window, with the parent remaining usable
  after closing. Inspect baseline, main-journal, session-original and
  recovery/other sizes against the disposable files. Active, eligible and
  uncompiled/dependent counts agree with the prepared fixture. Resize the window;
  rows, controls, preview and copyable outcome/recovery text remain accessible.
- [ ] **23. Explicit guidance and selection:** initially nothing is checked
  and age/count guides are off. Enable last-activity age and/or keep-newest
  eligible-session guidance. Merely changing those controls does not change
  existing checks; **Select matching sessions** applies both limits. Inspect
  individual checks, no-match selection, clearing and sorting. Selected session
  identities stay attached to the correct originals.
- [ ] **24. Protected originals:** the active session, closed pending logs
  and pending-dependent empty sessions cannot be selected. Age alone never
  makes them eligible, and opening management does not compile them. After
  deliberately compiling and reopening management, only closed originals with
  exact compiled proof become eligible. Record unavailable empty/pending
  subcases separately rather than assuming coverage.
- [ ] **25. Private destination and space preview:** choose an ordinary existing
  folder outside Kenshi/upload and reserved tracking storage. Protected locations
  are rejected. Preview shows selected payload bytes plus manifest-space needs.
  A same-drive destination explicitly does not promise drive-space savings.
  If another drive is available, its preview describes source-drive reclamation.
  Treat byte counts as file lengths, not exact compressed/allocation/free-space
  deltas. Preview and cancelling folder selection create no archive.
- [ ] **26. Confirmation refusal and no-save boundaries:** leave Scribe
  notes/tasks and an associated draft unsaved. Open, select, preview, decline
  Archive's default-No confirmation and close management. No originals, journal,
  marker, baseline, text files, draft association, native annotations or `.mod`
  bytes change. The unsaved text remains available; no AI data is sent.
- [ ] **27. Successful archive:** on the backed-up disposable fixture, select
  closed eligible originals and explicitly confirm. A unique private archive
  contains `manifest.json` and exact session copies with matching identities,
  byte lengths and checksums. Only those redundant source files disappear.
  Main journal, marker, baseline, recovery files, active/pending originals and
  the current head stay unchanged. No old/incomplete archive is overwritten
  or automatically removed.
- [ ] **28. Evidence and continued recording:** browse history after archival.
  Original recordings, order, reversals, packets and IDs remain available once
  each; archival creates no new compilation debt. Record a new deliberate edit
  and Compile: the current session still works, and the new recording is added
  without replacing older evidence or resetting the baseline.
- [ ] **29. Explicit restore and repeated restore:** cancel the manifest picker
  or decline Restore's confirmation: originals remain archived and nothing is
  saved. Explicitly restore the correct archive: missing originals return
  byte-for-byte, without duplicate evidence or replacing the current session/
  draft. Repeat Restore: already-present unchanged files are counted, not
  overwritten. Archive copies and their manifest remain.
- [ ] **30. Cold Resume and later-journal restore:** archive again on the
  disposable fixture, close/restart with deliberate native save/discard choices
  and confirm Resume. All recorded evidence remains available from the retained
  journal. After further recording/compilation, restore the older archive:
  its exact entries are accepted against the extended journal, without
  reverting gameplay, the head or the newer recordings.
- [ ] **31. Cancellation/partial operation, if observable:** if a disposable
  transfer lasts long enough, request Cancel or close management while it runs.
  Cancellation waits for a safe file-operation boundary; some operations may
  already have completed. The outcome identifies the archive/recovery path and
  never claims an all-or-nothing move. Keep remaining originals and archive
  copies, then explicitly restore/retry missing originals. If cancellation
  cannot be exercised, record Not run, not Pass. Do not force device loss,
  process termination, disk exhaustion or power failure.
- [ ] **32. Invalid archives/conflicts, with prepared disposable copies only:**
  a foreign-baseline archive, damaged copied payload, unsafe/duplicate manifest
  entry or differing restore target is rejected without overwriting competing
  bytes. Preserve all copies; do not change IDs/checksums to bypass validation.
  Missing baseline/journal/pending history requires coherent closed-writer
  backup recovery, not a claim that this archive is a complete backup.
  Record each unavailable negative fixture as Not run or Blocked.

## Recording results

Record **Pass**, **Fail**, **Not run** or **Blocked** for each numbered item;
for optional legacy/context-only, reviewed-draft, another-drive or recovery
cases, state what was
unavailable rather than assuming Pass. Include the release, executable,
test configuration and any error text. Record owner-operated observations
separately from independently checked file/hash evidence.

For a check containing multiple cases, name which were actually exercised and
which remain Not run/Blocked; a partially exercised item is not an unqualified
Pass. Checklist updates do not reissue historical release packages or establish
new acceptance.

Use the [test-result issue form](https://github.com/Atlasroar/FCS-Scribe/issues/new?template=05-test-result.yml)
if needed. Do not attach private journals, AI packets, workspaces or shared
native notes. The earlier saved-mod discrepancy and broader support limits
remain open under [Validation and limitations](Validation-and-Limitations);
this checklist does not resolve or overwrite them.
