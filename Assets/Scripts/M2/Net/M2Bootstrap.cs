using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace BeMyArms.M2
{
    public enum M2Role
    {
        Host,
        Server,
        Client
    }

    /// <summary>
    /// Starts the M2 spike as host, dedicated server or client. Role can be forced with
    /// <c>-m2-role server|client|host</c> so a headless server build and a client run separately.
    /// </summary>
    public class M2Bootstrap : MonoBehaviour
    {
        public NetworkManager Manager;
        public GameObject BodyPrefab;
        public M2Role Role = M2Role.Host;
        public ushort Port = 7778;

        void Start()
        {
            string roleArg = GetArg("-m2-role");
            if (!string.IsNullOrEmpty(roleArg)) Role = ParseRole(roleArg);

            NetworkManager manager = Manager != null ? Manager : NetworkManager.Singleton;
            if (manager == null)
            {
                Debug.LogError("[M2] No NetworkManager found.");
                return;
            }

            var transport = manager.GetComponent<UnityTransport>();
            if (transport != null) transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");

            if (Role != M2Role.Client) manager.OnServerStarted += OnServerStarted;

            switch (Role)
            {
                case M2Role.Server: manager.StartServer(); break;
                case M2Role.Client: manager.StartClient(); break;
                default: manager.StartHost(); break;
            }

            Debug.Log($"[M2] bootstrap role={Role} port={Port} tickRate={manager.NetworkConfig.TickRate}");
        }

        void OnServerStarted()
        {
            if (BodyPrefab == null)
            {
                Debug.LogError("[M2] BodyPrefab not assigned.");
                return;
            }

            GameObject go = Instantiate(BodyPrefab);
            go.GetComponent<NetworkObject>().Spawn(true);
            Debug.Log("[M2] body spawned");
        }

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        static M2Role ParseRole(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "server": return M2Role.Server;
                case "client": return M2Role.Client;
                default: return M2Role.Host;
            }
        }
    }
}
