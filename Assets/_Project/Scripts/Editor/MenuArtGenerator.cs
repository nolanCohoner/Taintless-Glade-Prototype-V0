using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace TaintlessGlade.EditorTools
{
    /// <summary>
    /// Génère par script les images du menu (aucune image à dessiner à la main) :
    ///  - le feuillage noir qui encadre l'écran (coins haut-droit et bas-gauche, 2 calques de profondeur chacun) ;
    ///  - les silhouettes noires des personnages « bientôt disponibles » ;
    ///  - la vignette de la salle de test, un point et un cercle flou pour l'UI.
    /// Pour changer l'allure du feuillage : modifie les valeurs de <see cref="Masses"/> / <see cref="Layers"/> ci-dessous
    /// puis lance Tools > Taintless Glade > Menu > Régénérer les images du menu (le menu se met à jour tout seul).
    /// </summary>
    public static class MenuArtGenerator
    {
        public const string OutDir = "Assets/_Project/Art/Menu";

        // ------------------------------------------------------------------ Réglages du feuillage

        const int FoliageWidth = 1280;
        const int FoliageHeight = 720;

        class Mass
        {
            public string name;
            /// <summary>Coin d'ancrage (0..1, origine en bas à gauche).</summary>
            public Vector2 corner;
            /// <summary>Rayons de la masse pleine, en fraction de l'écran (largeur, hauteur).</summary>
            public Vector2 radius;
            public int seed;
        }

        class Layer
        {
            public string name;
            public float radiusScale;
            public int leaves;
            public Vector2 leafLength;
            public Color color;
            public int seed;
        }

        static readonly Mass[] Masses =
        {
            new Mass { name = "TopRight", corner = new Vector2(1f, 1f), radius = new Vector2(0.46f, 0.42f), seed = 11 },
            new Mass { name = "BottomLeft", corner = new Vector2(0f, 0f), radius = new Vector2(0.36f, 0.58f), seed = 23 },
        };

        static readonly Layer[] Layers =
        {
            new Layer { name = "Back", radiusScale = 1.12f, leaves = 640, leafLength = new Vector2(46f, 125f), color = new Color(0.02f, 0.055f, 0.045f), seed = 1 },
            new Layer { name = "Front", radiusScale = 0.96f, leaves = 460, leafLength = new Vector2(62f, 160f), color = new Color(0.0f, 0.0f, 0.0f), seed = 2 },
        };

        public static string FoliagePath(string massName, string layerName) => $"{OutDir}/Foliage_{massName}_{layerName}.png";
        public static IEnumerable<string> MassNames { get { foreach (Mass m in Masses) yield return m.name; } }
        public static IEnumerable<string> LayerNames { get { foreach (Layer l in Layers) yield return l.name; } }

        // ------------------------------------------------------------------ Entrée

        [MenuItem("Tools/Taintless Glade/Menu/Régénérer les images du menu")]
        public static void GenerateAllMenu()
        {
            GenerateAll();
            MainMenuBuilder.Build();
        }

        public static void GenerateAll()
        {
            Directory.CreateDirectory(OutDir);
            try
            {
                float step = 0f;
                foreach (Mass m in Masses)
                    foreach (Layer l in Layers)
                    {
                        EditorUtility.DisplayProgressBar("Taintless Glade", "Feuillage " + m.name + " " + l.name + "...", step / (Masses.Length * Layers.Length + 2f));
                        WritePng(FoliagePath(m.name, l.name), RenderFoliage(m, l));
                        step += 1f;
                    }
                EditorUtility.DisplayProgressBar("Taintless Glade", "Silhouettes...", 0.8f);
                GenerateSilhouettes();
                GenerateRoomThumbnail();
                WritePng(OutDir + "/UI_Pixel.png", SolidTexture(4, Color.white));
                WritePng(OutDir + "/UI_SoftCircle.png", SoftCircleTexture(64));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh();
        }

        static void WritePng(string path, Texture2D texture)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        // ------------------------------------------------------------------ Feuillage

        static Texture2D RenderFoliage(Mass mass, Layer layer)
        {
            int w = FoliageWidth, h = FoliageHeight;
            var alpha = new float[w * h];
            var rng = new Random(mass.seed * 1000 + layer.seed);
            float seed = mass.seed * 7.31f + layer.seed * 3.7f;

            Vector2 inward = new Vector2(mass.corner.x > 0.5f ? -1f : 1f, mass.corner.y > 0.5f ? -1f : 1f);
            Vector2 radius = mass.radius * layer.radiusScale;

            // Masse pleine : ellipse centrée sur le coin, au contour déformé par du bruit.
            for (int y = 0; y < h; y++)
            {
                float ny = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + 0.5f) / w;
                    float dx = Mathf.Abs(nx - mass.corner.x) / radius.x;
                    float dy = Mathf.Abs(ny - mass.corner.y) / radius.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float warp = 1f + 0.24f * (Fbm(nx * 4.5f + seed, ny * 4.5f - seed) * 2f - 1f);
                    float r = d / warp;
                    alpha[y * w + x] = Mathf.Clamp01((0.86f - r) * 34f);
                }
            }

            // Feuilles sur le contour, orientées vers l'extérieur de la masse, avec des sous-feuilles.
            for (int i = 0; i < layer.leaves; i++)
            {
                float t = (float)rng.NextDouble() * Mathf.PI * 0.5f;
                float rho = 0.78f + (float)rng.NextDouble() * 0.34f;
                Vector2 origin = new Vector2(
                    (mass.corner.x + inward.x * Mathf.Cos(t) * radius.x * rho) * w,
                    (mass.corner.y + inward.y * Mathf.Sin(t) * radius.y * rho) * h);
                Vector2 normal = new Vector2(inward.x * Mathf.Cos(t) / (radius.x * w), inward.y * Mathf.Sin(t) / (radius.y * h)).normalized;
                float angle = Mathf.Atan2(normal.y, normal.x) + ((float)rng.NextDouble() - 0.5f) * 2f * 70f * Mathf.Deg2Rad;
                float length = Mathf.Lerp(layer.leafLength.x, layer.leafLength.y, Mathf.Pow((float)rng.NextDouble(), 1.4f));
                float bend = ((float)rng.NextDouble() - 0.5f) * 0.5f;

                StampLeaf(alpha, w, h, origin, angle, length, 0.38f, bend);

                if (rng.NextDouble() < 0.6)
                {
                    Vector2 tip = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length * 0.55f;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float childAngle = angle + s * (35f + (float)rng.NextDouble() * 25f) * Mathf.Deg2Rad;
                        StampLeaf(alpha, w, h, tip, childAngle, length * (0.5f + (float)rng.NextDouble() * 0.2f), 0.4f, -bend);
                    }
                }
            }

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            Color c = layer.color;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha[i]));
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Feuille pointue (profil en sinus), légèrement courbée. Couverture lissée pour un bord sans escalier.</summary>
        static void StampLeaf(float[] alpha, int w, int h, Vector2 p, float angle, float length, float widthRatio, float bend)
        {
            float halfMax = length * widthRatio * 0.5f;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            Vector2 end = p + new Vector2(c, s) * length;
            float pad = halfMax + Mathf.Abs(bend) * length + 2f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p.x, end.x) - pad));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(p.x, end.x) + pad));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p.y, end.y) - pad));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(p.y, end.y) + pad));

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - p.x, dy = y + 0.5f - p.y;
                    float u = dx * c + dy * s;
                    if (u < 0f || u > length) continue;
                    float v = -dx * s + dy * c;
                    float t = u / length;
                    float half = halfMax * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f)), 0.8f);
                    float cover = Mathf.Clamp01(half - Mathf.Abs(v - bend * length * t * t) + 0.5f);
                    int idx = y * w + x;
                    if (cover > alpha[idx]) alpha[idx] = cover;
                }
            }
        }

        // Bruit de valeur lissé (suffisant pour déformer un contour).
        static float Hash(int x, int y)
        {
            unchecked
            {
                int n = x * 374761393 + y * 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0xffff) / 65535f;
            }
        }

        static float ValueNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Mathf.Lerp(Hash(xi, yi), Hash(xi + 1, yi), fx);
            float b = Mathf.Lerp(Hash(xi, yi + 1), Hash(xi + 1, yi + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static float Fbm(float x, float y)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < 4; o++)
            {
                sum += ValueNoise(x, y) * amp;
                norm += amp;
                x *= 2.03f; y *= 2.03f; amp *= 0.5f;
            }
            return sum / norm;
        }

        // ------------------------------------------------------------------ Silhouettes des personnages à venir

        struct Blob
        {
            public float cx, cy, rx, ry, rot, n;
            public Blob(float cx, float cy, float rx, float ry, float rot = 0f, float n = 2f)
            { this.cx = cx; this.cy = cy; this.rx = rx; this.ry = ry; this.rot = rot; this.n = n; }
        }

        static readonly Dictionary<string, Blob[]> Silhouettes = new Dictionary<string, Blob[]>
        {
            { "fox", new[]
                {
                    new Blob(0.46f, 0.30f, 0.25f, 0.15f), new Blob(0.74f, 0.47f, 0.13f, 0.11f), new Blob(0.89f, 0.42f, 0.08f, 0.04f, -10f),
                    new Blob(0.69f, 0.64f, 0.04f, 0.10f, 14f), new Blob(0.79f, 0.64f, 0.04f, 0.10f, -14f),
                    new Blob(0.17f, 0.36f, 0.18f, 0.075f, 28f), new Blob(0.31f, 0.11f, 0.03f, 0.11f, 0f, 3f), new Blob(0.41f, 0.11f, 0.03f, 0.11f, 0f, 3f),
                    new Blob(0.56f, 0.11f, 0.03f, 0.11f, 0f, 3f), new Blob(0.66f, 0.11f, 0.03f, 0.11f, 0f, 3f),
                } },
            { "rabbit", new[]
                {
                    new Blob(0.45f, 0.29f, 0.22f, 0.17f), new Blob(0.70f, 0.46f, 0.12f, 0.11f), new Blob(0.64f, 0.76f, 0.033f, 0.17f, 8f), new Blob(0.74f, 0.76f, 0.033f, 0.17f, -8f),
                    new Blob(0.21f, 0.29f, 0.06f, 0.06f), new Blob(0.34f, 0.14f, 0.10f, 0.07f, 0f, 3f), new Blob(0.62f, 0.11f, 0.03f, 0.11f, 0f, 3f),
                } },
            { "bear", new[]
                {
                    new Blob(0.46f, 0.34f, 0.27f, 0.22f), new Blob(0.77f, 0.52f, 0.14f, 0.13f), new Blob(0.70f, 0.66f, 0.05f, 0.05f), new Blob(0.83f, 0.66f, 0.05f, 0.05f),
                    new Blob(0.92f, 0.47f, 0.06f, 0.05f), new Blob(0.30f, 0.11f, 0.05f, 0.11f, 0f, 3f), new Blob(0.43f, 0.11f, 0.05f, 0.11f, 0f, 3f),
                    new Blob(0.57f, 0.11f, 0.05f, 0.11f, 0f, 3f), new Blob(0.68f, 0.11f, 0.05f, 0.11f, 0f, 3f),
                } },
            { "bat", new[]
                {
                    new Blob(0.50f, 0.44f, 0.07f, 0.14f), new Blob(0.50f, 0.62f, 0.07f, 0.07f), new Blob(0.46f, 0.71f, 0.025f, 0.05f, 10f), new Blob(0.54f, 0.71f, 0.025f, 0.05f, -10f),
                    new Blob(0.28f, 0.52f, 0.22f, 0.085f, 24f), new Blob(0.17f, 0.40f, 0.14f, 0.05f, -14f),
                    new Blob(0.72f, 0.52f, 0.22f, 0.085f, -24f), new Blob(0.83f, 0.40f, 0.14f, 0.05f, 14f),
                } },
            { "fish", new[]
                {
                    new Blob(0.50f, 0.38f, 0.30f, 0.30f), new Blob(0.50f, 0.70f, 0.16f, 0.045f, 0f, 3f), new Blob(0.50f, 0.07f, 0.20f, 0.05f, 0f, 3f),
                } },
        };

        public static string SilhouettePath(string id) => $"{OutDir}/Silhouette_{id}.png";

        static void GenerateSilhouettes()
        {
            foreach (var pair in Silhouettes)
                WritePng(SilhouettePath(pair.Key), RenderSilhouette(pair.Value));
        }

        static Texture2D RenderSilhouette(Blob[] blobs)
        {
            const int size = 384;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            var body = new Color(0.07f, 0.07f, 0.09f);
            var rim = new Color(0.8f, 0.1f, 0.12f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size, py = (y + 0.5f) / size;
                    float outer = Coverage(blobs, px, py, size, 0f);
                    float inner = Coverage(blobs, px, py, size, -0.1f);
                    float edge = Mathf.Clamp01(outer - inner);
                    Color c = Color.Lerp(body, rim, edge * 0.9f);
                    c.a = outer;
                    pixels[y * size + x] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        static float Coverage(Blob[] blobs, float px, float py, int size, float inflate)
        {
            float best = 0f;
            foreach (Blob b in blobs)
            {
                float dx = px - b.cx, dy = py - b.cy;
                float cos = Mathf.Cos(b.rot * Mathf.Deg2Rad), sin = Mathf.Sin(b.rot * Mathf.Deg2Rad);
                float qx = (dx * cos + dy * sin) / b.rx, qy = (-dx * sin + dy * cos) / b.ry;
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(qx), b.n) + Mathf.Pow(Mathf.Abs(qy), b.n), 1f / b.n);
                float cover = Mathf.Clamp01(((1f + inflate) - d) * Mathf.Min(b.rx, b.ry) * size + 0.5f);
                if (cover > best) best = cover;
            }
            return best;
        }

        // ------------------------------------------------------------------ Divers

        public const string RoomThumbPath = OutDir + "/RoomThumb_TestRoom.png";

        static void GenerateRoomThumbnail()
        {
            const int w = 512, h = 256;
            Texture2D floor = LoadReadable("Assets/_Project/Art/Textures/T_FloorStone.png");
            Texture2D wall = LoadReadable("Assets/_Project/Art/Textures/T_WallBricks.png");
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    Color c;
                    if (v > 0.55f)
                    {
                        c = wall != null ? wall.GetPixelBilinear(u * 2f, (v - 0.55f) * 3f) : new Color(0.25f, 0.22f, 0.2f);
                        c *= 0.55f;
                    }
                    else
                    {
                        c = floor != null ? floor.GetPixelBilinear(u * 2.5f, v * 2.5f) : new Color(0.2f, 0.2f, 0.22f);
                        c *= 0.5f + 0.5f * v;
                    }
                    // Lueur de torches + vignette.
                    float glow = Mathf.Exp(-(Mathf.Pow((u - 0.5f) * 2.2f, 2f) + Mathf.Pow((v - 0.7f) * 2.8f, 2f)));
                    c += new Color(0.55f, 0.28f, 0.08f) * glow * 0.55f;
                    float vig = 1f - 0.65f * Mathf.Pow(Mathf.Clamp01(Mathf.Sqrt(Mathf.Pow(u - 0.5f, 2f) + Mathf.Pow(v - 0.5f, 2f)) * 1.45f), 2f);
                    c *= vig;
                    c.a = 1f;
                    pixels[y * w + x] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            WritePng(RoomThumbPath, texture);
            if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
        }

        static Texture2D LoadReadable(string path)
        {
            if (!File.Exists(path)) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return texture.LoadImage(File.ReadAllBytes(path)) ? texture : null;
        }

        static Texture2D SolidTexture(int size, Color color)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        static Texture2D SoftCircleTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
