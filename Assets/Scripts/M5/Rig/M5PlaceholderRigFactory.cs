using System.Collections.Generic;
using BeMyArms.M0;
using UnityEngine;

namespace BeMyArms.M5
{
    public class M5PlaceholderSkinVariant
    {
        public string Id;
        public PrimitiveType Shape;
        public Vector3 Scale = Vector3.one;
        public Vector3 Offset = Vector3.zero;

        public M5PlaceholderSkinVariant(string id, PrimitiveType shape, Vector3 scale, Vector3 offset)
        {
            Id = id;
            Shape = shape;
            Scale = scale;
            Offset = offset;
        }
    }

    /// <summary>
    /// Builds greybox placeholder rigs and skins that conform to the contract. This is deliberately
    /// not production art: the point is to produce many valid, interchangeable variants so the
    /// "any P1 skin with any P2 skin" contract can be proven automatically.
    /// </summary>
    public static class M5PlaceholderRigFactory
    {
        public static readonly M5PlaceholderSkinVariant[] P1Variants =
        {
            new M5PlaceholderSkinVariant("p1_square", PrimitiveType.Cube, new Vector3(0.7f, 1.4f, 0.5f), new Vector3(0f, 0.7f, 0f)),
            new M5PlaceholderSkinVariant("p1_round", PrimitiveType.Capsule, new Vector3(0.7f, 0.9f, 0.7f), new Vector3(0f, 0.9f, 0f)),
            new M5PlaceholderSkinVariant("p1_tall", PrimitiveType.Capsule, new Vector3(0.55f, 1.2f, 0.55f), new Vector3(0f, 1.1f, 0f)),
        };

        public static readonly M5PlaceholderSkinVariant[] P2Variants =
        {
            new M5PlaceholderSkinVariant("p2_stub", PrimitiveType.Capsule, new Vector3(0.35f, 0.45f, 0.35f), new Vector3(0f, 0.3f, 0.25f)),
            new M5PlaceholderSkinVariant("p2_box", PrimitiveType.Cube, new Vector3(0.5f, 0.4f, 0.7f), new Vector3(0f, 0.25f, 0.3f)),
            new M5PlaceholderSkinVariant("p2_sphere", PrimitiveType.Sphere, new Vector3(0.45f, 0.45f, 0.45f), new Vector3(0f, 0.3f, 0.25f)),
        };

        public static GameObject CreateGameplayRig(M5RigContract contract = null)
        {
            contract = contract ?? M5RigContract.Default();
            var root = new GameObject(contract.RootName);
            root.AddComponent<M5RigAnimation>();
            root.AddComponent<M5GameplayRigStats>();

            // Sockets, shallowest first so their parents exist.
            var ordered = new List<M5SocketSpec>(contract.Sockets);
            ordered.Sort((a, b) => Depth(a.ParentPath).CompareTo(Depth(b.ParentPath)));
            for (int i = 0; i < ordered.Count; i++)
            {
                M5SocketSpec spec = ordered[i];
                if (spec.Name == contract.RootName) continue; // the root itself
                Transform parent = EnsurePath(root.transform, spec.ParentPath);
                var socket = new GameObject(spec.Name);
                socket.transform.SetParent(parent, false);
            }

            CreateHitbox(root.transform, contract, "Hitbox_Head", PrimitiveType.Sphere, new Vector3(0.32f, 0.32f, 0.32f));
            CreateHitbox(root.transform, contract, "Hitbox_Body", PrimitiveType.Capsule, new Vector3(0.7f, 0.9f, 0.7f));
            return root;
        }

        public static GameObject CreateSkin(M5RigRole role, M5PlaceholderSkinVariant variant)
        {
            var root = new GameObject();
            root.name = $"{role}Skin_{variant.Id}";
            var descriptor = root.AddComponent<M5SkinDescriptor>();
            descriptor.Role = role;
            descriptor.SkinId = variant.Id;
            descriptor.RigId = "default";

            GameObject mesh = GameObject.CreatePrimitive(variant.Shape);
            Collider collider = mesh.GetComponent<Collider>();
            if (collider != null) DestroyObject(collider); // cosmetics add no physics
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = variant.Offset;
            mesh.transform.localScale = variant.Scale;
            return root;
        }

        public static List<GameObject> CreateSkinVariants(M5RigRole role, int count)
        {
            M5PlaceholderSkinVariant[] source = role == M5RigRole.P1 ? P1Variants : P2Variants;
            var list = new List<GameObject>();
            for (int i = 0; i < count; i++)
                list.Add(CreateSkin(role, source[i % source.Length]));
            return list;
        }

        static void CreateHitbox(Transform root, M5RigContract contract, string name, PrimitiveType shape, Vector3 scale)
        {
            M5HitboxSpec spec = null;
            for (int i = 0; i < contract.Hitboxes.Count; i++)
                if (contract.Hitboxes[i].Name == name) spec = contract.Hitboxes[i];
            if (spec == null) return;

            Transform parent = name == "Hitbox_Head"
                ? EnsurePath(root, "Hips/Chest/Neck/Head")
                : EnsurePath(root, "Hips/Chest");

            GameObject hitbox = GameObject.CreatePrimitive(shape);
            hitbox.name = name;
            hitbox.transform.SetParent(parent, false);
            hitbox.transform.localPosition = Vector3.zero;
            hitbox.transform.localScale = scale;
            var region = hitbox.AddComponent<HitboxRegion>();
            region.region = spec.Region == "Head" ? HitboxRegion.Region.Head : HitboxRegion.Region.Body;
        }

        static Transform EnsurePath(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) return root;
            string[] segments = path.Split('/');
            Transform current = root;
            for (int i = 0; i < segments.Length; i++)
            {
                Transform next = current.Find(segments[i]);
                if (next == null)
                {
                    var go = new GameObject(segments[i]);
                    go.transform.SetParent(current, false);
                    next = go.transform;
                }
                current = next;
            }
            return current;
        }

        static int Depth(string path)
            => string.IsNullOrEmpty(path) ? 0 : path.Split('/').Length;

        static void DestroyObject(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
