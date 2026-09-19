using System.Collections.Generic;
using System.Text;
using BeMyArms.M0;
using UnityEngine;

namespace BeMyArms.M5
{
    public class M5RigValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public bool IsValid => Errors.Count == 0;
        public void Add(string error) => Errors.Add(error);
        public override string ToString() => IsValid ? "valid" : string.Join("; ", Errors);
    }

    /// <summary>
    /// Validates a combined shared body against the <see cref="M5RigContract"/>: required sockets,
    /// anchors, authoritative hitboxes, the animation interface, and the invariant that cosmetic
    /// layers add no hitboxes and no gameplay-stat components. Pure inspection — it never changes the
    /// rig. Used by automated tests and at runtime before presenting a combination.
    /// </summary>
    public static class M5RigValidator
    {
        public static M5RigValidationReport Validate(GameObject root, M5RigContract contract)
        {
            var report = new M5RigValidationReport();
            if (root == null || contract == null)
            {
                report.Add("root or contract is null");
                return report;
            }

            if (root.name != contract.RootName)
                report.Add($"root is '{root.name}', expected '{contract.RootName}'");

            // 1. Required sockets and anchors, with their expected parent path.
            for (int i = 0; i < contract.Sockets.Count; i++)
            {
                M5SocketSpec spec = contract.Sockets[i];
                Transform found = Find(root.transform, spec.Name);
                if (found == null)
                {
                    report.Add($"missing socket '{spec.Name}'");
                    continue;
                }
                if (!ParentPathMatches(found, root.transform, spec.ParentPath))
                    report.Add($"socket '{spec.Name}' has wrong parent chain (expected '{spec.ParentPath}')");
            }

            // 2. Authoritative hitboxes: present, correct region, and no extras anywhere on the rig.
            var allowed = new HashSet<string>();
            for (int i = 0; i < contract.Hitboxes.Count; i++) allowed.Add(contract.Hitboxes[i].Name);

            HitboxRegion[] hitboxes = root.GetComponentsInChildren<HitboxRegion>(true);
            for (int i = 0; i < contract.Hitboxes.Count; i++)
            {
                M5HitboxSpec spec = contract.Hitboxes[i];
                HitboxRegion match = null;
                for (int h = 0; h < hitboxes.Length; h++)
                    if (hitboxes[h].gameObject.name == spec.Name) { match = hitboxes[h]; break; }

                if (match == null)
                {
                    report.Add($"missing hitbox '{spec.Name}'");
                    continue;
                }
                if (match.region.ToString() != spec.Region)
                    report.Add($"hitbox '{spec.Name}' region {match.region} != {spec.Region}");
            }

            for (int h = 0; h < hitboxes.Length; h++)
            {
                if (!allowed.Contains(hitboxes[h].gameObject.name))
                    report.Add($"unexpected hitbox '{hitboxes[h].gameObject.name}' (cosmetics must not add hitboxes)");
            }

            // 3. Animation interface.
            bool hasAnimation = false;
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IM5RigAnimation) { hasAnimation = true; break; }
            if (!hasAnimation) report.Add("missing animation interface (IM5RigAnimation)");

            // 4. Gameplay stats component must exist on the authoritative rig.
            if (root.GetComponentInChildren<M5GameplayRigStats>(true) == null)
                report.Add("missing gameplay stats component (M5GameplayRigStats)");

            // 5. Cosmetic layers must be presentation-only.
            ValidateCosmeticLayer(root.transform, contract.P1CosmeticSocket, report);
            ValidateCosmeticLayer(root.transform, contract.P2CosmeticSocket, report);

            return report;
        }

        static void ValidateCosmeticLayer(Transform root, string socketName, M5RigValidationReport report)
        {
            Transform socket = Find(root, socketName);
            if (socket == null) return; // missing socket already reported

            HitboxRegion[] hitboxes = socket.GetComponentsInChildren<HitboxRegion>(true);
            for (int i = 0; i < hitboxes.Length; i++)
                report.Add($"cosmetic layer '{socketName}' contains hitbox '{hitboxes[i].gameObject.name}'");

            MonoBehaviour[] behaviours = socket.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IM5GameplayStatSource)
                    report.Add($"cosmetic layer '{socketName}' contains gameplay stat source '{behaviours[i].GetType().Name}'");
        }

        /// <summary>A stable signature of the authoritative hitboxes, so combinations can be compared.</summary>
        public static List<string> CaptureHitboxSignature(GameObject root)
        {
            var signature = new List<string>();
            if (root == null) return signature;
            HitboxRegion[] hitboxes = root.GetComponentsInChildren<HitboxRegion>(true);
            for (int i = 0; i < hitboxes.Length; i++)
            {
                Transform t = hitboxes[i].transform;
                string line = $"{t.name}|{hitboxes[i].region}|{Round(t.localPosition)}|{Round(t.localScale)}";
                signature.Add(line);
            }
            signature.Sort(System.StringComparer.Ordinal);
            return signature;
        }

        /// <summary>A stable signature of the authoritative gameplay stats.</summary>
        public static string CaptureStatsSignature(GameObject root)
        {
            M5GameplayRigStats stats = root != null ? root.GetComponentInChildren<M5GameplayRigStats>(true) : null;
            if (stats == null) return "no-stats";
            return $"hp={stats.MaxHealth};speed={stats.MoveSpeed};sector={stats.SectorHalfDegrees};radius={stats.BodyRadius}";
        }

        static string Round(Vector3 v)
            => $"{v.x:0.###},{v.y:0.###},{v.z:0.###}";

        static Transform Find(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            if (root.name == name) return root;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }

        static bool ParentPathMatches(Transform target, Transform root, string expectedPath)
        {
            var names = new List<string>();
            Transform current = target.parent;
            while (current != null && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            string actual = string.Join("/", names);
            return actual == (expectedPath ?? "");
        }
    }
}
