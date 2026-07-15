import os
import subprocess
import shutil
import sys

# --- Налаштування шляхів ---
# Raw-рядки (r"...") допомагають уникнути проблем з бекслешами у Windows
CSPROJ_PATH = r"D:\Steam\steamapps\common\Pit of Goblin\ModSource\POGConfig\POGConfig.csproj"
DLL_SOURCE  = r"D:\Steam\steamapps\common\Pit of Goblin\ModSource\POGConfig\bin\Release\net6.0\POGConfig.dll"
MODS_DIR    = r"D:\Steam\steamapps\common\Pit of Goblin\Mods"
DLL_DEST    = os.path.join(MODS_DIR, "POGConfig.dll")

def build_project():
    print(f"[BUILD] Building project: {os.path.basename(CSPROJ_PATH)}...")
    
    # Викликаємо dotnet CLI. Використовуємо Release конфігурацію
    result = subprocess.run(["dotnet", "build", CSPROJ_PATH, "-c", "Release"], text=True)
    
    if result.returncode != 0:
        print("\n[ERROR] Build failed. Deploy cancelled.")
        sys.exit(1)
        
    print("\n[OK] Build succeeded.")

def deploy_dll():
    if not os.path.exists(DLL_SOURCE):
        print(f"[ERROR] Built DLL not found at:\n{DLL_SOURCE}")
        sys.exit(1)

    # Створюємо папку Mods, якщо її раптом немає (наприклад, після перевстановлення гри)
    os.makedirs(MODS_DIR, exist_ok=True)
    
    print(f"[DEPLOY] Copying DLL to Mods:\n-> {MODS_DIR}")
    try:
        shutil.copy2(DLL_SOURCE, DLL_DEST)
        print("\n[OK] Deploy complete.")
    except PermissionError:
        print(f"\n[ERROR] ACCESS DENIED: Cannot overwrite '{DLL_DEST}'.")
        print("[HINT] The game may still be running. Close it and try again.")
    except Exception as e:
        print(f"\n[ERROR] Unexpected copy error: {e}")

if __name__ == "__main__":
    print("=" * 50)
    build_project()
    deploy_dll()
    print("=" * 50)