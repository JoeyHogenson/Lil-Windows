"""
Pregame graphics settings launcher (Windows only), in the style of old PC game
launchers (Bethesda's Skyrim launcher, etc). Run this instead of the game .exe
directly: it shows a small native window to pick resolution, refresh rate,
monitor, window mode and quality, applies them, then starts the actual game.

No third-party packages required - only the Python standard library
(tkinter + ctypes talking directly to the Windows display APIs).
"""

import ctypes
import json
import os
import subprocess
import sys
import time
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
from ctypes import wintypes

LAUNCHER_DIR = os.path.dirname(os.path.abspath(__file__))
# Where to look for the game .exe, with no manual setup: next to this script (if it's
# shipped alongside the build), or in a sibling "Build" folder (Unity's usual build output
# location relative to the project). Whichever build you produce, as long as it lands in
# one of these, the launcher finds it on its own - no re-pointing needed after a rebuild.
EXE_SEARCH_DIRS = [LAUNCHER_DIR, os.path.normpath(os.path.join(LAUNCHER_DIR, "..", "Build"))]
IGNORED_EXE_NAMES = {"unitycrashhandler64.exe", "unitycrashhandler32.exe"}
QUALITY_LEVELS = ["Very Low", "Low", "Medium", "High", "Very High", "Ultra"]
CONFIG_PATH = os.path.join(LAUNCHER_DIR, "launcher_settings.json")


def find_game_exe():
    for directory in EXE_SEARCH_DIRS:
        if not os.path.isdir(directory):
            continue
        candidates = sorted(
            f for f in os.listdir(directory)
            if f.lower().endswith(".exe") and f.lower() not in IGNORED_EXE_NAMES
        )
        if len(candidates) == 1:
            return os.path.join(directory, candidates[0])
    return None

user32 = ctypes.windll.user32

# ---------------------------------------------------------------------------
# Win32 structures / constants
# ---------------------------------------------------------------------------

class RECT(ctypes.Structure):
    _fields_ = [
        ("left", wintypes.LONG),
        ("top", wintypes.LONG),
        ("right", wintypes.LONG),
        ("bottom", wintypes.LONG),
    ]


class MONITORINFOEXW(ctypes.Structure):
    _fields_ = [
        ("cbSize", wintypes.DWORD),
        ("rcMonitor", RECT),
        ("rcWork", RECT),
        ("dwFlags", wintypes.DWORD),
        ("szDevice", ctypes.c_wchar * 32),
    ]


class DEVMODE(ctypes.Structure):
    _fields_ = [
        ("dmDeviceName", ctypes.c_wchar * 32),
        ("dmSpecVersion", wintypes.WORD),
        ("dmDriverVersion", wintypes.WORD),
        ("dmSize", wintypes.WORD),
        ("dmDriverExtra", wintypes.WORD),
        ("dmFields", wintypes.DWORD),
        ("dmPositionX", wintypes.LONG),
        ("dmPositionY", wintypes.LONG),
        ("dmDisplayOrientation", wintypes.DWORD),
        ("dmDisplayFixedOutput", wintypes.DWORD),
        ("dmColor", ctypes.c_short),
        ("dmDuplex", ctypes.c_short),
        ("dmYResolution", ctypes.c_short),
        ("dmTTOption", ctypes.c_short),
        ("dmCollate", ctypes.c_short),
        ("dmFormName", ctypes.c_wchar * 32),
        ("dmLogPixels", wintypes.WORD),
        ("dmBitsPerPel", wintypes.DWORD),
        ("dmPelsWidth", wintypes.DWORD),
        ("dmPelsHeight", wintypes.DWORD),
        ("dmDisplayFlags", wintypes.DWORD),
        ("dmDisplayFrequency", wintypes.DWORD),
        ("dmICMMethod", wintypes.DWORD),
        ("dmICMIntent", wintypes.DWORD),
        ("dmMediaType", wintypes.DWORD),
        ("dmDitherType", wintypes.DWORD),
        ("dmReserved1", wintypes.DWORD),
        ("dmReserved2", wintypes.DWORD),
        ("dmPanningWidth", wintypes.DWORD),
        ("dmPanningHeight", wintypes.DWORD),
    ]


