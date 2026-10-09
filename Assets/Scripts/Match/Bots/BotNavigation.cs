using System.Collections.Generic;
using BeMyArms.Networking;
using UnityEngine;

namespace BeMyArms.Match
{
    /// <summary>
    /// Bounded local reachability search over the SAME boxes/surfaces used by BodySim. No map names,
    /// coordinates or baked navmesh. Edges test the whole body, headroom, steps and safe drops.
    /// A single reachable floor per XZ cell: stacked multi-level routing needs a future layered graph.
    /// </summary>
    public sealed class BotNavigation
    {
        public struct Node { public Vector3 Position; public float Cost; public int Parent; public bool Crouch; }
        const float Cell = .75f;
        const int Half = 20, Width = Half * 2 + 1;
        readonly Node[] _nodes = new Node[Width * Width];
        readonly bool[] _closed = new bool[Width * Width];
        readonly List<int> _reachable = new List<int>();
        readonly List<int> _heap = new List<int>();
        readonly List<float> _priorities = new List<float>();
        public IReadOnlyList<int> Reachable => _reachable;
        public Node this[int index] => _nodes[index];
        public MovementCollision Collision { get; private set; }

        public void Search(MovementCollision collision, Vector3 origin)
        {
            Collision = collision;
            _reachable.Clear(); _heap.Clear(); _priorities.Clear();
            for (int i = 0; i < _nodes.Length; i++) { _nodes[i].Cost = float.PositiveInfinity; _closed[i] = false; }
            int root = Half * Width + Half;
            _nodes[root] = new Node { Position = origin, Parent = -1, Cost = 0f };
            Push(root, 0f);
            while (_heap.Count > 0)
            {
                int index = Pop();
                if (_closed[index]) continue;
                _closed[index] = true; _reachable.Add(index);
                Node from = _nodes[index];
                int x = index % Width, z = index / Width;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = x + dx, nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= Width || nz >= Width) continue;
                        int next = nz * Width + nx;
                        if (_closed[next]) continue;
                        Vector3 p = new Vector3(origin.x + (nx - Half) * Cell, from.Position.y,
                            origin.z + (nz - Half) * Cell);
                        if (!Travel(from.Position, p, out p, out bool crouch)) continue;
                        float cost = from.Cost + Cell * (dx != 0 && dz != 0 ? 1.4142f : 1f) * (crouch ? 1.5f : 1f);
                        if (cost > 22f || cost >= _nodes[next].Cost) continue;
                        _nodes[next] = new Node { Position = p, Parent = index, Cost = cost, Crouch = crouch };
                        Push(next, cost);
                    }
            }
        }

        public void Route(int goal, List<Vector3> route)
        {
            route.Clear();
            for (int i = goal; i >= 0; i = _nodes[i].Parent) route.Add(_nodes[i].Position);
            route.Reverse();
        }

        public bool Clear(Vector3 p, float height)
        {
            if (Collision == null) return true;
            float r = Collision.BodyRadius + .03f;
            if (Collision.HasBounds && (p.x < Collision.MinX + r || p.x > Collision.MaxX - r ||
                p.z < Collision.MinZ + r || p.z > Collision.MaxZ - r)) return false;
            foreach (var b in Collision.Solids)
                if (b.Above(p.y + Collision.StepHeight, out var blocking) && blocking.MinY < p.y + height &&
                    p.x > blocking.MinX - r && p.x < blocking.MaxX + r &&
                    p.z > blocking.MinZ - r && p.z < blocking.MaxZ + r) return false;
            return true;
        }

        public bool Travel(Vector3 from, Vector3 to, out Vector3 end, out bool crouch)
        {
            end = from; crouch = false;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z)) / .20f));
            for (int i = 1; i <= steps; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)steps);
                p.y = Collision != null ? Collision.SurfaceHeight(p.x, p.z, end.y + Collision.StepHeight + .05f) : from.y;
                if (p.y - end.y > (Collision != null ? Collision.StepHeight + .05f : .6f) || end.y - p.y > .6f || !Clear(p, 1.15f)) return false;
                crouch |= !Clear(p, 1.8f);
                end = p;
            }
            return true;
        }

        // Binary heap permits stale entries; closed cells discard them on pop.
        void Push(int index, float priority)
        {
            int i = _heap.Count; _heap.Add(index); _priorities.Add(priority);
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_priorities[parent] <= priority) break;
                _heap[i] = _heap[parent]; _priorities[i] = _priorities[parent]; i = parent;
            }
            _heap[i] = index; _priorities[i] = priority;
        }

        int Pop()
        {
            int result = _heap[0], last = _heap.Count - 1, index = _heap[last]; float priority = _priorities[last];
            _heap.RemoveAt(last); _priorities.RemoveAt(last);
            if (last == 0) return result;
            int i = 0;
            while (i * 2 + 1 < last)
            {
                int child = i * 2 + 1;
                if (child + 1 < last && _priorities[child + 1] < _priorities[child]) child++;
                if (_priorities[child] >= priority) break;
                _heap[i] = _heap[child]; _priorities[i] = _priorities[child]; i = child;
            }
            _heap[i] = index; _priorities[i] = priority;
            return result;
        }
    }
}
