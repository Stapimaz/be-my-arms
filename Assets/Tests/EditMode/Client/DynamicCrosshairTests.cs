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
                crosshair.SmoothingSeconds = 0;
                Canvas.ForceUpdateCanvases();
                crosshair.SetSpread(2.5f, camera, canvas, 1f / 60);
                Assert.AreEqual(DynamicCrosshair.ProjectRadius(2.5f, 72, camera.pixelHeight, canvas.scaleFactor), crosshair.RadiusCanvasUnits);
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, camera.pixelRect.center, null, out var expected);
                Assert.AreEqual(expected, (Vector2)rect.localPosition);
                crosshair.SetSpread(0, camera, canvas, 1f / 60); Assert.AreEqual(0, crosshair.RadiusCanvasUnits);
                Assert.IsFalse(crosshair.raycastTarget);
            }
            finally { Object.DestroyImmediate(canvas.gameObject); Object.DestroyImmediate(cameraObject); }
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void SmoothingIsFastFrameIndependentAndDoesNotOvershoot(int fps)
        {
            float opening = 0, closing = 3;
            for (int i = 0; i < fps / 5; i++)
            {
                opening = DynamicCrosshair.SmoothSpread(opening, 3, 1f / fps, .04f);
                closing = DynamicCrosshair.SmoothSpread(closing, 0, 1f / fps, .04f);
                Assert.That(opening, Is.InRange(0f, 3f)); Assert.That(closing, Is.InRange(0f, 3f));
            }
            float elapsed = (fps / 5) / (float)fps;
            Assert.AreEqual(3f * (1f - Mathf.Exp(-elapsed / .04f)), opening, .001f);
            Assert.Less(closing, .025f); Assert.Greater(opening, 2.975f);
            float firstFrame = DynamicCrosshair.SmoothSpread(0, 3, 1f / fps, .04f);
            Assert.Greater(firstFrame, 0); Assert.Less(firstFrame, 3, "Changes must not snap.");
        }

        [Test]
        public void LongStationarySprayStillExpandsAndDotModeDrawsOnlyTheCenter()
        {
            var canvas = Ui.CreateCanvas("CrosshairSprayTest");
            var cameraObject = new GameObject("CrosshairCamera", typeof(Camera));
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                var rect = Ui.Rect(canvas.transform, "Crosshair");
                var crosshair = rect.gameObject.AddComponent<DynamicCrosshair>();
                crosshair.SetSpread(0, camera, canvas, 1f / 60);
                var rifle = new RifleHandling();
                for (int i = 0; i < 16; i++)
                {
                    int burst = rifle.Shot(i * .125);
                    float previous = crosshair.SpreadDegrees;
                    crosshair.SetSpread(RifleHandling.SpreadDegrees(burst + 1), camera, canvas, .125f);
                    Assert.GreaterOrEqual(crosshair.SpreadDegrees, previous);
                }
                Assert.AreEqual(1.8f, crosshair.TargetSpreadDegrees);
                Assert.Greater(crosshair.SpreadDegrees, 1.7f);
                crosshair.SetSpread(0, camera, canvas, 1f / 60);
                Assert.That(crosshair.SpreadDegrees, Is.InRange(.01f, 1.7f), "Recovery should close smoothly.");
                crosshair.DynamicSpread = false;
                crosshair.SetSpread(4, camera, canvas, 1f / 60);
                using (var mesh = new UnityEngine.UI.VertexHelper())
                {
                    typeof(DynamicCrosshair).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly)
                        .Invoke(crosshair, new object[] { mesh });
                    Assert.AreEqual(8, mesh.currentVertCount, "Dot mode has only the dot and its contrast outline.");
                }
                crosshair.DynamicSpread = true; crosshair.SetSpread(0, camera, canvas, 1f / 60);
                Assert.AreEqual(0, crosshair.SpreadDegrees, "Mode/epoch reset cannot retain the previous spray.");
            }
            finally { Object.DestroyImmediate(canvas.gameObject); Object.DestroyImmediate(cameraObject); }
        }
    }
}
