using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace BeMyArms.M05.Ngo
{
    public enum BakeoffRole
    {
        Host,
        Server,
        Client
    }

    /// <summary>
    /// Starts the NGO candidate in server, client or host mode. Role can be forced with
    /// <c>-bakeoff-role server|client|host</c> so a headless server build and an editor client
    /// can be launched separately.
    /// </summary>
    public class NgoBakeoffBootstrap : MonoBehaviour
    {
        public NetworkManager Manager;
        public GameObject BodyPrefab;
        public BakeoffRole Role = BakeoffRole.Host;
        public bool AutoDrive = true;
        public Transform AutoAimTarget;
        public ushort Port = 7777;

        void Start()
        {
            string roleArg = GetArg("-bakeoff-role");
            if (!string.IsNullOrEmpty(roleArg)) Role = ParseRole(roleArg);

            NetworkManager manager = Manager != null ? Manager : NetworkManager.Singleton;
            if (manager == null)
            {
                Debug.LogError("[M05-NGO] No NetworkManager found.");
                return;
            }

            var transport = manager.GetComponent<UnityTransport>();
            if (transport != null) transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");

            if (Role != BakeoffRole.Client) manager.OnServerStarted += OnServerStarted;

            switch (Role)
            {
                case BakeoffRole.Server: manager.StartServer(); break;
                case BakeoffRole.Client: manager.StartClient(); break;
                default: manager.StartHost(); break;
            }

            Debug.Log($"[M05-NGO] bootstrap role={Role} port={Port} tickRate={manager.NetworkConfig.TickRate}");
        }

        void OnServerStarted()
        {
            if (BodyPrefab == null)
            {
                Debug.LogError("[M05-NGO] BodyPrefab not assigned.");
                return;
            }

            GameObject bodyGo = Instantiate(BodyPrefab);
            var body = bodyGo.GetComponent<NgoBakeoffBody>();
            var driver = bodyGo.GetComponent<NgoBakeoffInputDriver>();
            if (driver != null)
            {
                driver.Body = body;
                driver.AutoDrive = AutoDrive;
                driver.AutoAimTarget = AutoAimTarget;
            }

            var networkObject = bodyGo.GetComponent<NetworkObject>();
            networkObject.Spawn(true);
            Debug.Log("[M05-NGO] body spawned");
        }

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return null;
        }

        static BakeoffRole ParseRole(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "server": return BakeoffRole.Server;
                case "client": return BakeoffRole.Client;
                default: return BakeoffRole.Host;
            }
        }
    }
}
