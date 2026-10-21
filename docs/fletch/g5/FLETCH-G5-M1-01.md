# Fletch device check record (Gate G5)

| Field | Value |
|---|---|
| Reference | FLETCH-G5-M1-01 |
| Date | 2026-10-21 |
| Version | Fletch 0.1.0, commit 7945e85 (iOS Build green; app code identical to 6c75726) |
| IPA | Fletch-0.1.0-7945e85-unsigned.ipa, signed and installed with Signulous |
| Devices | iPad Pro 11-inch (2018, A12X, MTXN2B/A) / iPadOS 26.7.1. iPhone not tested. |
| Tester | Owner |

## Results

| Ref | Milestone | Check | What to look for | iPhone | iPad | Notes |
|---|---|---|---|---|---|---|
| D01 | M1 | Signulous signs and installs the IPA | Install completes; Fletch icon on Home Screen | Not tested | Pass | |
| D02 | M1 | App launches | Main view appears within 5 s; no crash on first open | Not tested | Pass | Catalog rendered 511 entries with artwork; 85 to 120 fps, GPU 1.6 to 1.9 ms, app memory about 220 MB (Metal HUD). Settings and catalog JSON loaded under full AOT. |
| D03 | M1 | Relaunch | Force-quit and reopen; no crash | Not tested | Pass | Reopened straight to the Library view with no error dialog, so settings persisted. |

## Observations for later milestones

| Ref | Observation | Milestone |
|---|---|---|
| O1 | Upstream announcement banner shown ("Update 3.5.1 ... Quiver can now install .tar.xz") | M2 T2.3 |
| O2 | Catalog loaded from quiverlauncher.com; empty-library text names "Quiver catalog" and quiverlauncher.com | M2 T2.3 |
| O3 | iPadOS Metal HUD warns that `LSSupportsGameMode` is missing; declare `false` (Fletch is not a game) | M2 T2.1 |
| O4 | iPad uses the wide layout with top-bar icons; touch targets and safe areas to be reviewed | M6 |

## Verdict

| Field | Value |
|---|---|
| Verdict | Pass |
| Reasons | D01 to D03 pass on the owner's iPad; no crash, settings persist across relaunch |
| Corrective actions | None for M1. Observations O1 to O4 are scheduled. iPhone is covered by the CI simulator smoke test only. |
| Signed off by | Owner (results reported 2026-10-21 23:11) |
| Date | 2026-10-21 |
