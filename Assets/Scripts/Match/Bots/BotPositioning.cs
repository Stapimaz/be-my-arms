using System.Collections.Generic;
using BeMyArms.Networking;
using UnityEngine;

namespace BeMyArms.Match
{
    /// <summary>
    /// Stable firing positions, real cover and short threat retreats. Decisions are slower than
    /// steering; incoming damage makes cover valuable, not random crouch/slide/jump animations.
    /// </summary>
    public sealed class BotPositioning
    {
        readonly BotNavigation _navigation = new BotNavigation();
        readonly List<Vector3> _route = new List<Vector3>();
        readonly List<Vector3> _visited = new List<Vector3>();
        Vector3 _goal, _hurtPosition, _lastPosition;
        float _decisionAt, _hurtUntil, _hideUntil, _peekUntil, _slideAt, _stuck;
        bool _hasGoal, _attemptedMove, _arrived;
        public Vector3 Goal => _goal;
        public bool Hiding { get; private set; }

        public void Reset()
        {
            _hasGoal = _attemptedMove = _arrived = Hiding = false;
            _decisionAt = _hurtUntil = _hideUntil = _peekUntil = _slideAt = _stuck = 0f;
            _route.Clear(); _visited.Clear();
        }

        public void Hurt(float now, Vector3 position)
        {
            // Continuous zone damage / a burst must not trigger a full route search every tick.
            if (now >= _hurtUntil) { _hurtPosition = position; _decisionAt = 0f; }
            _hurtUntil = now + 3f;
        }

        public P1Input Step(in BodyState state, MovementCollision map, BodyState? knownEnemy,
            bool enemyVisible, bool enemyFiring, bool partnerReloading, float preferredRange,
            float zoneRadius, float now, float dt)
        {
            Vector3 here = BotSight.Feet(state);
            bool pressure = now < _hurtUntil || (enemyVisible && enemyFiring);
            if (_attemptedMove && Vector3.Distance(here, _lastPosition) < .006f) _stuck += dt;
            else _stuck = Mathf.Max(0f, _stuck - dt * 2f);
            _lastPosition = here;
            if (!_hasGoal || now >= _decisionAt || _stuck > .6f)
            {
                Plan(state, map, knownEnemy, preferredRange, zoneRadius, pressure || partnerReloading, now);
                _decisionAt = now + 1f; _stuck = 0f;
            }

            float facing = state.BodyYaw;
            if (knownEnemy.HasValue)
            {
                Vector3 direction = BotSight.Feet(knownEnemy.Value) - here;
                facing = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            }
            else if (_hasGoal && Vector3.Distance(here, _goal) > 1f)
                facing = Mathf.Atan2(_goal.x - here.x, _goal.z - here.z) * Mathf.Rad2Deg;
            P1Input input = BotSteering.Turn(state.LookYaw, state.BodyYaw, facing, dt);

            Vector3 travel = Vector3.zero;
            while (_route.Count > 0 && Vector3.Distance(here, _route[0]) < .38f) _route.RemoveAt(0);
            if (_route.Count == 0 && _hasGoal && !_arrived)
            {
                _arrived = true;
                _visited.Add(here); if (_visited.Count > 12) _visited.RemoveAt(0);
            }
            // Pull to the farthest visible waypoint, but never cut a wall/diagonal corner.
            int waypoint = 0;
            for (int i = 1; i < _route.Count; i++)
                if (_navigation.Travel(here, _route[i], out _, out _)) waypoint = i;
                else break;
            bool lowPassage = false;
            if (_route.Count > 0)
            {
                if (_navigation.Travel(here, _route[waypoint], out _, out lowPassage))
                    travel = _route[waypoint] - here;
                else
                {
                    // An invalid route must not be retained by goal hysteresis on the next plan.
                    // No attempted movement here means the ordinary stuck timer cannot detect it.
                    _hasGoal = false; _route.Clear(); _decisionAt = 0f;
                }
            }
            travel.y = 0f;
            _attemptedMove = travel.magnitude > .25f;
            if (_attemptedMove)
            {
                Vector3 direction = travel.normalized;
                float look = BodySim.Normalize(state.LookYaw + input.LookYawDelta) * Mathf.Deg2Rad;
                input.MoveX = direction.x * Mathf.Cos(look) - direction.z * Mathf.Sin(look);
                input.MoveZ = direction.x * Mathf.Sin(look) + direction.z * Mathf.Cos(look);
                input.Sprint = pressure && !partnerReloading && travel.magnitude > 5f;
            }

            Hiding = false;
            if (knownEnemy.HasValue && !_attemptedMove)
            {
                Vector3 eye = BotSight.Eye(knownEnemy.Value);
                bool usefulDuck = BotSight.Exposure(map, here, eye, true) + .3f < BotSight.Exposure(map, here, eye, false);
                if (usefulDuck)
                {
                    // Cover/reload cycle: brief protection, then a deliberate firing opportunity.
                    if ((pressure || partnerReloading) && now >= _peekUntil && now >= _hideUntil)
                    { _hideUntil = now + 1.1f; _peekUntil = _hideUntil + 1.4f; }
                    Hiding = partnerReloading || now < _hideUntil;
                }
            }
            input.Crouch = lowPassage || Hiding || !_navigation.Clear(here, 1.8f);
            if (pressure && knownEnemy.HasValue && _attemptedMove && state.Grounded && state.ActionTimeLeft <= 0f &&
                state.PlanarSpeed >= 4f && now >= _slideAt && CanSlide(state, knownEnemy.Value, travel))
            { input.Slide = true; _slideAt = now + 7f; }
            return input;
        }

