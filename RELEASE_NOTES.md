# Quiver Launcher 3.5.1

## Android

- Quiver no longer crashes on start when the library uses compact grid cards.
- App Catalog artwork and app page icons show again.
- Installing from the list view works.
- Closing Quiver no longer crashes.
- The App Catalog's title, search and filters scroll away with the cards, so on a landscape handheld the cards get the whole screen. Scroll up or press Up from the first row to bring them back.

## Controllers

- Controller input now uses SDL3 on Windows, Linux and macOS. Linux ARM64 gets controller support for the first time, and Linux x64 and Intel Macs move to a much newer SDL, so more recent gamepads are recognised.
- Your saved buttons stay as they were, including the A/B layout on Nintendo pads. On Steam Deck, the controller and lizard mode work as before.
- On a Steam Deck, a button press counts once even when Steam also types its key, so A no longer toggles a checkbox straight back or opens and closes a dropdown.

## Safer installs and updates

- If several antivirus engines flag the file Quiver is about to download, Quiver asks first and says your antivirus may block the download or remove the app's files. Automatic updates leave these for you to install yourself.
- The app page says what **Verified** means: the release comes from the app's usual developer, its files haven't been swapped, and it waited 48 hours with its files scanned by VirusTotal.
- When a developer removes or replaces a release file, Quiver says so and offers the newest other verified release.
- Rolling releases, like nightly builds, are offered as an update when they're rebuilt under the same tag, and install again when the file matches the one GitHub publishes.
- When a release has several downloads, Update installs the one that matches the build you have, with only the version changed. If no single file matches, you still pick, and the matching file is listed first as "(Same build as installed)". Apps installed before this version ask once more on their next update.
- When a Windows or Mac release has both x64 and ARM64 downloads, Quiver installs the one for your computer's processor. ARM64 computers still get the x64 build when there is no ARM64 one.
- When Windows Security blocks a file or a file is locked, Quiver explains it whatever language Windows is in.
- For an app in a folder you chose, an AppImage update replaces only the file Quiver installed, and deleting the app moves only that file to the Trash. Before, other AppImages in that folder could be removed.

## Update checks and tokens

- Update checks run up to 8 apps at a time for each site, so a large library is checked in seconds rather than minutes. Apps late in the queue no longer report a timeout before they were checked.
- Apps hosted on codeberg.org can be added, with an optional Codeberg token in **Settings → Advanced**.
- Tokens are checked with GitHub, GitLab or Codeberg when you save them. If one is rejected during an update check, Quiver tells you to replace it.
- Settings shows how many GitHub requests you have left this hour. When the limit is used up, Quiver skips GitHub checks instead of failing each one.
- The updates page has a **Check again** button, and the updates button opens that page when updates are waiting.

## More downloads install

- `.tar.xz`, `.tar.bz2`, `.tar.zst` and `.tgz` archives extract like `.tar.gz`, and programs inside stay runnable.
- On Linux, `.deb` packages install into the app's folder, and `.run` setups and `.elf` programs install as single programs.
- On macOS, a disk image with a license agreement shows it and installs once you accept, instead of failing.
- Reinstalling a Flatpak whose exact build is already installed no longer fails.
- Update feed and patch files (`latest-linux.yml`, `.blockmap`, `.zsync`) are no longer offered as downloads.

## Linux

- Programs with dots in their names install, like P.T.'s `P.T.PC.Port.Setup-linux` and files ending in `.x86_64`.
- Flatpak bundles that come inside a zip are offered, and install like any other Flatpak.
- The AppImage uses Quiver's data in `~/.local/share/QuiverLauncher` when it's already there. **Settings → General → Quiver data folder** has a button that moves it there. Installed apps stay where they are.

## macOS

- **Create Desktop Shortcut** works, and picking a `.app` from several programs no longer fails.
- The controller stays with a `.app` game until you quit it, instead of jumping back to Quiver.
- Quiver's data can move to Application Support, with the same button as on Linux.

## Other improvements and fixes

- On short desktop windows, like a Steam Deck screen, the App Catalog's title, search and filters scroll away with the cards, and come back when the window is made taller.
- The sidebar's support button opens Quiver's Patreon page instead of Ko-fi.
- Switching away from Quiver and back keeps an app's page where you'd scrolled it, with the mouse, keyboard or a controller.
- New **Hide Quiver Window Controls** option under **Show OS Top Bar** in **Settings → Appearance**, so the window doesn't show two sets of buttons. Quiver's buttons come back in fullscreen and Steam Deck Gaming Mode.
- Sub-menus follow your theme instead of opening dark grey.
- The **Release asset filter** accepts wildcards: `*` for any text and `?` for one character, like `*portable*x64*`. Plain text still matches anywhere in the file name.

Thanks to sdelavega for the macOS shortcut and controller fixes, to ShizCalev for the faster update checks, Codeberg support and token checks, and to Tmd11a for the option to hide the window controls.
