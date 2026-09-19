"""Builds the production map kit for Duel/2v2 arenas and exports it to Unity.

Run headless:
  blender --background --factory-startup --python art/blender/scripts/make_mapkit.py

Modular, grid-friendly pieces (1 Blender unit = 1 m), authored Z-up and exported Y-up exactly like
the M6 assets. Map families (concept §17.2) reuse this kit with different layouts.
"""

import os
import sys
import bmesh

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import bma_common as C  # noqa: E402

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(SCRIPT_DIR, '..', '..', '..'))
ASSETS = os.path.join(REPO, 'Assets', 'Art')
BLEND = os.path.join(REPO, 'art', 'blender', 'blend', 'MapKit')


def _mats():
    return {
        'floor': C.material('BMA_Map_Floor', (0.24, 0.26, 0.29), 0.0, 0.7),
        'concrete': C.material('BMA_Concrete', (0.42, 0.44, 0.47), 0.0, 0.85),
        'metal': C.material('BMA_Env_Metal', (0.20, 0.22, 0.25), 0.55, 0.45),
        'accent': C.material('BMA_Env_Accent', (0.85, 0.55, 0.20), 0.20, 0.50),
        'team_a': C.material('BMA_Team_Alpha', (0.15, 0.65, 0.85), 0.10, 0.50),
        'team_b': C.material('BMA_Team_Beta', (0.85, 0.40, 0.18), 0.10, 0.50),
    }


def _begin():
    C.reset_scene()
    return _mats()


def _finish(name):
    C.unwrap_all()
    C.report_bounds()
    C.save_blend(os.path.join(BLEND, name + '.blend'))
    C.export_fbx(os.path.join(ASSETS, 'MapKit', name + '.fbx'))


def build_floor4(name):
    m = _begin()
    C.box('Floor', (0, 0, -0.1), (4.0, 4.0, 0.2), m['floor'], 0.02)
    for sign in (-1, 1):
        C.box('Edge_%d' % sign, (sign * 2.0, 0, -0.05), (0.12, 4.0, 0.10), m['metal'], 0.01)
    _finish(name)


def build_wall4x3(name):
    m = _begin()
    C.box('Wall', (0, 0, 1.5), (4.0, 0.30, 3.0), m['concrete'], 0.03)
    C.box('Base', (0, 0, 0.10), (4.1, 0.36, 0.20), m['metal'], 0.02)
    C.box('Cap', (0, 0, 2.95), (4.1, 0.36, 0.16), m['metal'], 0.02)
    C.box('Band', (0, -0.16, 1.6), (3.4, 0.05, 0.14), m['accent'], 0.008)
    _finish(name)


def build_halfwall4(name):
    m = _begin()
    C.box('Wall', (0, 0, 0.5), (4.0, 0.30, 1.0), m['concrete'], 0.03)
    C.box('Cap', (0, 0, 1.02), (4.1, 0.36, 0.10), m['metal'], 0.02)
    _finish(name)


def build_cover_low(name):
    m = _begin()
    C.box('Cover', (0, 0, 0.6), (1.8, 1.4, 1.2), m['concrete'], 0.05)
    C.box('Band', (0, 0, 1.05), (1.86, 1.46, 0.10), m['accent'], 0.01)
    _finish(name)


def build_cover_high(name):
    m = _begin()
    C.box('Cover', (0, 0, 1.0), (1.3, 1.3, 2.0), m['concrete'], 0.05)
    C.box('Cap', (0, 0, 2.02), (1.4, 1.4, 0.12), m['metal'], 0.02)
    C.box('Slit', (0, -0.66, 1.4), (0.7, 0.06, 0.10), m['accent'], 0.006)
    _finish(name)


def build_platform4(name):
    m = _begin()
    C.box('Platform', (0, 0, 0.5), (4.0, 4.0, 1.0), m['floor'], 0.03)
    C.box('Lip', (0, -2.1, 0.55), (4.0, 0.2, 0.30), m['metal'], 0.02)
    C.box('Marking', (0, 0, 1.02), (1.6, 1.6, 0.04), m['accent'], 0.008)
    _finish(name)


