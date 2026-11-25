using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TaintlessGlade
{
    /// <summary>
    /// La scène 3D qui sert de fond au menu : les personnages sont posés sur un grand cercle (la « roulette »).
    /// Au menu principal, seul le personnage choisi est visible, en gros plan ; dans la sélection de personnage,
    /// le cercle s'ouvre, la caméra recule et le cercle tourne comme dans The Binding of Isaac.
    /// </summary>
    public class MenuStage : MonoBehaviour
    {
        [Serializable]
        public class Slot
        {
            public CharacterStats stats;
            public Transform root;
            [Tooltip("Visuel billboard du personnage (ou silhouette).")] public Transform visual;
            public ScalePunch punch;
            public CharacterAnimator animator;
            public TextMeshPro title;
            [HideInInspector] public float scale;
        }

        [Serializable]
        public struct CameraPose
        {
            public Vector3 position;
            public Vector3 lookAt;
            public float fov;
        }

        public Camera cam;
        public Slot[] slots = Array.Empty<Slot>();

        [Header("Roulette")]
        public float ringRadius = 2.2f;
        [Range(0.3f, 1f)] public float farScale = 0.72f;

        [Header("Caméra")]
        public CameraPose mainPose = new CameraPose { position = new Vector3(0.1f, 1.6f, -3.3f), lookAt = new Vector3(0f, 0.62f, 0f), fov = 38f };
        public CameraPose selectPose = new CameraPose { position = new Vector3(1f, 1.9f, -5.2f), lookAt = new Vector3(1f, 0.7f, 0.6f), fov = 40f };
        [Tooltip("Légère dérive de la caméra, pour que la scène ne soit jamais figée (m).")]
        public float cameraDrift = 0.05f;

        Spring _spin;            // position de la roulette (en index, non borné)
        Spring _spread;          // 0 = menu principal, 1 = sélection ouverte
        Spring _view;            // interpolation des poses de caméra
        int _virtualIndex;
        bool _selectionOpen;

        public int Count => slots.Length;
        /// <summary>Index (dans slots) du personnage actuellement devant la caméra.</summary>
        public int FocusIndex => Mod(_virtualIndex, Mathf.Max(1, slots.Length));
        public Slot Focus => slots.Length > 0 ? slots[FocusIndex] : null;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            foreach (Slot s in slots) s.scale = 1f;
            ShowCharacter(IndexOfSelected(), true);
            _spread.Snap(0f);
            _view.Snap(0f);
            Layout();
        }

        static int Mod(int a, int n) => ((a % n) + n) % n;

        public int IndexOfSelected()
        {
            CharacterStats stats = GameSession.SelectedStats;
            for (int i = 0; i < slots.Length; i++) if (slots[i].stats == stats) return i;
            return 0;
        }

        /// <summary>Place directement la roulette sur ce personnage (via le plus court chemin si animé).</summary>
        public void ShowCharacter(int index, bool instant = false)
        {
            if (slots.Length == 0) return;
            int n = slots.Length;
            int delta = Mod(index - FocusIndex, n);
            if (delta > n / 2) delta -= n;
            _virtualIndex += delta;
            _spin.target = _virtualIndex;
            if (instant) _spin.Snap(_virtualIndex);
        }

        /// <summary>Tourne la roulette d'un cran (+1 = personnage suivant).</summary>
        public void Step(int direction)
        {
            if (slots.Length == 0) return;
            _virtualIndex += direction;
            _spin.target = _virtualIndex;
            _spin.velocity += direction * 2.5f;
            Punch(0.8f);
        }

        public void OpenSelection(bool open)
        {
            _selectionOpen = open;
            _spread.target = open ? 1f : 0f;
            _view.target = open ? 1f : 0f;
        }

        public void Punch(float strength = 1f)
        {
            Slot s = Focus;
            if (s != null && s.punch != null)
                s.punch.Punch(new Vector3(0.12f, -0.1f, 0f) * strength);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _spin.Step(dt, 170f, 15f);
            _spread.Step(dt, 150f, 13f);
            _view.Step(dt, 120f, 14f);
            Layout();
            UpdateCamera();
            UpdateLook();
        }

        void Layout()
        {
            int n = slots.Length;
            if (n == 0) return;
            float step = Mathf.PI * 2f / n;
            float spread = Mathf.Clamp01(_spread.value);
            int focus = FocusIndex;

            for (int i = 0; i < n; i++)
            {
                Slot s = slots[i];
                if (s.root == null) continue;

                // Distance (en crans) à la position courante de la roulette, ramenée dans [-n/2, n/2).
                float rel = i - _spin.value;
                rel = rel - n * Mathf.Floor(rel / n + 0.5f);
                float angle = rel * step;

                float radius = ringRadius * _spread.value;
                Vector3 position = new Vector3(Mathf.Sin(angle) * radius, 0f, (1f - Mathf.Cos(angle)) * radius);
                s.root.localPosition = position;

                float depth = Mathf.Clamp01((1f - Mathf.Cos(angle)) * 0.5f);
                float scale = Mathf.Lerp(1f, farScale, depth);
                // Au menu principal seul le personnage choisi existe ; les autres apparaissent quand la roulette s'ouvre.
                bool isFocus = i == focus;
                float visible = isFocus ? 1f : spread;
                s.scale = Mathf.Lerp(s.scale, visible, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                float finalScale = scale * Mathf.Max(0.0001f, s.scale);
                s.root.localScale = Vector3.one * finalScale;
                s.root.gameObject.SetActive(finalScale > 0.002f);

                if (s.title != null)
                {
                    float focusAlpha = Mathf.Clamp01(1f - Mathf.Abs(rel) * 0.45f);
                    Color c = s.title.color;
                    c.a = spread * spread * focusAlpha;
                    s.title.color = c;
                }
            }
        }

        void UpdateCamera()
        {
            if (cam == null) return;
            float v = _view.value;
            Vector3 position = Vector3.LerpUnclamped(mainPose.position, selectPose.position, v);
            Vector3 lookAt = Vector3.LerpUnclamped(mainPose.lookAt, selectPose.lookAt, v);
            float t = Time.unscaledTime;
            position += new Vector3(Mathf.Sin(t * 0.37f), Mathf.Sin(t * 0.53f + 1f) * 0.6f, 0f) * cameraDrift;
            cam.transform.position = position;
            cam.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            cam.fieldOfView = Mathf.LerpUnclamped(mainPose.fov, selectPose.fov, v);
        }

        /// <summary>Le personnage au premier plan regarde la souris (animations existantes du jeu : yeux, tête, demi-tour).</summary>
        void UpdateLook()
        {
            Slot s = Focus;
            if (s == null || s.animator == null || cam == null || Mouse.current == null) return;
            Vector2 mouse = Mouse.current.position.ReadValue();
            Vector3 head = cam.WorldToScreenPoint(s.root.position + Vector3.up * 0.75f * s.root.localScale.y);
            Vector2 d = mouse - new Vector2(head.x, head.y);
            s.animator.LookPixels = d;
            s.animator.AimScreen = d.sqrMagnitude > 900f ? d.normalized : Vector2.zero;
        }
    }
}
