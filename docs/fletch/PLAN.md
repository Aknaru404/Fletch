# Fletch programme: iOS and iPadOS port

| Field | Value |
|---|---|
| Repository | Aknaru404/Fletch (fork of tgeorgiadis/quiver-launcher 3.5.1, MIT) |
| Plan version | 0.2 (M0 approved, M1 approved, 2026-10-21) |
| Date | 2026-10-21 |
| Current milestone | see `docs/fletch/STATE` |

## 1. Objectives

| Ref | Objective | Measure |
|---|---|---|
| O1 | Produce an unsigned IPA that Signulous signs and installs on iPhone and iPad, iOS 17 or later | IPA from CI installs and launches on the owner's devices (G5) |
| O2 | Keep the library, tag filters, search and release tracking for GitHub, GitLab and Codeberg | Same library behaviour as Android, checked by the device checklist |
| O3 | Download `.ipa` release assets and hand them to Signulous or another signer through the iOS share sheet | Hand-off reaches the share sheet; the file opens in Files |
| O4 | Download Thunderstore and GameBanana mods into the app's Files folder for other apps to import | Mods visible under On My iPhone/iPad > Fletch |
| O5 | Rebrand as Fletch with no upstream services: no PostHog, no Quiver catalog API, no upstream announcements | Guards G-BRAND and G-UPSTREAM pass |
| O6 | Keep Desktop, Android and the test suite green, with a small diff against upstream | G1 and CI pass; judge-simplicity Pass |

Out of scope: launching downloaded apps, installing packages from inside the app, self-update, App Store submission, Quiver catalog browsing.

## 2. Fixed decisions

| Ref | Decision | Source |
|---|---|---|
| D1 | Scope: `.ipa` hand-off plus mods saved to Files | Owner, 2026-10-21 |
| D2 | Build on GitHub Actions macOS runners; this container builds and tests Linux only | Owner |
| D3 | Unsigned IPA; Signulous signs | Owner |
| D4 | Universal iPhone and iPad, minimum iOS 17.0 | Owner |
| D5 | Name Fletch, repo Aknaru404/Fletch | Owner |
| D6 | Telemetry and Quiver catalog both off | Owner |
| D7 | Gates G1 to G5 as defined in `CLAUDE.md` | Owner |
| D8 | Bundle ID `io.github.aknaru404.fletch` | Owner, 2026-10-21 |
| D9 | Generated fletching-mark icon, replaceable later | Owner, 2026-10-21 |
| D10 | Fletch checks its own GitHub releases and shows a notice with a link; no self-update | Owner, 2026-10-21 |
| D11 | Desktop and Android heads keep building and testing; not released from the fork | Owner, 2026-10-21 |
| D12 | Upstream services are disabled at the source (hosts blanked, catalog entry points hidden), not deleted, to keep the upstream diff small | Owner, 2026-10-21 |
| D13 | M2 rebrands identifiers and all text reachable on iOS; Desktop/Android-only text (tray, Mac menu, CLI, Windows installer, Flatpak, Android updater) stays as upstream | Owner, 2026-10-21 |
| D14 | The Fletch "new version available" notice moves to M8, when a release exists to detect; M2 only points update sources at Aknaru404/Fletch | Owner, 2026-10-21 |
| D15 | The fallback data folder is renamed to `Fletch` (macOS/XDG) with no migration from `QuiverLauncher`; Fletch is not released for desktop, and iOS keeps its data in Documents | G2 M2 T2.1-A1, 2026-10-21 |
| D16 | With the catalog off, the user's own "My app list" (CustomAppListLocation) is hidden with Browse; the saved value is kept. The catalog fallback host code was deleted rather than disabled because G-UPSTREAM forbids the host literal. The Steam shortcut tag rename may duplicate old desktop shortcuts (desktop not released). | G3 M2 C2, S5, C3, 2026-10-21 |

## 3. Architecture

