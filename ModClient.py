import os
import shutil
import subprocess
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
import threading
import json
import time
import psutil
import winreg
import datetime
import urllib.request
import zipfile
import io

CONFIG_FILE = "modclient_config.json"
LOG_FILE = "modclient.log"

def write_log(msg):
    timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    with open(LOG_FILE, "a", encoding="utf-8") as f:
        f.write(f"[{timestamp}] {msg}\n")
    print(msg)

def find_steam_path():
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\\Valve\\Steam") as key:
            steam_path, _ = winreg.QueryValueEx(key, "SteamPath")
            return steam_path
    except Exception:
        return None

def find_the_isle():
    steam_path = find_steam_path()
    library_folders = []
    if steam_path:
        library_folders.append(os.path.join(steam_path, "steamapps", "common"))
        vdf_path = os.path.join(steam_path, "steamapps", "libraryfolders.vdf")
        if os.path.exists(vdf_path):
            with open(vdf_path, encoding="utf-8") as f:
                for line in f:
                    if '"' in line and ":\\" in line:
                        parts = line.split('"')
                        for part in parts:
                            if ":\\" in part:
                                lib = part.strip().replace("\\\\", "\\")
                                library_folders.append(os.path.join(lib, "steamapps", "common"))
    for lib in library_folders:
        possible = os.path.join(lib, "The Isle")
        if os.path.isdir(possible):
            return possible
    return None

def save_config(config):
    with open(CONFIG_FILE, "w") as f:
        json.dump(config, f)

def load_config():
    if os.path.exists(CONFIG_FILE):
        with open(CONFIG_FILE, "r") as f:
            return json.load(f)
    return {}

def select_isle_folder():
    folder = filedialog.askdirectory(title="Select The Isle Root Folder")
    if folder:
        entry_isle_path.delete(0, tk.END)
        entry_isle_path.insert(0, folder)

def get_mod_folders():
    mods_path = os.path.join(os.getcwd(), "Mods")
    if not os.path.exists(mods_path):
        return []
    return [name for name in os.listdir(mods_path) if os.path.isdir(os.path.join(mods_path, name))]

def is_process_running(process_names):
    for proc in psutil.process_iter(['name']):
        try:
            if proc.info['name'] is not None:
                for n in process_names:
                    if n.lower() in proc.info['name'].lower():
                        return True
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            continue
    return False

def wait_for_main_game_exe(timeout=60):
    main_game_names = [
        "TheIsleClient-Win64-Shipping.exe", "theisleclient-win64-shipping.exe"
    ]
    waited = 0
    check_interval = 0.5
    while waited < timeout:
        if is_process_running(main_game_names):
            write_log("Main game exe appeared!")
            return True
        time.sleep(check_interval)
        waited += check_interval
    write_log("Main game exe did NOT appear within timeout.")
    return False

def wait_for_main_game_exe_exit_with_grace(grace_period=5):
    main_game_names = [
        "TheIsleClient-Win64-Shipping.exe", "theisleclient-win64-shipping.exe"
    ]
    write_log(f"Waiting for main game exe to fully exit (must stay closed for {grace_period}s)...")
    while True:
        while is_process_running(main_game_names):
            time.sleep(1)
        gone_since = time.time()
        while True:
            if is_process_running(main_game_names):
                write_log("Main game exe relaunched within grace period, waiting again.")
                break
            if time.time() - gone_since >= grace_period:
                write_log(f"Main game exe has been closed for {grace_period} seconds.")
                return
            time.sleep(0.5)

