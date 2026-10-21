# Fletch: iOS and iPadOS port of Quiver Launcher

Fletch is a fork of tgeorgiadis/quiver-launcher 3.5.1 (MIT). It uses Avalonia 12.1.1 and .NET 10. The deliverable is an unsigned IPA for iPhone and iPad on iOS 17 or later, which the owner signs and installs through Signulous.

- Plan, milestones and status: `docs/fletch/PLAN.md`
- Current milestone: `docs/fletch/STATE`
- Device test record: `docs/fletch/DEVICE-CHECKLIST.md`

## Rules

### Write app code only for the milestone task marked Approved in `docs/fletch/PLAN.md`
- The owner approves each milestone before work starts and signs it off after the device check.
- Work on harness, docs and CI may go ahead without approval. Changes to app code may not.

### NEVER put "Quiver" in anything the user sees or that identifies the app
- `TRADEMARKS.md` forbids it in a modified build: app name, window title, bundle ID, package name, data folder, user agent, icons.
- Use these identifiers:
  - Name: `Fletch`
  - Bundle ID: `io.github.aknaru404.fletch`
  - Data folder: `Fletch`
  - User agent: `Fletch/<version> (+https://github.com/Aknaru404/Fletch)`
- Internal namespaces and project names (`QuiverLauncher.*`) stay unchanged, to keep merges from upstream cheap.

### NEVER call upstream services
- The owner decided this build sends no data to the upstream author.
- Remove or disable:
  - PostHog (`Telemetry.cs`)
  - `api.quiverlauncher.com` and its `*.convex.site` fallback (`QuiverCatalogClient.cs`)
  - announcements fetched from upstream raw.githubusercontent.com (`AnnouncementDefaults.cs`)
  - the artwork resizer CDN `https://quiverlauncher.com/cdn-cgi/image/` (`LauncherArtworkLoader.cs:27`)
- Check: `scripts/harness/guards.sh` (rule G-UPSTREAM).

### Gate behaviour on a capability flag, not on a platform name
- About 100 `OperatingSystem.IsAndroid()` checks mix two meanings: "mobile layout" and "Android can install APKs". iOS shares the first and not the second.
- Add flags to `Services/PlatformCapabilities.cs` and branch on them:
  ```csharp
  if (PlatformCapabilities.CanInstallPackages) { ... }   // not: if (OperatingSystem.IsAndroid())
  ```

### NEVER use process launch, dynamic code or writes outside the sandbox on iOS paths
- iOS forbids launching processes. AOT has no JIT. The app can only write inside its own container.
- Banned on iOS code paths: `Process.Start`, `System.Reflection.Emit`, `DynamicMethod`, `Assembly.Load*`, and absolute paths that do not come from `FletchPaths`/`QuiverLauncherPaths`.
- Check: `scripts/harness/guards.sh` (rule G-IOSAPI).

### Use source-generated JSON for any new or touched serialisation
- Reflection-based `JsonSerializer` calls depend on trimming not removing members, which breaks silently on device.
- Example: add the type to a `JsonSerializerContext` and call `JsonSerializer.Serialize(value, FletchJsonContext.Default.AppEntry)`.

### Keep the Desktop and Android heads building and the test suite green
- All three heads share the App and Core libraries. A break shows up in CI first.

### Prove every change with tests
- Write the failing test first for logic changes (xUnit v3 with FluentAssertions in `QuiverLauncher.Tests`).
- UI-only changes are proved by simulator screenshots from CI.

### Work on `claude/*` branches and open draft PRs to `main`
- `main` is what the owner builds and installs.
- NEVER push to `main` directly. NEVER commit `.p12`, `.mobileprovision` or tokens.

## Gates

A task is done only when gates G1 and G2 pass. A milestone is done only when G1 to G5 pass. Report each gate as a table row with Pass or Rejected, the reason, and the corrective action.

| Gate | When | How |
|---|---|---|
| G1 Local | Every task | `scripts/harness/gate-local.sh` (build, tests, guards) |
| G2 Review | Every task | `task-reviewer` agent on the task diff |
| G3 Judges | Every milestone | `judge-correctness`, `judge-ios-platform`, `judge-aot-performance` and `judge-simplicity` run in parallel; `verifier` then re-checks every blocking finding |
| G4 CI | Every milestone | `scripts/harness/gate-ci.sh`: Linux tests, unsigned IPA, iPhone and iPad simulator screenshots |
| G5 Device | Milestones marked Device | Owner runs `docs/fletch/DEVICE-CHECKLIST.md` after installing through Signulous |

## Toolbox

- SDK: the SessionStart hook installs the .NET 10 SDK into `/root/.dotnet`. If `dotnet` is missing: `bash .claude/hooks/session-start.sh`.
- Local tests (Linux): `scripts/harness/gate-local.sh`. A full run takes about 9 minutes. For a quick loop: `dotnet test QuiverLauncher.Tests -c Release --filter "FullyQualifiedName~<Class>"`.
- iOS build: CI only (`.github/workflows/ios.yml`, macOS runner). This container cannot build for iOS.
- CI status and artifacts: `scripts/harness/gate-ci.sh <branch>`.
- Upstream reference (read-only): `git fetch https://github.com/tgeorgiadis/quiver-launcher main`.
- Skills: `superpowers:test-driven-development`, `superpowers:systematic-debugging`, `superpowers:verification-before-completion`, `ecc:dotnet-patterns`, `ecc:csharp-testing`.
