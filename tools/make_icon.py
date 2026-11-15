#!/usr/bin/env python3
"""Create a multi-resolution Windows app icon from the repository's brand artwork.

Requires Pillow: python -m pip install Pillow
Run from anywhere: python tools/make_icon.py
"""
from pathlib import Path

from PIL import Image, ImageOps

root = Path(__file__).resolve().parent.parent
source = root / "icon.png"
output = root / "src" / "Atmosphere" / "Assets" / "Atmosphere.ico"
output.parent.mkdir(parents=True, exist_ok=True)

with Image.open(source) as source_image:
    rgba = source_image.convert("RGBA")
    # Preserve the whole original symbol instead of cropping its glow.
    square = ImageOps.contain(rgba, (256, 256), Image.Resampling.LANCZOS)
    icon = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    icon.alpha_composite(square, ((256 - square.width) // 2, (256 - square.height) // 2))
    icon.save(output, format="ICO",
              sizes=[(16, 16), (24, 24), (32, 32), (48, 48),
                     (64, 64), (128, 128), (256, 256)])
print(f"Created {output.relative_to(root)}")
