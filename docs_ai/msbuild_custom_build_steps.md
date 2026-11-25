# Custom Build Steps and the IDE Up-to-Date Checks

Every Visual Studio build of a .vcxproj passes through two independent
up-to-date checks. Putting a custom build step's freshness signal into the
wrong layer makes the step silently stop running in IDE builds.

## The two layers

1. **IDE project-level check** — runs inside Visual Studio *before* deciding
   whether to invoke MSBuild for the project. It reads only the FileTracker
   `.tlog` files (plus `*.lastbuildstate`) in `$(TLogLocation)` (e.g.
   `x64/Debug/<Project>.tlog/`). Anything not recorded there does not exist
   for this check.
2. **MSBuild target/task level** — target `Inputs`/`Outputs` attributes and
   tracked-task checks (`GetOutOfDateItems`, CL command-line comparison).
   Reachable only when layer 1 decides to invoke MSBuild.

Command-line msbuild (`ci/` scripts) has no layer 1, so layer-2-only
incrementality works there — the staleness manifests **only in IDE builds**,
which is why it survives unnoticed.

## The footgun

`<Target Inputs="..." Outputs="..."><Exec .../></Target>` (typically on a
Utility project). Layer 2 sees the inputs; layer 1 sees nothing, because Exec
writes no tlogs. After one successful build (`.lastbuildstate` written, no
read/write tlogs) the IDE judges the project permanently up to date and never
invokes MSBuild again (verified 2026-08).

Target attributes have a second, layer-2 defect: input deletions are invisible
(a deleted file vanishes from the glob, and the remaining inputs are all older
than the outputs).

**Symptom:** outputs stay stale, VS reports the project up to date, Rebuild
"fixes" it.
**Diagnosis:** the project's `.tlog` directory contains only
`*.lastbuildstate` — no read/write tlogs.
**Verification:** touch an input, Build (not Rebuild) in VS, compare mtimes:
outputs and `lastbuildstate` both unchanged → layer 1 skipped the project;
`lastbuildstate` updated but outputs unchanged → MSBuild ran and the target
skipped.

## The pattern: CustomBuild items

Declare the step as a `CustomBuild` item — worked examples: every
CMake-generated vcxproj (core module projects, the `webextensions` packaging
step, ZERO_CHECK/INSTALL) uses this mechanism.

- `Include` = the script/primary input; metadata: `Command`,
  `AdditionalInputs` (the full input set — populate from a glob `<ItemGroup>`
  declared *before* the CustomBuild item), `Outputs`.
- The `CustomBuild` target (Microsoft.CppCommon.targets) runs
  `GetOutOfDateItems`, which both computes the out-of-date set (layer 2) and
  writes `CustomBuild.read/write/command.1.tlog` (feeding layer 1). One
  declaration drives both layers.
- Command-string changes retrigger the step (command tlog).
- Deletions are caught: the read tlog remembers the *previous* input set, and
  a recorded-but-missing input marks the item out of date.
- Clean deletes the declared outputs via the write tlog (CppClean); no custom
  clean target needed.
- Utility `ConfigurationType` is fine. Don't give the same file a second item
  type (e.g. `None`) alongside `CustomBuild`.

## When the input set can't be enumerated

If the real inputs are a huge external tree (local WebKit checkout, vendored
Chromium), don't pretend with a narrow input list. Make the step run every
build and self-gate instead: an NMake/Makefile project's
`NMakeBuildCommandLine` is always invoked by VS (`OrionCefSetup` +
`cef.py.cmd setup`), or set `<DisableFastUpToDateCheck>true</...>` on the
project — verified honored by the VC project system (2026-08), and it can be
scoped to one configuration (OrionBackend, WKLocalDebug only). The
underlying tool's own no-op (ninja, vcpkg cache hit) is the incrementality.

## Corollaries — other things layer 1 cannot see

- **`PreBuildEvent`/`PostBuildEvent`** run only when the project actually
  builds and never affect up-to-dateness. Never use one to refresh outputs
  whose inputs are outside the project's tracked sources (a codegen exe, an
  external source tree): the files that would dirty the project are the very
  ones the skipped step should have produced — circular staleness.
- **Target-injected compile options** (a target adding a `/D` define computed
  from a file): layer 1 sees neither the injected option nor the file it came
  from, so that file's changes do not rebuild in the IDE. Layer 2's CL
  command comparison catches it, so `ci/` builds stay correct. Generate a
  real header from the file via a CustomBuild item and `#include` it instead.
- **GUID churn orphans tlog dirs**: tlog dirs are named
  `<name>.<first-8-guid-hex>.tlog`; when a generated project's GUID changes
  (CMake regen / uuid syncing), the old dir is never cleaned — CppClean and
  `cmake --fresh` don't touch it — tripping MSB8028 and keeping the IDE
  check permanently failing, so the project builds every time.
  `fix_project_uuids.py.cmd` sweeps stale ones on every core regen — and
  since the GUIDs are now seeded into cmake (`-C build/project_guids.cmake`,
  see CLAUDE.md "Project GUID Synchronization") they no longer churn at all.
- **`AfterTargets` hooks like vcpkg's `VcpkgInstallManifestDependencies`**:
  input declarations on the target (`_ZVcpkgInstallManifestDependenciesInputs`
  in Directory.Build.props) work at layer 2 only. A portfile-only edit leaves
  OrionBackend's tlogs untouched → project layer-1 skipped → vcpkg never runs
  on F5. Run `./vcpkg.cmd install` manually after port changes.

## The csproj side: CPS fast up-to-date check and copy items

SDK-style projects (Orion.csproj) are checked by different machinery — the
.NET Project System FUTDC — which reads *evaluated* project items, not
tlogs. Verified behaviors (2026-08):

- **PRI wedge:** every `Content` item with copy metadata is an input to the
  "PRIGeneration" check set, but the generated `.pri` only advances when
  real resources (`Assets\*`) change. One always-advancing `Content` source
  (e.g. a native DLL relinked per build) wedges the project into a full
  rebuild on every build, forever. Path-loaded runtime files must be `None` with
  explicit `CopyToOutputDirectory` + `CopyToPublishDirectory`: still copied
  and published, not a PRI input.
- **Plain `None`/`Content` copy items are tracked** as source→destination
  pairs, and Build Acceleration copies a stale destination without invoking
  MSBuild at all ("accelerated 1 project(s), copying 1 file(s)").
- **`ProjectReference` `OutputItemType` injection is invisible:** injected
  items exist only while the referenced project's targets run, so the FUTDC
  never sees their copy pairs — the destination goes stale while VS reports
  up to date (the app loads DLLs from the output dir, so this is stale
  native code under F5). Declare such files as plain items with an explicit
  path — a wrong path then fails the build loudly — and keep the
  `ProjectReference` for build ordering only.
- **`IfDifferent` is content-aware:** a byte-identical source with a newer
  mtime is not re-copied; the check tolerates the resulting older
  destination mtime.
- **The `.xbf` echo:** WinUI targets declare output-dir `.xbf` files as
  `UpToDateCheckInput` with modified-since-build-start semantics, so a build
  that recompiles XAML is followed by exactly one extra rebuild.
- **Diagnosis:** Tools > Options > Projects and Solutions > SDK-Style
  Projects > up-to-date-check logging = Verbose; `FastUpToDate:` lines name
  the offending item and check set.
