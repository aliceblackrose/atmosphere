#!/usr/bin/env python3
"""Fail Windows packaging if Atmosphere.exe lacks a real Win32 application icon."""
from pathlib import Path
import sys

import pefile

if len(sys.argv) != 2:
    raise SystemExit("Usage: python tools/verify_win_icon.py <exe>")

exe_path = Path(sys.argv[1])
if not exe_path.is_file():
    raise SystemExit(f"Missing Windows executable: {exe_path}")

pe = pefile.PE(str(exe_path), fast_load=False)
pe.parse_data_directories(
    directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_RESOURCE"]]
)
ids = {entry.id for entry in getattr(pe, "DIRECTORY_ENTRY_RESOURCE", []).entries}
# 3 = RT_ICON and 14 = RT_GROUP_ICON, both are needed for Explorer icon display.
if 3 not in ids or 14 not in ids:
    raise SystemExit(
        f"{exe_path.name} is missing embedded Windows icon resources (found: {sorted(ids)})"
    )

print(f"Verified embedded Windows app icon in {exe_path.name}")