| Area | Approach | Main files |
|---|---|---|
| iOS head | New project `Fletch.iOS` (`net10.0-ios`, `Avalonia.iOS` 12.1.1, `SupportedOSPlatformVersion` 17.0). It references App with `QuiverExcludeWindowsDrawing=true` and SDL3 with `ExcludeAssets=native`, as the Android head does. | `Fletch.iOS/Fletch.iOS.csproj`, `AppDelegate.cs`, `Info.plist`, `Assets.xcassets` |
| Platform flags | Add `IsIOS`, `CanInstallPackages`, `CanLaunchApps`, `UsesMobileShell`, `SharesDownloadsToFiles` to `PlatformCapabilities`. Reclassify each `IsAndroid()` check as either mobile UI or Android install. | `Services/PlatformCapabilities.cs`, about 100 call sites |
| Target OS | Add `TargetOS.iOS`; `PlatformAssetMatcher` selects `.ipa` assets on iOS instead of excluding them | `lib/QuiverLauncher.Core/Models/TargetOS.cs`, `PlatformAssetMatcher.cs:180` |
| Install service | `IosAppInstallLaunchService`: Install downloads the `.ipa` to `Documents/Downloads` and presents `UIActivityViewController`; Launch is unsupported and hidden; Uninstall removes the downloaded file | new `Fletch.iOS/Services/`, `Services/IAppInstallLaunchService.cs` |
| Paths | Library under `Documents/` (visible in Files), cache under `Library/Caches` | `QuiverLauncherPaths.cs` (provider hook, as for Android) |
| Identity | `DisplayName`, `ApplicationId`, `Repository`, user agents, data folder name | `Services/QuiverLauncherProfile.cs`, `QuiverLauncherPaths.cs` |
| Upstream services | Compile out PostHog, catalog client and announcements behind one `FletchBuild` switch | `Telemetry.cs`, `QuiverCatalogClient.cs`, `AnnouncementDefaults.cs` |
| AOT | Mono AOT (iOS default), trimming at `SdkOnly` first; source-generated JSON and compiled bindings for iOS views in M7 | `JsonSerializerContext` types, `.axaml` |

## 4. Build and recompilation

| Step | Where | Command or action |
|---|---|---|
| B1 Linux build and tests | Container (G1) and CI `test` job | `scripts/harness/gate-local.sh` |
| B2 Select toolchain | CI, `macos-26-intel` | `sudo xcode-select -s /Applications/Xcode_26.3.app` (pack 26.2.10233 requires Xcode 26.3); `dotnet workload config --update-mode manifests`; `dotnet workload install ios --from-rollback-file scripts/ci/ios-workload-rollback.json` (pins the iOS 26.2 SDK pack; the 27.x workload needs Xcode 27, not yet on runner images) |
| B3 Device build, unsigned | CI | `scripts/ci/ios-build.sh ios-arm64 -p:EnableCodeSigning=false` (`dotnet build -c Release`; errors become annotations) |
| B4 Package IPA | CI | `mkdir Payload && cp -R <publish>/Fletch.app Payload/ && zip -qry Fletch-<version>-unsigned.ipa Payload` |
| B5 Simulator smoke | CI | `scripts/ci/ios-build.sh "$SIM_RID"`; boot newest iPhone and iPad Pro simulators; install; launch `io.github.aknaru404.fletch`; assert still running after 45 s; screenshot, log and `crash.log` per device |
| B6 Publish | CI on tag `fletch-v*` | GitHub Release with the IPA attached |
| B7 Install | Owner | Download IPA, upload to Signulous, install, run the device checklist |

CI layout: `.github/workflows/ios.yml`, one macOS job (device IPA, then simulator smoke) on pushes to `main` and `claude/**`; Linux tests run in `dotnet-desktop.yml` on the PR; release job added in M8. The upstream `dotnet-desktop.yml` pack and publish jobs are limited to `workflow_dispatch` on the fork, because they need upstream signing secrets.

## 5. Milestones and tasks

Status: Draft, Approved, In progress, Done. App code starts only for a task under an Approved milestone.

