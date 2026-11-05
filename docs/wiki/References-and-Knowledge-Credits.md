# References and knowledge credits

This page distinguishes upstream knowledge, local verification, project work,
and possible future dependencies. A citation is not a claim of endorsement or
direct contribution to FCS-Scribe.

## Authoritative integration sources

At the project owner's direction, **BFrizzleFoShizzle's documentation is
authoritative for integration decisions and must not be overridden without
explicit instruction**.

| Source | Knowledge used | Inspected reference |
| --- | --- | --- |
| [FCS Extended](https://github.com/BFrizzleFoShizzle/FCS_extended) | Managed plugin contract, extension discovery, assembly loading, initialization order, and existing framework conventions | [`8a53a855e45f946f47780387c615c734dc3b0b50`](https://github.com/BFrizzleFoShizzle/FCS_extended/tree/8a53a855e45f946f47780387c615c734dc3b0b50), especially `IPlugin.cs` and `Program.cs` |
| [RE_Kenshi](https://github.com/BFrizzleFoShizzle/RE_Kenshi) | Native game-runtime extension context, upstream installation/development guidance, and the recommendation to use prebuilt dependencies where applicable | [README at `c8dfbc9621b9263863722852f819c4c0f440475a`](https://github.com/BFrizzleFoShizzle/RE_Kenshi/blob/c8dfbc9621b9263863722852f819c4c0f440475a/README.md) |
| [KenshiLib](https://github.com/BFrizzleFoShizzle/KenshiLib) | Native plugin-development requirements, built-in hooking guidance, and UI-thread safety practices | [README at `1ce4a163739bbdf17254259039d7aa74fcc11cc0`](https://github.com/BFrizzleFoShizzle/KenshiLib/blob/1ce4a163739bbdf17254259039d7aa74fcc11cc0/README.md) |

Credit belongs to BFrizzleFoShizzle and the respective upstream contributors
for those projects and their documented integration knowledge. Native game
interfaces are not represented here as managed FCS editor APIs. FCS-Scribe
does not introduce a hook/detour system.

## Supplementary saved-data research

Credit to **lmaydev and OpenConstructionSet contributors** for
[OpenConstructionSet](https://github.com/lmaydev/OpenConstructionSet), its typed
mod-data SDK, readers/context model, and documentation.

Research used the [README](https://github.com/lmaydev/OpenConstructionSet/blob/c7ce7a752328a31687627fed65609fe8585c091c/README.md),
[context builder](https://github.com/lmaydev/OpenConstructionSet/blob/c7ce7a752328a31687627fed65609fe8585c091c/OpenConstructionSet/ContextBuilder.cs),
[record comparison model](https://github.com/lmaydev/OpenConstructionSet/blob/c7ce7a752328a31687627fed65609fe8585c091c/OpenConstructionSet/Mods/ModItem.cs),
and [framework/package metadata](https://github.com/lmaydev/OpenConstructionSet/blob/c7ce7a752328a31687627fed65609fe8585c091c/OpenConstructionSet/OpenConstructionSet.csproj).
Its usefulness is planned and bounded as described in
[OpenConstructionSet integration](OpenConstructionSet-Integration).

OCS is currently a research reference, **not bundled code or an implemented
dependency**. Its own README credits SCARaw for assistance with OCS; that is
upstream attribution, not a claim that SCARaw contributed to FCS-Scribe.

## Kenshi and local verification

Credit to **Lo-Fi Games** for Kenshi and its Forgotten Construction Set.
Kenshi/FCS binaries remain proprietary and are not included in this repository
or its release package.

The particular record/notes/save behaviors documented here were established
by read-only metadata/method inspection of the locally installed FCS build,
then tests against disposable copies. They are **project verification
findings**, not invented official API guarantees or statements attributed to
upstream authors. See the
[feasibility report](https://github.com/Atlasroar/FCS-Scribe/blob/main/docs/feasibility.md)
for the exact build fingerprint and limitations.

## FCS-Scribe work and licensing

The project is maintained under [Atlasroar/FCS-Scribe](https://github.com/Atlasroar/FCS-Scribe).
Implementation and documentation were developed with AI assistance from
GitHub Copilot; maintainers remain responsible for review, testing, and
release decisions.

FCS-Scribe is distributed under [GPL-3.0](https://github.com/Atlasroar/FCS-Scribe/blob/main/LICENSE).
FCS Extended's inspected repository declares GPL-3.0; OCS declares MIT.
These are separate upstream projects with their own notices and dependencies.
OCS integration must review the selected package's applicable notices before
distribution. No upstream/game binaries are bundled in FCS-Scribe releases.
