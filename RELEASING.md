# Releasing

The shipped app checks GitHub for a newer release on every launch. A release carries the whole
application *and* the catalogue, so one upload updates the code, the presets, the signatures, the
thumbnails and the offset cache together.

## One-time setup

Set your repository in `ERSwapper/UpdateSettings.cs`:

```csharp
public const string RepositoryOwner = "your-github-username";
public const string RepositoryName  = "ERSwapper";
```

Until that is changed from the placeholder the update check is skipped entirely, so nothing breaks
while you are still working locally.

## Cutting a release

1. **Curate the catalogue**, so that `ERSwapper/Config` holds the `presets.json`, `Signatures/`,
   `Thumbnails/` and `offset_cache.json` you want to ship. Publish into the **source** folder, never
   a `bin` output — the release is packaged from source, so publishing to `bin` silently ships the
   previous catalogue.

2. **Package**:

   ```powershell
   .\Tools\package-release.ps1 -Version 1.1.0
   ```

   This publishes a Release build, strips debug files, verifies the catalogue is not empty and that
   `texconv.exe` came along, then writes `artifacts/ERSwapper-v1.1.0.zip` and prints the item
   count, file counts, size and SHA-256.

3. **Tag and publish** on GitHub:

   - tag: `v1.1.0` (the leading `v` is optional, both parse)
   - attach `ERSwapper-v1.1.0.zip`
   - write release notes: they are shown verbatim in the update prompt
   - do not mark it as a draft or pre-release, both are ignored by the checker

## What the checker looks for

- the **latest** non-draft, non-prerelease release
- a tag that parses as a version and is **greater than** the running one
- an asset ending in `.zip`, preferring one whose name starts with `ERSwapper`

## What the user sees

A prompt with the version numbers, the size and your release notes, and three buttons:

- **Update now** — downloads with a progress bar, unpacks, then closes and reopens on the new
  version
- **Skip this version** — never asks again for that tag
- **Not now** — asks again next launch

The install itself is done by a small generated `.cmd` that waits for the app to exit, copies the
files over with `robocopy`, relaunches, and deletes itself. It never removes files, so anything you
drop from a release stays behind rather than being deleted.

## Content-only releases

Adding items needs no code change. Publish the catalogue, bump the patch version, package, upload.
Users get the new items through the same prompt.

## Versioning

`Tools/package-release.ps1` stamps the version into the assembly, so `-Version` is the single
source of truth. Keep the tag and that argument identical or the app will offer the same update
again after installing it.