| Milestone | Tasks | Gates | Device check | Status |
|---|---|---|---|---|
| M0 Harness | T0.1 `CLAUDE.md`, agents, gate scripts, SessionStart hook. T0.2 This plan. T0.3 Baseline: Linux suite 1,789 passed, 3 root-only failures tolerated. T0.4 Fork CI: upstream Windows, macOS and Flatpak packaging run only on manual dispatch (moved from T1.3; macOS runner capacity cancelled PR 1). | G1, owner review | No | Approved |
| M1 Toolchain spike | T1.1 Empty `Fletch.iOS` head showing the existing `MainView`. T1.2 `ios.yml` producing an unsigned IPA and simulator screenshots. T1.3 (moved to T0.4). T1.4 Owner installs via Signulous and confirms launch (Pass, FLETCH-G5-M1-01). T1.5 `Info.plist` file-sharing keys added early (from T5.3) so `crash.log` in Documents can be fetched from Files during G5. | G1 to G5 | Yes (launch only) | Done pending merge (G1 to G5 Pass) |
| M2 Identity and upstream services | T2.1 Identity: profile values, data folder name, user agents, update sources (Velopack, CLI, Android updater) to Aknaru404/Fletch, `LSSupportsGameMode` false (G5 M1-O3). T2.2 Telemetry inert: no build-time or env token, no PostHog host. T2.3 Catalog, announcements and artwork resizer disabled at source; catalog, Discord, Patreon and upstream repo entry points hidden or repointed. T2.5 Rebrand text reachable on iOS (D13). | G1 to G5 | Yes (identity and no upstream calls) | Approved |
| M3 Platform layer | T3.1 Capability flags and call-site audit. T3.2 iOS paths: caches (`Cache/`, artwork) under `Library/Caches` with a size cap (the artwork disk cache has no eviction and now stores full-size originals), library under Documents (G2 M1-A2, G3 M1-AP5, G3 M2-AP3); document or replace the `OverrideUserDataRoot` test seam used by the iOS head (G3 M1-S1); keep identity in the csproj only and drop duplicate Info.plist keys (G3 M1-S2). T3.3 `TargetOS.iOS` and asset selection. T3.5 iOS `IAppInstallLaunchService` so Launch and Install never reach the desktop `Process.Start` path (G2 M1-A7). T3.4 Disable Wine, Steam shortcuts, tray, gamepad and Velopack on iOS through the flags. | G1 to G4 | No | Draft |
| M4 IPA hand-off | T4.1 Download to `Documents/Downloads` with progress and cancel. T4.2 Share sheet hand-off. T4.3 Library states: Not downloaded, Downloaded, Update available. T4.4 Delete downloaded file. | G1 to G5 | Yes | Draft |
| M5 Mods to Files | T5.1 Thunderstore and GameBanana downloads to `Documents/Mods/<game>/`. T5.2 Hide install-into-game actions on iOS. T5.3 `Info.plist` file sharing keys (done early in T1.5). | G1 to G5 | Yes | Draft |
| M6 iPhone and iPad UI | T6.1 Safe areas and keyboard insets. T6.2 iPad regular-width layout, Split View and Stage Manager; add `UIApplicationSceneManifest` scene support (G3 M1-P1). T6.3 Touch targets and edge-swipe review. T6.4 Dark and light themes. T6.5 Hide or reword Settings text that promises a tray or self-updater on iOS (G2 M2-F3). | G1 to G5 | Yes | Draft |
| M7 AOT and performance | T7.0 Remove `JsonSerializerIsReflectionEnabledByDefault=true` from the iOS head once T7.1 lands (G3 M1-AP1). T7.1 Source-generated JSON for all persisted types. T7.2 Compiled bindings on iOS views. T7.3 Clear trimmer warnings; consider `TrimMode=full`. T7.4 Measure startup and IPA size. T7.5 Decode artwork to display size (`Bitmap.DecodeToWidth`) instead of decoding full-size originals (G3 M2-AP1, AP2). | G1 to G4 | No | Draft |
| M8 Release | T8.0 Fletch "new version available" notice on iOS against Aknaru404/Fletch releases (moved from T2.4, D14). T8.1 `fletch-v1.0.0` tag and GitHub Release. T8.2 Full device checklist on iPhone and iPad. T8.3 README section for Fletch. | G1 to G5 | Yes (full) | Draft |

## 6. Definition of done

| Level | Condition |
|---|---|
| Task | G1 Pass, G2 Pass, committed on a `claude/*` branch |
| Milestone | All tasks done; G3 Pass after the verifier; G4 Pass; G5 Pass where marked; draft PR reviewed and merged by the owner; `docs/fletch/STATE` advanced |

## 7. Open items for owner confirmation

None. Q1 to Q4 decided on 2026-10-21 as D8 to D11.

## 8. Risks

| Ref | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R1 | .NET iOS workload needs a newer Xcode than the runner image provides | Occurred (M1: workload 27.1 needs Xcode 27.1; runners top out at 26.6) | High | Workload pinned to 26.2.10233 with Xcode 26.3; revisit when runner images add Xcode 27 |
| R2 | Reflection-based JSON or bindings break under AOT or trimming on device only | Medium | High | Trimming at `SdkOnly` until M7; device check from M1 |
| R3 | Signulous rejects or mis-signs an unsigned IPA layout | Low | High | M1 installs on device before any feature work |
| R4 | Shared-code changes conflict with upstream releases | Medium | Medium | Capability flags; judge-simplicity; small diffs |
| R5 | iOS suspends long downloads in the background | Medium | Medium | Resume-safe downloads; clear failure state (M4) |
| R6 | GitHub API rate limits without a token | Medium | Low | Existing cache files; optional personal token setting |
| R7 | GitHub macOS runner capacity delays or cancels iOS jobs | Occurred (M1: arm64 runners not acquired 3 times) | Medium | Intel `macos-26-intel` runner; one macOS job per push; `workflow_dispatch` re-run; no macOS work on upstream jobs |
| R8 | An upstream Linux test fails intermittently (once in the first local baseline run, once in CI on 550953d) | Medium | Low | Re-run once; capture the test name from G1 TRX output when it recurs; fix or report upstream |

## 9. Ideas to explore (no decision, not planned)

