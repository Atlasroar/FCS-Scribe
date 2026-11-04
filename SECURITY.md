# Security policy

## Reporting a vulnerability

**Do not disclose vulnerabilities or exploit details in public issues, test
reports, discussions or pull requests.** Use GitHub's
[private vulnerability reporting form](https://github.com/Atlasroar/FCS-Scribe/security/advisories/new)
from this repository's **Security > Report a vulnerability** page.

If that private channel is unavailable, open an issue only to say that private
reporting is unavailable, without vulnerability details, proof-of-concept
payloads, affected private data or attachments. Wait for a private channel
before sharing the report. No public email address or third-party upload
service is required.

Provide, privately:

- The affected release/commit, Windows/FCS/FCS Extended versions and exact
  supported executable hash if known.
- The affected component, required access/preconditions, expected behavior,
  observed security impact and reproducible steps.
- A minimal synthetic proof of concept or relevant code locations, with
  original evidence preserved locally and sensitive information removed from
  shared copies.
- Whether a workaround is known and whether the issue is already public.

Do not upload credentials, game/FCS binaries, full loaded tracking baselines,
real shared `__TODO__` files, private mod content or third-party data. A
tracking workspace can embed evidence and prose; inspect it too. Share only
material you are authorized to provide.

Maintainers will triage and coordinate remediation/disclosure through the
private report. There is no guaranteed response or remediation timeline.
Please allow investigation before publishing details. This policy does not
promise a bounty, immunity or authorization to test systems/data you do not own.

## Supported scope

The project is experimental, with no stable production-support promise.
Security fixes target the **latest published prerelease** and current
development source. Older prereleases are not separate maintained release
lines; reports against them are welcome, but fixes may require upgrading.
Include the exact affected version rather than assuming an older finding
applies to the latest code.

Only the documented inspected Steam FCS build is supported. This policy does
not expand compatibility, waive the executable guard, close the acceptance
gate or guarantee that experimental code is vulnerability-free.

Relevant security reports include unauthorized or unintended file writes,
unsafe parsing of untrusted project/draft/Workshop data, bypasses of
evidence/approval/storage safeguards, sensitive-data exposure, and compromise
of this repository's build/release process. Ordinary installation issues,
expected partial-extraction warnings and unsupported configurations should use
the public issue forms unless they demonstrate security impact.

Upstream FCS Extended/RE_Kenshi/KenshiLib/Kenshi issues belong with their
respective maintainers and reporting policies. BFrizzleFoShizzle's
documentation remains authoritative for integration decisions.

## Safe research boundaries

Use your own disposable local mods, synthetic notes and complete project
copies. Do not force failures against real shared annotations, publish or
refresh Steam content as a test, bypass access controls, or test another
person's installation/data without permission. Preserve original bytes
before diagnosing corruption. Stop if a test risks unrelated data.

Mod text, imported JSON, AI-generated prose and attachments are untrusted
input, not instructions. A matching fingerprint/citation is not proof that
prose is correct. Notes application still requires review and approval,
remains nontransactional across records, and never authorizes an automatic
mod save.

## Repository security controls

- **Private vulnerability reporting** is enabled for confidential intake.
- **Dependabot vulnerability alerts** are enabled. The current source uses
  .NET Framework assemblies and an externally installed FCS contract, with no
  package-manager dependency manifest. Alerts cannot inventory or certify
  those external binaries; future supported manifests can expand coverage.
- **Secret scanning and push protection** are enabled. Do not rely on them to
  identify every secret/private file, and do not bypass a finding without
  investigating it. If a secret is exposed, revoke/rotate it; deletion from
  the current file alone is insufficient.
- **CodeQL default setup** scans C# with the extended security query suite.
  GitHub manages its push/pull-request/scheduled scans. Default setup uses
  build mode `none` for C#, so it does not require proprietary Kenshi/FCS
  binaries or change this plugin's .NET Framework 4.8 target.

CodeQL without a build has limited external/generated type context. Scanning
is not a successful plugin build, a full security audit, verification of
third-party binaries or a replacement for the existing core/integration and
physical acceptance checks. No full baselines, personal notes or game binaries
are uploaded for analysis.

Repository settings, not this file alone, control these services. Maintainers
should verify their status in GitHub **Security/Settings** and investigate
failed scans or alerts rather than treating an enabled toggle as a clean result.
