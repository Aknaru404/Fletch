# Fletch device checklist (Gate G5)

| Field | Value |
|---|---|
| Reference | FLETCH-G5-<milestone>-<nn> |
| Date | YYYY-MM-DD |
| Version | Fletch <version>, commit <sha> |
| IPA | artifact or release name |
| Devices | iPhone model / iOS version; iPad model / iPadOS version |
| Tester | Owner |

Record Pass or Rejected for each item that applies to the milestone. Note the device for any Rejected item.

## Results

| Ref | Milestone | Check | What to look for | iPhone | iPad | Notes |
|---|---|---|---|---|---|---|
| D01 | M1 | Signulous signs and installs the IPA | Install completes; Fletch icon on Home Screen | | | |
| D02 | M1 | App launches | Main view appears within 5 s; no crash on first open | | | |
| D03 | M1 | Relaunch | Force-quit and reopen; no crash | | | |
| D04 | M2 | Identity | Home Screen name "Fletch"; no "Quiver" text or logo anywhere in the app | | | |
| D05 | M4 | Add an app from a GitHub repo with an `.ipa` asset | Entry appears in the library with the latest version | | | |
| D06 | M4 | Download `.ipa` | Progress shown; cancel works; file appears in Files > Fletch > Downloads | | | |
| D07 | M4 | Share hand-off | Share sheet opens; Signulous (or Files) receives the file | | | |
| D08 | M4 | Update available | After a newer release, the library shows Update available | | | |
| D09 | M4 | Background during download | Switch apps mid-download; on return the download resumes or shows a clear failure | | | |
| D10 | M5 | Mod download | Thunderstore and GameBanana mods saved under Files > Fletch > Mods/<game> | | | |
| D11 | M6 | Rotation and safe areas | Portrait and landscape; nothing under the notch, Dynamic Island or home indicator | | | |
| D12 | M6 | iPad Split View and Stage Manager | Layout adapts when resized; no clipped controls | | | |
| D13 | M6 | Keyboard | Search field stays visible above the on-screen keyboard | | | |
| D14 | M6 | Dark and light mode | Both readable; follows the system setting | | | |
| D15 | M8 | Library persistence | Tags, filters and entries survive app restart and device restart | | | |
| D16 | M8 | Offline | With Airplane Mode on, the library opens and shows a clear offline state | | | |

## Verdict

| Field | Value |
|---|---|
| Verdict | Pass / Rejected |
| Reasons | |
| Corrective actions | |
| Signed off by | |
| Date | |
