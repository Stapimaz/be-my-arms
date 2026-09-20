using System;
using System.Collections.Generic;
using BeMyArms.M2;
using UnityEngine;

namespace BeMyArms.M3
{
    [Serializable]
    public struct M3MapSpawn
    {
        public int Team;
        public int Body;
        public int Role;
        public Vector3 Position;
        public float Yaw;
    }

    /// <summary>
    /// Optional map-provided spawn data. When present in a match scene, the director spawns bodies
    /// at these poses instead of its built-in fallback, so production arenas drive match setup
    /// without coupling the networking layer to the map authoring layer.
    /// </summary>
    public class M3MapSpawns : MonoBehaviour
    {
        public Vector3 BoundsSize = new Vector3(24f, 8f, 24f);
        public List<M3MapSpawn> Spawns = new List<M3MapSpawn>();
        /// <summary>Legacy XZ rectangles of cover/walls; used only when the arena hierarchy is absent.</summary>
        public List<Vector4> Obstacles = new List<Vector4>();

        public bool TryGet(int team, int body, int role, out M3MapSpawn spawn)
        {
            for (int i = 0; i < Spawns.Count; i++)
            {
                if (Spawns[i].Team == team && Spawns[i].Body == body && Spawns[i].Role == role)
                {
                    spawn = Spawns[i];
                    return true;
                }
            }
            spawn = default;
            return false;
        }

        public bool TryGetBodyPose(int team, int body, out Vector3 position, out float yaw)
        {
            M3MapSpawn p1, p2;
            if (TryGet(team, body, 0, out p1) && TryGet(team, body, 1, out p2))
            {
                position = (p1.Position + p2.Position) * 0.5f;
                yaw = p1.Yaw;
                return true;
            }
            if (TryGet(team, body, 0, out p1))
            {
                position = p1.Position;
                yaw = p1.Yaw;
                return true;
            }
            position = Vector3.zero;
            yaw = 0f;
            return false;
        }

        /// <summary>
        /// Builds the deterministic 3D arena collision used by the server AND client prediction.
        /// Classification is done from the authored prefab hierarchy (piece names), so both sides
        /// derive an identical model from the same scene: floors ground, ramps slope, walls/pillars
        /// block, low cover/platform/crates are climbable, catwalks are thin walkways you can pass
        /// under, and doorways keep their central opening. Engine-free result, so the pure sim can
        /// resolve it.
        /// </summary>
        public M2MovementCollision BuildCollision()
        {
            var collision = new M2MovementCollision
            {
                HasBounds = BoundsSize.x > 0f && BoundsSize.z > 0f,
                MinX = -BoundsSize.x * 0.5f,
                MaxX = BoundsSize.x * 0.5f,
                MinZ = -BoundsSize.z * 0.5f,
                MaxZ = BoundsSize.z * 0.5f,
                BaseGroundY = 0f
            };

            Transform arena = FindArena();
            if (arena == null)
            {
                BuildFromLegacyObstacles(collision);
                return collision;
            }

            for (int i = 0; i < arena.childCount; i++)
            {
                Transform piece = arena.GetChild(i);
                string name = piece.name;
                if (name.StartsWith("BMA_Map_Floor")) continue;
                if (!TryBounds(piece, out Bounds b)) continue;

                if (name.StartsWith("BMA_Map_Ramp")) AddRamp(collision, piece, b);
                else if (name.StartsWith("BMA_Map_Catwalk")) AddCatwalk(collision, b);
                else if (name.StartsWith("BMA_Map_Doorway")) AddDoorway(collision, piece);
                else if (name.StartsWith("BMA_Map_Platform") || name.StartsWith("BMA_Map_Crate") || name.StartsWith("BMA_Map_Cover"))
                {
                    // Solid, climbable block: its top is walkable via the collision's step band.
                    collision.AddBox(b.min.x, b.min.y, b.min.z, b.max.x, b.max.y, b.max.z);
                }
                else if (name.StartsWith("BMA_Map_SpawnPad"))
                {
                    // A small flat pad; nothing to block.
                }
                else
                {
                    // Walls, pillars, lintels and anything unclassified are solid.
                    collision.AddBox(b.min.x, b.min.y, b.min.z, b.max.x, b.max.y, b.max.z);
                }
            }

            return collision;
        }

