# Fletch

Fletch is an unofficial iOS and iPadOS fork of [Quiver Launcher](https://github.com/tgeorgiadis/quiver-launcher). It is not affiliated with, endorsed by or supported by Quiver Launcher or its author.

It keeps a personal library of apps released on GitHub, GitLab and Codeberg, tracks their new releases, hands `.ipa` release files to the signing tool of your choice through the iOS share sheet, and saves mods from Thunderstore and GameBanana to the Files app.

iOS does not let one app install or launch another, so Fletch does not do either. It sends no usage data and does not contact Quiver Launcher's services.

## Status

Fletch is in development. Progress, milestones and decisions are in [docs/fletch/PLAN.md](docs/fletch/PLAN.md).

| Item | Value |
|---|---|
| Devices | iPhone and iPad |
| Minimum OS | iOS and iPadOS 17.0 |
| Bundle ID | `io.github.aknaru404.fletch` |
| Distribution | Unsigned IPA from GitHub Actions; sign and install with your own sideloading tool |

## Install

1. Open the latest green **iOS Build** run under [Actions](https://github.com/Aknaru404/Fletch/actions) and download its artifact.
2. Unzip it and take `Fletch-<version>-<commit>-unsigned.ipa`.
3. Sign and install it with your sideloading tool. iPadOS may ask you to turn on Developer Mode.

## Build

The iOS app builds only on macOS. See `.github/workflows/ios.yml` and steps B1 to B7 in [docs/fletch/PLAN.md](docs/fletch/PLAN.md). The shared code and tests build on any OS with the .NET 10 SDK:

```bash
dotnet test QuiverLauncher.Tests -c Release
```

## Credits and licence

Fletch is based on [Quiver Launcher](https://github.com/tgeorgiadis/quiver-launcher) by Thomas Georgiadis, which is itself a fork of [GithubLauncher](https://github.com/SirDiabo/GithubLauncher) by SirDiabo. The code is under the [MIT License](LICENSE), whose copyright notice is kept unchanged.

"Quiver" and "Quiver Launcher" are marks of the upstream project, used here only to describe where Fletch comes from, as [TRADEMARKS.md](TRADEMARKS.md) allows. The upstream README is kept in [docs/upstream/README.md](docs/upstream/README.md) for reference. Its download links are for the official upstream builds, not for Fletch.

Report Fletch problems in this repository's issues, not to the upstream project.
