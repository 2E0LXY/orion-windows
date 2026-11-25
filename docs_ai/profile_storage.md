# Profile Storage: Per-Profile Databases, Common Registry, Process-per-Profile

State as of 2026-09. Companion to the storage-layout section in `CLAUDE.md`.

## Layout

```
%LOCALAPPDATA%\com.kagi.orion\data\
  orion.db                     Common (cross-profile) DB — OrionBackend-owned
  Profiles\<id>\profile.db     Per-profile core-module DB (WAL)
  webkit\Profiles\<uuid>\      Engine site data (uuid derives from
  blink\<uuid>\                profileId+engineId, see utils.cpp). Each blink
                               dir is a per-profile CEF user data dir
                               (root_cache_path == cache_path; site data in
                               Default\). Per-profile roots because CEF holds
                               a process-singleton lock keyed on
                               root_cache_path — a shared root would block
                               parallel profile processes
  profiles.db.pre-split.bak    The legacy single DB, kept after the split
```

## Process model

**One profile per process.** `Orion.exe --profile=<id>` (no argument = the
registry's default profile). Each process:

1. `Program.Main`: after the CEF-subprocess early-return, resolves the
   profile via `ResolveStartupProfile` (runs the one-time legacy split /
   registry seeding, so the answer is authoritative even on first launch),
   then acquires the per-profile single-instance gate
   (`Services/SingleInstance.cs`: named mutex
   `Local\Orion.com.kagi.orion.Profile.<id>` + named pipe). A duplicate
   launch forwards "open" to the owner and exits; the owner opens a new
   window.
2. `App.OnLaunched`: `InitOrionCore(profileId)` opens
   `Profiles\<id>\profile.db`; the engine comes from the profile's registry
   row (`--engine=` overrides), so **profiles genuinely run different
   engines** — in different processes.
3. Opening another profile (`MainWindow.OpenForProfile`) spawns
   `Orion.exe --profile=<other>`.

The core module still believes it manages "the profiles DB" — it just gets
an explicit path (`CoreApi::init(profiles_db_path)`, the single ~5-line core
touch). Inside each profile.db there are exactly two profile rows: core's
hidden internal profile (id 1) and the real profile carrying its **global
registry id**. Registry ids therefore never collide with 1
(`CommonDb::kMinProfileId = 2`).

## Common DB (orion.db)

Owner: `OrionBackend/common_db.cpp` (raw sqlite3, UI thread, synchronous).
Schema v2 (v1 → v2 adds `deleted_at`, migrated in `apply_schema`):

- `profiles(id, name, icon, engine, seq_num, is_default, show_in_address_bar,
  show_badge_in_app_icon, keyboard_shortcut, deleted_at)` — the profile
  registry. **The sole authority** for which profiles exist, their engines,
  and the default (the per-profile DBs' own `is_default` flags diverge after
  the split and must not be consulted). Deleted profiles keep their row with
  `deleted_at != 0` forever: `get_profiles()` filters them out, but
  `create_profile`'s `MAX(id)+1` sees them, so **profile ids are never
  reused** (a reused id would resurrect any leftover data dirs, whose names
  derive deterministically from the id).
- `app_settings(key, value, type)` — app-wide settings shared by all
  profiles (e.g. `settings::general::app_opens_with`), seeded from the
  legacy `common_settings` table. Exports: `GetAppSetting`/`SetAppSetting`.
  Per-profile settings stay in each profile.db (`Get/SetSetting` exports).
- `meta(key, value)` — `schema_version`.

Profile exports (`GetProfiles`, `CreateProfile(name, icon, engine)`,
`UpdateProfile(..., engine)`, `DeleteProfile`, `MoveProfileEntry`,
`SetDefaultProfile`) are registry-backed; `GetCurrentProfileId` returns the
process's profile. `DeleteProfile` returns false when refused (last profile);
otherwise it marks the row (`UPDATE ... SET deleted_at`) and reassigns the
default among live rows if needed — the registry mutation is all it does.

Data-dir removal (the core dir and both engines' site-data dirs, matching
macOS/GTK's hard delete of the data) always runs in a *surviving* process:
**a process cannot remove its own data dirs** — its file handles release
only at process termination, and its engine child processes (CEF
utility/GPU, WebKit helpers) exit after engine shutdown returns, holding
handles under the dirs until then. `Services/ProfileDataCleanup.cs` drives
it off the retained deleted rows (`GetDeletedProfileIds` export): wait for
the profile's single-instance claim to clear (`WaitForExit` — immediate for
a stopped profile; a dying owner releases it early in shutdown), then the
`DeleteProfileData` export — a plain `remove_all` per dir, retried briefly
to bridge the owner's final teardown. Dirs are deleted under their own
names: ids are never reused, so nothing can observe or reattach a
half-deleted dir, and a failed or interrupted delete is simply resumed by
any later sweep (the permanent row keeps naming it).

- Deleting a *running* profile closes it (macOS/GTK parity): the deleter
  forwards `profile-deleted` over the target's single-instance pipe; the
  target just exits (`MainWindow.ExitAll()`). After every `DeleteProfile`
  the deleter runs `ProfileDataCleanup.RunFor` — immediate for a stopped
  target, after exit for a running one.
- Deleting the profile *currently in use* warns in the confirm dialog,
  relaunches the default profile, and exits; no cleanup runs in the dying
  process.
- The catch-all net (`ProfileDataCleanup.Run()`) runs at every launch and
  on every forwarded single-instance "open" and sweeps every deleted-row
  id. The "open" trigger is what cleans up after a self-deletion — the
  dying process's successor spawn usually reaches an already-running
  default as a forwarded open, so no fresh launch (and no `OnLaunched`)
  happens.

Every registry mutation (create /
update / delete / move / set-default, via the OrionBackend.cs wrappers)
broadcasts `profiles-changed` over the other profiles' single-instance pipes;
receivers raise `App.ProfilesChangedRemotely` (settings pages re-read
GetProfiles) and refresh the EntityShortcuts cache — the macOS analog is the
OrionProfilesDidUpdate distributed notification.

## Legacy split (one-time migration)

`CommonDb::migrate_legacy_if_needed()`, run by `ResolveStartupProfile` /
`InitOrionCore` when the registry is empty and `data\profiles.db` exists:

1. Checkpoint the legacy WAL; read profiles, each profile's
   `settings::general::browser_engine`, and `common_settings`.
2. Seed the registry + app settings.
3. Per profile: **file-copy** the whole legacy DB to
   `Profiles\<id>\profile.db` (preserves schema + applied migrations), then
   `DELETE FROM profiles WHERE id NOT IN (internal, <id>)` — the FK cascade
   erases every other profile's data. The internal profile's own child data
   (pre-profile-era history/bookmarks) is kept **only in the default
   profile's copy**; other copies delete it per-table. `common_settings` is
   emptied everywhere; `foreign_key_check` then `VACUUM`.
4. Rename the legacy file to `profiles.db.pre-split.bak`.

Idempotent: a populated registry is the "done" marker. Duplicated global
favicon rows in each copy are cache data and harmless.

## New-profile provisioning

Creating a profile only writes a registry row + creates its folder. On the
profile's **first launch**, `InitOrionCore` lets core migrate a fresh
profile.db (creates the internal profile), then pre-seeds SQLite's rowid
assignment (`CommonDb::preseed_profile_rowid`: throwaway row with id N-1)
so core's own `create_profile` allocates exactly the registry id N together
with all its default rows (bookmark folders, shared-pinned-tabs window,
default settings, toolbar items); the throwaway row is then removed.

## Caveats / follow-ups

- Profiles deleted before the deleted-row design have no registry row, so
  `ProfileDataCleanup` cannot see their leftover dirs — those need manual
  removal.
- `profile.db` is never cleanly closed at exit: a `-wal` file survives every
  quit, and the handle outlives `g_core_api.reset()`, releasing only at
  process termination — open core-module follow-up (WAL recovery works).
- `DpapiPasswordStorageTests` gate core init behind `ORION_CORE_TESTS=1`
  because `InitOrionCore` now has the migration side effect.
- orion-linux/Linux keeps the single-DB model; the core touch is the optional
  `CoreApi::init(profiles_db_path)` parameter only.
