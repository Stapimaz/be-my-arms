using System.Collections.Generic;
using System.Text;

namespace BeMyArms.M7
{
    /// <summary>
    /// The content the project must contain to field a shippable Duel/2v2 match set. Pure data so it
    /// can be unit-tested and reused by CI; the editor gathers the real project contents into a
    /// snapshot and evaluates it.
    /// </summary>
    public class M7ContentSnapshot
    {
        public int DuelMaps;
        public int TwoVsTwoMaps;
        public int P1Skins;
        public int P2Skins;
        public int Weapons;
        public int Utility;
        public int EnvironmentPieces;
        public int MapKitPieces;
        public int SfxClips;
        public int MusicClips;
        public int VfxEffects;
    }

    /// <summary>Minimum content volume for a shippable match set. All values are production targets.</summary>
    public class M7ContentRequirements
    {
        public int MinDuelMaps = 1;
        public int MinTwoVsTwoMaps = 1;
        public int MinP1Skins = 2;
        public int MinP2Skins = 2;
        public int MinWeapons = 1;
        public int MinUtility = 1;
        public int MinEnvironmentPieces = 4;
        public int MinMapKitPieces = 12;
        public int MinSfxClips = 15;
        public int MinMusicClips = 2;
        public int MinVfxEffects = 8;
    }

    public class M7ContentReport
    {
        public readonly List<string> Failures = new List<string>();
        public readonly List<string> Notes = new List<string>();
        public bool IsValid => Failures.Count == 0;

        public void Require(string label, int actual, int required)
        {
            if (actual < required) Failures.Add($"{label}: {actual} < required {required}");
            else Notes.Add($"{label}: {actual} (>= {required})");
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(IsValid ? "MATCH SET READY" : "MATCH SET INCOMPLETE");
            for (int i = 0; i < Failures.Count; i++) sb.AppendLine("  MISSING: " + Failures[i]);
            for (int i = 0; i < Notes.Count; i++) sb.AppendLine("  ok: " + Notes[i]);
            return sb.ToString().TrimEnd();
        }
    }

    public static class M7ContentManifest
    {
        public static M7ContentReport Evaluate(M7ContentSnapshot snapshot, M7ContentRequirements requirements = null)
        {
            requirements = requirements ?? new M7ContentRequirements();
            var report = new M7ContentReport();
            if (snapshot == null)
            {
                report.Failures.Add("no content snapshot");
                return report;
            }

            report.Require("Duel maps", snapshot.DuelMaps, requirements.MinDuelMaps);
            report.Require("2v2 maps", snapshot.TwoVsTwoMaps, requirements.MinTwoVsTwoMaps);
            report.Require("P1 skins", snapshot.P1Skins, requirements.MinP1Skins);
            report.Require("P2 skins", snapshot.P2Skins, requirements.MinP2Skins);
            report.Require("weapons", snapshot.Weapons, requirements.MinWeapons);
            report.Require("utility", snapshot.Utility, requirements.MinUtility);
            report.Require("environment pieces", snapshot.EnvironmentPieces, requirements.MinEnvironmentPieces);
            report.Require("map kit pieces", snapshot.MapKitPieces, requirements.MinMapKitPieces);
            report.Require("SFX clips", snapshot.SfxClips, requirements.MinSfxClips);
            report.Require("music/ambience clips", snapshot.MusicClips, requirements.MinMusicClips);
            report.Require("VFX effects", snapshot.VfxEffects, requirements.MinVfxEffects);
            return report;
        }
    }
}
