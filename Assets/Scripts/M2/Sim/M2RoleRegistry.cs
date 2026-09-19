using System.Collections.Generic;

namespace BeMyArms.M2
{
    /// <summary>
    /// Pure connection-to-role binding with token-based reconnect. Handles the double-role edge:
    /// if a token reconnects while its previous connection still holds the role, the old connection
    /// is reported as displaced so the server can drop it and the role is reclaimed safely.
    /// </summary>
    public class M2RoleRegistry
    {
        public const byte RoleNone = 255;
        public const byte RoleP1 = 0;
        public const byte RoleP2 = 1;

        readonly Dictionary<ulong, byte> _connectionRole = new Dictionary<ulong, byte>();
        readonly Dictionary<string, byte> _tokenRole = new Dictionary<string, byte>();
        readonly HashSet<byte> _botRoles = new HashSet<byte>();

        /// <summary>
        /// Assign a role to a connection. Returns the assigned role and, when a role is reclaimed
        /// from a still-connected previous owner, that displaced client id (else ulong.MaxValue).
        /// </summary>
        public byte Assign(ulong clientId, string token, byte desiredRole, out ulong displacedClientId)
        {
            displacedClientId = ulong.MaxValue;

            byte target;
            if (!string.IsNullOrEmpty(token) && _tokenRole.TryGetValue(token, out byte remembered))
            {
                target = remembered;
            }
            else if ((desiredRole == RoleP1 || desiredRole == RoleP2) && !RoleTaken(desiredRole))
            {
                target = desiredRole;
            }
            else
            {
                target = FindFreeRole();
            }

            if (target != RoleNone)
            {
                ulong owner = Owner(target);
                if (owner != ulong.MaxValue && owner != clientId)
                {
                    displacedClientId = owner;
                    _connectionRole.Remove(owner);
                }
            }

            _connectionRole[clientId] = target;
            if (!string.IsNullOrEmpty(token) && target != RoleNone) _tokenRole[token] = target;
            if (target != RoleNone) _botRoles.Remove(target); // a human owner always clears any bot
            return target;
        }

        /// <summary>Temporarily hand a role to a bot (e.g. while its human is disconnected).</summary>
        public void SetBot(byte role)
        {
            if (role != RoleNone) _botRoles.Add(role);
        }

        public void ClearBot(byte role) => _botRoles.Remove(role);

        public bool IsBot(byte role) => _botRoles.Contains(role);

        /// <summary>True when exactly one owner (human or bot) holds the role — never two.</summary>
        public bool HasSingleOwner(byte role)
            => role != RoleNone && (IsBot(role) ^ RoleTaken(role));

        public byte Release(ulong clientId)
        {
            if (_connectionRole.TryGetValue(clientId, out byte role))
            {
                _connectionRole.Remove(clientId);
                return role;
            }
            return RoleNone;
        }

        public bool HasRole(ulong clientId, byte role)
            => _connectionRole.TryGetValue(clientId, out byte r) && r == role;

        public bool RoleTaken(byte role) => Owner(role) != ulong.MaxValue;

        public int AssignedCount => _connectionRole.Count;

        ulong Owner(byte role)
        {
            foreach (var kvp in _connectionRole)
                if (kvp.Value == role) return kvp.Key;
            return ulong.MaxValue;
        }

        byte FindFreeRole()
        {
            if (!RoleTaken(RoleP1)) return RoleP1;
            if (!RoleTaken(RoleP2)) return RoleP2;
            return RoleNone;
        }
    }
}
