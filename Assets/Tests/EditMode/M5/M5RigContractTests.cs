using System.Collections.Generic;
using BeMyArms.M0;
using NUnit.Framework;
using UnityEngine;

namespace BeMyArms.M5.Tests
{
    /// <summary>
    /// The M5 acceptance proof: the standardized P1/P2 rig contract plus the combinatorial
    /// "any P1 skin with any P2 skin" mount test, and the invariant that cosmetics never alter
    /// authoritative hitboxes or gameplay stats.
    /// </summary>
    public class M5RigContractTests
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
            GameObject rig = Track(M5PlaceholderRigFactory.CreateGameplayRig());
            M5RigValidationReport report = M5RigValidator.Validate(rig, M5RigContract.Default());
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        [Test]
        public void MissingCosmeticSocket_IsRejected()
        {
            GameObject rig = Track(M5PlaceholderRigFactory.CreateGameplayRig());
            Transform socket = Find(rig.transform, "Cosmetic_P2");
            Assert.IsNotNull(socket);
            Object.DestroyImmediate(socket.gameObject);

            GameObject p1 = Track(M5PlaceholderRigFactory.CreateSkin(M5RigRole.P1, M5PlaceholderRigFactory.P1Variants[0]));
            GameObject p2 = Track(M5PlaceholderRigFactory.CreateSkin(M5RigRole.P2, M5PlaceholderRigFactory.P2Variants[0]));
            M5AssembledBody body = M5MountAssembler.Assemble(rig, p1, p2);

            Assert.IsFalse(body.IsValid);
            ReportContains(body.Report, "Cosmetic_P2");
        }

        [Test]
        public void WrongRoleSkin_IsRejected()
        {
            GameObject rig = Track(M5PlaceholderRigFactory.CreateGameplayRig());
            GameObject p1 = Track(M5PlaceholderRigFactory.CreateSkin(M5RigRole.P1, M5PlaceholderRigFactory.P1Variants[0]));
            GameObject p2 = Track(M5PlaceholderRigFactory.CreateSkin(M5RigRole.P2, M5PlaceholderRigFactory.P2Variants[0]));

            // Feed the P2 skin into the P1 socket.
            M5AssembledBody body = M5MountAssembler.Assemble(rig, p2, p1);
            Assert.IsFalse(body.IsValid);
            ReportContains(body.Report, "P1 socket received a P2 skin");
        }

        // ---- Cosmetic safety negatives ----

        [Test]
        public void CosmeticWithHitbox_IsRejected()
        {
            GameObject rig = Track(M5PlaceholderRigFactory.CreateGameplayRig());
            GameObject p1 = Track(M5PlaceholderRigFactory.CreateSkin(M5RigRole.P1, M5PlaceholderRigFactory.P1Variants[0]));
            GameObject p2 = Track(M5PlaceholderRigFactory.CreateSkin(M5RigRole.P2, M5PlaceholderRigFactory.P2Variants[0]));

            var sneaky = new GameObject("SneakyHitbox");
            sneaky.transform.SetParent(p1.transform, false);
            sneaky.AddComponent<HitboxRegion>().region = HitboxRegion.Region.Head;

            M5AssembledBody body = M5MountAssembler.Assemble(rig, p1, p2);
            Assert.IsFalse(body.IsValid);
            ReportContains(body.Report, "contains hitbox");
        }

        [Test]
        public void CosmeticWithGameplayStats_IsRejected()
        {
            GameObject rig = Track(M5PlaceholderRigFactory.CreateGameplayRig());
            GameObject p1 = Track(M5PlaceholderRigFactory.CreateSkin(M5RigRole.P1, M5PlaceholderRigFactory.P1Variants[0]));
            GameObject p2 = Track(M5PlaceholderRigFactory.CreateSkin(M5RigRole.P2, M5PlaceholderRigFactory.P2Variants[0]));

            p2.AddComponent<M5GameplayRigStats>();

            M5AssembledBody body = M5MountAssembler.Assemble(rig, p1, p2);
            Assert.IsFalse(body.IsValid);
            ReportContains(body.Report, "gameplay stat source");
        }

        // ---- The combinatorial proof ----

