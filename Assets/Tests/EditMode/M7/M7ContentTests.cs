using BeMyArms.M3;
using BeMyArms.M7;
using BeMyArms.M7.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M7.Tests
{
    /// <summary>
    /// M7 acceptance proof: the pure match-set manifest behaves correctly, and the real project
    /// content (maps, audio, VFX, map kit) satisfies the shippable match-set requirements.
    /// </summary>
    public class M7ContentTests
    {
        // ---- Pure manifest ----

        [Test]
        public void Manifest_FailsWhenContentMissing()
        {
            var report = M7ContentManifest.Evaluate(new M7ContentSnapshot());
            Assert.IsFalse(report.IsValid);
            Assert.Greater(report.Failures.Count, 0);
        }

        [Test]
        public void Manifest_PassesForACompleteMatchSet()
        {
            var snapshot = new M7ContentSnapshot
            {
                DuelMaps = 1, TwoVsTwoMaps = 1, P1Skins = 2, P2Skins = 2, Weapons = 1, Utility = 1,
                EnvironmentPieces = 4, MapKitPieces = 15, SfxClips = 18, MusicClips = 3, VfxEffects = 11
            };
            var report = M7ContentManifest.Evaluate(snapshot);
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        // ---- Real project content ----

        [Test]
        public void Maps_AreValidAndComplete()
        {
            M7MapValidationReport report = M7MapValidator.ValidateAll();
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        [Test]
        public void Maps_CoverDuelAndTwoVsTwo_WithCorrectSpawns()
        {
            int duel = 0, twoVsTwo = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:M7MapDefinition"))
            {
                var map = AssetDatabase.LoadAssetAtPath<M7MapDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (map == null) continue;
                if (map.Family == M7MapFamily.Duel)
                {
                    duel++;
                    Assert.AreEqual(4, map.Spawns.Count, "Duel needs 4 spawns (2 bodies x 2 roles).");
                }
                else
                {
                    twoVsTwo++;
                    Assert.AreEqual(8, map.Spawns.Count, "2v2 needs 8 spawns (4 bodies x 2 roles).");
                }
                Assert.GreaterOrEqual(map.CoverCount, 6);
                Assert.GreaterOrEqual(map.LaneCount, 2);
                Assert.GreaterOrEqual(map.MaxVerticality, 1.0f);
            }
            Assert.GreaterOrEqual(duel, 1);
            Assert.GreaterOrEqual(twoVsTwo, 1);
        }

        [Test]
        public void AudioLibrary_CoversAllEvents()
        {
            var library = AssetDatabase.LoadAssetAtPath<M7AudioLibrary>(M7AudioLibraryBuilder.LibraryPath);
            Assert.IsNotNull(library, "audio library missing");
            foreach (M7AudioId id in M7AudioIds.All)
                Assert.IsNotNull(library.Get(id), "missing audio event " + id);
        }

        [Test]
        public void VfxLibrary_CoversAllEffects()
        {
            var library = AssetDatabase.LoadAssetAtPath<M7VfxLibrary>(M7VfxLibraryBuilder.LibraryPath);
            Assert.IsNotNull(library, "vfx library missing");
            foreach (M7VfxId id in M7VfxIds.All)
                Assert.IsNotNull(library.Get(id), "missing vfx " + id);
        }

        [Test]
        public void Project_MeetsShippableMatchSet()
        {
            M7ContentReport report = M7ContentValidator.Evaluate();
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        [Test]
        public void MapSpawns_BodyPoseIsRoleMidpoint()
        {
            var go = new GameObject("m7_map_spawns");
            var spawns = go.AddComponent<M3MapSpawns>();
            spawns.Spawns.Add(new M3MapSpawn { Team = 0, Body = 0, Role = 0, Position = new Vector3(-0.9f, 0f, -9f), Yaw = 0f });
            spawns.Spawns.Add(new M3MapSpawn { Team = 0, Body = 0, Role = 1, Position = new Vector3(0.9f, 0f, -9f), Yaw = 0f });

            Assert.IsTrue(spawns.TryGetBodyPose(0, 0, out Vector3 position, out float yaw));
            Assert.AreEqual(0f, position.x, 0.0001f, "body spawns at the role midpoint");
            Assert.AreEqual(-9f, position.z, 0.0001f);
            Assert.AreEqual(0f, yaw, 0.0001f);

            Object.DestroyImmediate(go);
        }
    }
}