def restore_vanilla_settings(default_settings, settings_path, binaries_dir=None):
    ok = True
    if not os.path.exists(default_settings):
        write_log("No vanilla settings.json found!")
        ok = False
    else:
        try:
            shutil.copy2(default_settings, settings_path)
            write_log("Restored vanilla settings.json.")
        except Exception as e:
            write_log(f"Failed to restore vanilla settings.json: {e}")
            ok = False

    if binaries_dir:
        dsound_path = os.path.join(binaries_dir, "dsound.dll")
        if os.path.exists(dsound_path):
            try:
                os.remove(dsound_path)
                write_log("Deleted dsound.dll.")
            except Exception as e:
                write_log(f"Failed to delete dsound.dll: {e}")
                ok = False
        else:
            write_log("dsound.dll not found, nothing to delete.")
        # Also remove Bitfix/sig.lua if present (keeping prior behavior)
        bitfix_dir = os.path.join(binaries_dir, "Bitfix")
        siglua = os.path.join(bitfix_dir, "sig.lua")
        if os.path.exists(siglua):
            try:
                os.remove(siglua)
                write_log("Deleted Bitfix/sig.lua.")
            except Exception as e:
                write_log(f"Failed to delete Bitfix/sig.lua: {e}")
                ok = False
    return ok

def download_mod_files():
    mods_path = os.path.join(os.getcwd(), "Mods")
    url = git_url_entry.get().strip()
    status.set("Downloading latest mod files...")
    write_log(f"Starting mod download from {url}")
    try:
        response = urllib.request.urlopen(url)
        # ZIP detection
        is_zip = url.lower().endswith('.zip') or response.info().get('Content-Type', '').startswith('application/zip')
        if is_zip:
            with zipfile.ZipFile(io.BytesIO(response.read())) as z:
                topdir = z.namelist()[0].split("/")[0]
                for member in z.namelist():
                    if member.endswith('/'):
                        continue
                    rel_path = os.path.relpath(member, topdir)
                    if rel_path == ".":
                        continue
                    target_path = os.path.join(mods_path, rel_path)
                    os.makedirs(os.path.dirname(target_path), exist_ok=True)
                    with z.open(member) as src, open(target_path, "wb") as dst:
                        shutil.copyfileobj(src, dst)
            status.set("Latest mod files installed (zip)!")
            write_log("Latest mod files installed from zip.")
        else:
            filename = os.path.basename(url)
            if not filename:
                filename = "downloaded_file"
            dest_path = os.path.join(mods_path, filename)
            with open(dest_path, "wb") as out:
                out.write(response.read())
            status.set(f"Downloaded {filename} to Mods/")
            write_log(f"Downloaded {filename} to Mods/ (not zip)")
        refresh_mods()
    except Exception as e:
        status.set("Failed to download mod files.")
        write_log(f"Failed to download/extract mod: {e}")
        messagebox.showerror("Error", f"Failed to download/extract mod:\n{e}")

# ---------- NEW: UE4SS Helpers ----------
def copy_tree(src_root, dst_root):
    for rootdir, dirs, files in os.walk(src_root):
        rel = os.path.relpath(rootdir, src_root)
        target_dir = os.path.join(dst_root, rel) if rel != "." else dst_root
        os.makedirs(target_dir, exist_ok=True)
        for f in files:
            s = os.path.join(rootdir, f)
            d = os.path.join(target_dir, f)
            try:
                shutil.copy2(s, d)
                write_log(f"Copied tool: {s} -> {d}")
            except Exception as e:
                write_log(f"Failed to copy tool file {s}: {e}")

def remove_tree_mirror(src_root, dst_root):
    """Remove in dst_root whatever exists in src_root (mirror delete)."""
    if not os.path.isdir(src_root):
        return
    for rootdir, dirs, files in os.walk(src_root, topdown=False):
        rel = os.path.relpath(rootdir, src_root)
        target_dir = os.path.join(dst_root, rel) if rel != "." else dst_root
        # Remove files
        for f in files:
            dfile = os.path.join(target_dir, f)
            if os.path.exists(dfile):
                try:
                    os.remove(dfile)
                    write_log(f"Removed UE4SS file: {dfile}")
                except Exception as e:
                    write_log(f"Failed to remove UE4SS file {dfile}: {e}")
        # Try to prune empty dirs
        if os.path.isdir(target_dir):
            try:
                if not os.listdir(target_dir):
                    os.rmdir(target_dir)
                    write_log(f"Removed empty dir: {target_dir}")
            except Exception:
                pass

