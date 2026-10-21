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
| B2 Select toolchain | CI, `macos-15` or newer | `sudo xcode-select -s /Applications/Xcode_<version required by the .NET 10 iOS workload>.app`; `dotnet workload install ios` |
| B3 Device build, unsigned | CI | `dotnet publish Fletch.iOS -c Release -f net10.0-ios -r ios-arm64 -p:EnableCodeSigning=false -p:ArchiveOnBuild=false` |
| B4 Package IPA | CI | `mkdir Payload && cp -R <publish>/Fletch.app Payload/ && zip -qry Fletch-<version>-unsigned.ipa Payload` |
| B5 Simulator smoke | CI | Build `-r iossimulator-arm64`; `xcrun simctl boot` iPhone and iPad; `simctl install`; `simctl launch io.github.aknaru404.fletch`; wait 20 s; `simctl io booted screenshot` per device |
| B6 Publish | CI on tag `fletch-v*` | GitHub Release with the IPA attached |
| B7 Install | Owner | Download IPA, upload to Signulous, install, run the device checklist |

CI layout: new workflow `.github/workflows/ios.yml` (jobs `test`, `ios-build`, `ios-simulator`, `release`). The upstream `dotnet-desktop.yml` pack and publish jobs are limited to `workflow_dispatch` on the fork, because they need upstream signing secrets.

## 5. Milestones and tasks

Status: Draft, Approved, In progress, Done. App code starts only for a task under an Approved milestone.

| Milestone | Tasks | Gates | Device check | Status |
|---|---|---|---|---|
| M0 Harness | T0.1 `CLAUDE.md`, agents, gate scripts, SessionStart hook. T0.2 This plan. T0.3 Baseline: Linux suite 1,789 passed, 3 root-only failures tolerated. T0.4 Fork CI: upstream Windows, macOS and Flatpak packaging run only on manual dispatch (moved from T1.3; macOS runner capacity cancelled PR 1). | G1, owner review | No | Approved |
| M1 Toolchain spike | T1.1 Empty `Fletch.iOS` head showing the existing `MainView`. T1.2 `ios.yml` producing an unsigned IPA and simulator screenshots. T1.3 (moved to T0.4). T1.4 Owner installs via Signulous and confirms launch. T1.5 `Info.plist` file-sharing keys added early (from T5.3) so `crash.log` in Documents can be fetched from Files during G5. | G1 to G5 | Yes (launch only) | Approved |
| M2 Identity and upstream services | T2.1 Fletch identifiers and icon. T2.2 Remove telemetry. T2.3 Remove catalog and announcements, hide the catalog UI. T2.4 Update checks point to Aknaru404/Fletch releases (notify only). | G1 to G4 | No | Draft |
| M3 Platform layer | T3.1 Capability flags and call-site audit. T3.2 iOS paths: caches (`Cache/`, artwork) under `Library/Caches`, library under Documents (G2 M1-A2). T3.3 `TargetOS.iOS` and asset selection. T3.5 iOS `IAppInstallLaunchService` so Launch and Install never reach the desktop `Process.Start` path (G2 M1-A7). T3.4 Disable Wine, Steam shortcuts, tray, gamepad and Velopack on iOS through the flags. | G1 to G4 | No | Draft |
| M4 IPA hand-off | T4.1 Download to `Documents/Downloads` with progress and cancel. T4.2 Share sheet hand-off. T4.3 Library states: Not downloaded, Downloaded, Update available. T4.4 Delete downloaded file. | G1 to G5 | Yes | Draft |
| M5 Mods to Files | T5.1 Thunderstore and GameBanana downloads to `Documents/Mods/<game>/`. T5.2 Hide install-into-game actions on iOS. T5.3 `Info.plist` file sharing keys (done early in T1.5). | G1 to G5 | Yes | Draft |
| M6 iPhone and iPad UI | T6.1 Safe areas and keyboard insets. T6.2 iPad regular-width layout, Split View and Stage Manager. T6.3 Touch targets and edge-swipe review. T6.4 Dark and light themes. | G1 to G5 | Yes | Draft |
| M7 AOT and performance | T7.1 Source-generated JSON for all persisted types. T7.2 Compiled bindings on iOS views. T7.3 Clear trimmer warnings; consider `TrimMode=full`. T7.4 Measure startup and IPA size. | G1 to G4 | No | Draft |
| M8 Release | T8.1 `fletch-v1.0.0` tag and GitHub Release. T8.2 Full device checklist on iPhone and iPad. T8.3 README section for Fletch. | G1 to G5 | Yes (full) | Draft |

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
| R1 | .NET iOS workload needs a newer Xcode than the runner image provides | Medium | High | M1 spike pins the runner and Xcode version first |
| R2 | Reflection-based JSON or bindings break under AOT or trimming on device only | Medium | High | Trimming at `SdkOnly` until M7; device check from M1 |
| R3 | Signulous rejects or mis-signs an unsigned IPA layout | Low | High | M1 installs on device before any feature work |
| R4 | Shared-code changes conflict with upstream releases | Medium | Medium | Capability flags; judge-simplicity; small diffs |
| R5 | iOS suspends long downloads in the background | Medium | Medium | Resume-safe downloads; clear failure state (M4) |
| R6 | GitHub API rate limits without a token | Medium | Low | Existing cache files; optional personal token setting |
| R7 | GitHub macOS runner capacity delays or cancels iOS jobs | Medium | Medium | One macOS job per push; `workflow_dispatch` re-run; no macOS work on upstream jobs |
