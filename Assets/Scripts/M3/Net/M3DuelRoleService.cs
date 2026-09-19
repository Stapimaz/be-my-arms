using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.M3
{
    /// <summary>
    /// Server-side connection-to-slot authority for the Duel, assigned during NGO connection
    /// approval so a reconnecting client is authorized before any replicated body exists. Also owns
    /// temporary bot substitution: when a human disconnects their slot goes to a bot, and the next
    /// connection with the same token atomically takes it back (same policy as M2, four slots).
    /// </summary>
    public class M3DuelRoleService : MonoBehaviour
    {
        public static M3DuelRoleService Instance { get; private set; }

        public readonly M3DuelRoster Registry = new M3DuelRoster();

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

        void OnNgoClientConnected(ulong id) => Trace($"NGO client connected id={id} count={_manager.ConnectedClientsIds.Count}");

        void OnApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            Decode(request.Payload, out string token, out byte desired);
            int team = (byte)(desired / M3DuelSlots.RolesPerTeam);
            int role = (byte)(desired % M3DuelSlots.RolesPerTeam);
            Trace($"approval ENTER id={request.ClientNetworkId} token='{token}' desired={(desired == 255 ? "any" : M3DuelSlots.Name((M3DuelSlot)desired))}");

            M3DuelSlot slot = Registry.Assign(request.ClientNetworkId, token, team, role, out ulong displaced);
            if (displaced != ulong.MaxValue)
            {
                Trace($"reclaim slot {M3DuelSlots.Name(slot)} -> client {request.ClientNetworkId}; dropping stale {displaced}");
                _manager.DisconnectClient(displaced);
            }

            response.Approved = true;
            response.CreatePlayerObject = false;
            Trace($"approval EXIT id={request.ClientNetworkId} slot={M3DuelSlots.Name(slot)} botActive={Registry.IsBot(slot)} singleOwner={Registry.HasSingleOwner(slot)}");
        }

        void OnDisconnect(ulong clientId)
        {
            M3DuelSlot slot = Registry.Release(clientId);
            if (slot != M3DuelSlot.None)
            {
                Registry.SetBot(slot);
                Trace($"client {clientId} disconnected -> slot {M3DuelSlots.Name(slot)} to BOT (singleOwner={Registry.HasSingleOwner(slot)})");
            }
            else
            {
                Trace($"client {clientId} disconnected with no slot");
            }
        }

        void Update()
        {
            if (!_manager || !_manager.IsServer) return;
            if (Time.realtimeSinceStartup < _nextStatusLog) return;
            _nextStatusLog = Time.realtimeSinceStartup + 10f;
            Trace($"status connected={_manager.ConnectedClientsIds.Count} assigned={Registry.AssignedCount} " +
                  $"A[P1={(Registry.SlotTaken(M3DuelSlot.TeamAP1) ? "human" : Registry.IsBot(M3DuelSlot.TeamAP1) ? "bot" : "-")} " +
                  $"P2={(Registry.SlotTaken(M3DuelSlot.TeamAP2) ? "human" : Registry.IsBot(M3DuelSlot.TeamAP2) ? "bot" : "-")}] " +
                  $"B[P1={(Registry.SlotTaken(M3DuelSlot.TeamBP1) ? "human" : Registry.IsBot(M3DuelSlot.TeamBP1) ? "bot" : "-")} " +
                  $"P2={(Registry.SlotTaken(M3DuelSlot.TeamBP2) ? "human" : Registry.IsBot(M3DuelSlot.TeamBP2) ? "bot" : "-")}]");
        }

        public bool HasSlot(ulong clientId, M3DuelSlot slot) => Registry.HasSlot(clientId, slot);

        public bool IsBot(M3DuelSlot slot) => Registry.IsBot(slot);

        static void Trace(string message) => Debug.Log($"[M3-trace] t={Time.realtimeSinceStartup:0.000} {message}");

        public static byte[] Encode(string token, byte desiredSlot)
        {
            string t = token ?? "";
            int count = Encoding.UTF8.GetByteCount(t);
            var bytes = new byte[count + 1];
            Encoding.UTF8.GetBytes(t, 0, t.Length, bytes, 0);
            bytes[count] = desiredSlot;
            return bytes;
        }

        static void Decode(byte[] payload, out string token, out byte desired)
        {
            if (payload == null || payload.Length == 0)
            {
                token = "";
                desired = (byte)M3DuelSlot.None;
                return;
            }
            desired = payload[payload.Length - 1];
            token = Encoding.UTF8.GetString(payload, 0, payload.Length - 1);
        }
    }
}