def install_ue4ss_tools():
    isle_path = entry_isle_path.get().strip()
    if not os.path.isdir(isle_path):
        messagebox.showerror("Error", "The Isle folder does not exist.")
        return
    tools_src = os.path.join(os.getcwd(), "UE4SS_Tools")
    if not os.path.isdir(tools_src):
        messagebox.showerror("Error", "UE4SS_Tools folder not found next to the client.")
        return
    binaries_dir = os.path.join(isle_path, "TheIsle", "Binaries", "Win64")
    os.makedirs(binaries_dir, exist_ok=True)
    copy_tree(tools_src, binaries_dir)
    messagebox.showinfo("UE4SS Tools", "UE4SS_Tools installed to Win64.")

def mod_workflow(isle_path, mod_folder, defaults_folder, status_callback):
    mods_dir = os.path.join(os.getcwd(), "Mods")

    default_settings = os.path.join(defaults_folder, "settings.json")
    eac_dir = os.path.join(isle_path, "EasyAntiCheat")
    binaries_dir = os.path.join(isle_path, "TheIsle", "Binaries", "Win64")
    bitfix_dir = os.path.join(binaries_dir, "Bitfix")
    paks_dir = os.path.join(isle_path, "TheIsle", "Content", "Paks")
    settings_path = os.path.join(eac_dir, "Settings.json")

    # Copy settings.json from /Mods to game
    src_settings = os.path.join(mods_dir, "settings.json")
    if os.path.exists(src_settings):
        try:
            shutil.copy2(src_settings, settings_path)
            write_log(f"Copied settings.json from {src_settings} to {settings_path}")
        except Exception as e:
            write_log(f"Failed to copy settings.json: {e}")
    else:
        write_log(f"settings.json not found in {mods_dir}")

    # Copy dsound.dll from /Mods to game
    src_dsound = os.path.join(mods_dir, "dsound.dll")
    dst_dsound = os.path.join(binaries_dir, "dsound.dll")
    if os.path.exists(src_dsound):
        try:
            shutil.copy2(src_dsound, dst_dsound)
            write_log(f"Copied dsound.dll from {src_dsound} to {dst_dsound}")
        except Exception as e:
            write_log(f"Failed to copy dsound.dll: {e}")
    else:
        write_log(f"dsound.dll not found in {mods_dir}")

    # Copy sig.lua from /Mods to Bitfix folder
    os.makedirs(bitfix_dir, exist_ok=True)
    src_sig = os.path.join(mods_dir, "sig.lua")
    dst_sig = os.path.join(bitfix_dir, "sig.lua")
    if os.path.exists(src_sig):
        try:
            shutil.copy2(src_sig, dst_sig)
            write_log(f"Copied sig.lua from {src_sig} to {dst_sig}")
        except Exception as e:
            write_log(f"Failed to copy sig.lua: {e}")
    else:
        write_log(f"sig.lua not found in {mods_dir}")

    # Copy other .lua files from /Mods (except sig.lua)
    for fname in os.listdir(mods_dir):
        if fname.lower().endswith('.lua') and fname.lower() != "sig.lua":
            src = os.path.join(mods_dir, fname)
            dst = os.path.join(bitfix_dir, fname)
            try:
                shutil.copy2(src, dst)
                write_log(f"Copied {fname} to {bitfix_dir}")
            except Exception as e:
                write_log(f"Failed to copy {fname}: {e}")

    # Copy per-mod files (e.g. .pak, .ucas, .utoc) from /Mods/[MOD]
    for fname in os.listdir(mod_folder):
        ext = os.path.splitext(fname)[1].lower()
        if ext in ('.pak', '.ucas', '.utoc'):
            src = os.path.join(mod_folder, fname)
            dst = os.path.join(paks_dir, f"mymod_P{ext}")
            try:
                shutil.copy2(src, dst)
                write_log(f"Copied and renamed {fname} to {dst}")
            except Exception as e:
                write_log(f"Failed to copy {fname}: {e}")

    # --- Minimize on Launch (checkbox support) ---
    if minimize_on_launch.get():
        root.iconify()

    # Launch the game (Steam/EAC launcher step)
    launcher_path = os.path.join(isle_path, "theisle.exe")
    if not os.path.exists(launcher_path):
        messagebox.showerror("Error", "Cannot find theisle.exe in The Isle root folder.")
        write_log("theisle.exe not found in root!")
        if minimize_on_launch.get():
            root.deiconify()
            root.lift()
            root.focus_force()
        return

    status_callback("Launching The Isle (Steam/EAC)...")
    write_log("Launching Steam/EAC launcher...")
    subprocess.Popen([launcher_path])

    status_callback("Waiting for main game to start (max 60s)...")
    appeared = wait_for_main_game_exe(timeout=60)
    if not appeared:
        reverted = restore_vanilla_settings(
            os.path.join(defaults_folder, "settings.json"),
            settings_path,
            binaries_dir=binaries_dir
        )
        # Also remove any UE4SS tools mirrored from UE4SS_Tools
        remove_tree_mirror(os.path.join(os.getcwd(), "UE4SS_Tools"), binaries_dir)

        if reverted:
            status_callback("Game failed to start (main exe not found, reverted). Files reverted!")
            write_log("Game failed to start (main exe not found, reverted!)")
        else:
            status_callback("Game failed to start (main exe not found, NOT reverted!)")
            write_log("Game failed to start (main exe not found, NOT reverted!)")
        if minimize_on_launch.get():
            root.deiconify()
            root.lift()
            root.focus_force()
        return

    status_callback("Game running... waiting for full shutdown.")
    wait_for_main_game_exe_exit_with_grace(grace_period=5)
    reverted = restore_vanilla_settings(
        os.path.join(defaults_folder, "settings.json"),
        settings_path,
        binaries_dir=binaries_dir
    )
    # Remove any UE4SS tools mirrored from UE4SS_Tools after shutdown
    remove_tree_mirror(os.path.join(os.getcwd(), "UE4SS_Tools"), binaries_dir)

    if reverted:
        status_callback("Game closed. Vanilla settings restored.")
        write_log("Game closed. Vanilla settings restored.")
    else:
        status_callback("Game closed. Vanilla settings NOT restored!")
        write_log("Game closed. Vanilla settings NOT restored!")

    # --- Restore GUI on game exit if minimized ---
    if minimize_on_launch.get():
        root.deiconify()
        root.lift()
        root.focus_force()

