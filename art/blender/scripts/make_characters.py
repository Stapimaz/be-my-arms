"""Builds the first-pass production P1 and P2 character skins and exports them to Unity.

Run headless:
  blender --background --factory-startup --python art/blender/scripts/make_characters.py

These are coherent stylized/readable production *tests*, not a locked final art direction.
P1 owns the body (head/torso/legs); P2 owns an independent upper-body/arms/head layer that
mounts at Cosmetic_P2 and must never conceal P1's head.
"""

import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import bma_common as C  # noqa: E402

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(SCRIPT_DIR, '..', '..', '..'))
ASSETS = os.path.join(REPO, 'Assets', 'Art')
BLEND = os.path.join(REPO, 'art', 'blender', 'blend')


def _mats(suit_name, accent_name):
    suit = C.material(suit_name, (0.20, 0.45, 0.55), 0.0, 0.7)
    armor = C.material('BMA_Armor', (0.11, 0.12, 0.14), 0.25, 0.55)
    accent = C.material(accent_name, (0.15, 0.75, 0.85), 0.30, 0.40)
    visor = C.material('BMA_Visor', (0.05, 0.25, 0.40), 0.60, 0.20)
    return suit, armor, accent, visor


def build_p1(name, suit_name, accent_name, heavy):
    C.reset_scene()
    suit, armor, accent, visor = _mats(suit_name, accent_name)
    w = 1.22 if heavy else 1.0

    C.box('Hips', (0, 0, 0.95), (0.40 * w, 0.28, 0.24), armor, 0.02)
    C.box('Belt', (0, 0, 1.03), (0.44 * w, 0.30, 0.07), accent, 0.01)
    C.box('Torso', (0, 0, 1.23), (0.48 * w, 0.30, 0.44), suit, 0.03, taper=0.84)
    C.box('ChestPlate', (0, -0.15, 1.36), (0.42 * w, 0.09, 0.34), armor, 0.02)

    if heavy:
        for side in (-1, 1):
            C.sphere('ShoulderPad_%d' % side, (side * 0.34, 0.0, 1.44), 0.16, armor, scale=(1.0, 0.9, 0.7))
        C.box('Backpack', (0, 0.20, 1.28), (0.34, 0.16, 0.42), armor, 0.02)
        C.box('BackAccent', (0, 0.29, 1.28), (0.20, 0.04, 0.24), accent, 0.01)

    C.cylinder('Neck', (0, 0, 1.55), 0.07, 0.14, 'Z', suit, seg=12)
    C.sphere('Head', (0, -0.01, 1.70), 0.15, suit, scale=(1.0, 1.05, 1.12))
    C.box('HeadCowl', (0, 0.03, 1.72), (0.25, 0.22, 0.17), armor, 0.02)
    C.box('Visor', (0, -0.14, 1.70), (0.20, 0.05, 0.09), visor, 0.01)

    for side in (-1, 1):
        x = side * 0.13 * w
        C.cylinder('Thigh_%d' % side, (x, 0, 0.66), 0.095, 0.50, 'Z', suit, seg=14, radius2=0.085)
        C.cylinder('Shin_%d' % side, (x, 0, 0.28), 0.080, 0.44, 'Z', armor, seg=14, radius2=0.070)
        C.box('Foot_%d' % side, (x, -0.06, 0.05), (0.16, 0.30, 0.10), armor, 0.02)

    C.unwrap_all()
    C.report_bounds()
    C.save_blend(os.path.join(BLEND, 'P1', name + '.blend'))
    C.export_fbx(os.path.join(ASSETS, 'Characters', 'P1', name + '.fbx'))


def build_p2(name, suit_name, accent_name, heavy):
    C.reset_scene()
    suit, armor, accent, visor = _mats(suit_name, accent_name)

    # Local space: origin is the P2 mount (the shoulder anchor). Unity relative -> Blender local
    # is (x, -z, y).
    def rel(x, y, z):
        return (x, -z, y)

    C.box('Collar', rel(0, 0.02, 0.0), (0.42, 0.34, 0.22), armor, 0.02)
    C.box('CollarTrim', rel(0, -0.02, 0.12), (0.30, 0.24, 0.06), accent, 0.01)
    for side in (-1, 1):
        C.sphere('ShoulderPod_%d' % side, rel(side * 0.25, 0.0, 0.02), 0.12, armor, scale=(1.0, 0.9, 0.85))
        C.cylinder('UpperArm_%d' % side, rel(side * 0.34, -0.07, -0.05), 0.075, 0.26, 'X', suit, seg=12)
        C.sphere('Elbow_%d' % side, rel(side * 0.40, -0.17, -0.10), 0.085, armor)
        C.cylinder('Forearm_%d' % side, rel(side * 0.24, -0.24, -0.13), 0.070, 0.26, 'Y', suit, seg=12)
        C.box('Hand_%d' % side, rel(side * 0.10, -0.13, 0.29), (0.11, 0.13, 0.11), armor, 0.02)

    # P2's own sensor head sits behind and to the side so it never conceals P1's head.
    C.box('P2Head', rel(0.15, 0.16, -0.18), (0.22, 0.20, 0.20), armor, 0.02)
    C.box('P2Visor', rel(0.15, 0.06, -0.18), (0.14, 0.04, 0.07), visor, 0.01)
    C.cylinder('P2Antenna', rel(0.15, 0.16, -0.02), 0.02, 0.20, 'Z', accent, seg=8)

    if heavy:
        C.box('P2Backpack', rel(0, 0.18, -0.02), (0.30, 0.20, 0.24), armor, 0.02)
        C.box('P2BackAccent', rel(0, 0.28, -0.02), (0.16, 0.04, 0.14), accent, 0.01)

    C.unwrap_all()
    C.report_bounds()
    C.save_blend(os.path.join(BLEND, 'P2', name + '.blend'))
    C.export_fbx(os.path.join(ASSETS, 'Characters', 'P2', name + '.fbx'))


def main():
    build_p1('BMA_P1_Ranger', 'BMA_Suit_Alpha', 'BMA_Accent_Cool', heavy=False)
    build_p1('BMA_P1_Brute', 'BMA_Suit_Beta', 'BMA_Accent_Warm', heavy=True)
    build_p2('BMA_P2_Scout', 'BMA_Suit_Alpha', 'BMA_Accent_Cool', heavy=False)
    build_p2('BMA_P2_Heavy', 'BMA_Suit_Beta', 'BMA_Accent_Warm', heavy=True)
    print('[bma] characters done')


main()
