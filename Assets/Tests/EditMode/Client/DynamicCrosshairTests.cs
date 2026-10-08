using BeMyArms.Match;
using NUnit.Framework;
using UnityEngine;

namespace BeMyArms.Client.Tests
{
    public class DynamicCrosshairTests
    {
        [TestCase(0f, 72f, 1080f, 1f)]
        [TestCase(.75f, 72f, 1080f, 1f)]
        [TestCase(2.5f, 72f, 1440f, 1.333333f)]
        [TestCase(5.8f, 90f, 720f, .666667f)]
        public void ProjectedEnvelopeContainsActualServerSpreadRays(float spread, float fov, float height, float scale)
        {
            float radius = DynamicCrosshair.ProjectRadius(spread, fov, height, scale);
            float focal = height * .5f / Mathf.Tan(fov * .5f * Mathf.Deg2Rad) / scale;
            for (uint shot = 0; shot < 1024; shot++)
            {
                RifleHandling.Spread(shot, 23, spread, out float yaw, out float pitch);
                Vector3 ray = Quaternion.Euler(pitch, yaw, 0) * Vector3.forward;
                float projected = new Vector2(ray.x / ray.z, ray.y / ray.z).magnitude * focal;
                Assert.LessOrEqual(projected, radius + .0001f);
            }
        }

        [Test]
        public void ProjectionUsesFovResolutionAndCanvasScaleRatherThanArbitraryPixelGap()
        {
            float baseline = DynamicCrosshair.ProjectRadius(2.5f, 72, 1080, 1);
            Assert.AreEqual(0, DynamicCrosshair.ProjectRadius(0, 72, 1080, 1));
            Assert.AreEqual(baseline, DynamicCrosshair.ProjectRadius(2.5f, 72, 2160, 2), .0001f);
            Assert.Greater(DynamicCrosshair.ProjectRadius(2.5f, 50, 1080, 1), baseline);
            Assert.Less(DynamicCrosshair.ProjectRadius(.75f, 72, 1080, 1), baseline);
        }

        [Test]
        public void GraphicTracksCameraViewportAndDoesNotBlockInput()
        {
            var canvas = Ui.CreateCanvas("CrosshairTest");
            var cameraObject = new GameObject("CrosshairCamera", typeof(Camera));
            try
            {
                var camera = cameraObject.GetComponent<Camera>(); camera.fieldOfView = 72;
                camera.rect = new Rect(.2f, .1f, .5f, .5f);
                var rect = Ui.Rect(canvas.transform, "Crosshair");
                Ui.Place(rect, new Vector2(.5f, .5f), Vector2.zero, new Vector2(28, 28));
                var crosshair = rect.gameObject.AddComponent<DynamicCrosshair>(); crosshair.raycastTarget = false;
                Canvas.ForceUpdateCanvases();
                crosshair.SetSpread(2.5f, camera, canvas);
                Assert.AreEqual(DynamicCrosshair.ProjectRadius(2.5f, 72, camera.pixelHeight, canvas.scaleFactor), crosshair.RadiusCanvasUnits);
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, camera.pixelRect.center, null, out var expected);
                Assert.AreEqual(expected, (Vector2)rect.localPosition);
                crosshair.SetSpread(0, camera, canvas); Assert.AreEqual(0, crosshair.RadiusCanvasUnits);
                Assert.IsFalse(crosshair.raycastTarget);
            }
            finally { Object.DestroyImmediate(canvas.gameObject); Object.DestroyImmediate(cameraObject); }
        }
    }
}
