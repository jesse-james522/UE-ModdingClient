# **Universal UE Mod Client**

A simple, universal Windows mod launcher and mod manager for Unreal Engine (UE4/UE5) Steam games.

## **Features**

* Auto-detects installed UE games in all Steam libraries
* Supports both client and server launch exes, labeled in the UI
* Lets you select and use any mod set per game/server
* Easy mod installation from a GitHub ZIP or file URL
* Handles UE5 iostore and UE5/UE4 legacy files
* Automatic vanilla file backup and restoration

---

## **How to Use**

### **Launching the Mod Client**

* Double-click the `Universal UE Mod Client.exe` to start.
* It will auto-detect compatible Unreal Engine games installed via Steam.

### **Installing Mods from the Internet**

1. Copy your GitHub ZIP or direct download link to the field.
2. Select the target game in the “Install mods to:” dropdown.
3. Click “Install most up to date mod files” to automatically download and extract the mod into the correct folder.

### **Adding a Local Mod**

* Create a new folder for your mod inside:

  ```
  Mods/<GameName>/<YourModSetName>/
  ```
* Place your mod files (such as `.pak`, `.ucas`) in this folder.

> **Example:**
> To add a “TestMod” for “The Isle”, put your mod files in
> `Mods/The Isle/TestMod/`
> Then select “TestMod” as the mod set in the launcher UI.

### **Launching the Game With Mods**

1. Select the game in the dropdown.
2. Select the mod set you want to use.
3. Click “Install & Launch (Auto settings swap)” to load up the game with mods.

### **Resetting to Vanilla**

* Use “Reset to Vanilla” to restore only original settings for a game.
* Use “Full Wipe Modded Files (CAREFUL!)” to delete all modded files for a game.

---

## **Folder Structure**

* `Mods/<GameName>/<ModSet>/`
  (Put your mod’s `.pak`, `.ucas`, `settings.json`, etc. here)
* `Defaults/<GameName>/settings.json`
  (Auto-backed up vanilla/original settings on first launch)
* `Mods/dsound.dll`, `Mods/sig.lua`, more or less universal UE modding script that lets the game load extra files taken from: [https://github.com/trumank/bitfix](https://github.com/trumank/bitfix)

---

## **Notes**

* In EOS EAC games the EAC is disabled via edit of settings.json to allow for custom files, this means any server with EAC will not allow you to join, trying to force your way in will most likely lead to you being banned. I only intended for this to be used on servers that allow such modding.
* Both “client” and “server” exes are detected and listed separately to minimize confusion in case of having both downloaded.
* Only download or add mods from sources you trust.

---

## **Troubleshooting**

* If something goes wrong, or the vanilla restore fails, verify your game files in Steam.
* If your mod does not appear, ensure it is inside the correct subfolder:
  `Mods/<GameName>/<YourModSetName>/`

---
