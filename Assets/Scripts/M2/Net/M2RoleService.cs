using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M2
{
    /// <summary>
    /// Server-side connection-to-role authority, assigned during NGO connection approval (so a
    /// reconnecting client is authorized before any replicated body exists). Also owns the
    /// temporary bot substitution: when a human disconnects their role is handed to a bot, and the
    /// next human connection with the same token atomically takes it back.
    ///
    /// Every step is traced so the reconnect path can be diagnosed layer by layer.
    /// </summary>
    public class M2RoleService : MonoBehaviour
    {
        public const byte RoleNone = 255;
        public const byte RoleP1 = 0;
        public const byte RoleP2 = 1;

        public static M2RoleService Instance { get; private set; }

        public readonly M2RoleRegistry Registry = new M2RoleRegistry();

        NetworkManager _manager;
        float _nextStatusLog;

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
            _manager.OnClientConnectedCallback += OnNgoClientConnected;
            _manager.OnClientDisconnectCallback += OnDisconnect;
        }

        void OnNgoClientConnected(ulong id)
        {
            Trace($"NGO client connected id={id} count={_manager.ConnectedClientsIds.Count}");
        }

        void OnApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            Decode(request.Payload, out string token, out byte desired);
            Trace($"approval ENTER id={request.ClientNetworkId} token='{token}' desired={Name(desired)} connected={_manager.ConnectedClientsIds.Count}");

            byte role = Registry.Assign(request.ClientNetworkId, token, desired, out ulong displaced);
            if (displaced != ulong.MaxValue)
            {
                Trace($"reclaim role {Name(role)} -> client {request.ClientNetworkId}; dropping stale {displaced}");
                _manager.DisconnectClient(displaced);
            }

            response.Approved = true;
            response.CreatePlayerObject = false;
            Trace($"approval EXIT id={request.ClientNetworkId} role={Name(role)} botActive={Registry.IsBot(role)} singleOwner={Registry.HasSingleOwner(role)}");
        }

        void OnDisconnect(ulong clientId)
        {
            byte role = Registry.Release(clientId);
            if (role != RoleNone)
            {
                Registry.SetBot(role);
                Trace($"client {clientId} disconnected -> role {Name(role)} to BOT (singleOwner={Registry.HasSingleOwner(role)})");
            }
            else
            {
                Trace($"client {clientId} disconnected with no role");
            }
        }

        void Update()
        {
            if (!_manager || !_manager.IsServer) return;
            if (Time.realtimeSinceStartup < _nextStatusLog) return;
            _nextStatusLog = Time.realtimeSinceStartup + 5f;
            Trace($"status listening={_manager.IsListening} connected={_manager.ConnectedClientsIds.Count} " +
                  $"P1[bot={Registry.IsBot(RoleP1)} human={Registry.RoleTaken(RoleP1)}] " +
                  $"P2[bot={Registry.IsBot(RoleP2)} human={Registry.RoleTaken(RoleP2)}]");
        }

        public bool HasRole(ulong clientId, byte role) => Registry.HasRole(clientId, role);

        public static string Name(byte role) => role == RoleP1 ? "P1" : role == RoleP2 ? "P2" : "spectator";

        static void Trace(string message) => Debug.Log($"[M2-trace] t={Time.realtimeSinceStartup:0.000} {message}");

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
