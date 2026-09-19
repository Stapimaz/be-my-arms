"""Builds a small modular environment kit and exports it to Unity.

Run headless:
  blender --background --factory-startup --python art/blender/scripts/make_env.py
"""

import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import bma_common as C  # noqa: E402

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(SCRIPT_DIR, '..', '..', '..'))
ASSETS = os.path.join(REPO, 'Assets', 'Art')
BLEND = os.path.join(REPO, 'art', 'blender', 'blend')


def _shared():
    concrete = C.material('BMA_Concrete', (0.42, 0.44, 0.47), 0.0, 0.85)
    env_metal = C.material('BMA_Env_Metal', (0.20, 0.22, 0.25), 0.55, 0.45)
    accent = C.material('BMA_Env_Accent', (0.85, 0.55, 0.20), 0.20, 0.50)
    return concrete, env_metal, accent


def build_wall():
    C.reset_scene()
    concrete, env_metal, accent = _shared()
    C.box('WallPanel', (0, 0, 1.5), (4.0, 0.20, 3.0), concrete, 0.03)
    C.box('WallBase', (0, 0, 0.10), (4.2, 0.30, 0.20), env_metal, 0.02)
    C.box('WallTop', (0, 0, 2.95), (4.2, 0.30, 0.16), env_metal, 0.02)
    for x in (-1.9, 0.0, 1.9):
        C.box('WallTrim_%d' % int(x * 10), (x, -0.08, 1.5), (0.10, 0.10, 2.7), accent, 0.01)
    C.unwrap_all()
    C.report_bounds()
    C.save_blend(os.path.join(BLEND, 'Environment', 'BMA_Env_Wall.blend'))
    C.export_fbx(os.path.join(ASSETS, 'Environment', 'BMA_Env_Wall.fbx'))


def build_crate():
    C.reset_scene()
    concrete, env_metal, accent = _shared()
    C.box('CrateBody', (0, 0, 0.5), (1.0, 1.0, 1.0), concrete, 0.04)
    for x in (-0.45, 0.45):
        for y in (-0.52, 0.52):
            C.box('CrateBrace_%d_%d' % (int(x * 10), int(y * 10)), (x, y, 0.5), (0.10, 0.04, 1.0), env_metal, 0.01)
    for z in (0.15, 0.85):
        C.box('CrateBand_%d' % int(z * 100), (0, 0, z), (1.05, 1.05, 0.06), accent, 0.01)
    C.unwrap_all()
    C.report_bounds()
    C.save_blend(os.path.join(BLEND, 'Environment', 'BMA_Env_Crate.blend'))
    C.export_fbx(os.path.join(ASSETS, 'Environment', 'BMA_Env_Crate.fbx'))


def build_platform():
    C.reset_scene()
    concrete, env_metal, accent = _shared()
    C.box('PlatformSlab', (0, 0, 0.2), (4.0, 4.0, 0.4), concrete, 0.03)
    C.box('PlatformLip', (0, -2.2, 0.2), (4.0, 0.2, 0.5), env_metal, 0.02)
    C.box('Step1', (0, -2.7, 0.15), (2.0, 1.0, 0.30), concrete, 0.02)
    C.box('Step2', (0, -3.5, 0.075), (2.0, 0.8, 0.15), concrete, 0.02)
    C.box('PlatformAccent', (0, 0, 0.42), (1.2, 1.2, 0.04), accent, 0.01)
    C.unwrap_all()
    C.report_bounds()
    C.save_blend(os.path.join(BLEND, 'Environment', 'BMA_Env_Platform.blend'))
    C.export_fbx(os.path.join(ASSETS, 'Environment', 'BMA_Env_Platform.fbx'))


def build_pillar():
    C.reset_scene()
    concrete, env_metal, accent = _shared()
    C.cylinder('PillarShaft', (0, 0, 1.4), 0.28, 2.8, 'Z', concrete, seg=20)
    C.box('PillarBase', (0, 0, 0.10), (0.8, 0.8, 0.20), env_metal, 0.02)
    C.box('PillarCap', (0, 0, 2.75), (0.8, 0.8, 0.20), env_metal, 0.02)
    C.cylinder('PillarBand', (0, 0, 1.9), 0.31, 0.10, 'Z', accent, seg=20)
    C.unwrap_all()
    C.report_bounds()
    C.save_blend(os.path.join(BLEND, 'Environment', 'BMA_Env_Pillar.blend'))
    C.export_fbx(os.path.join(ASSETS, 'Environment', 'BMA_Env_Pillar.fbx'))


def main():
    build_wall()
    build_crate()
    build_platform()
    build_pillar()
    print('[bma] environment kit done')


main()
