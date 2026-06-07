# Orion Mod Launcher

A lightweight WPF launcher for **The Isle (Evrima)** that automates the full mod injection workflow — no manual file shuffling required.

## What it does

1. **Checks for updates** on launch by comparing against the latest GitHub release
2. **Downloads** the mod files automatically if a new version is available
3. **Injects** — disables EAC, deploys bypass shims, and copies mod files into the game
4. **Launches** the game on demand
5. **Removes** everything cleanly, restoring the game to a vanilla state

EAC is always bypassed when injecting — this is required for modded play and is not optional.

## Usage

1. Run `OrionLauncher.exe`
2. If the game directory isn't detected automatically, set it in **Settings**
3. Click **Inject** — the launcher will download any updates and set everything up
4. Click **Launch Game** (or check **Launch after inject** to do both in one click)
5. When done, click **Remove** to restore the game to vanilla

## Distribution

The launcher ships as a single self-contained `.exe` — no .NET installation required.

The `vendor/` folder must sit next to the exe and contain:
```
vendor/
  winhttp.dll
  UniversalSigBypasser.asi
```

## Settings

| Option | Description |
|---|---|
| Game Directory | Override the auto-detected Steam install path |
| Restore Game to Vanilla | Removes EAC bypass, shims, and mod files from the game folder |
| Clear Downloaded Mods | Deletes the local mod cache — files re-download on next inject |

## Credits

**[Ultimate ASI Loader](https://github.com/ThirteenAG/Ultimate-ASI-Loader)** by ThirteenAG
Proxy DLL (`winhttp.dll`) that loads `.asi` plugins into the game process.

**[UniversalSigBypasser](https://github.com/Dmgvol/UE_Modding)** by Dmgvol
ASI plugin that patches Unreal Engine 5's PAK signature verification, allowing unsigned mod PAKs to be mounted.

---

*This tool is for modding purposes on private/unofficial servers. Use responsibly.*
