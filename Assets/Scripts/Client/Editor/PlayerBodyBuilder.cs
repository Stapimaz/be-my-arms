using System.IO;
using BeMyArms.Match;
using BeMyArms.Match.EditorTools;
using BeMyArms.Product;
using BeMyArms.Content;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace BeMyArms.Client.EditorTools
{
    /// <summary>
    /// Builds the production player-body prefab: the Match networked body head with the Product/Content shared-body
    /// rig, one P1 skin + one P2 skin mounted through the contract, the P2 aim pivot + rifle + grip
    /// targets, the P2 arm IK and the Mecanim presentation driver. The authoritative components are
    /// unchanged.
    /// </summary>
    public static class PlayerBodyBuilder
    {
        public const string BodyPrefabPath = "Assets/Art/Characters/PlayerBody.prefab";
        public const string DirectorPrefabPath = "Assets/Art/Characters/PlayerDirector.prefab";

        const string P1SkinPath = FighterBuilder.P1SkinPath;
        const string P2SkinPath = FighterBuilder.P2SkinPath;
        const string RiflePath = "Assets/Art/Weapons/Prefabs/BMA_Weapon_Rifle.prefab";
        const string PrimaryRiflePath = FighterBuilder.RiflePath;

        public static void EnsurePrefabs(out GameObject bodyPrefab, out GameObject directorPrefab)
        {
            FighterBuilder.BuildAll();
            bodyPrefab = BuildBody();
            directorPrefab = BuildDirector(bodyPrefab);
        }

        static GameObject BuildBody()
        {
            MatchSampleSceneBuilder.EnsurePrefabs(out GameObject baseBody, out _);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(baseBody);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            Transform capsule = go.transform.Find("Visual");
            if (capsule != null) Object.DestroyImmediate(capsule.gameObject);

            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetConventions.RigPrefabPath);
            var rigInstance = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            PrefabUtility.UnpackPrefabInstance(rigInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            rigInstance.transform.SetParent(go.transform, false);
            rigInstance.transform.localPosition = Vector3.zero;

            var p1Skin = AssetDatabase.LoadAssetAtPath<GameObject>(P1SkinPath);
            var p2Skin = AssetDatabase.LoadAssetAtPath<GameObject>(P2SkinPath);
            AssembledBody assembled = MountAssembler.Assemble(rigInstance, p1Skin, p2Skin);
            if (!assembled.IsValid) Debug.LogWarning("[Client] player body rig invalid: " + assembled.Report);
            // P2 is a separate cosmetic surface, skinned to P1's SAME bone transforms. There
            // is no second locomotion Animator or translated, independently rotating fragment.
            var sharedAnimator = FindAnimator(assembled.P1Skin);
            var armsSurface = Find(sharedAnimator.transform, "P2_Surface");
            if (armsSurface != null) armsSurface.SetParent(assembled.P2Skin.transform, true);
            foreach (var builder in rigInstance.GetComponentsInChildren<RigBuilder>(true)) Object.DestroyImmediate(builder);
            var authority = go.GetComponent<NetworkBody>();
            var prediction = go.GetComponent<NetworkBodyClient>();
            authority.JumpSpeed = prediction.JumpSpeed = BeMyArms.Networking.BodySim.DefaultJumpSpeed;
            authority.Gravity = prediction.Gravity = BeMyArms.Networking.BodySim.DefaultGravity;
            authority.NeckYawLimitDegrees = prediction.NeckYawLimitDegrees = BeMyArms.Networking.BodySim.DefaultNeckYawLimitDegrees;
            authority.BodyFollowThresholdDegrees = prediction.BodyFollowThresholdDegrees = BeMyArms.Networking.BodySim.DefaultBodyFollowThresholdDegrees;
            authority.BodyFollowSpeedDegreesPerSecond = prediction.BodyFollowSpeedDegreesPerSecond = BeMyArms.Networking.BodySim.DefaultBodyFollowSpeedDegreesPerSecond;

            // Hitbox triggers stay out of the camera deoccluder's raycasts (Default layer only) and
            // out of any Unity physics query, since gameplay collision is deterministic and custom.
            foreach (BeMyArms.Core.HitboxRegion hitbox in go.GetComponentsInChildren<BeMyArms.Core.HitboxRegion>(true))
                hitbox.gameObject.layer = 2;

            // P2 aim pivot under the contract's WeaponAnchor. It carries the rifle and the grip
            // targets the arm IK solves to, so the arms aim with the authoritative aim (not the root).
            Transform weaponAnchor = Find(rigInstance.transform, "WeaponAnchor");
            var aimPivot = new GameObject("AimPivot");
            aimPivot.transform.SetParent(weaponAnchor != null ? weaponAnchor : rigInstance.transform, false);
            // Pulled slightly back from the anchor so the support arm can reach the foregrip.
            aimPivot.transform.localPosition = new Vector3(.10f, -.04f, -.17f);
            aimPivot.transform.localRotation = Quaternion.identity;

            var riflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrimaryRiflePath);
            if (riflePrefab == null) riflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiflePath);
            var rifle = (GameObject)Object.Instantiate(riflePrefab);
            rifle.name = "Weapon";
            rifle.transform.SetParent(aimPivot.transform, false);
            rifle.transform.localPosition = Vector3.zero;
            rifle.transform.localRotation = Quaternion.identity;

            // Hand targets come from the weapon itself (its own bounds), so both hands always grip
            // the rifle instead of hand-tuned screen offsets.
            WeaponBuilder.EnsureGrips(rifle, out Transform gripR, out Transform gripL, out Transform muzzle);

            TwoBoneIKConstraint ikL = FindConstraint(rigInstance, "ArmIK_L");
            TwoBoneIKConstraint ikR = FindConstraint(rigInstance, "ArmIK_R");
            if (ikL != null) ikL.data.target = gripL;
            if (ikR != null) ikR.data.target = gripR;

            var animator = go.AddComponent<CharacterAnimator>();
            animator.Body = go.GetComponent<NetworkBody>();
            animator.Client = go.GetComponent<NetworkBodyClient>();
            animator.P1Skin = assembled.P1Skin != null ? assembled.P1Skin.transform : null;
            animator.P2Skin = assembled.P2Skin != null ? assembled.P2Skin.transform : null;
            animator.P1Animator = FindAnimator(assembled.P1Skin);
            animator.P2Animator = animator.P1Animator;
            animator.AimPivot = aimPivot.transform;
            animator.Weapon = rifle.transform;
            animator.Muzzle = muzzle;
            animator.ArmIkL = ikL;
            animator.ArmIkR = ikR;
            var pose = go.AddComponent<RiflePose>();
            pose.Model = animator.P2Animator.transform;
            pose.Weapon = rifle.transform;
            pose.Body = animator.Body;
            SmoothFighterBuilder.AuthorGrips(sharedAnimator.gameObject, rifle);

            var client = go.GetComponent<NetworkBodyClient>();
            if (client != null) client.Presentation = rigInstance.transform;

            Directory.CreateDirectory(Path.GetDirectoryName(BodyPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, BodyPrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(BodyPrefabPath, ImportAssetOptions.ForceUpdate);
            return prefab;
        }

        static GameObject BuildDirector(GameObject bodyPrefab)
        {
            var go = new GameObject("PlayerDirector");
            go.AddComponent<Unity.Netcode.NetworkObject>();
            var director = go.AddComponent<MatchDirector>();
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
