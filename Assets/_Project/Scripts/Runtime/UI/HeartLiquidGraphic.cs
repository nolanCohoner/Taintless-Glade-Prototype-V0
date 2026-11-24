using UnityEngine;
using UnityEngine.UI;

namespace TaintlessGlade
{
    /// <summary>
    /// Liquide 2D dessiné en code à l'intérieur du cœur : le mesh épouse l'intérieur du cadre (heartFrameNormal, 1500x1500)
    /// et sa surface est une ligne ondulée et inclinable, mise à jour à chaque image par HeartHud.
    /// Toutes les coordonnées internes sont celles du sprite (px, origine en haut à gauche).
    /// </summary>
    public class HeartLiquidGraphic : MaskableGraphic
    {
        public const float ImageSize = 1500f;
        /// <summary>Surface quand le cœur est vide / plein (au-dessus du bord haut : plein même quand ça ballotte).</summary>
        public const float VEmpty = 1370f, VFull = 190f;

        // Intérieur du cadre, élargi de quelques px pour glisser sous les bords du métal (le cadre est dessiné par-dessus).
        static readonly Vector2[] Polygon =
        {
            new Vector2(420f, 270f),    // lobe gauche
            new Vector2(748f, 520f),    // creux central
            new Vector2(1082f, 270f),   // lobe droit
            new Vector2(1340f, 600f),   // flanc droit
            new Vector2(748f, 1350f),   // pointe
            new Vector2(160f, 600f),    // flanc gauche
        };

        const int Columns = 72;
        const int AreaSamples = 160;

        static bool _ready;
        static float _xMin, _xMax;
        static float[] _top, _bottom, _areaFraction;

        [Header("Surface (mis à jour par HeartHud)")]
        public float surfaceV = VEmpty;
        [Tooltip("Pente de la surface (px verticaux par px horizontal) : le liquide penche.")]
        public float slope;
        public float amplitude1 = 5f, amplitude2 = 2f;
        public float phase1, phase2;

        [Header("Couleurs")]
        public Color bottomColor = new Color(0.38f, 0.01f, 0.05f, 1f);
        public Color midColor = new Color(0.72f, 0.05f, 0.09f, 1f);
        public Color brightColor = new Color(0.95f, 0.16f, 0.17f, 1f);
        public Color surfaceColor = new Color(1f, 0.5f, 0.48f, 1f);

        /// <summary>Quand false, rien n'est dessiné (cœur vide).</summary>
        public bool filled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _ready = false;

        static void Prepare()
        {
            if (_ready) return;
            _ready = true;

            _xMin = float.MaxValue;
            _xMax = float.MinValue;
            foreach (Vector2 p in Polygon)
            {
                _xMin = Mathf.Min(_xMin, p.x);
                _xMax = Mathf.Max(_xMax, p.x);
            }

            _top = new float[Columns + 1];
            _bottom = new float[Columns + 1];
            for (int i = 0; i <= Columns; i++)
            {
                // Les deux extrémités sont décalées de 0,5 px : une verticale pile sur un sommet ne croiserait qu'un point.
                float x = Mathf.Lerp(_xMin + 0.5f, _xMax - 0.5f, i / (float)Columns);
                Interval(x, out _top[i], out _bottom[i]);
            }

            // Volume sous la surface selon sa hauteur : la vie (proportionnelle au volume) se lit alors comme du vrai liquide.
            var area = new float[AreaSamples + 1];
            for (int k = 0; k <= AreaSamples; k++)
            {
                float v = Mathf.Lerp(VEmpty, VFull, k / (float)AreaSamples);
                float sum = 0f;
                for (int i = 0; i <= Columns; i++)
                    sum += Mathf.Max(0f, _bottom[i] - Mathf.Max(_top[i], v));
                area[k] = sum;
            }
            _areaFraction = new float[AreaSamples + 1];
            for (int k = 0; k <= AreaSamples; k++) _areaFraction[k] = area[AreaSamples] > 0f ? area[k] / area[AreaSamples] : 0f;
        }

        static void Interval(float x, out float top, out float bottom)
        {
            top = float.MaxValue;
            bottom = float.MinValue;
            for (int i = 0; i < Polygon.Length; i++)
            {
                Vector2 a = Polygon[i], b = Polygon[(i + 1) % Polygon.Length];
                if (Mathf.Approximately(a.x, b.x) || x < Mathf.Min(a.x, b.x) || x > Mathf.Max(a.x, b.x)) continue;
                float y = a.y + (b.y - a.y) * (x - a.x) / (b.x - a.x);
                top = Mathf.Min(top, y);
                bottom = Mathf.Max(bottom, y);
            }
            if (top > bottom) top = bottom = 0f;
        }

        /// <summary>Hauteur de surface (px du sprite) pour que le liquide occupe cette fraction (0..1) du volume du cœur.</summary>
        public static float SurfaceForFraction(float fraction)
        {
            Prepare();
            fraction = Mathf.Clamp01(fraction);
            for (int k = 1; k <= AreaSamples; k++)
            {
                if (_areaFraction[k] < fraction) continue;
                float prev = _areaFraction[k - 1];
                float t = _areaFraction[k] > prev ? (fraction - prev) / (_areaFraction[k] - prev) : 0f;
                return Mathf.Lerp(VEmpty, VFull, (k - 1 + t) / AreaSamples);
            }
            return VFull;
        }

        float Wave(float x) => amplitude1 * Mathf.Sin(x * 0.0105f + phase1) + amplitude2 * Mathf.Sin(x * 0.027f - phase2 * 1.3f);

        Vector2 ToLocal(float u, float v)
        {
            Rect r = rectTransform.rect;
            return new Vector2(r.xMin + u / ImageSize * r.width, r.yMax - v / ImageSize * r.height);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!filled) return;
            Prepare();

            for (int i = 0; i <= Columns; i++)
            {
                float x = Mathf.Lerp(_xMin + 0.5f, _xMax - 0.5f, i / (float)Columns);
                float bottom = _bottom[i];
                float surface = Mathf.Clamp(surfaceV + slope * (x - ImageSize * 0.5f) + Wave(x), _top[i], bottom);

                // De bas en haut : fond sombre, rouge moyen, rouge vif juste sous la surface, ligne claire à la surface.
                AddVertex(vh, x, bottom, bottomColor);
                AddVertex(vh, x, Mathf.Min(bottom, surface + 130f), midColor);
                AddVertex(vh, x, Mathf.Min(bottom, surface + 14f), brightColor);
                AddVertex(vh, x, surface, surfaceColor);

                if (i == 0) continue;
                int c = i * 4, p = (i - 1) * 4;
                for (int row = 0; row < 3; row++)
                {
                    vh.AddTriangle(p + row, p + row + 1, c + row);
                    vh.AddTriangle(c + row, p + row + 1, c + row + 1);
                }
            }
        }

        void AddVertex(VertexHelper vh, float u, float v, Color vertexColor)
        {
            Color32 tint = vertexColor * color;
            vh.AddVert(ToLocal(u, v), tint, Vector2.zero);
        }
    }
}
