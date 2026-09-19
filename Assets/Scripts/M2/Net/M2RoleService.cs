using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M2
{
    /// <summary>
    /// Server-side connection-to-role authority, assigned during NGO connection approval. Because
    /// the role is bound before any replicated body exists, a late-joining or reconnecting client is
    /// authorized even if it cannot yet see the body — which is what made the earlier reconnect test
    /// fail. Token-based restore and the double-role reclaim live here.
    /// </summary>
    public class M2RoleService : MonoBehaviour
    {
        public const byte RoleNone = 255;
        public const byte RoleP1 = 0;
        public const byte RoleP2 = 1;

        public static M2RoleService Instance { get; private set; }

        public readonly M2RoleRegistry Registry = new M2RoleRegistry();

        NetworkManager _manager;

        void Awake()
        {
            Instance = this;
            _manager = GetComponent<NetworkManager>();
        }

        public void InstallServerHooks()
        {
            if (_manager == null) _manager = GetComponent<NetworkManager>();
            if (_manager == null) return;
            _manager.NetworkConfig.ConnectionApproval = true;
            _manager.ConnectionApprovalCallback = OnApproval;
            _manager.OnClientDisconnectCallback += OnDisconnect;
        }

        void OnApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            Decode(request.Payload, out string token, out byte desired);

            byte role = Registry.Assign(request.ClientNetworkId, token, desired, out ulong displaced);
            if (displaced != ulong.MaxValue)
            {
                Debug.Log($"[M2] reclaim: role {Name(role)} taken by client {request.ClientNetworkId}; dropping stale client {displaced}");
                _manager.DisconnectClient(displaced);
            }

            Debug.Log($"[M2] approved client {request.ClientNetworkId} -> {Name(role)} (token '{token}')");
            response.Approved = true;
            response.CreatePlayerObject = false;
        }

        void OnDisconnect(ulong clientId)
        {
            byte role = Registry.Release(clientId);
            if (role != RoleNone)
                Debug.Log($"[M2] client {clientId} disconnected from role {Name(role)} (role freed)");
        }

        public bool HasRole(ulong clientId, byte role) => Registry.HasRole(clientId, role);

        public static string Name(byte role) => role == RoleP1 ? "P1" : role == RoleP2 ? "P2" : "spectator";

        public static byte[] Encode(string token, byte desiredRole)
        {
            string t = token ?? "";
            int count = Encoding.UTF8.GetByteCount(t);
            var bytes = new byte[count + 1];
            Encoding.UTF8.GetBytes(t, 0, t.Length, bytes, 0);
            bytes[count] = desiredRole;
            return bytes;
        }

        static void Decode(byte[] payload, out string token, out byte desired)
        {
            if (payload == null || payload.Length == 0)
            {
                token = "";
                desired = RoleNone;
                return;
            }
            desired = payload[payload.Length - 1];
            token = Encoding.UTF8.GetString(payload, 0, payload.Length - 1);
        }
    }
}
