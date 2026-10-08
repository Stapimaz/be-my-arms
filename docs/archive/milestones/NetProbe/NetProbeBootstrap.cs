using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace BeMyArms.NetProbe
{
    /// <summary>
    /// Minimal NGO/UTP connection-lifecycle probe. No gameplay, no approval, no tokens, no bots, no
    /// scene sync. Purpose: classify whether a force-killed client upsets the other connection.
    ///   -probe-role server|client
    ///   -probe-port &lt;port&gt;
    ///   -probe-exit-after &lt;seconds&gt;
    /// </summary>
    public class NetProbeBootstrap : MonoBehaviour
    {
        public NetworkManager Manager;
        public ushort Port = 7790;

        string _mode = "server";
        float _exitAfter;
        float _nextStatus;

        void Start()
        {
            ParseArgs();

            NetworkManager m = Manager != null ? Manager : NetworkManager.Singleton;
            m.NetworkConfig.EnableSceneManagement = false;
            m.NetworkConfig.ForceSamePrefabs = false;

            var transport = m.GetComponent<UnityTransport>();
            if (transport != null)
            {
                m.NetworkConfig.NetworkTransport = transport;
                transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
                transport.DisconnectTimeoutMS = 5000;
            }

            m.OnClientConnectedCallback += id => Log($"CONNECTED id={id} count={m.ConnectedClientsIds.Count}");
            m.OnClientDisconnectCallback += id => Log($"DISCONNECTED id={id} reason='{m.DisconnectReason}'");

            if (_mode == "server")
            {
                m.StartServer();
                Log("SERVER start");
            }
            else
            {
                m.StartClient();
                Log("CLIENT start");
            }
        }

        void Update()
        {
            NetworkManager m = Manager != null ? Manager : NetworkManager.Singleton;
            if (Time.realtimeSinceStartup >= _nextStatus)
            {
                _nextStatus = Time.realtimeSinceStartup + 5f;
                Log($"status listening={m.IsListening} connected={m.ConnectedClientsIds.Count} connectedClient={m.IsConnectedClient}");
            }

            if (_exitAfter > 0f && Time.realtimeSinceStartup >= _exitAfter)
            {
                Log("exit requested");
                if (m.IsListening) m.Shutdown();
                Application.Quit();
            }
        }

        void ParseArgs()
        {
            string role = GetArg("-probe-role");
            if (!string.IsNullOrEmpty(role)) _mode = role.ToLowerInvariant();
            if (ushort.TryParse(GetArg("-probe-port"), out ushort p)) Port = p;
            if (float.TryParse(GetArg("-probe-exit-after"), out float e)) _exitAfter = e;
        }

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        static void Log(string message) => Debug.Log($"[probe] t={Time.realtimeSinceStartup:0.000} {message}");
    }
}
