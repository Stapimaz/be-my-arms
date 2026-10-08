using System.Collections.Generic;
using System.IO;
using BeMyArms.Core;
using BeMyArms.Product;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.Content.EditorTools
{
    /// <summary>
    /// Builds the production prefabs: the shared-body gameplay rig, character skin wrappers,
    /// weapon/utility wrappers and environment-kit wrappers. Everything is generated from the
    /// imported FBX + the Content conventions so the pipeline stays reproducible.
    /// </summary>
    public static class AssetPrefabBuilders
    {
        // ---- Shared body rig ----

        public static GameObject BuildSharedBodyRig()
        {
            var root = new GameObject(RigLayout.RootName);
            root.AddComponent<RigAnimation>();
            root.AddComponent<GameplayRigStats>();

            Transform hips = Child(root.transform, "Hips", RigLayout.Hips);
            Transform chest = Child(hips, "Chest", RigLayout.Chest);
            Transform neck = Child(chest, "Neck", RigLayout.Neck);
            Transform head = Child(neck, "Head", RigLayout.Head);

            Child(chest, "ShoulderAnchor", RigLayout.ShoulderAnchor);
            Child(chest, "WeaponAnchor", RigLayout.WeaponAnchor);
            Child(chest, "UtilityAnchor", RigLayout.UtilityAnchor);
            Child(chest, "P2CameraAnchor", RigLayout.P2CameraAnchor);
            Child(neck, "P1CameraAnchor", RigLayout.P1CameraAnchor);
            Child(root.transform, "Cosmetic_P1", RigLayout.CosmeticP1);
            Child(chest, "Cosmetic_P2", RigLayout.CosmeticP2);

            BuildHitbox(head, "Hitbox_Head", HitboxRegion.Region.Head, RigLayout.HitboxHeadScale, 0.5f);
            BuildHitbox(chest, "Hitbox_Body", HitboxRegion.Region.Body, RigLayout.HitboxBodyScale, 0.5f);

            string path = AssetConventions.RigPrefabPath;
            SavePrefab(root, path);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void BuildHitbox(Transform parent, string name, HitboxRegion.Region region, Vector3 scale, float radius)
        {
            GameObject go = Child(parent, name, Vector3.zero).gameObject;
            go.transform.localScale = scale;
            var collider = go.AddComponent<CapsuleCollider>();
            collider.radius = radius;
            collider.height = 2f;
            collider.isTrigger = true;
            go.AddComponent<HitboxRegion>().region = region;
        }

        // ---- Character skins ----

        public static List<string> BuildAllCharacterSkins()
        {
            var built = new List<string>();
            foreach (string model in FindModels(AssetConventions.P1Dir))
                built.Add(BuildSkin(model, RigRole.P1, Path.GetFileNameWithoutExtension(model)));
            foreach (string model in FindModels(AssetConventions.P2Dir))
                built.Add(BuildSkin(model, RigRole.P2, Path.GetFileNameWithoutExtension(model)));
            return built;
        }

        public static string BuildSkin(string modelPath, RigRole role, string skinId)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new FileNotFoundException($"model not found: {modelPath}");

            var root = new GameObject(skinId);
            var descriptor = root.AddComponent<SkinDescriptor>();
            descriptor.Role = role;
            descriptor.SkinId = skinId;
            descriptor.RigId = RigLayout.RigId;

            AttachModel(root.transform, model);
            RemapMaterials(root);
            StripGameplayComponents(root);

            string dir = role == RigRole.P1 ? AssetConventions.P1Dir : AssetConventions.P2Dir;
            string path = $"{dir}/{AssetConventions.PrefabSubdir}/{skinId}.prefab";
            SavePrefab(root, path);
            return path;
        }

        // ---- Weapons / utility ----

        public static List<string> BuildAllWeapons()
        {
            var built = new List<string>();
            foreach (string model in FindModels(AssetConventions.WeaponDir))
            {
                string id = Path.GetFileNameWithoutExtension(model);
                AssetWeaponKind kind = id.StartsWith(AssetConventions.UtilityPrefix) ? AssetWeaponKind.Utility : AssetWeaponKind.Primary;
                built.Add(BuildWeapon(model, id, kind));
            }
            return built;
        }

        public static string BuildWeapon(string modelPath, string weaponId, AssetWeaponKind kind)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new FileNotFoundException($"model not found: {modelPath}");

            var root = new GameObject(weaponId);
            var descriptor = root.AddComponent<WeaponDescriptor>();
            descriptor.WeaponId = weaponId;
            descriptor.Kind = kind;
            descriptor.MountSocket = kind == AssetWeaponKind.Utility ? "UtilityAnchor" : "WeaponAnchor";

            AttachModel(root.transform, model);
            RemapMaterials(root);
            StripGameplayComponents(root);

            string path = $"{AssetConventions.WeaponDir}/{AssetConventions.PrefabSubdir}/{weaponId}.prefab";
            SavePrefab(root, path);
            return path;
        }

        // ---- Environment kit ----

        public static List<string> BuildAllEnvironment()
        {
            var built = new List<string>();
            foreach (string model in FindModels(AssetConventions.EnvironmentDir))
            {
                string id = Path.GetFileNameWithoutExtension(model);
                built.Add(BuildEnvironment(model, id));
            }
            return built;
        }

        public static string BuildEnvironment(string modelPath, string assetId)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new FileNotFoundException($"model not found: {modelPath}");

            var root = new GameObject(assetId);
            var descriptor = root.AddComponent<EnvironmentDescriptor>();
            descriptor.AssetId = assetId;
            descriptor.KitCategory = "blockout";

            AttachModel(root.transform, model);
            RemapMaterials(root);

            string path = $"{AssetConventions.EnvironmentDir}/{AssetConventions.PrefabSubdir}/{assetId}.prefab";
            SavePrefab(root, path);
            return path;
        }

        // ---- Helpers ----

        static void AttachModel(Transform parent, GameObject model)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = model.name;
            instance.transform.SetParent(parent, false);
            // Preserve the model's imported root transform exactly. An FBX root carries the axis
            // and unit conversion (the P1 skins import rotated 270 deg X and scaled x100, the P2
            // skins import identity); forcing position/rotation/scale here previously collapsed the
            // P1 body to ~1 cm and laid it along Z, so only the P2 arms/weapon were visible.
        }

        static void RemapMaterials(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool dirty = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    Material palette = MaterialPalette.GetOrCreate(materials[i].name);
                    if (palette != null && palette != materials[i])
                    {
                        materials[i] = palette;
                        dirty = true;
                    }
                }
                if (dirty) renderer.sharedMaterials = materials;
            }
        }

        static void StripGameplayComponents(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            foreach (HitboxRegion hitbox in root.GetComponentsInChildren<HitboxRegion>(true))
                Object.DestroyImmediate(hitbox);
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour is IGameplayStatSource) Object.DestroyImmediate(behaviour);
        }

        static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        static List<string> FindModels(string folder)
        {
            var results = new List<string>();
            if (!AssetDatabase.IsValidFolder(folder)) return results;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                results.Add(AssetDatabase.GUIDToAssetPath(guid));
            results.Sort();
            return results;
        }

        static void SavePrefab(GameObject root, string assetPath)
        {
            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }

        static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
