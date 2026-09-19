using System.Collections.Generic;
using System.IO;
using BeMyArms.M0;
using BeMyArms.M5;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M6.EditorTools
{
    /// <summary>
    /// Builds the production prefabs: the shared-body gameplay rig, character skin wrappers,
    /// weapon/utility wrappers and environment-kit wrappers. Everything is generated from the
    /// imported FBX + the M6 conventions so the pipeline stays reproducible.
    /// </summary>
    public static class M6PrefabBuilders
    {
        // ---- Shared body rig ----

        public static GameObject BuildSharedBodyRig()
        {
            var root = new GameObject(M6RigLayout.RootName);
            root.AddComponent<M5RigAnimation>();
            root.AddComponent<M5GameplayRigStats>();

            Transform hips = Child(root.transform, "Hips", M6RigLayout.Hips);
            Transform chest = Child(hips, "Chest", M6RigLayout.Chest);
            Transform neck = Child(chest, "Neck", M6RigLayout.Neck);
            Transform head = Child(neck, "Head", M6RigLayout.Head);

            Child(chest, "ShoulderAnchor", M6RigLayout.ShoulderAnchor);
            Child(chest, "WeaponAnchor", M6RigLayout.WeaponAnchor);
            Child(chest, "UtilityAnchor", M6RigLayout.UtilityAnchor);
            Child(chest, "P2CameraAnchor", M6RigLayout.P2CameraAnchor);
            Child(neck, "P1CameraAnchor", M6RigLayout.P1CameraAnchor);
            Child(root.transform, "Cosmetic_P1", M6RigLayout.CosmeticP1);
            Child(chest, "Cosmetic_P2", M6RigLayout.CosmeticP2);

            BuildHitbox(head, "Hitbox_Head", HitboxRegion.Region.Head, M6RigLayout.HitboxHeadScale, 0.5f);
            BuildHitbox(chest, "Hitbox_Body", HitboxRegion.Region.Body, M6RigLayout.HitboxBodyScale, 0.5f);

            string path = M6AssetConventions.RigPrefabPath;
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
            foreach (string model in FindModels(M6AssetConventions.P1Dir))
                built.Add(BuildSkin(model, M5RigRole.P1, Path.GetFileNameWithoutExtension(model)));
            foreach (string model in FindModels(M6AssetConventions.P2Dir))
                built.Add(BuildSkin(model, M5RigRole.P2, Path.GetFileNameWithoutExtension(model)));
            return built;
        }

        public static string BuildSkin(string modelPath, M5RigRole role, string skinId)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new FileNotFoundException($"model not found: {modelPath}");

            var root = new GameObject(skinId);
            var descriptor = root.AddComponent<M5SkinDescriptor>();
            descriptor.Role = role;
            descriptor.SkinId = skinId;
            descriptor.RigId = M6RigLayout.RigId;

            AttachModel(root.transform, model);
            RemapMaterials(root);
            StripGameplayComponents(root);

            string dir = role == M5RigRole.P1 ? M6AssetConventions.P1Dir : M6AssetConventions.P2Dir;
            string path = $"{dir}/{M6AssetConventions.PrefabSubdir}/{skinId}.prefab";
            SavePrefab(root, path);
            return path;
        }

        // ---- Weapons / utility ----

        public static List<string> BuildAllWeapons()
        {
            var built = new List<string>();
            foreach (string model in FindModels(M6AssetConventions.WeaponDir))
            {
                string id = Path.GetFileNameWithoutExtension(model);
                M6WeaponKind kind = id.StartsWith(M6AssetConventions.UtilityPrefix) ? M6WeaponKind.Utility : M6WeaponKind.Primary;
                built.Add(BuildWeapon(model, id, kind));
            }
            return built;
        }

        public static string BuildWeapon(string modelPath, string weaponId, M6WeaponKind kind)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new FileNotFoundException($"model not found: {modelPath}");

            var root = new GameObject(weaponId);
            var descriptor = root.AddComponent<M6WeaponDescriptor>();
            descriptor.WeaponId = weaponId;
            descriptor.Kind = kind;
            descriptor.MountSocket = kind == M6WeaponKind.Utility ? "UtilityAnchor" : "WeaponAnchor";

            AttachModel(root.transform, model);
            RemapMaterials(root);
            StripGameplayComponents(root);

            string path = $"{M6AssetConventions.WeaponDir}/{M6AssetConventions.PrefabSubdir}/{weaponId}.prefab";
            SavePrefab(root, path);
            return path;
        }

        // ---- Environment kit ----

        public static List<string> BuildAllEnvironment()
        {
            var built = new List<string>();
            foreach (string model in FindModels(M6AssetConventions.EnvironmentDir))
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
            var descriptor = root.AddComponent<M6EnvironmentDescriptor>();
            descriptor.AssetId = assetId;
            descriptor.KitCategory = "blockout";

            AttachModel(root.transform, model);
            RemapMaterials(root);

            string path = $"{M6AssetConventions.EnvironmentDir}/{M6AssetConventions.PrefabSubdir}/{assetId}.prefab";
            SavePrefab(root, path);
            return path;
        }

        // ---- Helpers ----

        static void AttachModel(Transform parent, GameObject model)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = model.name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
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
                    Material palette = M6MaterialPalette.GetOrCreate(materials[i].name);
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
                if (behaviour is IM5GameplayStatSource) Object.DestroyImmediate(behaviour);
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
