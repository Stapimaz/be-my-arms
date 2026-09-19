"""Builds the first-pass production P1 and P2 character skins and exports them to Unity.

Run headless:
  blender --background --factory-startup --python art/blender/scripts/make_characters.py

Art direction (PROPOSED, see docs/M7_ART_DIRECTION.md): grounded stylized tactical sci-fi —
matte layered armour, high-contrast team accents, emissive visors, restrained panel detail, and
articulated mechanical joints (no bare ball joints). This is a coherent production-test direction,
not a locked final identity.

Anatomy contract (readability correction):
  * P1 is the main humanoid: head, neck, torso, pelvis, legs — NO arms. P1's head is the only
    critical/headshot region and must stay clearly exposed.
  * P2 is an upper-chest / clavicle / shoulder layer with two complete arms
    (shoulder -> upper arm -> elbow -> forearm -> wrist/hand). It is not a helmet, backpack or
    second head. Its hands sit on the weapon grip/handguard so the weapon reads as held.
  * P2 may carry a small cosmetic sensor/eye, kept clearly separate from (and below) P1's head.
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


def _pauldron(tag, center, size, mat, accent, trim=True):
    """An angular layered shoulder plate rather than a sphere."""
    C.box('Pauldron_%s' % tag, center, size, mat, 0.03, taper=0.85)
    if trim:
        cx, cy, cz = center
        C.box('PauldronTrim_%s' % tag, (cx, cy - size[1] * 0.55, cz), (size[0] * 0.85, 0.05, size[2] * 0.7), accent, 0.01)


def build_p1(name, suit_name, accent_name, heavy):
    C.reset_scene()
    suit, armor, accent, visor = _mats(suit_name, accent_name)
    w = 1.22 if heavy else 1.0

    # Pelvis / torso with layered plates and panel accents.
    C.box('Hips', (0, 0, 0.95), (0.40 * w, 0.28, 0.24), armor, 0.02)
    C.box('Belt', (0, 0, 1.03), (0.44 * w, 0.30, 0.07), accent, 0.01)
    C.box('BeltBuckle', (0, -0.15, 1.03), (0.12, 0.05, 0.09), accent, 0.008)
    C.box('Torso', (0, 0, 1.23), (0.48 * w, 0.30, 0.44), suit, 0.03, taper=0.84)
    C.box('ChestPlate', (0, -0.15, 1.36), (0.42 * w, 0.09, 0.34), armor, 0.02)
    C.box('ChestStrip', (0, -0.175, 1.30), (0.07, 0.05, 0.26), accent, 0.006)
    C.box('BackPlate', (0, 0.14, 1.30), (0.34 * w, 0.06, 0.32), armor, 0.02)
    C.box('SidePlate_L', (-0.24 * w, 0, 1.24), (0.06, 0.24, 0.32), armor, 0.015)
    C.box('SidePlate_R', (0.24 * w, 0, 1.24), (0.06, 0.24, 0.32), armor, 0.015)

    if heavy:
        _pauldron('L', (-0.34, 0.0, 1.44), (0.22, 0.30, 0.16), armor, accent)
        _pauldron('R', (0.34, 0.0, 1.44), (0.22, 0.30, 0.16), armor, accent)
        C.box('Backpack', (0, 0.23, 1.28), (0.32, 0.16, 0.40), armor, 0.02)
        C.box('BackpackStrap_L', (-0.13, -0.02, 1.30), (0.05, 0.30, 0.30), accent, 0.005)
        C.box('BackpackStrap_R', (0.13, -0.02, 1.30), (0.05, 0.30, 0.30), accent, 0.005)

    # Neck and an exposed, clearly identifiable head. Nothing covers the face or silhouette.
    C.cylinder('Neck', (0, 0, 1.56), 0.075, 0.16, 'Z', suit, seg=12)
    C.box('NeckGuard', (0, 0.05, 1.57), (0.20, 0.13, 0.13), armor, 0.015)
    C.sphere('Head', (0, -0.01, 1.72), 0.155, suit, scale=(1.0, 1.05, 1.12))
    C.box('Jaw', (0, -0.09, 1.635), (0.15, 0.10, 0.09), armor, 0.012)
    C.box('Brow', (0, -0.125, 1.785), (0.185, 0.07, 0.05), armor, 0.01)
    C.box('EyeVisor', (0, -0.16, 1.735), (0.155, 0.035, 0.055), visor, 0.005)
    C.box('HeadCrest', (0, 0.05, 1.85), (0.14, 0.16, 0.06), accent, 0.01)
    C.box('EarPlate_L', (-0.155, 0.0, 1.71), (0.035, 0.10, 0.11), armor, 0.008)
    C.box('EarPlate_R', (0.155, 0.0, 1.71), (0.035, 0.10, 0.11), armor, 0.008)

    # Legs with knee pads.
    for side in (-1, 1):
        x = side * 0.13 * w
        C.cylinder('Thigh_%d' % side, (x, 0, 0.66), 0.095, 0.50, 'Z', suit, seg=14, radius2=0.085)
        C.box('KneePad_%d' % side, (x, -0.03, 0.47), (0.17, 0.17, 0.15), armor, 0.02)
        C.cylinder('Shin_%d' % side, (x, 0, 0.28), 0.080, 0.44, 'Z', armor, seg=14, radius2=0.070)
        C.box('ShinStrip_%d' % side, (x, -0.09, 0.28), (0.05, 0.04, 0.30), accent, 0.005)
        C.box('Foot_%d' % side, (x, -0.06, 0.05), (0.16, 0.30, 0.10), armor, 0.02)

    C.unwrap_all()
    C.report_bounds()
    C.save_blend(os.path.join(BLEND, 'P1', name + '.blend'))
    C.export_fbx(os.path.join(ASSETS, 'Characters', 'P1', name + '.fbx'))


def build_p2(name, suit_name, accent_name, heavy):
    C.reset_scene()
    suit, armor, accent, visor = _mats(suit_name, accent_name)

    # Local space: origin is the P2 mount (the shoulder anchor). Unity-relative (x, y, z) maps to
    # Blender local (x, -z, y). The weapon anchor is at rb(0, -0.13, +0.29).
    def rb(x, y, z):
        return (x, -z, y)

    # Upper-chest / clavicle layer.
    C.box('Collar', rb(0, -0.01, 0.0), (0.42, 0.20, 0.18), armor, 0.02)
    C.box('CollarTrim', rb(0, -0.02, 0.13), (0.26, 0.20, 0.05), accent, 0.01)
    C.box('SpinePlate', rb(0, 0.12, -0.02), (0.22, 0.10, 0.20), armor, 0.02)
    C.box('Clavicle_L', rb(-0.16, -0.04, 0.06), (0.18, 0.14, 0.09), suit, 0.02)
    C.box('Clavicle_R', rb(0.16, -0.04, 0.06), (0.18, 0.14, 0.09), suit, 0.02)

    # Angular pauldrons (no spheres).
    for side in (-1, 1):
        sx = 1.15 if heavy else 1.0
        _pauldron('%d' % side, rb(side * 0.25, 0.03, 0.0), (0.20 * sx, 0.26 * sx, 0.16 * sx), armor, accent)

    # Two complete anatomical arms with articulated (sleeved) joints and forearm plates.
    arms = {
        1: ((0.205, -0.02, 0.0), (0.36, -0.26, -0.02), (0.08, -0.20, 0.26)),
        -1: ((-0.205, -0.02, 0.0), (-0.30, -0.16, 0.26), (-0.06, -0.08, 0.52)),
    }
    for side, (shoulder, elbow, wrist) in arms.items():
        tag = 'R' if side > 0 else 'L'
        s, e, w = rb(*shoulder), rb(*elbow), rb(*wrist)
        upper_dir = C.Vector(e) - C.Vector(s)
        fore_dir = C.Vector(w) - C.Vector(e)

        C.joint_sleeve('ShoulderSleeve_%s' % tag, s, upper_dir, 0.17, 0.098, suit)
        C.limb('UpperArm_%s' % tag, s, e, 0.080, 0.062, suit, seg=14)
        C.joint_sleeve('ElbowPad_%s' % tag, e, fore_dir, 0.14, 0.072, armor)
        C.limb('Forearm_%s' % tag, e, w, 0.060, 0.050, suit, seg=14)
        C.box_dir('Vambrace_%s' % tag, C.Vector(e) + fore_dir * 0.45, (0.105, 0.105, 0.18), fore_dir, armor, 0.012)
        C.box_dir('VambraceTrim_%s' % tag, C.Vector(e) + fore_dir * 0.58, (0.06, 0.06, 0.10), fore_dir, accent, 0.006)
        C.box_dir('Hand_%s' % tag, w, (0.085, 0.10, 0.14), fore_dir, armor, 0.012)
        C.box_dir('Knuckles_%s' % tag, C.Vector(w) + fore_dir.normalized() * 0.075, (0.075, 0.09, 0.06), fore_dir, accent, 0.008)

    # Cosmetic sensor pod: small, to the side and behind, and clearly BELOW P1's head. The gameplay
    # camera anchor is independent of this.
    C.limb('SensorStalk', rb(0.14, 0.05, -0.05), rb(0.22, 0.09, -0.15), 0.020, 0.018, accent, seg=8)
    C.box('SensorPod', rb(0.24, 0.10, -0.17), (0.13, 0.13, 0.11), armor, 0.015)
    C.box('SensorLens', rb(0.24, 0.10, -0.24), (0.07, 0.045, 0.05), visor, 0.005)

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
