using System.Collections.Generic;
using System.IO;
using System.Text;
using BeMyArms.M5;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M6.EditorTools
{
    public class M6ValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Notes = new List<string>();
        public bool IsValid => Errors.Count == 0;

        public void Error(string message) => Errors.Add(message);
        public void Note(string message) => Notes.Add(message);

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(IsValid ? "VALID" : "INVALID");
            for (int i = 0; i < Errors.Count; i++) sb.AppendLine("  ERROR: " + Errors[i]);
            for (int i = 0; i < Notes.Count; i++) sb.AppendLine("  note: " + Notes[i]);
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Validates the imported production assets against the M6 conventions and the M5 rig contract,
    /// and proves arbitrary P1 x P2 mounting with weapons attached to the correct anchors.
    /// </summary>
    public static class M6AssetValidator
    {
        public static M6ValidationReport ValidateAll()
        {
            var report = new M6ValidationReport();
            ValidateImports(report);
            ValidateRig(report);
            ValidateCombinations(report);
            return report;
        }

        // ---- Imported asset conventions ----

        static void ValidateImports(M6ValidationReport report)
        {
            foreach (string model in FindModels(M6AssetConventions.ArtRoot))
            {
                string name = Path.GetFileNameWithoutExtension(model);
                Bounds bounds = MeasureInstanced(model);
                Vector3 size = bounds.size;
                int triangles = CountTriangles(model);
                int budget = model.StartsWith(M6AssetConventions.P1Dir) ? M6AssetConventions.P1TriangleBudget
                    : model.StartsWith(M6AssetConventions.P2Dir) ? M6AssetConventions.P2TriangleBudget
                    : model.StartsWith(M6AssetConventions.WeaponDir) ? M6AssetConventions.WeaponTriangleBudget
                    : M6AssetConventions.EnvironmentTriangleBudget;
                if (triangles > budget)
                    report.Error($"{name}: {triangles} triangles exceeds LOD0 budget {budget}");

                if (AssetImporter.GetAtPath(model) is ModelImporter importer)
                {
                    if (!Mathf.Approximately(importer.globalScale, 1f))
                        report.Error($"{name}: globalScale {importer.globalScale} != 1");
                    if (!importer.useFileScale)
                        report.Error($"{name}: useFileScale is off (scale may be wrong)");
                    if (importer.bakeAxisConversion)
                        report.Error($"{name}: bakeAxisConversion should be off (Blender exports Y-up)");
                }
                else
                {
                    report.Error($"{name}: no ModelImporter");
                }

                if (model.StartsWith(M6AssetConventions.P1Dir))
                {
                    RequirePrefix(name, M6AssetConventions.P1Prefix, report);
                    RequireRange(name + " height", size.y, M6AssetConventions.P1HeightRange, report);
                }
                else if (model.StartsWith(M6AssetConventions.P2Dir))
                {
                    RequirePrefix(name, M6AssetConventions.P2Prefix, report);
                    RequireRange(name + " size", Mathf.Max(size.x, size.y, size.z), M6AssetConventions.P2SizeRange, report);
                }
                else if (model.StartsWith(M6AssetConventions.WeaponDir))
                {
                    bool utility = name.StartsWith(M6AssetConventions.UtilityPrefix);
                    if (!utility) RequirePrefix(name, M6AssetConventions.WeaponPrefix, report);
                    Vector2 range = utility ? M6AssetConventions.UtilitySizeRange : M6AssetConventions.WeaponLengthRange;
                    RequireRange(name + (utility ? " size" : " length"), Mathf.Max(size.x, size.y, size.z), range, report);
                }
                else if (model.StartsWith(M6AssetConventions.EnvironmentDir))
                {
                    RequirePrefix(name, M6AssetConventions.EnvironmentPrefix, report);
                    if (Mathf.Max(size.x, size.y, size.z) > M6AssetConventions.EnvironmentPieceMax.y + 0.01f)
                        report.Error($"{name}: environment piece too large ({size})");
                }

                report.Note($"{name}: tris={triangles} size=({size.x:0.000},{size.y:0.000},{size.z:0.000})");
            }
        }

        static void RequirePrefix(string name, string prefix, M6ValidationReport report)
        {
            if (!name.StartsWith(prefix)) report.Error($"{name}: expected name prefix '{prefix}'");
        }

        static void RequireRange(string label, float value, Vector2 range, M6ValidationReport report)
        {
            if (value < range.x || value > range.y)
                report.Error($"{label}: {value:0.000} outside [{range.x}, {range.y}]");
        }

        // ---- Contract on the rig ----

        static void ValidateRig(M6ValidationReport report)
        {
            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(M6AssetConventions.RigPrefabPath);
            if (rigPrefab == null)
            {
                report.Error("shared body rig prefab missing: " + M6AssetConventions.RigPrefabPath);
                return;
            }

            GameObject rig = Object.Instantiate(rigPrefab);
            M5RigValidationReport contract = M5RigValidator.Validate(rig, M5RigContract.Default());
            if (!contract.IsValid) report.Error("rig contract: " + contract);
            else report.Note("rig contract valid on " + rigPrefab.name);
            foreach (string anchor in new[] { "WeaponAnchor", "UtilityAnchor", "P2CameraAnchor", "P1CameraAnchor", "ShoulderAnchor" })
                if (Find(rig.transform, anchor) == null) report.Error($"rig missing anchor '{anchor}'");
            Object.DestroyImmediate(rig);
        }

        // ---- Arbitrary P1 x P2 mounting ----

        static void ValidateCombinations(M6ValidationReport report)
        {
            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(M6AssetConventions.RigPrefabPath);
            List<string> p1 = FindPrefabs(M6AssetConventions.P1Dir);
            List<string> p2 = FindPrefabs(M6AssetConventions.P2Dir);
            if (rigPrefab == null || p1.Count == 0 || p2.Count == 0)
            {
                report.Error($"combination proof needs rig + P1/P2 prefabs (p1={p1.Count} p2={p2.Count})");
                return;
            }

            string riflePath = FindPrefabLike(M6AssetConventions.WeaponDir, M6AssetConventions.WeaponPrefix);
            string utilityPath = FindPrefabLike(M6AssetConventions.WeaponDir, M6AssetConventions.UtilityPrefix);

            int valid = 0;
            for (int i = 0; i < p1.Count; i++)
            {
                for (int j = 0; j < p2.Count; j++)
                {
                    GameObject rig = Object.Instantiate(rigPrefab);
                    string baseHitboxes = string.Join("|", M5RigValidator.CaptureHitboxSignature(rig));
                    string baseStats = M5RigValidator.CaptureStatsSignature(rig);

                    GameObject p1Skin = AssetDatabase.LoadAssetAtPath<GameObject>(p1[i]);
                    GameObject p2Skin = AssetDatabase.LoadAssetAtPath<GameObject>(p2[j]);
                    M5AssembledBody body = M5MountAssembler.Assemble(rig, p1Skin, p2Skin);
                    string label = $"{Path.GetFileNameWithoutExtension(p1[i])} + {Path.GetFileNameWithoutExtension(p2[j])}";

                    if (!body.IsValid)
                    {
                        report.Error($"combination {label}: {body.Report}");
                    }
                    else
                    {
                        valid++;
                        if (string.Join("|", M5RigValidator.CaptureHitboxSignature(rig)) != baseHitboxes)
                            report.Error($"combination {body.P1SkinId}+{body.P2SkinId}: authoritative hitboxes changed");
                        if (M5RigValidator.CaptureStatsSignature(rig) != baseStats)
                            report.Error($"combination {body.P1SkinId}+{body.P2SkinId}: gameplay stats changed");

                        // Weapons mount at contract anchors and are visibly gripped by P2's hands.
                        ValidateWeaponMount(rig, body, "WeaponAnchor", riflePath, checkGrip: true, report, label);
                        ValidateWeaponMount(rig, body, "UtilityAnchor", utilityPath, checkGrip: false, report, label);
                    }

                    M5MountAssembler.Disassemble(body);
                    Object.DestroyImmediate(rig);
                }
            }

            report.Note($"combinations valid: {valid}/{p1.Count * p2.Count} (P1={p1.Count}, P2={p2.Count})");
        }

        static void ValidateWeaponMount(GameObject rig, M5AssembledBody body, string socketName, string prefabPath, bool checkGrip, M6ValidationReport report, string label)
        {
            if (string.IsNullOrEmpty(prefabPath)) return;
            Transform socket = Find(rig.transform, socketName);
            if (socket == null) { report.Error($"{label}: missing socket '{socketName}'"); return; }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { report.Error($"{label}: weapon prefab missing {prefabPath}"); return; }
            GameObject instance = Object.Instantiate(prefab, socket, false);
            instance.name = "attached_" + Path.GetFileNameWithoutExtension(prefabPath);
            instance.transform.localPosition = Vector3.zero;
            if (instance.transform.parent != socket)
                report.Error($"{label}: {instance.name} did not mount under {socketName}");

            if (checkGrip && body != null && body.P2Skin != null)
            {
                Bounds weapon = ComputeBounds(instance);
                int hands = 0;
                foreach (Transform t in body.P2Skin.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith("Hand_")) continue;
                    hands++;
                    float distance = weapon.SqrDistance(t.position);
                    if (distance > 0.20f * 0.20f)
                        report.Error($"{label}: {t.name} is {Mathf.Sqrt(distance):0.00}m from the weapon bounds (weapon should read as held)");
                }
                if (hands == 0) report.Note($"{label}: P2 skin has no Hand_* transforms for the grip check");
            }

            Object.DestroyImmediate(instance);
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

        // ---- Helpers ----

        static int CountTriangles(string modelPath)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return 0;
            GameObject instance = Object.Instantiate(model);
            int triangles = 0;
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh != null) triangles += mesh.triangles.Length / 3;
            }
            Object.DestroyImmediate(instance);
            return triangles;
        }

        static Bounds MeasureInstanced(string modelPath)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return new Bounds(Vector3.zero, Vector3.zero);
            GameObject instance = Object.Instantiate(model);
            Bounds bounds = new Bounds();
            bool first = true;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            Object.DestroyImmediate(instance);
            return bounds;
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
