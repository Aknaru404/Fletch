# Quiver name and logo

The code in this repository is licensed under the [MIT License](LICENSE). The license covers the code only. It
grants no rights to the project's names or logo.

The names **Quiver** and **Quiver Launcher**, the **quiverlauncher.com** domain, and the Quiver logo and app icons
are the project's marks. They identify the official app and website, made by Thomas Georgiadis. This page says how
you may use them.

Quiver Launcher started as a fork of [GithubLauncher](https://github.com/SirDiabo/GithubLauncher) by SirDiabo. That
project's name is SirDiabo's, and this page doesn't cover it.

## You may

- Say truthfully that your project is based on, forked from, or works with Quiver Launcher, for example "a fork of
  Quiver Launcher" in your README.
- Share and link to the official, unmodified builds from this repository's releases, under their own name.
- Use the names when you write or talk about the project, such as in reviews, articles, videos and bug reports.

## You may not, in a modified version or another project

- Use "Quiver" in its name, including the app name, window title, installer, package name, repository name or domain.
- Use the Quiver logo or app icons, or artwork that could be mistaken for them.
- Keep the official app and package identifiers. A modified build that keeps them installs over the official app,
  shares its data folder and can take its updates.
- Suggest that it is official, endorsed by, or supported by Quiver.

When you release a fork, give it its own name, icon and identifiers. In this app they are:

- `DisplayName`, `ApplicationId`, `Repository`, `ExecutableName` and the user agents in
  `Services/QuiverLauncherProfile.cs`. `Repository` is where the app looks for its own updates.
- `AppName` in `Services/QuiverLauncherPaths.cs`, the name of the data folder.
- `PACK_ID` in `.github/workflows/dotnet-desktop.yml`, the Velopack package the installer and updates use.
- `ApplicationId` in `QuiverLauncher.Android/QuiverLauncher.Android.csproj`.
- The app icons: `icon.ico`, `Assets/quiver-icon.png` and the Android launcher icons in
  `QuiverLauncher.Android/Resources/`, plus the name and logo shown in the app itself.

## Questions

If you're unsure whether a use is fine, or want permission for one this page doesn't allow, open an issue in this
repository and ask.
