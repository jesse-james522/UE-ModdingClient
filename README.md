# The Isle Mod Client

A Windows mod manager and launcher for the dinosaur survival game The Isle (Steam).

Features:
- Auto-detects your The Isle installation in Steam
- Lets you select and use any mod set for The Isle
- Easy mod install from ZIP or GitHub link
- Automatic backup and restoration of your original settings.json
- Reset to vanilla or fully wipe all modded files with a button

How to use: Double-click `The Isle Mod Client.exe` (auto-detects your The Isle folder, or use “Browse…” to select it). To install mods, paste a ZIP/GitHub link and click “Install most up to date mod files” or add your own mod files to a folder inside `Mods/`. Pick your mod set, then click “Install & Launch” to start The Isle with your mods (optionally minimize the launcher on start). To remove mods, use “Reset to Vanilla (settings only)” to restore original settings or “Full Wipe Modded Files (CAREFUL!)” to delete all mods.

EAC Bypass: This tool disables EasyAntiCheat by patching settings.json. **Only use on private/modded servers.** Trying to join official or EAC servers will not work and get you kicked on join. Only install mods from trusted sources. If something goes wrong or restoring vanilla fails, use the reset buttons or verify your game files in Steam.

Troubleshooting: If your mod doesn’t show up, check that it’s in the correct `Mods/<ModSet>/` folder. If the game doesn’t work right, use “Reset to Vanilla” or verify files in Steam.

How to build from source: 

Requierments:
Download modclient.py
Have windows 10/11
Phyton 3.10+
pyinstaller installed with this command if you have Phyton 3.10+ in cmd: pip install psutil pyinstaller

Building:
Run command py -m PyInstaller --onefile --noconsole "ModClient.py" in cmd in the file location of your mod file or add the entire location of the ModClient.py file 

File structure(CAN BE FOUND IN THE GITHUB OR THE RELEASES):
<your folder with the exe>/
  The Isle Mod Client.exe           # or modclient.exe from /dist
  Mods/
    settings.json                   # MODDED settings.json (used on launch)
    dsound.dll                      # copied to Binaries/Win64 at launch (if present)
    sig.lua                         # copied to Binaries/Win64/Bitfix at launch (if present)
    <YourModSetName>/
      *.pak                         # iostore or legacy files
      *.ucas
      *.utoc
      ... (any per-mod files)
  Defaults/
    settings.json                   # VANILLA settings.json (backup used to restore)


Discord: https://discord.gg/WxJc2NTVjC
