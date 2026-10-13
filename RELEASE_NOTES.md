# Quiver Launcher 3.5.1-rc.2

This is a prerelease for testing before 3.5.1.

## New since rc.1

- Controllers now go through SDL3 on every desktop build. Please try your pads, including on Steam Deck, Linux and Mac.
- Apps from codeberg.org can be added and updated.
- Update checks run several apps at once, so a big library finishes much faster.
- When a release has several downloads, updates pick the one matching the build you installed instead of asking every time.
- Rolling releases, like nightly builds, are offered again when they're rebuilt under the same tag.
- Updating or deleting an app in a folder you chose no longer touches your other files in that folder.
- An app's page keeps its scroll position when you switch back to Quiver.
- On Android, the App Catalog's search and filters scroll away with the cards.

## Controllers

- Controller input now uses SDL3 on Windows, Linux and macOS. Linux ARM64 gets controller support for the first time, and Linux x64 and Intel Macs move to a much newer SDL, so more recent gamepads are recognised.
- Your saved buttons stay as they were, including the A/B layout on Nintendo pads. On Steam Deck, the controller and lizard mode work as before.

## Safer installs and updates

- If several antivirus engines flag the file Quiver is about to download, Quiver asks first and says your antivirus may block the download or remove the app's files. Automatic updates leave these for you to install yourself.
- The app page says what **Verified** means: the release comes from the app's usual developer, its files haven't been swapped, and it waited 48 hours with its files scanned by VirusTotal.
- When a developer removes or replaces a release file, Quiver says so and offers the newest other verified release.
- Rolling releases, like nightly builds, are offered as an update when they're rebuilt under the same tag, and install again when the file matches the one GitHub publishes.
- When a release has several downloads, Update installs the one that matches the build you have, with only the version changed. If no single file matches, you still pick, and the matching file is listed first as "(Same build as installed)". Apps installed before this version ask once more on their next update.
- For an app in a folder you chose, an AppImage update replaces only the file Quiver installed, and deleting the app moves only that file to the Trash. Before, other AppImages in that folder could be removed.

## Update checks and tokens

- Update checks run up to 8 apps at a time for each site, so a large library is checked in seconds rather than minutes. Apps late in the queue no longer report a timeout before they were checked.
- Apps hosted on codeberg.org can be added, with an optional Codeberg token in **Settings → Advanced**.
- Tokens are checked with GitHub, GitLab or Codeberg when you save them. If one is rejected during an update check, Quiver tells you to replace it.
- Settings shows how many GitHub requests you have left this hour. When the limit is used up, Quiver skips GitHub checks instead of failing each one.
- The updates page has a **Check again** button, and the updates button opens that page when updates are waiting.

## Linux

- Programs with dots in their names install, like P.T.'s `P.T.PC.Port.Setup-linux` and files ending in `.x86_64`.
- Flatpak bundles that come inside a zip are offered, and install like any other Flatpak.
- The AppImage uses Quiver's data in `~/.local/share/QuiverLauncher` when it's already there. **Settings → General → Quiver data folder** has a button that moves it there. Installed apps stay where they are.

## macOS

- **Create Desktop Shortcut** works, and picking a `.app` from several programs no longer fails.
- The controller stays with a `.app` game until you quit it, instead of jumping back to Quiver.
- Quiver's data can move to Application Support, with the same button as on Linux.

## Android

- The App Catalog's title, search and filters scroll away with the cards, so on a landscape handheld the cards get the whole screen. Scroll up or press Up from the first row to bring them back.
- Quiver no longer crashes on start when the library uses compact grid cards.
- App Catalog artwork and app page icons show again.
- Installing from the list view works.
- Closing Quiver no longer crashes.

## Other improvements and fixes

- Switching away from Quiver and back keeps an app's page where you'd scrolled it, with the mouse, keyboard or a controller.
- Sub-menus follow your theme instead of opening dark grey.
- The **Release asset filter** accepts wildcards: `*` for any text and `?` for one character, like `*portable*x64*`. Plain text still matches anywhere in the file name.

Thanks to sdelavega for the macOS shortcut and controller fixes, and to ShizCalev for the faster update checks, Codeberg support and token checks.
