using System.Collections.Generic;
using UnityEngine;

namespace BeMyArms.M5
{
    /// <summary>
    /// Runtime proof of the cosmetic contract: builds several placeholder P1 and P2 skins and
    /// combines every pair on one gameplay rig, validating each result. No pair-specific work is
    /// performed. Attach this to an object in a scene to validate at runtime.
    /// </summary>
    public class M5MountDemo : MonoBehaviour
    {
        public int VariantsPerRole = 3;
        public bool RunOnStart = true;

        public bool Completed { get; private set; }
        public bool AllValid { get; private set; }
        public int CombinationsValidated { get; private set; }
        public string LastReport { get; private set; } = "";

        GameObject _rig;
        readonly List<GameObject> _skins = new List<GameObject>();

        void Start()
        {
            if (RunOnStart) Run();
        }

        public void Run()
        {
            if (Completed) return;

            M5RigContract contract = M5RigContract.Default();
            _rig = M5PlaceholderRigFactory.CreateGameplayRig(contract);
            List<GameObject> p1Skins = M5PlaceholderRigFactory.CreateSkinVariants(M5RigRole.P1, VariantsPerRole);
            List<GameObject> p2Skins = M5PlaceholderRigFactory.CreateSkinVariants(M5RigRole.P2, VariantsPerRole);
            _skins.AddRange(p1Skins);
            _skins.AddRange(p2Skins);

            int total = 0;
            int valid = 0;
            for (int i = 0; i < p1Skins.Count; i++)
            {
                for (int j = 0; j < p2Skins.Count; j++)
                {
                    M5AssembledBody body = M5MountAssembler.Assemble(_rig, p1Skins[i], p2Skins[j], contract);
                    total++;
                    if (body.IsValid) valid++;
                    else Debug.LogError($"[M5] invalid combination {body.P1SkinId}+{body.P2SkinId}: {body.Report}");
                    M5MountAssembler.Disassemble(body);
                }
            }

            CombinationsValidated = total;
            AllValid = valid == total;
            Completed = true;
            LastReport = $"[M5] mount matrix {valid}/{total} combinations valid";
            Debug.Log(LastReport);
        }

        void OnDestroy()
        {
            DestroyObject(_rig);
            for (int i = 0; i < _skins.Count; i++) DestroyObject(_skins[i]);
            _skins.Clear();
        }

        static void DestroyObject(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }

    /// <summary>
    /// Presentation helper for the combine/separate states of concept §5.2: the P2 layer sits at the
    /// mounted offset during a round and blends to a separated offset for lobby/pre-match/post-match.
    /// </summary>
    public class M5MountPresenter : MonoBehaviour
    {
        public Transform P2Layer;
        public Vector3 CombinedOffset = Vector3.zero;
        public Vector3 SeparatedOffset = new Vector3(0.7f, -0.15f, 0.35f);
        public float BlendSpeed = 5f;

        public bool Combined { get; private set; } = true;

        public void SetCombined(bool combined) => Combined = combined;

        void Update()
        {
            if (P2Layer == null) return;
            Vector3 target = Combined ? CombinedOffset : SeparatedOffset;
            float k = 1f - Mathf.Exp(-BlendSpeed * Time.deltaTime);
            P2Layer.localPosition = Vector3.Lerp(P2Layer.localPosition, target, k);
        }
    }
}
