using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>
    /// Builds the stylized CC0 rifle prefab from the Quaternius Low Poly Guns Pack. The imported FBX
    /// is normalized (barrel axis aligned to +Z, muzzle forward, fixed world length) and gets hand
    /// grip + muzzle markers derived from its own bounds, so the P2 arm IK grips the actual weapon
    /// rather than hand-tuned screen offsets.
    /// </summary>
    public static class M7WeaponBuilder
    {
        public const string ModelPath = "Assets/ThirdParty/QuaterniusLowPolyGunsPack/AssaultRifle_1.fbx";
        public const string PrefabPath = "Assets/Art/Weapons/Prefabs/BMA_Weapon_Rifle_Quaternius.prefab";
        public const float TargetLength = 0.9f;

        public const string GripRightName = "Grip_R";
        public const string GripLeftName = "Grip_L";
        public const string MuzzleName = "Muzzle";
        /// <summary>Roll of the trigger-hand grip about the actual hand's long axis (degrees).</summary>
        public const float GripRoll = 30f;

        /// <summary>Right-hand bone and a finger it parents, used to find the hand's long axis.</summary>
        public const string HandBoneName = "DEF-hand.R";
        public const string HandFingerBoneName = "DEF-f_middle.01.R";

        public static GameObject Build()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError("[M7weapon] missing rifle model: " + ModelPath);
                return null;
            }

            var root = new GameObject("BMA_Weapon_Rifle_Quaternius");
            var pivot = new GameObject("Model");
            pivot.transform.SetParent(root.transform, false);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "ModelMesh";
            instance.transform.SetParent(pivot.transform, false);
            // Keep the imported transform (its axis/scale conversion) and normalize on the pivot.

            Bounds initial = WorldBounds(instance);
            int axis = LongestAxis(initial.size);
            bool muzzlePositive = MuzzleAtPositiveEnd(instance, axis);
            float length = initial.size[axis];

            Quaternion align = AlignRotation(axis, muzzlePositive);
            float scale = TargetLength / Mathf.Max(0.0001f, length);
            pivot.transform.localRotation = align;
            pivot.transform.localScale = Vector3.one * scale;

            // Center the rifle on the root origin so grips can be expressed from its bounds.
            Bounds placed = WorldBounds(instance);
            pivot.transform.localPosition = -placed.center;

            Bounds final = WorldBounds(instance);
            Vector3 size = new Vector3(Mathf.Abs(final.size.x), Mathf.Abs(final.size.y), Mathf.Abs(final.size.z));
            if (size.z < 0.0001f) size.z = TargetLength;

            Debug.Log($"[M7weapon] rifle axis={axis} muzzlePositive={muzzlePositive} scale={scale:0.0000} size={size:F3}");

            CreateMarker(root.transform, MuzzleName, new Vector3(0f, 0.02f * size.y, 0.5f * size.z), Quaternion.identity);
            // Grips start unrotated; BakeTriggerHandRoll() later orients the trigger-hand grip
            // relative to the actual right hand's aim pose (see its doc).
            CreateMarker(root.transform, GripRightName, new Vector3(0f, -0.30f * size.y, -0.20f * size.z), Quaternion.identity);
            CreateMarker(root.transform, GripLeftName, new Vector3(0f, -0.24f * size.y, 0.20f * size.z), Quaternion.identity);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        /// <summary>
        /// Finds the weapon's grip/muzzle markers, creating bounds-derived ones if absent (so an
        /// arbitrary weapon prefab still gets a consistent two-hand grip).
        /// </summary>
        public static void EnsureGrips(GameObject weapon, out Transform gripRight, out Transform gripLeft, out Transform muzzle)
        {
            gripRight = Find(weapon.transform, GripRightName);
            gripLeft = Find(weapon.transform, GripLeftName);
            muzzle = Find(weapon.transform, MuzzleName);
            if (gripRight != null && gripLeft != null) return;

            Bounds b = LocalBounds(weapon);
            Vector3 size = b.size;
            if (size.z < 0.0001f) size.z = Mathf.Max(size.x, TargetLength);
            float length = Mathf.Max(size.z, TargetLength);
            Vector3 center = b.center;
            if (gripRight == null)
                gripRight = CreateMarker(weapon.transform, GripRightName, center + new Vector3(0f, -0.30f * size.y, -0.20f * length), Quaternion.identity);
            if (gripLeft == null)
                gripLeft = CreateMarker(weapon.transform, GripLeftName, center + new Vector3(0f, -0.24f * size.y, 0.20f * length), Quaternion.identity);
            if (muzzle == null)
                muzzle = CreateMarker(weapon.transform, MuzzleName, center + new Vector3(0f, 0f, 0.5f * length), Quaternion.identity);
        }

        /// <summary>
        /// Orients the trigger-hand grip relative to the actual right hand instead of an arbitrary
        /// socket Euler: samples the arm rig in its aim pose, finds the hand's long (wrist-to-finger)
        /// axis, and rolls the hand's aim orientation about that axis. The resulting grip local
        /// rotation is written into the weapon prefab, so both the world body and the first-person
        /// viewmodel follow it without any per-frame feedback. Returns false (leaving the identity
        /// grip) if the arms model or bones are missing.
        /// </summary>
        public static bool BakeTriggerHandRoll(string armsModelPath, string aimClipSuffix, float degrees)
        {
            GameObject armsAsset = AssetDatabase.LoadAssetAtPath<GameObject>(armsModelPath);
            if (armsAsset == null)
            {
                Debug.LogWarning("[M7weapon] grip roll: missing arms model " + armsModelPath);
                return false;
            }

            var arms = (GameObject)PrefabUtility.InstantiatePrefab(armsAsset);
            PrefabUtility.UnpackPrefabInstance(arms, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            arms.transform.localRotation = Quaternion.identity;
            arms.transform.localScale = Vector3.one;

            Transform[] all = arms.GetComponentsInChildren<Transform>(true);
            var pos = new Vector3[all.Length];
            var rot = new Quaternion[all.Length];
            for (int i = 0; i < all.Length; i++) { pos[i] = all[i].localPosition; rot[i] = all[i].localRotation; }

            Transform hand = Find(arms.transform, HandBoneName);
            if (hand == null)
            {
                Restore(arms, all, pos, rot);
                Object.DestroyImmediate(arms);
                Debug.LogWarning("[M7weapon] grip roll: missing hand bone " + HandBoneName);
                return false;
            }

            AnimationClip clip = FindClip(armsModelPath, aimClipSuffix);
            if (clip != null) clip.SampleAnimation(arms, 0f);

            Transform finger = Find(arms.transform, HandFingerBoneName);
            Vector3 longAxisWorld = (finger != null && (finger.position - hand.position).sqrMagnitude > 1e-8f)
                ? (finger.position - hand.position).normalized
                : hand.rotation * Vector3.up;

            // Roll the aimed hand about its own long axis, then express that as a rotation relative to
            // the weapon root (which is also the grip's parent in the runtime prefabs).
            Quaternion targetHand = Quaternion.AngleAxis(degrees, longAxisWorld) * hand.rotation;

            var tempWeapon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            tempWeapon.transform.SetParent(arms.transform, false);
            tempWeapon.transform.localPosition = Vector3.zero;
            tempWeapon.transform.localRotation = Quaternion.identity;
            Quaternion rel = Quaternion.Inverse(tempWeapon.transform.rotation) * targetHand;
            Object.DestroyImmediate(tempWeapon);

            Restore(arms, all, pos, rot);
            Object.DestroyImmediate(arms);

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            Transform grip = Find(contents.transform, GripRightName);
            if (grip == null)
            {
                PrefabUtility.UnloadPrefabContents(contents);
                Debug.LogWarning("[M7weapon] grip roll: missing " + GripRightName);
                return false;
            }
            grip.localRotation = rel;
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log($"[M7weapon] trigger-hand grip roll baked: {degrees:0.#}° about hand axis {longAxisWorld:F3}");
            return true;
        }

        static void Restore(GameObject go, Transform[] all, Vector3[] pos, Quaternion[] rot)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                all[i].localPosition = pos[i];
                all[i].localRotation = rot[i];
            }
        }

        static AnimationClip FindClip(string fbxPath, string suffix)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip &&
                    !clip.name.StartsWith("__preview__") &&
                    (clip.name == suffix || clip.name.EndsWith("|" + suffix)))
                    return clip;
            }
            Debug.LogWarning($"[M7weapon] missing clip '{suffix}' in {fbxPath}");
            return null;
        }

        static Transform CreateMarker(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            return go.transform;
        }

        static int LongestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z) return 0;
            if (size.y >= size.x && size.y >= size.z) return 1;
            return 2;
        }

        static Quaternion AlignRotation(int axis, bool muzzlePositive)
        {
            Quaternion align;
            switch (axis)
            {
                case 0: align = Quaternion.Euler(0f, -90f, 0f); break; // +X -> +Z
                case 1: align = Quaternion.Euler(90f, 0f, 0f); break;  // +Y -> +Z
                default: align = Quaternion.identity; break;           // +Z already
            }
            // After alignment the barrel points +Z when the muzzle was on the positive end; flip 180
            // around Y when it was on the negative end.
            if (!muzzlePositive) align = Quaternion.Euler(0f, 180f, 0f) * align;
            return align;
        }

        static bool MuzzleAtPositiveEnd(GameObject instance, int axis)
        {
            var points = new List<Vector3>();
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                Matrix4x4 m = filter.transform.localToWorldMatrix;
                Vector3[] verts = mesh.vertices;
                for (int i = 0; i < verts.Length; i++) points.Add(m.MultiplyPoint3x4(verts[i]));
            }
            if (points.Count == 0) return true;

            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < points.Count; i++) { float v = points[i][axis]; min = Mathf.Min(min, v); max = Mathf.Max(max, v); }
            float length = max - min;
            if (length < 0.0001f) return true;
            float band = 0.12f * length;

            float lowCross = CrossExtent(points, axis, min, min + band);
            float highCross = CrossExtent(points, axis, max - band, max);
            return highCross <= lowCross;
        }

        static float CrossExtent(List<Vector3> points, int axis, float lo, float hi)
        {
            int a = (axis + 1) % 3;
            int b = (axis + 2) % 3;
            float amin = float.MaxValue, amax = float.MinValue, bmin = float.MaxValue, bmax = float.MinValue;
            bool any = false;
            for (int i = 0; i < points.Count; i++)
            {
                float v = points[i][axis];
                if (v < lo || v > hi) continue;
                any = true;
                amin = Mathf.Min(amin, points[i][a]); amax = Mathf.Max(amax, points[i][a]);
                bmin = Mathf.Min(bmin, points[i][b]); bmax = Mathf.Max(bmax, points[i][b]);
            }
            if (!any) return float.MaxValue;
            return (amax - amin) + (bmax - bmin);
        }

        static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * TargetLength);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>Renderer bounds expressed in the weapon root's local space.</summary>
        static Bounds LocalBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            Transform root = go.transform;
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * TargetLength);
            Bounds b = new Bounds();
            bool first = true;
            for (int r = 0; r < renderers.Length; r++)
            {
                Vector3 min = renderers[r].bounds.min;
                Vector3 max = renderers[r].bounds.max;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                    Vector3 local = root.InverseTransformPoint(corner);
                    if (first) { b = new Bounds(local, Vector3.zero); first = false; }
                    else b.Encapsulate(local);
                }
            }
            return b;
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) if (all[i].name == name) return all[i];
            return null;
        }
    }
}