def install_and_launch():
    isle_path = entry_isle_path.get().strip()
    mod_name = combo_mod.get()
    if not os.path.isdir(isle_path):
        messagebox.showerror("Error", "The Isle folder does not exist.")
        return
    if not mod_name:
        messagebox.showerror("Error", "Please select a mod folder.")
        return

    config = {"isle_path": isle_path, "git_url": git_url_entry.get().strip()}
    save_config(config)

    mod_folder = os.path.join(os.getcwd(), "Mods", mod_name)
    defaults_folder = os.path.join(os.getcwd(), "Defaults")

    threading.Thread(target=mod_workflow, args=(isle_path, mod_folder, defaults_folder, status.set), daemon=True).start()

def refresh_mods():
    combo_mod['values'] = get_mod_folders()

# ---------- NEW: No EAC, No Mods workflow ----------
def no_mods_workflow():
    isle_path = entry_isle_path.get().strip()
    if not os.path.isdir(isle_path):
        messagebox.showerror("Error", "The Isle folder does not exist.")
        return
    defaults_folder = os.path.join(os.getcwd(), "Defaults")
    eac_dir = os.path.join(isle_path, "EasyAntiCheat")
    settings_path = os.path.join(eac_dir, "Settings.json")
    binaries_dir = os.path.join(isle_path, "TheIsle", "Binaries", "Win64")

    mods_dir = os.path.join(os.getcwd(), "Mods")
    mod_settings = os.path.join(mods_dir, "settings.json")
    if not os.path.exists(mod_settings):
        messagebox.showerror("Error", "Mods/settings.json not found (required to disable EAC).")
        return
    try:
        shutil.copy2(mod_settings, settings_path)
        write_log("[NoMods] Copied modded settings.json to disable EAC")
    except Exception as e:
        messagebox.showerror("Error", f"Failed to copy modded settings.json:\n{e}")
        return

    if minimize_on_launch.get():
        root.iconify()

    launcher_path = os.path.join(isle_path, "theisle.exe")
    if not os.path.exists(launcher_path):
        messagebox.showerror("Error", "Cannot find theisle.exe in The Isle root folder.")
        if minimize_on_launch.get():
            root.deiconify(); root.lift(); root.focus_force()
        return

    status.set("Launching The Isle (No EAC, No Mods)...")
    write_log("[NoMods] Launching…")
    subprocess.Popen([launcher_path])

    appeared = wait_for_main_game_exe(timeout=60)
    if not appeared:
        restore_vanilla_settings(os.path.join(defaults_folder, "settings.json"), settings_path, binaries_dir=binaries_dir)
        remove_tree_mirror(os.path.join(os.getcwd(), "UE4SS_Tools"), binaries_dir)
        if minimize_on_launch.get():
            root.deiconify(); root.lift(); root.focus_force()
        return

    status.set("Game running (No EAC, No Mods)… waiting for shutdown.")
    wait_for_main_game_exe_exit_with_grace(grace_period=5)
    restore_vanilla_settings(os.path.join(defaults_folder, "settings.json"), settings_path, binaries_dir=binaries_dir)
    remove_tree_mirror(os.path.join(os.getcwd(), "UE4SS_Tools"), binaries_dir)

    if minimize_on_launch.get():
        root.deiconify(); root.lift(); root.focus_force()