        /// <summary>Ramps rise from +Z to -Z in local space (verified against the kit mesh).</summary>
        static void AddRamp(M2MovementCollision collision, Transform piece, Bounds b)
        {
            Vector3 rise = piece.rotation * Vector3.back; // direction of increasing height, world space
            if (Mathf.Abs(rise.x) >= Mathf.Abs(rise.z))
            {
                float low = rise.x >= 0f ? b.min.y : b.max.y;
                float high = rise.x >= 0f ? b.max.y : b.min.y;
                collision.AddSurface(b.min.x, b.min.z, b.max.x, b.max.z, low, high, 0);
            }
            else
            {
                float low = rise.z >= 0f ? b.min.y : b.max.y;
                float high = rise.z >= 0f ? b.max.y : b.min.y;
                collision.AddSurface(b.min.x, b.min.z, b.max.x, b.max.z, low, high, 1);
            }
        }

        static void AddCatwalk(M2MovementCollision collision, Bounds b)
        {
            // Thin walkway slab; its top is walkable and the space underneath stays passable.
            float slab = Mathf.Max(0.12f, b.size.y * 0.2f);
            collision.AddBox(b.min.x, b.max.y - slab, b.min.z, b.max.x, b.max.y, b.max.z);
        }

        /// <summary>Doorways keep a central opening (lanes); only the jambs and lintel are solid.</summary>
        static void AddDoorway(M2MovementCollision collision, Transform piece)
        {
            const float halfWidth = 2f;
            const float openingHalf = 0.85f;
            const float thicknessMin = -0.2f;
            const float thicknessMax = 0.25f;
            const float jambTop = 2.25f;
            const float lintelTop = 3.0f;

            AddLocalBox(collision, piece,
                new Vector3(-halfWidth, 0f, thicknessMin),
                new Vector3(-openingHalf, jambTop, thicknessMax));
            AddLocalBox(collision, piece,
                new Vector3(openingHalf, 0f, thicknessMin),
                new Vector3(halfWidth, jambTop, thicknessMax));
            AddLocalBox(collision, piece,
                new Vector3(-halfWidth, jambTop, thicknessMin),
                new Vector3(halfWidth, lintelTop, thicknessMax));
        }

        /// <summary>Add an axis-aligned world box from a local-space box under a (Y-rotated) piece.</summary>
        static void AddLocalBox(M2MovementCollision collision, Transform piece, Vector3 localMin, Vector3 localMax)
        {
            Vector3 centre = piece.TransformPoint((localMin + localMax) * 0.5f);
            Vector3 e = (localMax - localMin) * 0.5f;
            Vector3 ex = piece.TransformVector(new Vector3(e.x, 0f, 0f));
            Vector3 ey = piece.TransformVector(new Vector3(0f, e.y, 0f));
            Vector3 ez = piece.TransformVector(new Vector3(0f, 0f, e.z));
            float hx = Mathf.Abs(ex.x) + Mathf.Abs(ey.x) + Mathf.Abs(ez.x);
            float hy = Mathf.Abs(ex.y) + Mathf.Abs(ey.y) + Mathf.Abs(ez.y);
            float hz = Mathf.Abs(ex.z) + Mathf.Abs(ey.z) + Mathf.Abs(ez.z);
            collision.AddBox(centre.x - hx, centre.y - hy, centre.z - hz,
                             centre.x + hx, centre.y + hy, centre.z + hz);
        }

        void BuildFromLegacyObstacles(M2MovementCollision collision)
        {
            for (int i = 0; i < Obstacles.Count; i++)
            {
                Vector4 o = Obstacles[i];
                collision.AddBox(o.x, 0f, o.y, o.z, BoundsSize.y, o.w);
            }
        }

        static Transform FindArena()
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].name == "Arena")
                    return roots[i].transform;
            return null;
        }

        static bool TryBounds(Transform piece, out Bounds bounds)
        {
            Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
    }
}