        [Test]
        public void EveryP1SkinCombinesWithEveryP2Skin()
        {
            GameObject rig = Track(M5PlaceholderRigFactory.CreateGameplayRig());
            List<GameObject> p1Skins = M5PlaceholderRigFactory.CreateSkinVariants(M5RigRole.P1, 3);
            List<GameObject> p2Skins = M5PlaceholderRigFactory.CreateSkinVariants(M5RigRole.P2, 3);
            for (int i = 0; i < p1Skins.Count; i++) Track(p1Skins[i]);
            for (int i = 0; i < p2Skins.Count; i++) Track(p2Skins[i]);

            string baseHitboxes = Join(M5RigValidator.CaptureHitboxSignature(rig));
            string baseStats = M5RigValidator.CaptureStatsSignature(rig);

            int combinations = 0;
            for (int i = 0; i < p1Skins.Count; i++)
            {
                for (int j = 0; j < p2Skins.Count; j++)
                {
                    M5AssembledBody body = M5MountAssembler.Assemble(rig, p1Skins[i], p2Skins[j]);
                    combinations++;

                    Assert.IsTrue(body.IsValid, $"{body.P1SkinId}+{body.P2SkinId}: {body.Report}");
                    Assert.AreEqual(p1Skins[i].GetComponent<M5SkinDescriptor>().SkinId, body.P1SkinId);
                    Assert.AreEqual(p2Skins[j].GetComponent<M5SkinDescriptor>().SkinId, body.P2SkinId);

                    // Cosmetics do not move authoritative hitboxes or stats.
                    Assert.AreEqual(baseHitboxes, Join(M5RigValidator.CaptureHitboxSignature(rig)), "hitboxes changed");
                    Assert.AreEqual(baseStats, M5RigValidator.CaptureStatsSignature(rig), "stats changed");

                    // Cosmetic layers carry no hitboxes and no gameplay stats.
                    Assert.AreEqual(0, body.P1Skin.GetComponentsInChildren<HitboxRegion>(true).Length);
                    Assert.AreEqual(0, body.P2Skin.GetComponentsInChildren<HitboxRegion>(true).Length);
                    Assert.IsNull(body.P1Skin.GetComponentInChildren<M5GameplayRigStats>(true));
                    Assert.IsNull(body.P2Skin.GetComponentInChildren<M5GameplayRigStats>(true));

                    M5MountAssembler.Disassemble(body);
                }
            }

            Assert.AreEqual(9, combinations, "3 P1 skins x 3 P2 skins.");
        }

        [Test]
        public void CombinedBody_KeepsIndependentP1P2Identities()
        {
            GameObject rig = Track(M5PlaceholderRigFactory.CreateGameplayRig());
            GameObject p1 = Track(M5PlaceholderRigFactory.CreateSkin(M5RigRole.P1, M5PlaceholderRigFactory.P1Variants[0]));
            GameObject p2 = Track(M5PlaceholderRigFactory.CreateSkin(M5RigRole.P2, M5PlaceholderRigFactory.P2Variants[2]));

            M5AssembledBody body = M5MountAssembler.Assemble(rig, p1, p2);
            Assert.IsTrue(body.IsValid, body.Report.ToString());
            Assert.AreNotEqual(body.P1SkinId, body.P2SkinId, "P1 and P2 cosmetic identities stay distinct.");
            Assert.AreEqual(M5RigRole.P1, body.P1Skin.GetComponent<M5SkinDescriptor>().Role);
            Assert.AreEqual(M5RigRole.P2, body.P2Skin.GetComponent<M5SkinDescriptor>().Role);

            // Disassembly removes the cosmetics and leaves the rig contract valid.
            M5MountAssembler.Disassemble(body);
            Assert.IsTrue(M5RigValidator.Validate(rig, M5RigContract.Default()).IsValid);
        }

        static string Join(List<string> items) => string.Join("\n", items);

        static Transform Find(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }

        static void ReportContains(M5RigValidationReport report, string fragment)
        {
            for (int i = 0; i < report.Errors.Count; i++)
                if (report.Errors[i].Contains(fragment)) return;
            Assert.Fail($"expected report to contain '{fragment}', got: {report}");
        }
    }
}
