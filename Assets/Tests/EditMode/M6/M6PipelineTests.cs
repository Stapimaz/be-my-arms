using System.Collections.Generic;
using System.IO;
using BeMyArms.M0;
using BeMyArms.M5;
using BeMyArms.M6.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M6.Tests
{
    /// <summary>
    /// M6 acceptance proof: the Blender-authored production assets import through the conventions,
    /// satisfy the M5 rig contract, and any P1 skin mounts with any P2 skin at the correct anchors
    /// with weapons attached and authoritative hitboxes/stats unchanged.
    /// </summary>
    public class M6PipelineTests
    {
        [Test]
        public void ProductionAssets_PassConventionsAndContract()
        {
            M6ValidationReport report = M6AssetValidator.ValidateAll();
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.Greater(report.Notes.Count, 0, "expected per-asset convention notes");
        }

        [Test]
        public void RigPrefab_SatisfiesContract()
        {
            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(M6AssetConventions.RigPrefabPath);
            Assert.IsNotNull(rigPrefab, "shared body rig prefab missing");

            GameObject rig = Object.Instantiate(rigPrefab);
            M5RigValidationReport contract = M5RigValidator.Validate(rig, M5RigContract.Default());
            Assert.IsTrue(contract.IsValid, contract.ToString());

            foreach (string anchor in new[] { "WeaponAnchor", "UtilityAnchor", "P2CameraAnchor", "P1CameraAnchor", "ShoulderAnchor", "Cosmetic_P1", "Cosmetic_P2" })
                Assert.IsNotNull(Find(rig.transform, anchor), "missing anchor " + anchor);

            Object.DestroyImmediate(rig);
        }

        [Test]
        public void EveryP1SkinCombinesWithEveryP2Skin()
        {
            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(M6AssetConventions.RigPrefabPath);
            List<string> p1 = FindPrefabs(M6AssetConventions.P1Dir);
            List<string> p2 = FindPrefabs(M6AssetConventions.P2Dir);
            Assert.IsNotNull(rigPrefab);
            Assert.GreaterOrEqual(p1.Count, 2, "expected at least two production P1 skins");
            Assert.GreaterOrEqual(p2.Count, 2, "expected at least two production P2 skins");

            foreach (string p1Path in p1)
            {
                foreach (string p2Path in p2)
                {
                    GameObject rig = Object.Instantiate(rigPrefab);
                    string baseHitboxes = string.Join("|", M5RigValidator.CaptureHitboxSignature(rig));
                    string baseStats = M5RigValidator.CaptureStatsSignature(rig);

                    GameObject p1Skin = AssetDatabase.LoadAssetAtPath<GameObject>(p1Path);
                    GameObject p2Skin = AssetDatabase.LoadAssetAtPath<GameObject>(p2Path);
                    M5AssembledBody body = M5MountAssembler.Assemble(rig, p1Skin, p2Skin);

                    string label = $"{Path.GetFileNameWithoutExtension(p1Path)} + {Path.GetFileNameWithoutExtension(p2Path)}";
                    Assert.IsTrue(body.IsValid, $"{label}: {body.Report}");
                    Assert.AreNotEqual(body.P1SkinId, body.P2SkinId, "P1/P2 identities must stay distinct");
                    Assert.AreEqual(baseHitboxes, string.Join("|", M5RigValidator.CaptureHitboxSignature(rig)), $"{label}: hitboxes changed");
                    Assert.AreEqual(baseStats, M5RigValidator.CaptureStatsSignature(rig), $"{label}: stats changed");

                    // Cosmetic layers carry no authoritative data.
                    Assert.AreEqual(0, body.P1Skin.GetComponentsInChildren<HitboxRegion>(true).Length);
                    Assert.AreEqual(0, body.P2Skin.GetComponentsInChildren<HitboxRegion>(true).Length);
                    Assert.IsNull(body.P1Skin.GetComponentInChildren<M5GameplayRigStats>(true));
                    Assert.IsNull(body.P2Skin.GetComponentInChildren<M5GameplayRigStats>(true));

                    M5MountAssembler.Disassemble(body);
                    Object.DestroyImmediate(rig);
                }
            }
        }

        [Test]
        public void WeaponsMountAtContractAnchors()
        {
            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(M6AssetConventions.RigPrefabPath);
            Assert.IsNotNull(rigPrefab);
            string riflePath = FindPrefabLike(M6AssetConventions.WeaponDir, M6AssetConventions.WeaponPrefix);
            string grenadePath = FindPrefabLike(M6AssetConventions.WeaponDir, M6AssetConventions.UtilityPrefix);
            Assert.IsNotNull(riflePath, "rifle prefab missing");
            Assert.IsNotNull(grenadePath, "grenade prefab missing");

            GameObject rig = Object.Instantiate(rigPrefab);
            GameObject rifle = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(riflePath), Find(rig.transform, "WeaponAnchor"), false);
            GameObject grenade = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(grenadePath), Find(rig.transform, "UtilityAnchor"), false);

            Assert.AreEqual("WeaponAnchor", rifle.transform.parent.name);
            Assert.AreEqual("UtilityAnchor", grenade.transform.parent.name);
            Assert.IsNotNull(rifle.GetComponent<M6WeaponDescriptor>());
            Assert.AreEqual(M6WeaponKind.Utility, grenade.GetComponent<M6WeaponDescriptor>().Kind);

            Object.DestroyImmediate(rig);
        }

        [Test]
        public void P2HandsGripTheWeaponInEveryCombination()
        {
            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(M6AssetConventions.RigPrefabPath);
            string riflePath = FindPrefabLike(M6AssetConventions.WeaponDir, M6AssetConventions.WeaponPrefix);
            string p1Path = FindPrefabs(M6AssetConventions.P1Dir)[0];
            List<string> p2 = FindPrefabs(M6AssetConventions.P2Dir);
            Assert.IsNotNull(rigPrefab);
            Assert.IsNotNull(riflePath, "rifle prefab missing");

            GameObject p1Skin = AssetDatabase.LoadAssetAtPath<GameObject>(p1Path);
            foreach (string p2Path in p2)
            {
                GameObject rig = Object.Instantiate(rigPrefab);
                M5AssembledBody body = M5MountAssembler.Assemble(rig, p1Skin, AssetDatabase.LoadAssetAtPath<GameObject>(p2Path));
                Assert.IsTrue(body.IsValid, body.Report.ToString());

                Transform socket = Find(rig.transform, "WeaponAnchor");
                GameObject rifle = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(riflePath), socket, false);
                Bounds weapon = ComputeBounds(rifle);

                int hands = 0;
                foreach (Transform t in body.P2Skin.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith("Hand_")) continue;
                    hands++;
                    float distance = weapon.SqrDistance(t.position);
                    Assert.LessOrEqual(distance, 0.20f * 0.20f,
                        $"{Path.GetFileNameWithoutExtension(p2Path)}: {t.name} is {Mathf.Sqrt(distance):0.00}m from the weapon");
                }
                Assert.GreaterOrEqual(hands, 2, "P2 skin should expose two hands for the grip check");

                M5MountAssembler.Disassemble(body);
                Object.DestroyImmediate(rig);
            }
        }

        static Bounds ComputeBounds(GameObject go)
        {
            Bounds bounds = new Bounds();
            bool first = true;
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        static List<string> FindPrefabs(string modelDir)
        {
            string folder = $"{modelDir}/{M6AssetConventions.PrefabSubdir}";
            var results = new List<string>();
            if (!AssetDatabase.IsValidFolder(folder)) return results;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                results.Add(AssetDatabase.GUIDToAssetPath(guid));
            results.Sort();
            return results;
        }

        static string FindPrefabLike(string modelDir, string prefix)
        {
            foreach (string path in FindPrefabs(modelDir))
                if (Path.GetFileNameWithoutExtension(path).StartsWith(prefix)) return path;
            return null;
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }
    }
}
