using System;
using System.Collections.Generic;
using System.IO;
using BeMyArms.Client;
using UnityEngine;

namespace BeMyArms.QA
{
    /// <summary>Opt-in disposable-player frame trace. Never installs itself, changes controls,
    /// camera transforms or gameplay state; this assembly is development/Pipeline-only.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class P2MotionProbe : MonoBehaviour
    {
        [Serializable] public struct Frame
        {
            public float Time, Dt, X, Y, Z, AuthX, AuthY, AuthZ, Eye, AnchorError, AimError;
            public uint Tick;
        }
        [Serializable] public class Trace { public Frame[] Frames; }
        readonly List<Frame> _frames = new List<Frame>();
        string _path;
        double _end;
        public void Begin(string path, float duration) { _path = path; _end = Time.timeAsDouble + duration; }
        void LateUpdate()
        {
            if (_path == null) return;
            var player = FindAnyObjectByType<LocalPlayer>(); var client = player != null ? player.LocalClient : null;
            if (client != null && client.IsLocalOwnBody && client.LocalRoleIndex == 1 && player.LocalCamera != null)
            {
                var state = client.ViewState; var p = client.VisualPosition;
                var expected = Quaternion.Euler(Mathf.Clamp(client.LocalAimPitch, -80, 80), client.LocalAimYaw, 0);
                _frames.Add(new Frame { Time = Time.time, Dt = Time.deltaTime, Tick = state.SimulationTick,
                    X = p.x, Y = p.y, Z = p.z, AuthX = state.PosX, AuthY = state.PosY, AuthZ = state.PosZ,
                    Eye = client.VisualEyeHeight, AnchorError = Vector3.Distance(player.LocalCamera.transform.position, p + Vector3.up * client.VisualEyeHeight),
                    AimError = Quaternion.Angle(expected, player.LocalCamera.transform.rotation) });
            }
            if (Time.timeAsDouble < _end) return;
            File.WriteAllText(_path, JsonUtility.ToJson(new Trace { Frames = _frames.ToArray() }));
            _path = null; Destroy(this);
        }
    }
}
