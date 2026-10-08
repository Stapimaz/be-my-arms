using UnityEngine;
using UnityEngine.UI;

namespace BeMyArms.Client
{
    /// <summary>Fast, smoothed preview of total next-round spread, not a hit marker or target lock.
    /// Smoothing affects presentation only; projection uses the actual world-camera FOV.</summary>
    public sealed class DynamicCrosshair : MaskableGraphic
    {
        public float SpreadDegrees { get; private set; }
        public float TargetSpreadDegrees { get; private set; }
        public float RadiusCanvasUnits { get; private set; }
        public float SmoothingSeconds = .04f;
        [SerializeField] bool _dynamicSpread = true;
        bool _hasSpread;

        /// <summary>Future settings UI can disable dynamics for a fixed center dot, without
        /// changing gun accuracy or requiring a separate crosshair implementation.</summary>
        public bool DynamicSpread
        {
            get => _dynamicSpread;
            set { if (_dynamicSpread == value) return; _dynamicSpread = value; ResetSpread(); }
        }

        public static float SmoothSpread(float current, float target, float deltaTime, float responseSeconds)
        {
            target = Mathf.Max(0, target);
            if (responseSeconds <= 0) return target;
            float value = Mathf.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0, deltaTime) / responseSeconds));
            return Mathf.Abs(value - target) < .001f ? target : value;
        }

        public void ResetSpread()
        {
            _hasSpread = false;
            SpreadDegrees = TargetSpreadDegrees = RadiusCanvasUnits = 0;
            SetVerticesDirty();
        }

        protected override void OnEnable() { base.OnEnable(); ResetSpread(); }

        public static float ProjectRadius(float degrees, float verticalFov, float pixelHeight, float canvasScale)
            => Mathf.Tan(Mathf.Max(0, degrees) * Mathf.Deg2Rad) * pixelHeight * .5f /
                (Mathf.Tan(verticalFov * .5f * Mathf.Deg2Rad) * Mathf.Max(.0001f, canvasScale));

        public void SetSpread(float degrees, Camera camera, Canvas canvas, float deltaTime)
        {
            TargetSpreadDegrees = Mathf.Max(0, degrees);
            SpreadDegrees = _hasSpread ? SmoothSpread(SpreadDegrees, TargetSpreadDegrees, deltaTime, SmoothingSeconds) : TargetSpreadDegrees;
            _hasSpread = true;
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
            // Smoothed angular spread preview. Four brighter ticks keep the familiar
            // shooter crosshair; their 4-unit minimum gap is readability, not added bullet error.
            if (DynamicSpread && radius > .1f)
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
            for (int i = 0; DynamicSpread && i < 4; i++)
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
