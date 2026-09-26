using System.IO;
using BeMyArms.M3;
using BeMyArms.M3.EditorTools;
using BeMyArms.M5;
using BeMyArms.M6;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace BeMyArms.M7.EditorTools
{
    /// <summary>
    /// Builds the production player-body prefab: the M3 networked body head with the M5/M6 shared-body
    /// rig, one P1 skin + one P2 skin mounted through the contract, the P2 aim pivot + rifle + grip
    /// targets, the P2 arm IK and the Mecanim presentation driver. The authoritative components are
    /// unchanged.
    /// </summary>
    public static class M7PlayerBodyBuilder
    {
        public const string BodyPrefabPath = "Assets/Art/Characters/M7PlayerBody.prefab";
        public const string DirectorPrefabPath = "Assets/Art/Characters/M7PlayerDirector.prefab";

        const string P1SkinPath = M7CharacterBodyBuilder.P1SkinPrefabPath;
        const string P2SkinPath = M7CharacterBodyBuilder.P2SkinPrefabPath;
        const string RiflePath = "Assets/Art/Weapons/Prefabs/BMA_Weapon_Rifle.prefab";
        const string KenneyRiflePath = M7CharacterBodyBuilder.RiflePrefabPath;

        // Aim-relative grip positions (metres) for the P2 two-bone arm IK.
        static readonly Vector3 GripRight = new Vector3(0.10f, -0.09f, -0.06f);
        static readonly Vector3 GripLeft = new Vector3(-0.01f, -0.05f, 0.28f);

        public static void EnsurePrefabs(out GameObject bodyPrefab, out GameObject directorPrefab)
        {
            M7CharacterBodyBuilder.EnsureImportSettings();
            M7CharacterBodyBuilder.BuildAll();
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

            // Hitbox triggers stay out of the camera deoccluder's raycasts (Default layer only) and
            // out of any Unity physics query, since gameplay collision is deterministic and custom.
            foreach (BeMyArms.M0.HitboxRegion hitbox in go.GetComponentsInChildren<BeMyArms.M0.HitboxRegion>(true))
                hitbox.gameObject.layer = 2;

            // P2 aim pivot under the contract's WeaponAnchor. It carries the rifle and the grip
            // targets the arm IK solves to, so the arms aim with the authoritative aim (not the root).
            Transform weaponAnchor = Find(rigInstance.transform, "WeaponAnchor");
            var aimPivot = new GameObject("AimPivot");
            aimPivot.transform.SetParent(weaponAnchor != null ? weaponAnchor : rigInstance.transform, false);
            aimPivot.transform.localPosition = Vector3.zero;
            aimPivot.transform.localRotation = Quaternion.identity;

            var riflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyRiflePath);
            if (riflePrefab == null) riflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiflePath);
            var rifle = (GameObject)Object.Instantiate(riflePrefab);
            rifle.name = "Weapon";
            rifle.transform.SetParent(aimPivot.transform, false);
            rifle.transform.localPosition = Vector3.zero;
            rifle.transform.localRotation = Quaternion.identity;

            Transform gripR = Marker(aimPivot.transform, "HandTarget_R", GripRight);
            Transform gripL = Marker(aimPivot.transform, "HandTarget_L", GripLeft);

            TwoBoneIKConstraint ikL = FindConstraint(rigInstance, "ArmIK_L");
            TwoBoneIKConstraint ikR = FindConstraint(rigInstance, "ArmIK_R");
            if (ikL != null) ikL.data.target = gripL;
            if (ikR != null) ikR.data.target = gripR;

            var animator = go.AddComponent<M7CharacterAnimator>();
            animator.Body = go.GetComponent<M3DuelBody>();
            animator.Client = go.GetComponent<M3DuelClient>();
            animator.P1Skin = assembled.P1Skin != null ? assembled.P1Skin.transform : null;
            animator.P2Skin = assembled.P2Skin != null ? assembled.P2Skin.transform : null;
            animator.P1Animator = FindAnimator(assembled.P1Skin);
            animator.P2Animator = FindAnimator(assembled.P2Skin);
            animator.AimPivot = aimPivot.transform;
            animator.Weapon = rifle.transform;
            animator.ArmIkL = ikL;
            animator.ArmIkR = ikR;

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

        static Transform Marker(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        static TwoBoneIKConstraint FindConstraint(GameObject root, string name)
        {
            foreach (TwoBoneIKConstraint c in root.GetComponentsInChildren<TwoBoneIKConstraint>(true))
                if (c.gameObject.name == name) return c;
            return null;
        }

        static Animator FindAnimator(GameObject skin)
            => skin != null ? skin.GetComponentInChildren<Animator>(true) : null;

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
