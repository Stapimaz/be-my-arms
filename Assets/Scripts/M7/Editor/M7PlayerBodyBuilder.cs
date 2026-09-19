using System.IO;
using BeMyArms.M3;
using BeMyArms.M3.EditorTools;
using BeMyArms.M5;
using BeMyArms.M6;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>
    /// Builds the production player-body prefab: the M3 networked body head with the M5/M6 shared-body
    /// rig, one P1 skin + one P2 skin mounted through the contract, the rifle at the weapon anchor and
    /// the procedural animation layer. The authoritative components are unchanged.
    /// </summary>
    public static class M7PlayerBodyBuilder
    {
        public const string BodyPrefabPath = "Assets/Art/Characters/M7PlayerBody.prefab";
        public const string DirectorPrefabPath = "Assets/Art/Characters/M7PlayerDirector.prefab";

        const string P1SkinPath = "Assets/Art/Characters/P1/Prefabs/BMA_P1_Ranger.prefab";
        const string P2SkinPath = "Assets/Art/Characters/P2/Prefabs/BMA_P2_Scout.prefab";
        const string RiflePath = "Assets/Art/Weapons/Prefabs/BMA_Weapon_Rifle.prefab";

        public static void EnsurePrefabs(out GameObject bodyPrefab, out GameObject directorPrefab)
        {
            bodyPrefab = BuildBody();
            directorPrefab = BuildDirector(bodyPrefab);
        }

        static GameObject BuildBody()
        {
            M3DuelSceneBuilder.EnsurePrefabs(out GameObject baseBody, out _);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(baseBody);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            Transform capsule = go.transform.Find("Visual");
            if (capsule != null) Object.DestroyImmediate(capsule.gameObject);

            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(M6AssetConventions.RigPrefabPath);
            var rigInstance = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            PrefabUtility.UnpackPrefabInstance(rigInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            rigInstance.transform.SetParent(go.transform, false);
            rigInstance.transform.localPosition = Vector3.zero;

            var p1Skin = AssetDatabase.LoadAssetAtPath<GameObject>(P1SkinPath);
            var p2Skin = AssetDatabase.LoadAssetAtPath<GameObject>(P2SkinPath);
            M5AssembledBody assembled = M5MountAssembler.Assemble(rigInstance, p1Skin, p2Skin);
            if (!assembled.IsValid) Debug.LogWarning("[M7] player body rig invalid: " + assembled.Report);

            var riflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiflePath);
            var rifle = (GameObject)Object.Instantiate(riflePrefab);
            rifle.name = "Weapon";
            Transform weaponAnchor = Find(rigInstance.transform, "WeaponAnchor");
            rifle.transform.SetParent(weaponAnchor != null ? weaponAnchor : rigInstance.transform, false);
            rifle.transform.localPosition = Vector3.zero;
            rifle.transform.localRotation = Quaternion.identity;

            var animator = go.AddComponent<M7CharacterAnimator>();
            animator.Body = go.GetComponent<M3DuelBody>();
            animator.P1Skin = assembled.P1Skin != null ? assembled.P1Skin.transform : null;
            animator.P2Skin = assembled.P2Skin != null ? assembled.P2Skin.transform : null;
            animator.Weapon = rifle.transform;

            var client = go.GetComponent<M3DuelClient>();
            if (client != null) client.Presentation = rigInstance.transform;

            Directory.CreateDirectory(Path.GetDirectoryName(BodyPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, BodyPrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        static GameObject BuildDirector(GameObject bodyPrefab)
        {
            var go = new GameObject("M7PlayerDirector");
            go.AddComponent<Unity.Netcode.NetworkObject>();
            var director = go.AddComponent<M3DuelDirector>();
            director.BodyPrefab = bodyPrefab;

            Directory.CreateDirectory(Path.GetDirectoryName(DirectorPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, DirectorPrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }
    }
}
