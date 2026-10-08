using UnityEngine;
using UnityEngine.UI;

namespace BeMyArms.Client
{
    /// <summary>Projected angular spread envelope, not a hit marker or target lock.
    /// No animation lag or arbitrary spread multiplier: the ring uses the actual world-camera FOV.</summary>
    public sealed class DynamicCrosshair : MaskableGraphic
    {
        public float SpreadDegrees { get; private set; }
        public float RadiusCanvasUnits { get; private set; }

        public static float ProjectRadius(float degrees, float verticalFov, float pixelHeight, float canvasScale)
            => Mathf.Tan(Mathf.Max(0, degrees) * Mathf.Deg2Rad) * pixelHeight * .5f /
                (Mathf.Tan(verticalFov * .5f * Mathf.Deg2Rad) * Mathf.Max(.0001f, canvasScale));

        public void SetSpread(float degrees, Camera camera, Canvas canvas)
        {
            SpreadDegrees = Mathf.Max(0, degrees);
            RadiusCanvasUnits = ProjectRadius(SpreadDegrees, camera.fieldOfView, camera.pixelHeight, canvas.scaleFactor);
            var canvasRect = (RectTransform)canvas.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, camera.pixelRect.center, null, out var center);
            rectTransform.localPosition = new Vector3(center.x, center.y, 0);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float radius = RadiusCanvasUnits;
            var ringColor = color; ringColor.a *= .35f;
            // Circle bounds the spread disk in the aim plane. Four brighter ticks keep the familiar
            // shooter crosshair; their 4-unit minimum gap is readability, not added bullet error.
            if (radius > .1f)
            {
                const int segments = 64;
                for (int i = 0; i < segments; i++)
                {
                    float a = i * (Mathf.PI * 2 / segments), b = (i + 1) * (Mathf.PI * 2 / segments);
                    var from = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    var to = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
                    Line(mesh, from, to, 1f, ringColor);
                }
            }
            float gap = Mathf.Max(4f, radius);
            for (int i = 0; i < 4; i++)
            {
                var axis = i == 0 ? Vector2.up : i == 1 ? Vector2.down : i == 2 ? Vector2.left : Vector2.right;
                Line(mesh, axis * gap, axis * (gap + 9f), 4f, new Color(0, 0, 0, .65f));
                Line(mesh, axis * gap, axis * (gap + 9f), 2f, color);
            }
            Line(mesh, new Vector2(-2, 0), new Vector2(2, 0), 4f, new Color(0, 0, 0, .65f));
            Line(mesh, new Vector2(-1, 0), new Vector2(1, 0), 2f, color);
        }

        static void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint)
        {
            var normal = new Vector2(-(to - from).y, (to - from).x).normalized * (width * .5f);
            int start = mesh.currentVertCount;
            mesh.AddVert(from - normal, tint, Vector2.zero); mesh.AddVert(from + normal, tint, Vector2.zero);
            mesh.AddVert(to + normal, tint, Vector2.zero); mesh.AddVert(to - normal, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
