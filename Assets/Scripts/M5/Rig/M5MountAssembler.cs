using UnityEngine;

namespace BeMyArms.M5
{
    /// <summary>The result of combining one P1 skin with one P2 skin on a gameplay rig.</summary>
    public class M5AssembledBody
    {
        public GameObject Root;
        public GameObject P1Skin;
        public GameObject P2Skin;
        public string P1SkinId = "";
        public string P2SkinId = "";
        public M5RigValidationReport Report;

        public bool IsValid => Report != null && Report.IsValid;
    }

    /// <summary>
    /// Combines an arbitrary P1 skin with an arbitrary P2 skin on the standardized gameplay rig, at
    /// the contract's cosmetic sockets, then validates the result. Because both skins are authored
    /// against the same contract and mounted at fixed sockets, no pair-specific work is required —
    /// this is the mechanism the M5 acceptance proof exercises across every combination.
    /// </summary>
    public static class M5MountAssembler
    {
        public static M5AssembledBody Assemble(GameObject gameplayRig, GameObject p1Skin, GameObject p2Skin, M5RigContract contract = null)
        {
            contract = contract ?? M5RigContract.Default();
            var body = new M5AssembledBody { Root = gameplayRig };
            if (gameplayRig == null)
            {
                body.Report = new M5RigValidationReport();
                body.Report.Add("no gameplay rig");
                return body;
            }

            body.P1Skin = Mount(gameplayRig, contract.P1CosmeticSocket, p1Skin, M5RigRole.P1, out string p1Error, out string p1Id);
            body.P2Skin = Mount(gameplayRig, contract.P2CosmeticSocket, p2Skin, M5RigRole.P2, out string p2Error, out string p2Id);
            body.P1SkinId = p1Id;
            body.P2SkinId = p2Id;

            body.Report = M5RigValidator.Validate(gameplayRig, contract);
            if (p1Error != null) body.Report.Add(p1Error);
            if (p2Error != null) body.Report.Add(p2Error);
            return body;
        }

        /// <summary>Removes the mounted cosmetic layers, leaving the authoritative rig untouched.</summary>
        public static void Disassemble(M5AssembledBody body)
        {
            if (body == null) return;
            Destroy(body.P1Skin);
            Destroy(body.P2Skin);
            body.P1Skin = null;
            body.P2Skin = null;
        }

        static GameObject Mount(GameObject rig, string socketName, GameObject skin, M5RigRole role, out string error, out string skinId)
        {
            error = null;
            skinId = "";
            Transform socket = Find(rig.transform, socketName);
            if (socket == null) { error = $"missing cosmetic socket '{socketName}'"; return null; }
            if (skin == null) { error = $"no {role} skin provided"; return null; }

            M5SkinDescriptor descriptor = skin.GetComponent<M5SkinDescriptor>();
            if (descriptor == null) { error = $"{role} skin has no M5SkinDescriptor"; return null; }
            if (descriptor.Role != role) { error = $"{role} socket received a {descriptor.Role} skin"; return null; }

            GameObject instance = Object.Instantiate(skin, socket, false);
            instance.name = $"{role}Skin_{descriptor.SkinId}";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            skinId = descriptor.SkinId;
            return instance;
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            if (root.name == name) return root;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }

        static void Destroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }
}
