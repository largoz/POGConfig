"""Build POGConfig (Release) and copy the DLL into the game's Mods folder.

The game folder is, in order:
  1. the POG_GAME_DIR environment variable, or
  2. the first parent of this script that holds "Pit of Goblin.exe"
     (the old layout, <game>/ModSource/POGConfig, and the _Modding clone both work).
Run from anywhere:  python build_and_deploy.py
"""
import os
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
CSPROJ = HERE / "POGConfig.csproj"
DLL = HERE / "bin" / "Release" / "net6.0" / "POGConfig.dll"
GAME_EXE = "Pit of Goblin.exe"


def find_game_dir() -> Path:
    env = os.environ.get("POG_GAME_DIR")
    if env:
        p = Path(env).expanduser()
        if not (p / GAME_EXE).exists():
            sys.exit(f"[ERROR] POG_GAME_DIR={p} does not contain {GAME_EXE}")
        return p
    for parent in HERE.parents:
        if (parent / GAME_EXE).exists():
            return parent
    sys.exit(f"[ERROR] No parent folder of {HERE} contains {GAME_EXE}. "
             "Set POG_GAME_DIR to the game folder.")


def find_dotnet() -> str:
    found = shutil.which("dotnet")
    if found:
        return found
    home = Path.home() / ".dotnet" / ("dotnet.exe" if os.name == "nt" else "dotnet")
    if home.exists():
        return str(home)
    sys.exit("[ERROR] dotnet SDK not found (not on PATH, not in ~/.dotnet).")


def main():
    game = find_game_dir()
    print(f"[BUILD] {CSPROJ.name} against {game}")
    rc = subprocess.run([find_dotnet(), "build", str(CSPROJ), "-c", "Release",
                         f"-p:GameDir={game}"]).returncode
    if rc != 0:
        sys.exit("[ERROR] Build failed. Nothing deployed.")
    if not DLL.exists():
        sys.exit(f"[ERROR] Built DLL not found: {DLL}")

    mods = game / "Mods"
    mods.mkdir(exist_ok=True)
    try:
        shutil.copy2(DLL, mods / DLL.name)
    except PermissionError:
        sys.exit(f"[ERROR] Cannot overwrite {mods / DLL.name}. Is the game still running?")
    print(f"[OK] Deployed to {mods / DLL.name}")


if __name__ == "__main__":
    main()
