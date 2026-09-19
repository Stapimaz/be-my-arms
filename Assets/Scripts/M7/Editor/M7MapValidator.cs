using System.Collections.Generic;
using BeMyArms.M7;
using UnityEditor;
using UnityEngine;

namespace BeMyArms.M7.EditorTools
{
    public class M7MapValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Notes = new List<string>();
        public bool IsValid => Errors.Count == 0;

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(IsValid ? "maps VALID" : "maps INVALID");
            foreach (string e in Errors) sb.AppendLine("  ERROR: " + e);
            foreach (string n in Notes) sb.AppendLine("  note: " + n);
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Validates every <see cref="M7MapDefinition"/> record: family, bounds, complete team/body/role
    /// spawns inside the play area, and the required cover/lane/verticality facts.
    /// </summary>
    public static class M7MapValidator
    {
        public static M7MapValidationReport ValidateAll()
        {
            var report = new M7MapValidationReport();
            string[] guids = AssetDatabase.FindAssets("t:M7MapDefinition");
            if (guids.Length == 0) report.Errors.Add("no map definitions found");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var map = AssetDatabase.LoadAssetAtPath<M7MapDefinition>(path);
                if (map == null) continue;
                Validate(map, path, report);
            }
            return report;
        }

        static void Validate(M7MapDefinition map, string path, M7MapValidationReport report)
        {
            string label = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(map.MapId)) report.Errors.Add($"{label}: empty MapId");
            if (!string.IsNullOrEmpty(map.ScenePath) && !System.IO.File.Exists(map.ScenePath))
                report.Errors.Add($"{label}: scene missing at {map.ScenePath}");

            float halfX = map.BoundsSize.x * 0.5f;
            float halfZ = map.BoundsSize.z * 0.5f;
            if (map.BoundsSize.x < 16f || map.BoundsSize.x > 80f) report.Errors.Add($"{label}: bounds x {map.BoundsSize.x}");
            if (map.BoundsSize.z < 16f || map.BoundsSize.z > 80f) report.Errors.Add($"{label}: bounds z {map.BoundsSize.z}");

            int bodies = map.Family == M7MapFamily.Duel ? 1 : 2;
            for (int team = 0; team < 2; team++)
            {
                for (int body = 0; body < bodies; body++)
                {
                    for (int role = 0; role < 2; role++)
                    {
                        if (!map.TryGetSpawn(team, body, role, out M7Spawn spawn))
                        {
                            report.Errors.Add($"{label}: missing spawn T{team}B{body}P{role + 1}");
                            continue;
                        }
                        if (Mathf.Abs(spawn.Position.x) > halfX || Mathf.Abs(spawn.Position.z) > halfZ)
                            report.Errors.Add($"{label}: spawn T{team}B{body}P{role + 1} outside bounds {spawn.Position}");
                    }
                }
            }

            if (map.CoverCount < 6) report.Errors.Add($"{label}: cover {map.CoverCount} < 6");
            if (map.LaneCount < 2) report.Errors.Add($"{label}: lanes {map.LaneCount} < 2");
            if (map.MaxVerticality < 1.0f) report.Errors.Add($"{label}: verticality {map.MaxVerticality:0.00} < 1.0");
            if (map.MaxVerticality > 6.0f) report.Errors.Add($"{label}: verticality {map.MaxVerticality:0.00} too high");

            report.Notes.Add($"{label}: {map.Family} spawns={map.Spawns.Count} cover={map.CoverCount} lanes={map.LaneCount} verticality={map.MaxVerticality:0.00}");
        }
    }
}
