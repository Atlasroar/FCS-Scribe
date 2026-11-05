# Testing checklist

Self-guided acceptance checks for **v0.1.0-beta.23**, including the new history
browser, beta 22 Markdown exports and closely related regressions. Every item
starts **Not run**; beta 21's owner-reported Pass results do not carry forward.
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

## Recording results

Record **Pass**, **Fail**, **Not run** or **Blocked** for each numbered item;
for optional legacy/context-only or reviewed-draft cases, state what was
unavailable rather than assuming Pass. Include the release, executable,
test configuration and any error text. Record owner-operated observations
separately from independently checked file/hash evidence.

Use the [test-result issue form](https://github.com/Atlasroar/FCS-Scribe/issues/new?template=05-test-result.yml)
if needed. Do not attach private journals, AI packets, workspaces or shared
native notes. The earlier saved-mod discrepancy and broader support limits
remain open under [Validation and limitations](Validation-and-Limitations);
this checklist does not resolve or overwrite them.