def build_ramp4(name):
    m = _begin()
    # A proper wedge: rises 1.0 m over 4.0 m.
    bm = bmesh.new()
    verts = [
        bm.verts.new((-2.0, -2.0, 0.0)), bm.verts.new((2.0, -2.0, 0.0)),
        bm.verts.new((-2.0, 2.0, 0.0)), bm.verts.new((2.0, 2.0, 0.0)),
        bm.verts.new((-2.0, 2.0, 1.0)), bm.verts.new((2.0, 2.0, 1.0)),
    ]
    for f in [(0, 1, 3, 2), (0, 2, 4), (1, 5, 3), (0, 4, 5, 1), (2, 3, 5, 4)]:
        bm.faces.new([verts[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    C._finish('Ramp', bm, m['concrete'])
    C.box('RampEdge', (0, 2.0, 1.02), (4.0, 0.18, 0.10), m['metal'], 0.02)
    _finish(name)


def build_railing4(name):
    m = _begin()
    C.limb('TopRail', (-2.0, 0, 1.0), (2.0, 0, 1.0), 0.05, 0.05, m['metal'], seg=10)
    C.limb('MidRail', (-2.0, 0, 0.55), (2.0, 0, 0.55), 0.035, 0.035, m['metal'], seg=10)
    for i in range(-2, 3):
        C.box('Post_%d' % i, (i * 1.0, 0, 0.5), (0.09, 0.09, 1.0), m['metal'], 0.01)
    _finish(name)


def build_doorway4(name):
    m = _begin()
    C.box('Left', (-1.5, 0, 1.5), (1.0, 0.30, 3.0), m['concrete'], 0.03)
    C.box('Right', (1.5, 0, 1.5), (1.0, 0.30, 3.0), m['concrete'], 0.03)
    C.box('Lintel', (0, 0, 2.7), (2.0, 0.30, 0.60), m['concrete'], 0.03)
    C.box('Header', (0, -0.16, 2.35), (1.6, 0.05, 0.10), m['accent'], 0.006)
    _finish(name)


def build_catwalk4(name):
    m = _begin()
    C.box('Deck', (0, 0, 1.0), (4.0, 1.4, 0.20), m['floor'], 0.02)
    C.box('Kerb', (0, 0.75, 1.15), (4.0, 0.10, 0.12), m['accent'], 0.006)
    C.limb('Rail', (-2.0, -0.65, 1.85), (2.0, -0.65, 1.85), 0.05, 0.05, m['metal'], seg=10)
    for i in range(-2, 3):
        C.box('Post_%d' % i, (i * 1.0, -0.65, 1.4), (0.08, 0.08, 0.9), m['metal'], 0.01)
    _finish(name)


def build_crate(name):
    m = _begin()
    C.box('Crate', (0, 0, 0.6), (1.2, 1.2, 1.2), m['concrete'], 0.05)
    for z in (0.25, 0.95):
        C.box('Band_%d' % int(z * 100), (0, 0, z), (1.26, 1.26, 0.07), m['accent'], 0.01)
    _finish(name)


def build_spawnpad(name, team):
    m = _begin()
    mat = m['team_a'] if team == 'A' else m['team_b']
    C.box('Pad', (0, 0, 0.04), (2.6, 2.6, 0.08), mat, 0.01)
    C.box('Chevron1', (0, 0.5, 0.09), (1.4, 0.18, 0.03), m['metal'], 0.005)
    C.box('Chevron2', (0, 0.0, 0.09), (1.0, 0.18, 0.03), m['metal'], 0.005)
    C.box('Chevron3', (0, -0.5, 0.09), (0.6, 0.18, 0.03), m['metal'], 0.005)
    _finish(name)


def build_pillar(name):
    m = _begin()
    C.box('Pillar', (0, 0, 1.5), (0.9, 0.9, 3.0), m['concrete'], 0.03)
    C.box('Base', (0, 0, 0.12), (1.2, 1.2, 0.24), m['metal'], 0.02)
    C.box('Cap', (0, 0, 2.92), (1.2, 1.2, 0.24), m['metal'], 0.02)
    C.box('Band', (0, -0.47, 1.5), (0.6, 0.05, 2.0), m['accent'], 0.006)
    _finish(name)


def build_corner(name):
    m = _begin()
    C.box('WallX', (2.0, 0, 1.5), (4.0, 0.30, 3.0), m['concrete'], 0.03)
    C.box('WallY', (0, 2.0, 1.5), (0.30, 4.0, 3.0), m['concrete'], 0.03)
    C.box('BaseX', (2.0, 0, 0.10), (4.1, 0.36, 0.20), m['metal'], 0.02)
    C.box('BaseY', (0, 2.0, 0.10), (0.36, 4.1, 0.20), m['metal'], 0.02)
    _finish(name)


def main():
    build_floor4('BMA_Map_Floor4')
    build_wall4x3('BMA_Map_Wall4x3')
    build_halfwall4('BMA_Map_HalfWall4')
    build_cover_low('BMA_Map_Cover_Low')
    build_cover_high('BMA_Map_Cover_High')
    build_platform4('BMA_Map_Platform4')
    build_ramp4('BMA_Map_Ramp4')
    build_railing4('BMA_Map_Railing4')
    build_doorway4('BMA_Map_Doorway4')
    build_catwalk4('BMA_Map_Catwalk4')
    build_crate('BMA_Map_Crate')
    build_spawnpad('BMA_Map_SpawnPad_A', 'A')
    build_spawnpad('BMA_Map_SpawnPad_B', 'B')
    build_pillar('BMA_Map_Pillar')
    build_corner('BMA_Map_Corner')
    print('[bma] map kit done')


main()
