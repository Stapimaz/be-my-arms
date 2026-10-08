using UnityEngine;

namespace BeMyArms.Match
{
    /// <summary>
    /// Draws the closing-zone boundary as a ground ring, scaling to the server-replicated radius.
    /// Presentation only (greybox placeholder).
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "BeMyArms.M3", "BeMyArms.M3", "M3ZoneVisual")]
    public class ZoneVisual : MonoBehaviour
    {
        public int Segments = 96;
        public float Height = 0.06f;

        LineRenderer _line;
        MatchDirector _director;

        void Start()
        {
            if (Application.isBatchMode) { enabled = false; return; }
            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.widthMultiplier = 0.15f;
            _line.positionCount = Segments;
            _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.startColor = new Color(1f, 0.25f, 0.2f, 0.85f);
            _line.endColor = new Color(1f, 0.25f, 0.2f, 0.85f);
        }

        void Update()
        {
            if (_director == null) _director = MatchDirector.Instance;
            if (_director == null || _line == null) return;

            float radius = _director.ZoneRadius.Value;
            if (radius <= 0f) { _line.enabled = false; return; }
            _line.enabled = true;

            for (int i = 0; i < Segments; i++)
            {
                float angle = (i / (float)Segments) * Mathf.PI * 2f;
                _line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Height, Mathf.Sin(angle) * radius));
            }
        }
    }
}
