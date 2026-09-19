"""Builds the first-pass production weapons (rifle + utility grenade) and exports them to Unity.

Run headless:
  blender --background --factory-startup --python art/blender/scripts/make_weapons.py
"""

import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import bma_common as C  # noqa: E402

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(SCRIPT_DIR, '..', '..', '..'))
ASSETS = os.path.join(REPO, 'Assets', 'Art')
BLEND = os.path.join(REPO, 'art', 'blender', 'blend')


def build_rifle():
    C.reset_scene()
    metal = C.material('BMA_Weapon_Metal', (0.14, 0.15, 0.17), 0.75, 0.35)
    grip = C.material('BMA_Grip', (0.08, 0.09, 0.10), 0.10, 0.75)
    accent = C.material('BMA_Accent_Cool', (0.15, 0.75, 0.85), 0.30, 0.40)

    C.box('Receiver', (0, -0.10, 0.02), (0.07, 0.34, 0.10), metal, 0.01)
    C.box('Handguard', (0, -0.34, 0.03), (0.055, 0.22, 0.07), grip, 0.01)
    C.cylinder('Barrel', (0, -0.52, 0.03), 0.018, 0.30, 'Y', metal, seg=12)
    C.cylinder('Muzzle', (0, -0.68, 0.03), 0.026, 0.06, 'Y', grip, seg=12)
    C.box('Stock', (0, 0.13, 0.0), (0.055, 0.22, 0.10), grip, 0.02)
    C.box('StockPad', (0, 0.25, 0.0), (0.06, 0.03, 0.12), metal, 0.01)
    C.box('Magazine', (0, -0.06, -0.11), (0.05, 0.10, 0.15), grip, 0.01)
    C.box('Grip', (0, 0.05, -0.11), (0.05, 0.07, 0.14), grip, 0.01)
    C.box('Rail', (0, -0.14, 0.08), (0.03, 0.24, 0.02), metal, 0.005)
    C.box('Sight', (0, -0.08, 0.11), (0.035, 0.10, 0.05), accent, 0.005)

    C.unwrap_all()
    C.report_bounds()
    C.save_blend(os.path.join(BLEND, 'Weapons', 'BMA_Weapon_Rifle.blend'))
    C.export_fbx(os.path.join(ASSETS, 'Weapons', 'BMA_Weapon_Rifle.fbx'))


def build_grenade():
    C.reset_scene()
    metal = C.material('BMA_Weapon_Metal', (0.14, 0.15, 0.17), 0.75, 0.35)
    accent = C.material('BMA_Accent_Warm', (0.85, 0.45, 0.15), 0.30, 0.40)

    C.sphere('GrenadeBody', (0, 0, 0.06), 0.06, metal, scale=(1.0, 1.0, 1.1))
    C.cylinder('GrenadeCap', (0, 0, 0.14), 0.028, 0.05, 'Z', accent, seg=12)
    C.cylinder('GrenadePin', (0.05, 0, 0.15), 0.006, 0.06, 'X', accent, seg=8)
    C.box('GrenadeLever', (0, -0.045, 0.14), (0.02, 0.06, 0.02), accent, 0.004)

    C.unwrap_all()
    C.report_bounds()
    C.save_blend(os.path.join(BLEND, 'Weapons', 'BMA_Utility_Grenade.blend'))
    C.export_fbx(os.path.join(ASSETS, 'Weapons', 'BMA_Utility_Grenade.fbx'))


def main():
    build_rifle()
    build_grenade()
    print('[bma] weapons done')


main()
