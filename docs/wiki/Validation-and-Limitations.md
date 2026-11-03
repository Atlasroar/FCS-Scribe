# Validation and limitations

The plugin is experimental. The purpose of validation is to establish exact
behavior within a stated scope, not to turn partial extraction into a claim of
complete editor compatibility.

## Automated and disposable-copy evidence

The recorded checks passed:

| Check | Result | Boundary |
| --- | --- | --- |
| Dependency-free core checks | 47 passed | Deterministic changes, immutable baseline, typed values, references, Workshop context, stale drafts, schema compatibility and serialization |
| Actual FCS methods on copied vanilla data | 88 passed | Includes Workshop/cache/preview/MDI/help coverage plus real fields, references, instance tombstones, notes, dirty state and save/reopen |
| Actual FCS methods on a representative copied mod | 87 passed | Same follow-up coverage; no instances in this fixture, so that mutation was explicitly skipped and covered by vanilla |

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
FCS MDI parenting and every action button's hover/focus help are exercised
without running the full interactive message loop.

Original game/mod files are only copied; test saves go to a fresh ignored
`build\integration-fixtures` directory. The Framework test host is explicitly
32-bit like this FCS build. Generated fixtures can be inspected and deleted.

## Reproduce

Run in Windows PowerShell 5.1 from the repository root:

```powershell
.\scripts\Test.ps1 -FcsDirectory '<Kenshi>'
.\scripts\Integration-Test.ps1 -FcsDirectory '<Kenshi>' -FixtureMod '<record-bearing.mod>'
.\scripts\Integration-Test.ps1 -FcsDirectory '<Kenshi>' -FixtureMod '<Kenshi>\data\gamedata.base'
.\scripts\Inspect-Fcs.ps1 -FcsDirectory '<Kenshi>'
```

Builds/inspection do not install the plugin. Source binaries used for local
checks must come from an appropriate installed FCS/FCS Extended distribution.

## Remaining acceptance gate

The real FCS Extended workflow still needs a manual acceptance pass for
extension discovery, toolbar placement, field-focus/control commits, review,
export/import/paste, draft editing/cancellation/approval, stale/duplicate
messages, notes inspection, and normal save/restart.
Also check embedded resize/minimize/restore/close behavior, bottom help layout,
Workshop tab refresh, field agreement with a real Workshop dialog and preview
archive selection/cancellation.

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
- Baselines/draft-export associations do not survive closing the plugin window.
- Capture runs synchronously on the editor thread; large stacks can pause UI.
- Matching evidence IDs does not establish that AI prose is factually correct.
- OCS integration, persistent history, Markdown exports, wiki-ready gameplay
  exports, and direct AI-provider connections are planned, not implemented.