| Ref | Idea | Notes |
|---|---|---|
| X1 | One-tap JIT for apps that need it (emulators): Fletch "Launch with JIT" hands the bundle ID to StikDebug via its URL scheme | JIT cannot be enabled permanently on iOS: a debugger must attach on each launch. Needs StikDebug installed, a one-time pairing file made on a computer, and a `get-task-allow` signature from Signulous. Not needed for natively compiled engines. Recorded 2026-10-21 at the owner's request; no decision. |
| X2 | Per-game iOS builds of open-source engines (for example dhewm3, OpenJK, Arx Libertatis, OpenMW) as separate apps, rendered through Metal (ANGLE or MoltenVK), tracked and delivered through Fletch | Each game becomes its own IPA. The owner supplies legitimately owned game data through Files. Console static recompilations are research only. Device limits: about 4 GB RAM and little free storage. Recorded 2026-10-21; no decision. |

### X2 estimates (owner's game list, 2026-10-21; ideas only)

Assumptions: iPad Pro 11-inch (2018, A12X, 4 GB RAM), owner-supplied game data, GitHub Actions macOS minutes free. Cost is Claude usage, scaled from this programme's M0 and M1 (about $10 to $15 metered).

| Title | Route | Expected performance | Time to tested IPA | Cost (tokens / money) |
|---|---|---|---|---|
| Diablo + Hellfire | DevilutionX (already targets iOS) | Excellent, 60 fps | 0.5 to 1 day | ~3–8M / $5–15 |
| Fallout 1, Fallout 2 | fallout1-ce, fallout2-ce (already target iOS) | Excellent, 60 fps | 0.5 to 1 day each | ~3–8M / $5–15 each |
| Baldur's Gate 1/2, Icewind Dale, Planescape: Torment | GemRB (iOS support), or buy official Enhanced Editions | Excellent | 1 to 2 days (0 if bought) | ~8–20M / $10–30 |
| TES II: Daggerfall | Daggerfall Unity (Unity Editor in CI) | Good, 60 fps | 1 to 2 weeks | ~30–80M / $40–120 |
| TES III: Morrowind | OpenMW (OSG, Bullet, MyGUI, FFmpeg, Lua without JIT) | Good, 30 to 60 fps, reduced view distance | 1 to 3 weeks | ~40–120M / $60–200 |
| Arx Fatalis | Arx Libertatis via ANGLE to Metal | Good, 60 fps | 3 to 7 days | ~20–50M / $30–80 |
| KOTOR 1/2 | Official iOS apps (open engine incomplete) | Excellent | none | $0 |
| Fallout 3/NV/4, Oblivion, Skyrim, Diablo II/III, Assassin's Creed, God of War (PS2), Guild Wars 1/2, Xbox 360 RPGs | Not possible: closed engines with no open reimplementation, online-only, or no recompilation; 360 titles exceed 4 GB RAM | n/a | n/a | n/a |

Constraints: about 4.2 GB free storage on the device; Morrowind data with expansions is about 1.2 GB.

### X3 Xbox 360 recompilation basis (2026-10-21; ideas only, no decision)

| Stage | What | Purpose | Rough effort |
|---|---|---|---|
| A | Port an existing 360 recompilation (UnleashedRecomp, built on XenonRecomp) to iPadOS: ARM64 in place of x64 vector code, Metal backend, iOS packaging | Builds a reusable iOS runtime and Metal layer on a project known to work | Weeks to a few months; about $300 to $1,500; medium risk |
| B | Generalise Stage A into a 360-to-iOS toolkit | Shared basis for later titles | Weeks; about $200 to $800 |
| C | Assassin's Creed 1 from the owner's own dump on that toolkit | First-of-its-kind recompilation | Months; several thousand dollars; high risk of an incomplete result |

- The instruction set and translator are the same for every game. The per-game work is code analysis, statically linked middleware, GPU usage, timing and threading.
- The A12X (CPU, GPU, 4 GB RAM) is not the limiting factor for native recompiled code. The engineering effort is.
- Stage C needs a Mac with Xcode for debugging.
- Public/private split, as UnleashedRecomp and Zelda 64: Recompiled do: the recompiler toolkit, iOS runtime, Metal renderer and the game's patches and configuration can be public, with free macOS CI, because they contain no game code. The owner's dump, the code generated from it and the resulting IPA stay private (private repository with private CI, or a Mac) and are for personal sideloading only, because public CI artifacts would distribute translated game code. Only that private build step needs paid macOS minutes.
- Decrypting a console executable sits in a legal grey area, including in the UK.
- Oblivion (360) and Assassin's Creed (360) have no existing recompilation project. REA (morluto/rea) can help only with binary analysis; it does not recompile.
