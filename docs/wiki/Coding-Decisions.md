# Coding decisions

These are project decisions, not claims that FCS exposes a stable supported
editing SDK. See [References and knowledge credits](References-and-Knowledge-Credits)
for authoritative sources and the distinction between documentation and local
verification.

## Integrate with FCS Extended, not a replacement editor

The user wants to edit normally in FCS. FCS Extended supplies a real managed
plugin entry point and already owns editor startup. A second desktop editor
would duplicate that workflow and would not inherently see unsaved FCS state.

## Guard and isolate reflection

The upstream entry point supplies an assembly, not documented contracts for
records, notes, or saves. The adapter therefore uses only inspected members
of one exact executable hash. Unknown versions fail explicitly rather than
trying similar member names. Reflection stays out of the reusable core.

All editor operations stay on the WinForms thread. No background read mutates
or traverses live editor collections concurrently, and no hook library is
introduced as a speculative workaround.

## Use native MDI hosting and separate contextual help

FCS's inspected main form is a standard WinForms MDI container, and its own
Workshop and notes forms use that parent. Scribe uses the same mechanism
instead of a second owned top-level window. Button hover and keyboard-focus
help use a dedicated bottom label so descriptions never erase action results.

## Capture Workshop context without invoking publishing behavior

The active header, matching `ModInfo.info`, and staged change-notes textbox
have different lifecycles. A stale dialog from another mod must not supply
metadata. Without a matching dialog, read the known fields of the local
`.info` XML cache with DTDs/external resolution disabled, surfacing malformed
data rather than using FCS's silent `ModData.Load` fallback.

FCS's UI refresh rewrites dependency declarations, and its upload handler saves
the mod. Neither is an acceptable extraction API. Read the verified dependency
derivation methods directly instead. Cache provenance, unknown values and
unpublished IDs remain explicit; cached values do not prove published state.

Archive the local `.img` preview as its actual PNG/JPEG/GIF encoding, preserving
the original bytes and SHA-256. Respect FCS's 1 MB bound in the 32-bit process.
Use a new destination outside the installation, never a mod or existing archive.
This is not a Steam image downloader or a generic external-file tracker.

## Make the baseline explicit and immutable

Comparison means "changed since Start tracking," not "different from vanilla."
The baseline is copied so later caller/editor mutation cannot rewrite history.
Starting a new tracking file establishes it; beta 2 checkpoint review
advances it only after the reviewed state is saved successfully. Applying notes
or ordinary FCS saving does not. Earlier edits are explicitly out of scope.

Legacy AI packets cannot restore a baseline because their context omits loaded
records. Conversion preserves their original bytes/evidence and explicitly
starts a fresh current-state checkpoint. New tracking documents stream full
captured state to disk rather than building giant JSON strings in the 32-bit
process. They are private local state, not an AI/Workshop distribution format.

Background workers operate only on detached snapshots. Live capture stays on
the editor thread; a busy gate prevents timer/user action overlap. A full
baseline is written once; timestamped session logs append changed records and
context instead of repeatedly rewriting that baseline/history. Flush success
and a small atomic head marker precede baseline adoption. The journal tracker
shares ownership of detached snapshots rather than duplicating them.

Compilation is distinct from recording: automatic mode consolidates every
30 minutes on a separate monotonic deadline; manual mode uses an explicit
action. Stable IDs/hashes make retrying compilation idempotent. Complete
session originals remain under user control. An incomplete final line requires
approval and a preserved recovery copy; complete corruption is never skipped.
An exclusive lease coordinates Scribe writers, with hashes for observed
external changes. These are not signatures or a universal filesystem lock.

Stable IDs define identity; renaming a display name must not create a fictitious
deletion/addition. Changing an actual stable ID remains a different identity.

## Preserve typed and layered evidence

Values retain type information and invariant/round-trip numeric text. Missing,
null, empty, zero, false, inherited data, and removal markers are not silently
collapsed. Paths escape structural delimiters.

Stored active overrides and effective values can be two views of the same
edit. Reference objects moved into FCS's removal collection retain one stable
path. Instance presence/tombstones are explicit. Unsupported scalar types fail
instead of becoming an untrustworthy textual approximation.

Unresolved field-level dependency provenance is labeled unresolved, not
reconstructed from an owner's name or guessed from the final merged value.

## Use deterministic changes and streaming fingerprints

Records and cells are compared in ordinal order. Fingerprints use a
length-framed stream and SHA-256 rather than building one enormous joined
string: actual vanilla-data testing exposed memory pressure in the 32-bit
editor. Fingerprints detect stale captured state; they are not signatures or
a claim to cover unextracted editor subsystems.

Documentation batch identity follows the baseline plus change catalogue.
Save-time dependency-header normalization must not silently create a fresh
baseline or make identical documentation look like a different change batch.

## Keep AI external and untrusted by default

Manual JSON handoff avoids provider/account/network prerequisites. Packet
instructions constrain drafts to evidence IDs, exact quantities, and visible
uncertainty. Mod text is data, never trusted instructions. Response validation
does not prove prose correctness; human review and approval remain mandatory.

## Append through FCS and leave Save to the user

FCS's verified Notes/TODO method preserves active and inherited text. The
existing-note append branch needs an explicit dirty flag. Existing TODO
categories are preserved rather than repurposed.

There is no custom binary writer, silent save, or attempt to embed the sidecar
format into a `.mod`. The notes interface has no verified transaction contract;
multi-target errors report possible partial in-memory application.
Beta 4 validates full, exactly-once evidence coverage before mutation. Safe
retry does not mean rollback: keep the complete original draft, skip only
targets with one matching completed block, and append the remaining targets.
The existing append format is shared between writing and retry comparison;
conflicting blocks fail explicitly rather than being treated as completed.

## Separate future saved-data analysis

OCS is useful complementary research, not a substitute for live-model access.
Its current .NET 9 target does not directly fit this Framework 4.8 plugin.
An optional process boundary is the proposed direction, pending the
[design checklist](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/plan.md).
No OCS dependency or writer ships in the initial plugin.
