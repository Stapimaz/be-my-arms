using System.Collections.Generic;
using BeMyArms.Core;
using NUnit.Framework;
using UnityEngine;

namespace BeMyArms.Product.Tests
{
    /// <summary>
    /// The Product acceptance proof: the standardized P1/P2 rig contract plus the combinatorial
    /// "any P1 skin with any P2 skin" mount test, and the invariant that cosmetics never alter
    /// authoritative hitboxes or gameplay stats.
    /// </summary>
    public class RigContractTests
    {
        readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        GameObject Track(GameObject go)
        {
            _created.Add(go);
            return go;
        }

        // ---- Contract validation ----

        [Test]
        public void DefaultContract_ValidatesPlaceholderRig()
        {
            GameObject rig = Track(PlaceholderRigFactory.CreateGameplayRig());
            RigValidationReport report = RigValidator.Validate(rig, RigContract.Default());
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        [Test]
        public void MissingCosmeticSocket_IsRejected()
        {
            GameObject rig = Track(PlaceholderRigFactory.CreateGameplayRig());
            Transform socket = Find(rig.transform, "Cosmetic_P2");
            Assert.IsNotNull(socket);
            Object.DestroyImmediate(socket.gameObject);

            GameObject p1 = Track(PlaceholderRigFactory.CreateSkin(RigRole.P1, PlaceholderRigFactory.P1Variants[0]));
            GameObject p2 = Track(PlaceholderRigFactory.CreateSkin(RigRole.P2, PlaceholderRigFactory.P2Variants[0]));
            AssembledBody body = MountAssembler.Assemble(rig, p1, p2);

            Assert.IsFalse(body.IsValid);
            ReportContains(body.Report, "Cosmetic_P2");
        }

        [Test]
        public void WrongRoleSkin_IsRejected()
        {
            GameObject rig = Track(PlaceholderRigFactory.CreateGameplayRig());
            GameObject p1 = Track(PlaceholderRigFactory.CreateSkin(RigRole.P1, PlaceholderRigFactory.P1Variants[0]));
            GameObject p2 = Track(PlaceholderRigFactory.CreateSkin(RigRole.P2, PlaceholderRigFactory.P2Variants[0]));

            // Feed the P2 skin into the P1 socket.
            AssembledBody body = MountAssembler.Assemble(rig, p2, p1);
            Assert.IsFalse(body.IsValid);
            ReportContains(body.Report, "P1 socket received a P2 skin");
        }

        // ---- Cosmetic safety negatives ----

        [Test]
        public void CosmeticWithHitbox_IsRejected()
        {
            GameObject rig = Track(PlaceholderRigFactory.CreateGameplayRig());
            GameObject p1 = Track(PlaceholderRigFactory.CreateSkin(RigRole.P1, PlaceholderRigFactory.P1Variants[0]));
            GameObject p2 = Track(PlaceholderRigFactory.CreateSkin(RigRole.P2, PlaceholderRigFactory.P2Variants[0]));

            var sneaky = new GameObject("SneakyHitbox");
            sneaky.transform.SetParent(p1.transform, false);
            sneaky.AddComponent<HitboxRegion>().region = HitboxRegion.Region.Head;

            AssembledBody body = MountAssembler.Assemble(rig, p1, p2);
            Assert.IsFalse(body.IsValid);
            ReportContains(body.Report, "contains hitbox");
        }

        [Test]
        public void CosmeticWithGameplayStats_IsRejected()
        {
            GameObject rig = Track(PlaceholderRigFactory.CreateGameplayRig());
            GameObject p1 = Track(PlaceholderRigFactory.CreateSkin(RigRole.P1, PlaceholderRigFactory.P1Variants[0]));
            GameObject p2 = Track(PlaceholderRigFactory.CreateSkin(RigRole.P2, PlaceholderRigFactory.P2Variants[0]));

            p2.AddComponent<GameplayRigStats>();

            AssembledBody body = MountAssembler.Assemble(rig, p1, p2);
            Assert.IsFalse(body.IsValid);
            ReportContains(body.Report, "gameplay stat source");
        }

        // ---- The combinatorial proof ----

        [Test]
        public void EveryP1SkinCombinesWithEveryP2Skin()
        {
            GameObject rig = Track(PlaceholderRigFactory.CreateGameplayRig());
            List<GameObject> p1Skins = PlaceholderRigFactory.CreateSkinVariants(RigRole.P1, 3);
            List<GameObject> p2Skins = PlaceholderRigFactory.CreateSkinVariants(RigRole.P2, 3);
            for (int i = 0; i < p1Skins.Count; i++) Track(p1Skins[i]);
            for (int i = 0; i < p2Skins.Count; i++) Track(p2Skins[i]);

            string baseHitboxes = Join(RigValidator.CaptureHitboxSignature(rig));
            string baseStats = RigValidator.CaptureStatsSignature(rig);

            int combinations = 0;
            for (int i = 0; i < p1Skins.Count; i++)
            {
                for (int j = 0; j < p2Skins.Count; j++)
                {
                    AssembledBody body = MountAssembler.Assemble(rig, p1Skins[i], p2Skins[j]);
                    combinations++;

                    Assert.IsTrue(body.IsValid, $"{body.P1SkinId}+{body.P2SkinId}: {body.Report}");
                    Assert.AreEqual(p1Skins[i].GetComponent<SkinDescriptor>().SkinId, body.P1SkinId);
                    Assert.AreEqual(p2Skins[j].GetComponent<SkinDescriptor>().SkinId, body.P2SkinId);

                    // Cosmetics do not move authoritative hitboxes or stats.
                    Assert.AreEqual(baseHitboxes, Join(RigValidator.CaptureHitboxSignature(rig)), "hitboxes changed");
                    Assert.AreEqual(baseStats, RigValidator.CaptureStatsSignature(rig), "stats changed");

                    // Cosmetic layers carry no hitboxes and no gameplay stats.
                    Assert.AreEqual(0, body.P1Skin.GetComponentsInChildren<HitboxRegion>(true).Length);
                    Assert.AreEqual(0, body.P2Skin.GetComponentsInChildren<HitboxRegion>(true).Length);
                    Assert.IsNull(body.P1Skin.GetComponentInChildren<GameplayRigStats>(true));
                    Assert.IsNull(body.P2Skin.GetComponentInChildren<GameplayRigStats>(true));

                    MountAssembler.Disassemble(body);
                }
            }

            Assert.AreEqual(9, combinations, "3 P1 skins x 3 P2 skins.");
        }

        [Test]
        public void CombinedBody_KeepsIndependentP1P2Identities()
        {
            GameObject rig = Track(PlaceholderRigFactory.CreateGameplayRig());
            GameObject p1 = Track(PlaceholderRigFactory.CreateSkin(RigRole.P1, PlaceholderRigFactory.P1Variants[0]));
            GameObject p2 = Track(PlaceholderRigFactory.CreateSkin(RigRole.P2, PlaceholderRigFactory.P2Variants[2]));

            AssembledBody body = MountAssembler.Assemble(rig, p1, p2);
            Assert.IsTrue(body.IsValid, body.Report.ToString());
            Assert.AreNotEqual(body.P1SkinId, body.P2SkinId, "P1 and P2 cosmetic identities stay distinct.");
            Assert.AreEqual(RigRole.P1, body.P1Skin.GetComponent<SkinDescriptor>().Role);
            Assert.AreEqual(RigRole.P2, body.P2Skin.GetComponent<SkinDescriptor>().Role);

            // Disassembly removes the cosmetics and leaves the rig contract valid.
            MountAssembler.Disassemble(body);
            Assert.IsTrue(RigValidator.Validate(rig, RigContract.Default()).IsValid);
        }

        static string Join(List<string> items) => string.Join("\n", items);

        static Transform Find(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }

        static void ReportContains(RigValidationReport report, string fragment)
        {
            for (int i = 0; i < report.Errors.Count; i++)
                if (report.Errors[i].Contains(fragment)) return;
            Assert.Fail($"expected report to contain '{fragment}', got: {report}");
        }
    }
}
