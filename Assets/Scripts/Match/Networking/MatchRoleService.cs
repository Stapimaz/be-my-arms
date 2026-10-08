using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace BeMyArms.Match
{
    /// <summary>
    /// Server-side connection authority. Two modes:
    ///  - direct/dev: a connection is assigned a slot at NGO connection approval from its requested
    ///    team/body/role;
    ///  - matchmaker: a connection is **enqueued** at approval and the Matchmaking matchmaker assigns its slot
    ///    once a match proposal exists.
    /// In both modes a token restores its slot on reconnect and a disconnected slot becomes a
    /// temporary bot (never handed to another human).
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M3", "BeMyArms.M3", "M3DuelRoleService")]
    public class MatchRoleService : MonoBehaviour
    {
        public static MatchRoleService Instance { get; private set; }

        /// <summary>Clears the static instance so a later match cannot resolve a previous roster.</summary>
        public static void ResetStatics() => Instance = null;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public readonly MatchRoster Registry = new MatchRoster();

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
            Registry.BodiesPerTeam = MatchConfig.BodiesPerTeam;
            _manager.NetworkConfig.ConnectionApproval = true;
            _manager.ConnectionApprovalCallback = OnApproval;
            _manager.OnClientConnectedCallback += OnNgoClientConnected;
            _manager.OnClientDisconnectCallback += OnDisconnect;
        }

        void OnNgoClientConnected(ulong id) => Trace($"NGO client connected id={id} count={_manager.ConnectedClientsIds.Count}");

        void OnApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            Decode(request.Payload, out string token, out byte desired);
            response.Approved = true;
            response.CreatePlayerObject = false;

            if (MatchConfig.UseMatchmaker)
            {
                Registry.Enqueue(request.ClientNetworkId, token, desired, Time.realtimeSinceStartup, "local");
                Trace($"approval ENTER id={request.ClientNetworkId} token='{token}' pref={(desired > 2 ? "any" : desired == 0 ? "P1" : desired == 1 ? "P2" : "Either")} -> queued ({Registry.QueuedCount} queued)");
                return;
            }

            ulong displaced;
            int slot = MatchConfig.StrictSlots
                ? Registry.AssignExact(request.ClientNetworkId, token, desired, out displaced)
                : Registry.AssignPreferred(request.ClientNetworkId, token, desired, out displaced);
            if (slot < 0)
            {
                response.Approved = false;
                response.Reason = "That role is occupied or reserved for its reconnecting player. Choose the other role.";
                return;
            }
            if (displaced != ulong.MaxValue)
            {
                Trace($"reclaim slot {MatchSlots.Name(slot, Registry.BodiesPerTeam)} -> client {request.ClientNetworkId}; dropping stale {displaced}");
                _manager.DisconnectClient(displaced);
            }
            Trace($"approval ENTER id={request.ClientNetworkId} token='{token}' desired={(desired == 255 ? "any" : MatchSlots.Name(desired, Registry.BodiesPerTeam))}");
            Trace($"approval EXIT id={request.ClientNetworkId} slot={MatchSlots.Name(slot, Registry.BodiesPerTeam)} botActive={Registry.IsBot(slot)} singleOwner={Registry.HasSingleOwner(slot)}");
        }

        void OnDisconnect(ulong clientId)
        {
            int slot = Registry.Release(clientId);
            if (slot >= 0)
            {
                Registry.SetBot(slot);
                Trace($"client {clientId} disconnected -> slot {MatchSlots.Name(slot, Registry.BodiesPerTeam)} to BOT (singleOwner={Registry.HasSingleOwner(slot)})");
            }
            else
            {
                Trace($"client {clientId} disconnected with no slot (dequeued)");
            }
        }

        void Update()
        {
            if (!_manager || !_manager.IsServer) return;
            if (Time.realtimeSinceStartup < _nextStatusLog) return;
            _nextStatusLog = Time.realtimeSinceStartup + 10f;

            var parts = new StringBuilder();
            for (int team = 0; team < MatchSlots.Teams; team++)
            {
                for (int body = 0; body < Registry.BodiesPerTeam; body++)
                {
                    for (int role = 0; role < MatchSlots.RolesPerTeam; role++)
                    {
                        int slot = MatchSlots.Encode(team, body, role, Registry.BodiesPerTeam);
                        string state = Registry.SlotTaken(slot) ? "human" : Registry.IsBot(slot) ? "bot" : "-";
                        parts.Append($"{MatchSlots.Name(slot, Registry.BodiesPerTeam)}={state} ");
                    }
                }
            }
            Trace($"status connected={_manager.ConnectedClientsIds.Count} assigned={Registry.AssignedCount} queued={Registry.QueuedCount} | {parts}");
        }

        public bool HasSlot(ulong clientId, int slot) => Registry.HasSlot(clientId, slot);

        public bool IsBot(int slot) => Registry.IsBot(slot);

        /// <summary>Moves a queued connection into a match slot (used by the matchmaker host).</summary>
        public int AssignFromMatch(ulong clientId, string token, int slot, out ulong displaced)
        {
            int assigned = Registry.AssignSlot(clientId, token, slot, out displaced);
            if (displaced != ulong.MaxValue && _manager != null) _manager.DisconnectClient(displaced);
            return assigned;
        }

        static void Trace(string message) => Debug.Log($"[Match-trace] t={Time.realtimeSinceStartup:0.000} {message}");

        public static byte[] Encode(string token, byte desired)
        {
            string t = token ?? "";
            int count = Encoding.UTF8.GetByteCount(t);
            var bytes = new byte[count + 1];
            Encoding.UTF8.GetBytes(t, 0, t.Length, bytes, 0);
            bytes[count] = desired;
            return bytes;
        }

        static void Decode(byte[] payload, out string token, out byte desired)
        {
            if (payload == null || payload.Length == 0)
            {
                token = "";
                desired = 255;
                return;
            }
            desired = payload[payload.Length - 1];
            token = Encoding.UTF8.GetString(payload, 0, payload.Length - 1);
        }
    }
}