# ----- GUI -----
root = tk.Tk()
root.title("The Isle Mod Client")

frame = tk.Frame(root, padx=12, pady=12)
frame.pack()

tk.Label(frame, text="The Isle folder:").grid(row=0, column=0, sticky="w")
entry_isle_path = tk.Entry(frame, width=50)
entry_isle_path.grid(row=0, column=1)
btn_browse = tk.Button(frame, text="Browse...", command=select_isle_folder)
btn_browse.grid(row=0, column=2, padx=4)

tk.Label(frame, text="Select Mod:").grid(row=1, column=0, sticky="w")
combo_mod = ttk.Combobox(frame, values=get_mod_folders(), state="readonly", width=30)
combo_mod.grid(row=1, column=1)
btn_refresh = tk.Button(frame, text="Refresh_mod_list", command=refresh_mods)
btn_refresh.grid(row=1, column=2, padx=4)

btn_install = tk.Button(frame, text="Install & Launch", command=install_and_launch)
btn_install.grid(row=2, column=0, columnspan=2, pady=10)

# --- Minimize on launch option ---
minimize_on_launch = tk.BooleanVar(value=True)
chk_minimize = tk.Checkbutton(frame, text="Minimize launcher on game start", variable=minimize_on_launch)
chk_minimize.grid(row=2, column=2, padx=(10,0), sticky="w")

# --- GitHub download controls ---
btn_download = tk.Button(frame, text="Install most up to date mod files", command=lambda: threading.Thread(target=download_mod_files, daemon=True).start())
btn_download.grid(row=3, column=0, columnspan=3, pady=(5, 0))

disclaimer = tk.Label(frame, text="Warning: Only download from sources you trust!", fg="red")
disclaimer.grid(row=4, column=0, columnspan=3, pady=(0, 5))

tk.Label(frame, text="GitHub Mod Zip/File URL:").grid(row=5, column=0, sticky="w")
git_url_entry = tk.Entry(frame, width=50)
git_url_entry.grid(row=5, column=1, columnspan=2, sticky="we")
git_url_entry.insert(0, "https://github.com/jesse-james522/ClientMod/archive/refs/heads/main.zip")  # CHANGE TO YOUR REPO

status = tk.StringVar()
status.set("Ready.")
lbl_status = tk.Label(root, textvariable=status, anchor="w")
lbl_status.pack(fill="x", padx=12, pady=(0,8))

conf = load_config()
auto_path = find_the_isle()
if "isle_path" in conf:
    entry_isle_path.insert(0, conf["isle_path"])
elif auto_path:
    entry_isle_path.insert(0, auto_path)
else:
    entry_isle_path.insert(0, "")

if "git_url" in conf:
    git_url_entry.delete(0, tk.END)
    git_url_entry.insert(0, conf["git_url"])