        void Plan(in BodyState state, MovementCollision map, BodyState? enemy, float range, float zone, bool pressure, float now)
        {
            Vector3 here = BotSight.Feet(state);
            _navigation.Search(map, here);
            int best = _navigation.Reachable[0];
            float bestScore = Score(here, state, enemy, range, zone, pressure, now);
            float currentGoalScore = _hasGoal ? Score(_goal, state, enemy, range, zone, pressure, now) : float.NegativeInfinity;
            // Sample reachable cells at ~1.5m intervals plus the current location. Search still uses
            // all .75m cells, so scoring sparsity does not close narrow doorway routes.
            foreach (int index in _navigation.Reachable)
            {
                var node = _navigation[index];
                if (index % 2 != 0 && node.Cost > .01f) continue;
                float score = Score(node.Position, state, enemy, range, zone, pressure, now) - node.Cost * .22f;
                if (_hasGoal && Vector3.Distance(node.Position, _goal) < 1f) score += 2f;
                if (score > bestScore) { best = index; bestScore = score; }
            }
            Vector3 chosen = _navigation[best].Position;
            // Keep an already reachable route unless a better position really earns the interruption.
            if (_hasGoal && _route.Count > 0 && _stuck < .6f && bestScore < currentGoalScore + 2f) return;
            _goal = chosen; _hasGoal = true; _arrived = false; _navigation.Route(best, _route);
        }

        float Score(Vector3 p, in BodyState self, BodyState? enemy, float range, float zone, bool pressure, float now)
        {
            float score = 0f;
            float radial = new Vector2(p.x, p.z).magnitude;
            if (zone > 0f) score -= Mathf.Max(0f, radial - Mathf.Max(0f, zone - 2f)) * 8f;
            if (enemy.HasValue)
            {
                var target = enemy.Value;
                float distance = Vector3.Distance(p, BotSight.Feet(target));
                score -= Mathf.Abs(distance - range) * .9f;
                if (distance < 6f) score -= (6f - distance) * 3f;
                bool stand = _navigation.Clear(p, 1.8f);
                bool shot = stand && BotSight.Clear(_navigation.Collision, p + Vector3.up * 1.45f, BotSight.Chest(target));
                bool crouchShot = BotSight.Clear(_navigation.Collision, p + Vector3.up * 1.05f, BotSight.Chest(target));
                float exposed = BotSight.Exposure(_navigation.Collision, p, BotSight.Eye(target), false);
                float ducked = BotSight.Exposure(_navigation.Collision, p, BotSight.Eye(target), true);
                score += shot ? 7f : crouchShot ? 5f : -4f;
                if (shot && ducked + .3f < exposed) score += 9f; // duck/peek cover, not a wall with no firing exit
                score += (1f - ducked) * (pressure ? 10f : 2f);
                if (pressure && now < _hurtUntil && Vector3.Distance(p, _hurtPosition) < 2.5f) score -= 5f;
            }
            else
            {
                Vector3 delta = p - BotSight.Feet(self); delta.y = 0f;
                score -= Mathf.Abs(delta.magnitude - 7f);
                float yaw = self.BodyYaw * Mathf.Deg2Rad;
                score += Vector3.Dot(delta.normalized, new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw))) * 2f;
                foreach (Vector3 visited in _visited) score -= Mathf.Max(0f, 4f - Vector3.Distance(p, visited)) * 2f;
            }
            return score;
        }

        public bool CanSlide(in BodyState state, in BodyState enemy, Vector3 travel)
        {
            // The current slide cannot brake: validate its FULL 6.3m stopping corridor, not just the
            // next waypoint. No slides into walls, around corners, off ledges or through the enemy.
            if (travel.magnitude < 6.4f || travel.magnitude > 8f) return false;
            Vector3 here = BotSight.Feet(state), end = here + travel.normalized * 6.3f;
            if (!_navigation.Travel(here, end, out Vector3 grounded, out _) || Mathf.Abs(grounded.y - here.y) > .15f) return false;
            if (Vector3.Distance(grounded, BotSight.Feet(enemy)) < 6f) return false;
            return BotSight.Exposure(_navigation.Collision, grounded, BotSight.Eye(enemy), true) + .3f
                < BotSight.Exposure(_navigation.Collision, here, BotSight.Eye(enemy), false);
        }
    }
}
