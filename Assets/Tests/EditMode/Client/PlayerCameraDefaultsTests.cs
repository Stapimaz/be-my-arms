using NUnit.Framework;
using UnityEngine;

namespace BeMyArms.Client.Tests
{
    public class PlayerCameraDefaultsTests
    {
        [Test]
        public void P1UsesCloserThirdPersonDistanceWithoutChangingP2Fov()
        {
            var go = new GameObject("DisposableCameraDefaults");
            try
            {
                var player = go.AddComponent<LocalPlayer>();
                Assert.AreEqual(2.8f, player.P1Distance);
                Assert.AreEqual(72, player.FieldOfView);
                Assert.AreEqual(68, player.ViewmodelFieldOfView);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
