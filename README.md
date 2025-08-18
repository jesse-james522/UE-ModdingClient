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
Create these folders beside your built EXE. These are required for the client’s install/restore flow:

Defaults/settings.json
Your vanilla (original) The Isle settings.json.
The client copies this back when you reset to vanilla or when the game closes.

Mods/settings.json
Your modded settings.json.
The client copies this into The Isle/EasyAntiCheat/Settings.json on launch.

Mods/dsound.dll (optional)
Copied to The Isle/TheIsle/Binaries/Win64/dsound.dll on launch, removed on reset.

Mods/sig.lua (optional)
Copied to The Isle/TheIsle/Binaries/Win64/Bitfix/sig.lua on launch, removed on reset.

Mods/<YourModSetName>/
Put your per-mod game files here, such as .pak, .ucas, .utoc.
The client copies/renames them into .../Content/Paks as needed (e.g., mymod_P.pak, etc.).
Select this mod set in the launcher before starting the game.

Example layout (shown as lines, not a collapsible block):

The Isle Mod Client.exe

Defaults/settings.json

Mods/settings.json

Mods/dsound.dll 

Mods/sig.lua 

Mods/TestMod/ with .pak/.ucas/.utoc files inside(can be auto installed via a button click from the preset github URL if you dont change that in the source)


Discord: https://discord.gg/WxJc2NTVjC
