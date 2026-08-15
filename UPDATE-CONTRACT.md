# The update contract

The code that installs an update ships inside the version being replaced. Someone on v1.0.0 who
updates two years from now runs **v1.0.0's installer** against a zip built much later. That old
installer can never be fixed retroactively, so the shape of a release has to stay something it can
already handle.

Everything below is frozen. Breaking any of it strands every user who has not updated yet.

## The four guarantees

**1. The release asset is a `.zip` whose name starts with `ERSwapper`.**
The updater picks the first `.zip` asset starting with `ERSwapper`, falling back to any `.zip`.

**2. The zip contains a complete, self-contained app folder.**
Either at the root, or inside exactly one top-level folder. Never a patch, never a diff — a full
copy that works on its own.

**3. The executable is named `ERSwapper.exe`, forever.**
The installer looks for that name to confirm the download is valid and to relaunch afterwards. It
cannot be renamed without stranding old versions.

**4. Installing is: lay the files down, then start `ERSwapper.exe`.**
No install-time logic. No conditionals about what changed. Anything clever belongs in the *new*
binary, which runs `ConfigMigrator` at startup — see below.

## How the folders divide

The rule is: **if the release zip can produce it, it ships next to the exe. If only the user's
machine can produce it, it lives in AppData.**

| Next to the exe — `Config\` | In `%LocalAppData%\ERSwapper\` |
|---|---|
| `presets.json` — item catalogue | `settings.json` — paths and preferences |
| `bundles.json` — bundle to signature map | `History\` — swap log and original bytes |
| `unsupported.json` — known-unswappable list | `Thumbnails\` — previews built on this machine |
| `Signatures\*.sig` | `offset_cache.json` — offsets learned here |
| `Thumbnails\*.png` — pre-built previews | `unsupported.json` — recorded on this machine |
| `offset_cache.json` — seed offsets | `layout.json` — schema version |
| `release.json` — the manifest | `Backups\` — full bundle backups, if enabled |

`Config\` is **disposable**. On update it is mirrored, not merged, so a file removed in a new
version actually disappears instead of lingering and winning. Nothing in it is ever written at
runtime, so replacing it wholesale is always safe.

AppData is **never shipped and never wiped**. It survives every update.

Where both sides have a file of the same name — `offset_cache.json`, `unsupported.json` — the
shipped one is a read-only seed and the AppData one is what this machine learned. They are merged
on read, and the shipped copy never overwrites the user's.

## The version gate

Every zip carries `Config\release.json`:

```json
{
  "formatVersion": 1,
  "minimumInstallerVersion": 1,
  "appVersion": "1.0.0",
  "executableName": "ERSwapper.exe",
  "requiredFiles": ["presets.json", "bundles.json"]
}
```

`UpdateInstaller.InstallerVersion` is the highest format this build knows how to install. After
extracting but **before touching the install folder**, the installer compares the two. If the
download needs a newer installer, it stops and tells the user to extract that release by hand — once
they do, they are on a build whose installer understands the new shape and normal updating resumes.

This is what makes snowballing safe. A future release can change the install shape as long as it
raises `minimumInstallerVersion`: old clients decline cleanly instead of half-applying something
they do not understand.

**Raise `minimumInstallerVersion` whenever a release cannot be installed correctly by simply laying
the files down and starting the exe.**

## Migrations live in the new binary

`ConfigMigrator` runs at startup and carries `CurrentSchemaVersion`. AppData records the schema it
was last written under, in `layout.json`. On launch the new build sees the old number and migrates
forward.

Because this runs in the *newer* code, it can always understand every older layout — which is the
opposite of the installer's problem, and why all real work belongs here. A v1 install jumping
straight to v9 runs v9's migrator against a v1 AppData folder and lands in the right place, without
v1 having needed to know anything about v9.

Migrations must be **idempotent** and must tolerate a partly-migrated folder, since an update can be
interrupted.

## Release checklist

1. `Tools\package-release.ps1` — writes `release.json`, publishes, verifies, zips.
2. Confirm the zip has `ERSwapper.exe` and a populated `Config\`.
3. If the install shape changed, raise `minimumInstallerVersion` **and** `InstallerVersion`.
4. If the AppData layout changed, raise `CurrentSchemaVersion` and add the migration step.
5. Tag as `vX.Y.Z`; the tag is the version the updater compares against.