MONITORINFOF_PRIMARY = 0x1
ENUM_CURRENT_SETTINGS = -1
DM_PELSWIDTH = 0x80000
DM_PELSHEIGHT = 0x100000
DM_DISPLAYFREQUENCY = 0x400000
CDS_FULLSCREEN = 0x4
DISP_CHANGE_SUCCESSFUL = 0
SWP_NOZORDER = 0x0004
SWP_NOACTIVATE = 0x0010

# ---------------------------------------------------------------------------
# Display enumeration / mode switching
# ---------------------------------------------------------------------------

def enum_monitors():
    monitors = []
    MonitorEnumProc = ctypes.WINFUNCTYPE(
        ctypes.c_int, wintypes.HMONITOR, wintypes.HDC, ctypes.POINTER(RECT), wintypes.LPARAM
    )

    def callback(hMonitor, hdcMonitor, lprcMonitor, lParam):
        info = MONITORINFOEXW()
        info.cbSize = ctypes.sizeof(MONITORINFOEXW)
        user32.GetMonitorInfoW(hMonitor, ctypes.byref(info))
        monitors.append({
            "device": info.szDevice,
            "rect": (info.rcMonitor.left, info.rcMonitor.top, info.rcMonitor.right, info.rcMonitor.bottom),
            "primary": bool(info.dwFlags & MONITORINFOF_PRIMARY),
        })
        return 1

    user32.EnumDisplayMonitors(0, 0, MonitorEnumProc(callback), 0)
    monitors.sort(key=lambda m: not m["primary"])
    return monitors


def enum_resolutions(device_name):
    """Returns {(width, height): sorted list of refresh rates} for a device."""
    modes = {}
    i = 0
    while True:
        dm = DEVMODE()
        dm.dmSize = ctypes.sizeof(DEVMODE)
        if not user32.EnumDisplaySettingsW(device_name, i, ctypes.byref(dm)):
            break
        key = (dm.dmPelsWidth, dm.dmPelsHeight)
        modes.setdefault(key, set()).add(dm.dmDisplayFrequency)
        i += 1
    return {res: sorted(hz, reverse=True) for res, hz in modes.items()}


def get_current_mode(device_name):
    dm = DEVMODE()
    dm.dmSize = ctypes.sizeof(DEVMODE)
    user32.EnumDisplaySettingsW(device_name, ENUM_CURRENT_SETTINGS, ctypes.byref(dm))
    return dm.dmPelsWidth, dm.dmPelsHeight, dm.dmDisplayFrequency


def set_display_mode(device_name, width, height, hz):
    dm = DEVMODE()
    dm.dmSize = ctypes.sizeof(DEVMODE)
    user32.EnumDisplaySettingsW(device_name, ENUM_CURRENT_SETTINGS, ctypes.byref(dm))
    dm.dmPelsWidth = width
    dm.dmPelsHeight = height
    dm.dmDisplayFrequency = hz
    dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY
    return user32.ChangeDisplaySettingsExW(device_name, ctypes.byref(dm), None, CDS_FULLSCREEN, None) == DISP_CHANGE_SUCCESSFUL


def find_main_window(pid, timeout=15.0):
    result = []
    EnumWindowsProc = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)

    def callback(hwnd, lparam):
        if not user32.IsWindowVisible(hwnd):
            return True
        owner_pid = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(owner_pid))
        if owner_pid.value == pid and user32.GetWindow(hwnd, 4) == 0:  # GW_OWNER == 4, skip owned popups
            result.append(hwnd)
            return False
        return True

    deadline = time.time() + timeout
    while time.time() < deadline and not result:
        user32.EnumWindows(EnumWindowsProc(callback), 0)
        if not result:
            time.sleep(0.25)
    return result[0] if result else None


