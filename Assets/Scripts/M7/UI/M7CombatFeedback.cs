using BeMyArms.M3;
using UnityEngine;
using UnityEngine.UI;

namespace BeMyArms.M7
{
    /// <summary>
    /// Coherent combat feedback driven only by server-confirmed events (<see cref="M3CombatEvents"/>):
    /// a hitmarker + confirm sound for the shooter, a red damage vignette + hurt sound when the local
    /// body is hit, a world/body impact particle, and a distinct elimination overlay on death.
    /// </summary>
    public class M7CombatFeedback : MonoBehaviour
    {
        Canvas _canvas;
        Image _vignette;
        RectTransform _hitmarker;
        Image[] _hitArms;
        Text _death;

        M3DuelClient _client;

        float _hitTimer;
        float _hitDuration = 0.12f;
        Color _hitColor = new Color(1f, 1f, 1f, 0.95f);
        float _deathTimer;

        void Awake()
        {
            if (Application.isBatchMode) return;
            Build();
        }

        void OnEnable()
        {
            M3CombatEvents.Damage += OnDamage;
            M3CombatEvents.WorldImpact += OnWorldImpact;
        }

        void OnDisable()
        {
            M3CombatEvents.Damage -= OnDamage;
            M3CombatEvents.WorldImpact -= OnWorldImpact;
        }

        void Build()
        {
            _canvas = M7Ui.CreateCanvas("M7CombatFeedback", 120);

            _vignette = M7Ui.Panel(_canvas.transform, "DamageVignette", new Color(0.75f, 0.03f, 0.02f, 0f));
            M7Ui.Fill(_vignette.rectTransform, 0f, 0f, 0f, 0f);
            _vignette.sprite = RadialSprite();
            _vignette.type = Image.Type.Simple;
            _vignette.preserveAspect = false;
            _vignette.raycastTarget = false;
            _vignette.enabled = false;

            _hitmarker = M7Ui.Rect(_canvas.transform, "Hitmarker");
            M7Ui.Place(_hitmarker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            _hitArms = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var arm = M7Ui.Panel(_hitmarker, "Arm" + i, _hitColor);
                arm.raycastTarget = false;
                float sx = (i == 0 || i == 2) ? -1f : 1f;
                float sy = (i < 2) ? 1f : -1f;
                M7Ui.Place(arm.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(sx * 8f, sy * 8f), new Vector2(3f, 12f));
                arm.rectTransform.localRotation = Quaternion.Euler(0f, 0f, sx * sy * 45f);
                _hitArms[i] = arm;
            }
            _hitmarker.gameObject.SetActive(false);

            _death = M7Ui.Label(_canvas.transform, "Death", "", 54, TextAnchor.MiddleCenter);
            M7Ui.Place(_death.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(900f, 90f));
            _death.color = new Color(1f, 0.35f, 0.3f, 0.95f);
            _death.gameObject.SetActive(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (_hitTimer > 0f)
            {
                _hitTimer -= dt;
                if (_hitTimer <= 0f) _hitmarker.gameObject.SetActive(false);
            }

            if (_vignette != null)
            {
                float a = _vignette.color.a;
                if (a > 0f)
                {
                    a = Mathf.Max(0f, a - dt * 1.8f);
                    _vignette.color = new Color(0.75f, 0.03f, 0.02f, a);
                    _vignette.enabled = a > 0.002f;
                }
            }

            if (_deathTimer > 0f)
            {
                _deathTimer -= dt;
                if (_deathTimer <= 0f && _death != null) _death.gameObject.SetActive(false);
            }
        }

        void OnDamage(M3DamageEvent e)
        {
            if (_canvas == null) return; // dedicated server / headless: no local UI
            if (_client == null) _client = FindLocalClient();
            int team = _client != null ? _client.LocalTeam : -1;
            int body = _client != null ? _client.LocalBody : -1;
            bool localAttacker = team >= 0 && e.IsAttacker(team, body);
            bool localVictim = team >= 0 && e.IsVictim(team, body);

            if (M7VfxService.Instance != null && e.Point != Vector3.zero)
                M7VfxService.Instance.Spawn(M7VfxId.ImpactFlesh, e.Point, Quaternion.identity);

            if (localAttacker) ShowHitmarker(e.Killed);
            if (localVictim) ShowDamage(e.Killed);
        }

        void OnWorldImpact(Vector3 point)
        {
            if (_canvas == null) return;
            if (M7VfxService.Instance != null)
                M7VfxService.Instance.Spawn(M7VfxId.ImpactWorld, point, Quaternion.identity);
        }

        void ShowHitmarker(bool killed)
        {
            if (_hitmarker == null) return;
            _hitColor = killed ? new Color(1f, 0.30f, 0.25f, 1f) : new Color(1f, 1f, 1f, 0.95f);
            for (int i = 0; i < _hitArms.Length; i++) _hitArms[i].color = _hitColor;
            _hitDuration = killed ? 0.28f : 0.12f;
            _hitTimer = _hitDuration;
            _hitmarker.gameObject.SetActive(true);
            _hitmarker.localScale = Vector3.one * (killed ? 1.4f : 1.15f);

            M7AudioService audio = M7AudioService.Instance;
            if (audio != null) audio.Play(killed ? M7AudioId.Elimination : M7AudioId.HitBody);
        }

        void ShowDamage(bool killed)
        {
            _vignette.enabled = true;
            _vignette.color = new Color(0.75f, 0.03f, 0.02f, Mathf.Min(1f, _vignette.color.a + (killed ? 0.85f : 0.6f)));

            M7AudioService audio = M7AudioService.Instance;
            if (audio != null) audio.Play(M7AudioId.HitBody);

            if (killed)
            {
                _deathTimer = 1.8f;
                if (_death != null)
                {
                    _death.text = "ELIMINATED";
                    _death.gameObject.SetActive(true);
                }
                if (audio != null) audio.Play(M7AudioId.Elimination);
            }
        }

        M3DuelClient FindLocalClient()
        {
            M3DuelClient[] clients = FindObjectsByType<M3DuelClient>(FindObjectsSortMode.None);
            for (int i = 0; i < clients.Length; i++)
                if (clients[i].IsLocalOwnBody) return clients[i];
            return null;
        }

        /// <summary>Radial alpha sprite for the damage vignette (opaque at the edges, clear centre).</summary>
        static Sprite RadialSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDistance = center.magnitude;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, d));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