# --- Reset buttons ---
def reset_vanilla_only():
    isle_path = entry_isle_path.get().strip()
    defaults_folder = os.path.join(os.getcwd(), "Defaults")
    binaries_dir = os.path.join(isle_path, "TheIsle", "Binaries", "Win64")
    eac_dir = os.path.join(isle_path, "EasyAntiCheat")
    settings_path = os.path.join(eac_dir, "Settings.json")
    ok = restore_vanilla_settings(
        os.path.join(defaults_folder, "settings.json"),
        settings_path,
        binaries_dir=binaries_dir
    )
    # Remove UE4SS files mirrored from UE4SS_Tools
    remove_tree_mirror(os.path.join(os.getcwd(), "UE4SS_Tools"), binaries_dir)

    if ok:
        messagebox.showinfo("Reset", "Restored vanilla settings and removed dsound.dll.")
    else:
        messagebox.showwarning("Reset", "No vanilla files were restored (check files exist or verify on Steam).")

def reset_full_wipe():
    isle_path = entry_isle_path.get().strip()
    binaries_dir = os.path.join(isle_path, "TheIsle", "Binaries", "Win64")  # ensure defined
    eac_dir = os.path.join(isle_path, "EasyAntiCheat")
    settings_path = os.path.join(eac_dir, "Settings.json")
    defaults_folder = os.path.join(os.getcwd(), "Defaults")
    ok = restore_vanilla_settings(
        os.path.join(defaults_folder, "settings.json"),
        settings_path,
        binaries_dir=binaries_dir
    )
    # Remove UE4SS files mirrored from UE4SS_Tools
    remove_tree_mirror(os.path.join(os.getcwd(), "UE4SS_Tools"), binaries_dir)
    # Remove ALL modded files in Mods folder (careful!)
    mods_dir = os.path.join(os.getcwd(), "Mods")
    if os.path.isdir(mods_dir):
        for rootdir, dirs, files in os.walk(mods_dir):
            for f in files:
                try:
                    os.remove(os.path.join(rootdir, f))
                except Exception:
                    pass
    if ok:
        messagebox.showinfo("Reset", "Restored vanilla settings, removed dsound.dll and wiped modded files.")
    else:
        messagebox.showwarning("Reset", "No vanilla files were restored (check files exist or verify on Steam).")

btn_reset1 = tk.Button(frame, text="Reset to Vanilla (settings only)", command=reset_vanilla_only)
btn_reset1.grid(row=6, column=0, columnspan=2, pady=(2,2))

btn_reset2 = tk.Button(frame, text="Full Wipe Modded Files (CAREFUL!)", fg="red", command=reset_full_wipe)
btn_reset2.grid(row=7, column=0, columnspan=2, pady=(2,8))

# ---------- NEW: Show/Hide Modding Tools ----------
tools_visible = tk.BooleanVar(value=False)
def toggle_tools():
    if tools_visible.get():
        tools_frame.grid(row=8, column=0, columnspan=3, sticky="we", pady=(6,0))
    else:
        tools_frame.grid_remove()

chk_tools = tk.Checkbutton(frame, text="Show modding tools", variable=tools_visible, command=toggle_tools)
chk_tools.grid(row=8, column=0, sticky="w", pady=(4,0))

tools_frame = tk.Frame(frame, bd=1, relief="groove", padx=8, pady=6)
# Tools: install UE4SS and launch no-EAC/no-mods
btn_install_ue4ss = tk.Button(tools_frame, text="Install UE4SS Tools to Win64", command=install_ue4ss_tools)
btn_install_ue4ss.grid(row=0, column=0, padx=(0,8), pady=(0,4), sticky="w")

btn_launch_no_mods = tk.Button(tools_frame, text="Launch (No EAC, No Mods)", command=lambda: threading.Thread(target=no_mods_workflow, daemon=True).start())
btn_launch_no_mods.grid(row=0, column=1, padx=(0,8), pady=(0,4), sticky="w")

# start hidden
tools_frame.grid_remove()

root.resizable(False, False)
root.mainloop()