def move_window_to_monitor(hwnd, rect, width, height, cover_monitor):
    left, top, right, bottom = rect
    if cover_monitor:
        user32.SetWindowPos(hwnd, 0, left, top, width, height, SWP_NOZORDER | SWP_NOACTIVATE)
    else:
        mon_w, mon_h = right - left, bottom - top
        x = left + max(0, (mon_w - width) // 2)
        y = top + max(0, (mon_h - height) // 2)
        user32.SetWindowPos(hwnd, 0, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE)


# ---------------------------------------------------------------------------
# Config persistence
# ---------------------------------------------------------------------------

def load_config():
    if os.path.exists(CONFIG_PATH):
        try:
            with open(CONFIG_PATH, "r") as f:
                return json.load(f)
        except (json.JSONDecodeError, OSError):
            pass
    return {}


def save_config(data):
    with open(CONFIG_PATH, "w") as f:
        json.dump(data, f, indent=2)


# ---------------------------------------------------------------------------
# UI
# ---------------------------------------------------------------------------

class LauncherApp:
    def __init__(self, root):
        self.root = root
        self.config = load_config()
        self.game_exe = self.config.get("game_exe")
        if not self.game_exe or not os.path.exists(self.game_exe):
            self.game_exe = find_game_exe() or ""

        self.monitors = enum_monitors()
        self.monitor_labels = [
            f"Display {i + 1}{' (Primary)' if m['primary'] else ''}" for i, m in enumerate(self.monitors)
        ]

        root.title("Lil Windows - Graphics Settings")
        root.resizable(False, False)

        pad = {"padx": 10, "pady": 6}
        frame = ttk.Frame(root, padding=16)
        frame.grid()

        ttk.Label(frame, text="GRAPHICS SETTINGS", font=("Segoe UI", 14, "bold")).grid(
            row=0, column=0, columnspan=2, pady=(0, 12)
        )

        row = 1
        ttk.Label(frame, text="Display:").grid(row=row, column=0, sticky="w", **pad)
        self.display_var = tk.StringVar()
        self.display_box = ttk.Combobox(frame, textvariable=self.display_var, values=self.monitor_labels, state="readonly")
        self.display_box.grid(row=row, column=1, **pad)
        self.display_box.bind("<<ComboboxSelected>>", lambda e: self.on_display_changed())

        row += 1
        ttk.Label(frame, text="Resolution:").grid(row=row, column=0, sticky="w", **pad)
        self.res_var = tk.StringVar()
        self.res_box = ttk.Combobox(frame, textvariable=self.res_var, state="readonly")
        self.res_box.grid(row=row, column=1, **pad)
        self.res_box.bind("<<ComboboxSelected>>", lambda e: self.on_resolution_changed())

        row += 1
        ttk.Label(frame, text="Refresh Rate:").grid(row=row, column=0, sticky="w", **pad)
        self.hz_var = tk.StringVar()
        self.hz_box = ttk.Combobox(frame, textvariable=self.hz_var, state="readonly")
        self.hz_box.grid(row=row, column=1, **pad)

        row += 1
        ttk.Label(frame, text="Window Mode:").grid(row=row, column=0, sticky="w", **pad)
        self.mode_var = tk.StringVar()
        self.mode_box = ttk.Combobox(
            frame, textvariable=self.mode_var, state="readonly",
            values=["Fullscreen", "Borderless Window", "Windowed"],
        )
        self.mode_box.grid(row=row, column=1, **pad)

        row += 1
        ttk.Label(frame, text="Quality:").grid(row=row, column=0, sticky="w", **pad)
        self.quality_var = tk.StringVar()
        self.quality_box = ttk.Combobox(frame, textvariable=self.quality_var, state="readonly", values=QUALITY_LEVELS)
        self.quality_box.grid(row=row, column=1, **pad)

        row += 1
        self.exe_label = ttk.Label(frame, text=self._exe_display_text(), foreground="#666")
        self.exe_label.grid(row=row, column=0, columnspan=2, sticky="w", padx=10)

        row += 1
        btns = ttk.Frame(frame)
        btns.grid(row=row, column=0, columnspan=2, pady=(14, 0))
        ttk.Button(btns, text="Browse for game .exe...", command=self.browse_exe).pack(side="left", padx=4)
        ttk.Button(btns, text="Play", command=self.on_play).pack(side="left", padx=4)

        self._load_initial_selection()

    def _exe_display_text(self):
        if self.game_exe and os.path.exists(self.game_exe):
            return "Game: " + self.game_exe
        return "Game .exe not found automatically - use Browse below."

    def browse_exe(self):
        path = filedialog.askopenfilename(title="Locate game executable", filetypes=[("Executable", "*.exe")])
        if path:
            self.game_exe = path
            self.exe_label.config(text=self._exe_display_text())

    def _load_initial_selection(self):
        saved_display = self.config.get("display_index", 0)
        saved_display = saved_display if saved_display < len(self.monitors) else 0
        self.display_box.current(saved_display)
        self.on_display_changed(select_saved=True)

        self.mode_var.set(self.config.get("window_mode", "Fullscreen"))
        self.quality_var.set(self.config.get("quality", "High"))

    def on_display_changed(self, select_saved=False):
        idx = self.display_box.current()
        if idx < 0:
            idx = 0
        device = self.monitors[idx]["device"]
        self.modes = enum_resolutions(device)
        res_list = sorted(self.modes.keys(), key=lambda r: r[0] * r[1], reverse=True)
        self.res_options = [f"{w} x {h}" for w, h in res_list]
        self.res_box["values"] = self.res_options

        current_w, current_h, current_hz = get_current_mode(device)
        saved_res = self.config.get("resolution") if select_saved else None
        if saved_res and tuple(saved_res) in self.modes:
            target = tuple(saved_res)
        elif (current_w, current_h) in self.modes:
            target = (current_w, current_h)
        else:
            target = res_list[0] if res_list else (current_w, current_h)

        self.res_box.set(f"{target[0]} x {target[1]}")
        self.on_resolution_changed(preferred_hz=self.config.get("refresh_rate") if select_saved else current_hz)

    def on_resolution_changed(self, preferred_hz=None):
        w, h = self._parse_res(self.res_var.get())
        hz_list = self.modes.get((w, h), [60])
        self.hz_box["values"] = [f"{hz} Hz" for hz in hz_list]
        if preferred_hz in hz_list:
            self.hz_box.set(f"{preferred_hz} Hz")
        else:
            self.hz_box.set(f"{hz_list[0]} Hz")

    @staticmethod
    def _parse_res(text):
        w, h = text.split(" x ")
        return int(w), int(h)

    def on_play(self):
        if not os.path.exists(self.game_exe):
            messagebox.showerror("Game not found", "Locate the game .exe first (Browse button).")
            return

        display_idx = self.display_box.current()
        monitor = self.monitors[display_idx]
        width, height = self._parse_res(self.res_var.get())
        hz = int(self.hz_var.get().replace(" Hz", ""))
        window_mode = self.mode_var.get()
        quality = self.quality_var.get()

        save_config({
            "game_exe": self.game_exe,
            "display_index": display_idx,
            "resolution": [width, height],
            "refresh_rate": hz,
            "window_mode": window_mode,
            "quality": quality,
        })

        original_mode = get_current_mode(monitor["device"])
        set_display_mode(monitor["device"], width, height, hz)

        args = [self.game_exe, "-screen-width", str(width), "-screen-height", str(height), "-screen-quality", quality]
        if window_mode == "Fullscreen":
            args += ["-screen-fullscreen", "1"]
            if not monitor["primary"]:
                messagebox.showinfo(
                    "Heads up",
                    "Windows exclusive fullscreen normally opens on the primary display.\n"
                    "Pick Borderless Window instead to reliably target a secondary monitor.",
                )
        else:
            args += ["-screen-fullscreen", "0"]
            if window_mode == "Borderless Window":
                args += ["-popupwindow"]

        self.root.withdraw()
        try:
            proc = subprocess.Popen(args)

            if window_mode != "Fullscreen":
                hwnd = find_main_window(proc.pid)
                if hwnd:
                    move_window_to_monitor(
                        hwnd, monitor["rect"], width, height, cover_monitor=(window_mode == "Borderless Window")
                    )

            proc.wait()
        finally:
            set_display_mode(monitor["device"], *original_mode)
            self.root.deiconify()


def main():
    root = tk.Tk()
    LauncherApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
