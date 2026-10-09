using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TaintlessGlade.EditorTools
{
    /// <summary>
    /// Génère toute la salle de test (textures, matériaux, prefabs, scène) en un clic :
    /// menu Tools > Taintless Glade > Construire la salle de test.
    /// Relancer l'outil régénère tout (les stats du perso déjà créées sont conservées).
    /// </summary>
    public static class TestRoomBuilder
    {
        const string Root = "Assets/_Project";
        const string TextureDir = Root + "/Art/Textures";
        const string MaterialDir = Root + "/Art/Materials";
        const string PrefabDir = Root + "/Prefabs";
        const string DataDir = Root + "/Data";
        const string SceneDir = Root + "/Scenes";
        const string SettingsDir = Root + "/Settings";
        const string ScenePath = SceneDir + "/TestRoom.unity";

        const float RoomWidth = 18f;
        const float RoomDepth = 14f;
        const float WallHeight = 3f;
        const float WallThickness = 0.6f;

        static readonly Color AxolotlColor = new Color(1f, 0.6f, 0.78f);
        static readonly Color DummyColor = new Color(0.85f, 0.27f, 0.3f);

        class BuildContext
        {
            public Texture2D floor, wall, wood, circle, arrow, pixelBlob;
            public Sprite square;
            public Material floorMat, wallMat, pillarMat, woodMat, rockMat, corruptionMat;
            public Material particleAdd, particleAlpha, shadowMat, aimMat, glowMat, spriteMat, textMat;
            public TMP_FontAsset font;
            public GameObject impactVfx, hitVfx, muzzleVfx;
            public Projectile projectile;
            public DamageNumber damageNumber;
            public CharacterStats axolotl;
            public RigAnimationProfile quadrupedProfile;
            public VolumeProfile volumeProfile;
        }

        [MenuItem("Tools/Taintless Glade/Construire la salle de test")]
        public static void Build()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") == null || TMP_Settings.defaultFontAsset == null)
            {
                EditorUtility.DisplayDialog("TextMeshPro manquant",
                    "Importe d'abord les ressources TextMeshPro :\nWindow > TextMeshPro > Import TMP Essential Resources\npuis relance l'outil.", "OK");
                return;
            }
            if (Shader.Find("TaintlessGlade/Environment Pixel Dissolve") == null)
            {
                EditorUtility.DisplayDialog("Shader manquant", "Le shader TaintlessGlade/Environment Pixel Dissolve n'est pas compilé. Vérifie la Console.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Taintless Glade", "Textures...", 0.1f);
                EnsureFolders();
                var a = new BuildContext();
                CreateTextures(a);

                EditorUtility.DisplayProgressBar("Taintless Glade", "Matériaux...", 0.3f);
                CreateMaterials(a);

                EditorUtility.DisplayProgressBar("Taintless Glade", "Prefabs...", 0.5f);
                CreateData(a);
                CreatePrefabs(a);

                EditorUtility.DisplayProgressBar("Taintless Glade", "Scène...", 0.8f);
                BuildScene(a);
                ToonOutlineInstaller.Install();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            Debug.Log("[Taintless Glade] Salle de test générée : " + ScenePath + " — appuie sur Play !");
        }

        // ------------------------------------------------------------------ Dossiers

        static void EnsureFolders()
        {
            foreach (string dir in new[] { TextureDir, MaterialDir, PrefabDir, DataDir, SceneDir, SettingsDir })
            {
                string[] parts = dir.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
        }

        // ------------------------------------------------------------------ Textures procédurales

        static void CreateTextures(BuildContext a)
        {
            a.floor = SaveTexture("T_FloorStone", 512, FloorPixel, repeat: true);
            a.wall = SaveTexture("T_WallBricks", 512, WallPixel, repeat: true);
            a.wood = SaveTexture("T_WoodPlanks", 256, WoodPixel, repeat: true);
            a.circle = SaveTexture("T_SoftCircle", 64, CirclePixel, repeat: false);
            a.arrow = SaveTexture("T_AimArrow", 64, ArrowPixel, repeat: false);
            a.pixelBlob = SaveTexture("T_PixelBlob", 16, PixelBlobPixel, repeat: false, point: true);
            SaveTexture("T_PlaceholderSquare", 64, SquarePixel, repeat: false, sprite: true);
            a.square = AssetDatabase.LoadAssetAtPath<Sprite>(TextureDir + "/T_PlaceholderSquare.png");
        }

        delegate Color PixelFunc(float u, float v);

        static Texture2D SaveTexture(string name, int size, PixelFunc pixel, bool repeat, bool sprite = false, bool point = false)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
            tex.SetPixels(pixels);
            tex.Apply();

            string path = $"{TextureDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.alphaIsTransparency = !repeat;
            importer.mipmapEnabled = !sprite && !point;
            if (point) importer.filterMode = FilterMode.Point;
            if (sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = size;
                importer.filterMode = FilterMode.Point;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Bruit de valeur périodique -> textures qui se répètent sans couture.
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        static float PeriodicNoise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int Wrap(int i) => ((i % period) + period) % period;
            float a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
            float c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float u, float v, int period, int seed, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += PeriodicNoise(u, v, period, seed + i) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        static Color FloorPixel(float u, float v)
        {
            const int tiles = 4;
            int tx = Mathf.FloorToInt(u * tiles), ty = Mathf.FloorToInt(v * tiles);
            float lu = u * tiles - tx, lv = v * tiles - ty;
            float edge = Mathf.Min(Mathf.Min(lu, 1f - lu), Mathf.Min(lv, 1f - lv));

            Color stone = Color.Lerp(new Color(0.44f, 0.45f, 0.41f), new Color(0.33f, 0.36f, 0.34f), Hash(tx, ty, 7));
            stone *= 0.8f + 0.4f * Fbm(u, v, 16, 1, 4);
            stone *= Mathf.Lerp(0.75f, 1f, Mathf.SmoothStep(0.03f, 0.1f, edge));
            float moss = Mathf.SmoothStep(0.55f, 0.72f, Fbm(u, v, 4, 20, 3));
            stone = Color.Lerp(stone, new Color(0.25f, 0.38f, 0.18f), moss * 0.8f);

            Color grout = new Color(0.12f, 0.14f, 0.11f);
            Color c = edge < 0.025f ? grout : stone;
            c.a = 1f;
            return c;
        }

        static Color WallPixel(float u, float v)
        {
            const int rows = 8, bricksPerRow = 4;
            int row = Mathf.FloorToInt(v * rows);
            float shifted = u * bricksPerRow + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(shifted);
            float lu = shifted - col, lv = v * rows - row;
            float edge = Mathf.Min(Mathf.Min(lu, 1f - lu) * 0.5f, Mathf.Min(lv, 1f - lv));

            Color brick = Color.Lerp(new Color(0.46f, 0.45f, 0.5f), new Color(0.36f, 0.35f, 0.4f), Hash(col % bricksPerRow, row, 3));
            brick *= 0.8f + 0.4f * Fbm(u, v, 16, 5, 4);
            brick *= Mathf.Lerp(0.7f, 1f, Mathf.SmoothStep(0.02f, 0.12f, edge));

            // Mousse qui remonte depuis le bas du mur.
            float moss = Mathf.Clamp01((0.35f - v) / 0.35f) * Fbm(u, v, 8, 11, 3) * 1.8f;
            brick = Color.Lerp(brick, new Color(0.24f, 0.36f, 0.17f), Mathf.Clamp01(moss));

            Color c = edge < 0.035f ? new Color(0.13f, 0.12f, 0.14f) : brick;
            c.a = 1f;
            return c;
        }

        static Color WoodPixel(float u, float v)
        {
            const int planks = 4;
            int p = Mathf.FloorToInt(u * planks);
            float lu = u * planks - p;
            float grain = Fbm(u * 0.25f + p * 0.37f, v, 4, 31 + p, 4);
            Color wood = Color.Lerp(new Color(0.42f, 0.27f, 0.15f), new Color(0.55f, 0.37f, 0.2f), grain);
            wood *= Mathf.Lerp(0.7f, 1f, Mathf.SmoothStep(0f, 0.1f, Mathf.Min(lu, 1f - lu)));
            float frame = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (frame < 0.06f) wood *= 0.6f;
            wood.a = 1f;
            return wood;
        }

        static Color CirclePixel(float u, float v)
        {
            float r = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
            float alpha = Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, r), 1.5f);
            return new Color(1f, 1f, 1f, alpha);
        }

        static Color ArrowPixel(float u, float v)
        {
            // Chevron pointant vers +V (devient l'avant une fois posé au sol).
            float halfWidth = (0.9f - v) * 0.55f;
            float outer = Mathf.Clamp01((halfWidth - Mathf.Abs(u - 0.5f)) * 40f);
            float innerHalf = (0.6f - v) * 0.55f;
            float inner = Mathf.Clamp01((innerHalf - Mathf.Abs(u - 0.5f)) * 40f);
            float alpha = v > 0.1f && v < 0.9f ? Mathf.Clamp01(outer - inner) : 0f;
            return new Color(1f, 1f, 1f, alpha);
        }

        static Color SquarePixel(float u, float v)
        {
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (edge < 3f / 64f) return new Color(0.35f, 0.35f, 0.35f, 1f);
            float shade = Mathf.Lerp(0.85f, 1f, v);
            return new Color(shade, shade, shade, 1f);
        }

        static Color PixelBlobPixel(float u, float v)
        {
            // Disque plein, en gros pixels (texture 16x16 en filtrage Point).
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            return new Color(1f, 1f, 1f, r < 0.95f ? 1f : 0f);
        }

        // ------------------------------------------------------------------ Matériaux

        static void CreateMaterials(BuildContext a)
        {
            a.floorMat = EnvMaterial("M_Floor", a.floor, Color.white, 0.25f);
            a.wallMat = EnvMaterial("M_Wall", a.wall, Color.white, 1f / 3f);
            a.pillarMat = EnvMaterial("M_Pillar", a.wall, new Color(0.8f, 0.8f, 0.85f), 0.5f);
            a.woodMat = EnvMaterial("M_Wood", a.wood, Color.white, 1f);
            a.rockMat = EnvMaterial("M_Rock", a.floor, new Color(0.75f, 0.78f, 0.75f), 0.5f);
            a.corruptionMat = EnvMaterial("M_Corruption", a.floor, new Color(0.18f, 0.05f, 0.22f), 0.5f);
            a.corruptionMat.EnableKeyword("_EMISSION");
            a.corruptionMat.SetColor("_EmissionColor", new Color(0.55f, 0.12f, 0.85f) * 1.6f);
            EditorUtility.SetDirty(a.corruptionMat);

            a.particleAdd = ParticleMaterial("M_ParticleAdditive", a.circle, Color.white, additive: true);
            a.particleAlpha = ParticleMaterial("M_ParticleAlpha", a.circle, Color.white, additive: false);
            a.shadowMat = ParticleMaterial("M_BlobShadow", a.pixelBlob, new Color(0.05f, 0.02f, 0.1f, 0.35f), additive: false, queue: 2900);
            a.aimMat = ParticleMaterial("M_AimArrow", a.arrow, new Color(1.6f, 0.9f, 1.2f, 0.8f), additive: true, queue: 2900);
            a.glowMat = ParticleMaterial("M_ProjectileGlow", a.circle, new Color(2.2f, 1.2f, 1.7f, 1f), additive: true);

            Shader spriteShader = Shader.Find("TaintlessGlade/Sprite Character") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            a.spriteMat = SaveMaterial("M_Sprite", new Material(spriteShader));

            a.font = TMP_Settings.defaultFontAsset;
            var text = new Material(a.font.material);
            Shader overlay = Shader.Find("TextMeshPro/Distance Field Overlay");
            if (overlay != null) text.shader = overlay; // Les chiffres passent devant tout.
            text.EnableKeyword(ShaderUtilities.Keyword_Outline);
            text.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            text.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.12f, 0.03f, 0.08f, 1f));
            text.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
            a.textMat = SaveMaterial("M_DamageText", text);
        }

        static Material EnvMaterial(string name, Texture2D tex, Color color, float tiling)
        {
            var mat = new Material(Shader.Find("TaintlessGlade/Environment Pixel Dissolve"));
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_WallHeight", WallHeight);
            return SaveMaterial(name, mat);
        }

        static Material ParticleMaterial(string name, Texture2D tex, Color color, bool additive, int queue = -1)
        {
            var mat = new Material(Shader.Find("TaintlessGlade/Simple Particle"));
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_Color", color);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            mat.renderQueue = queue;
            return SaveMaterial(name, mat);
        }

        /// <summary>Crée ou met à jour l'asset en conservant son GUID (les références restent valides).</summary>
        static Material SaveMaterial(string name, Material mat)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mat, path);
                return mat;
            }
            existing.shader = mat.shader;
            existing.CopyPropertiesFromMaterial(mat);
            existing.shaderKeywords = mat.shaderKeywords;
            existing.renderQueue = mat.renderQueue;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mat);
            return existing;
        }

        // ------------------------------------------------------------------ Données

        static void CreateData(BuildContext a)
        {
            string path = DataDir + "/Stats_Axolotl.asset";
            a.axolotl = AssetDatabase.LoadAssetAtPath<CharacterStats>(path);
            if (a.axolotl == null)
            {
                a.axolotl = ScriptableObject.CreateInstance<CharacterStats>();
                a.axolotl.displayName = "Axolotl";
                a.axolotl.placeholderColor = AxolotlColor;
                AssetDatabase.CreateAsset(a.axolotl, path);
            }

            string profilePath = DataDir + "/AnimProfile_Quadruped.asset";
            a.quadrupedProfile = AssetDatabase.LoadAssetAtPath<RigAnimationProfile>(profilePath);
            if (a.quadrupedProfile == null)
            {
                a.quadrupedProfile = ScriptableObject.CreateInstance<RigAnimationProfile>();
                AssetDatabase.CreateAsset(a.quadrupedProfile, profilePath);
            }

            string volumePath = SettingsDir + "/TestRoomVolumeProfile.asset";
            a.volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumePath);
            if (a.volumeProfile == null)
            {
                VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, volumePath);

                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(0.9f);
                bloom.intensity.Override(0.9f);
                bloom.scatter.Override(0.65f);
                var vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.3f);
                vignette.color.Override(new Color(0.05f, 0.02f, 0.08f));
                var tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.mode.Override(TonemappingMode.Neutral);
                var colorAdjust = profile.Add<ColorAdjustments>(true);
                colorAdjust.saturation.Override(12f);
                colorAdjust.contrast.Override(8f);

                foreach (VolumeComponent component in profile.components)
                    AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(profile);
                a.volumeProfile = profile;
            }
        }

        // ------------------------------------------------------------------ Prefabs

        static void CreatePrefabs(BuildContext a)
        {
            a.impactVfx = SavePrefab(CreateBurst("VFX_SpitImpact", a.particleAdd, AxolotlColor, new Color(1f, 0.9f, 0.95f),
                count: 14, speed: new Vector2(1.5f, 4f), size: new Vector2(0.08f, 0.22f), lifetime: 0.35f, coneAngle: 0f, gravity: 0.6f, flashSize: 0.9f, flashColor: new Color(1.4f, 0.8f, 1.1f, 0.8f)));

            a.hitVfx = SavePrefab(CreateBurst("VFX_DummyHit", a.particleAdd, new Color(1f, 0.85f, 0.4f), new Color(1f, 0.35f, 0.25f),
                count: 18, speed: new Vector2(3f, 7f), size: new Vector2(0.06f, 0.16f), lifetime: 0.3f, coneAngle: 40f, gravity: 1f, flashSize: 1.3f, flashColor: new Color(1.5f, 1.3f, 1f, 0.9f)));

            a.muzzleVfx = SavePrefab(CreateBurst("VFX_SpitMuzzle", a.particleAdd, AxolotlColor, Color.white,
                count: 6, speed: new Vector2(1f, 2.5f), size: new Vector2(0.06f, 0.14f), lifetime: 0.2f, coneAngle: 25f, gravity: 0f, flashSize: 0.6f, flashColor: new Color(1.2f, 0.8f, 1f, 0.6f)));

            // Projectile : glow billboard + traînée de particules.
            var projectileGo = new GameObject("Projectile_Spit");
            var projectile = projectileGo.AddComponent<Projectile>();
            projectile.radius = 0.15f;
            projectile.impactVfxPrefab = a.impactVfx;

            GameObject glow = FlatQuad("Glow", projectileGo.transform, a.glowMat, 0.55f, Vector3.zero, faceUp: false);
            glow.AddComponent<Billboard>();

            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(projectileGo.transform, false);
            ParticleSystem trail = AddParticles(trailGo, a.particleAdd);
            var main = trail.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.3f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(AxolotlColor, new Color(0.8f, 0.6f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = trail.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 22f;
            var shape = trail.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            ShrinkAndFade(trail);

            a.projectile = SavePrefab(projectileGo).GetComponent<Projectile>();

            // Chiffre de dégâts.
            var numberGo = new GameObject("DamageNumber");
            TextMeshPro tmp = SetupText(numberGo, a, 7f, "12");
            numberGo.AddComponent<Billboard>();
            var number = numberGo.AddComponent<DamageNumber>();
            number.text = tmp;
            a.damageNumber = SavePrefab(numberGo).GetComponent<DamageNumber>();
        }

        static TextMeshPro SetupText(GameObject go, BuildContext a, float fontSize, string content)
        {
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = a.font;
            tmp.fontSharedMaterial = a.textMat;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
            tmp.text = content;
            return tmp;
        }

        static GameObject CreateBurst(string name, Material mat, Color colorA, Color colorB, int count, Vector2 speed, Vector2 size,
            float lifetime, float coneAngle, float gravity, float flashSize, Color flashColor)
        {
            var go = new GameObject(name);
            ParticleSystem ps = AddParticles(go, mat);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = coneAngle > 0f ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Sphere;
            shape.angle = coneAngle;
            shape.radius = 0.05f;

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(100f);
            limit.drag = new ParticleSystem.MinMaxCurve(4f);
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;

            ShrinkAndFade(ps);

            if (flashSize > 0f)
            {
                var flashGo = new GameObject("Flash");
                flashGo.transform.SetParent(go.transform, false);
                ParticleSystem flash = AddParticles(flashGo, mat);
                var fm = flash.main;
                fm.duration = 0.2f;
                fm.loop = false;
                fm.startLifetime = 0.09f;
                fm.startSpeed = 0f;
                fm.startSize = flashSize;
                fm.startColor = flashColor;
                fm.simulationSpace = ParticleSystemSimulationSpace.World;
                var fe = flash.emission;
                fe.rateOverTime = 0f;
                fe.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                var fs = flash.shape;
                fs.enabled = false;
                ShrinkAndFade(flash);
            }

            return go;
        }

        static ParticleSystem AddParticles(GameObject go, Material mat)
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var main = ps.main;
            main.playOnAwake = true;
            main.maxParticles = 300;
            return ps;
        }

        static void ShrinkAndFade(ParticleSystem ps)
        {
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;
        }

        static GameObject SavePrefab(GameObject go)
        {
            string path = $"{PrefabDir}/{go.name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ------------------------------------------------------------------ Scène

        static void BuildScene(BuildContext a)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lumière & ambiance
            var sunGo = new GameObject("Directional Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = UnityEngine.LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.8f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.48f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.38f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.15f, 0.2f);

            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = a.volumeProfile;

            // Salle
            Transform room = new GameObject("Room").transform;
            Block("Floor", room, new Vector3(0f, -0.25f, 0f), new Vector3(RoomWidth + WallThickness * 2f, 0.5f, RoomDepth + WallThickness * 2f), a.floorMat, fadeable: false);

            float wallY = WallHeight * 0.5f;
            float halfW = RoomWidth * 0.5f + WallThickness * 0.5f;
            float halfD = RoomDepth * 0.5f + WallThickness * 0.5f;
            Block("Wall_North", room, new Vector3(0f, wallY, halfD), new Vector3(RoomWidth + WallThickness * 2f, WallHeight, WallThickness), a.wallMat, fadeable: true);
            Block("Wall_South", room, new Vector3(0f, wallY, -halfD), new Vector3(RoomWidth + WallThickness * 2f, WallHeight, WallThickness), a.wallMat, fadeable: true);
            Block("Wall_East", room, new Vector3(halfW, wallY, 0f), new Vector3(WallThickness, WallHeight, RoomDepth), a.wallMat, fadeable: true);
            Block("Wall_West", room, new Vector3(-halfW, wallY, 0f), new Vector3(WallThickness, WallHeight, RoomDepth), a.wallMat, fadeable: true);

            var boundsGo = new GameObject("CameraBounds");
            boundsGo.transform.SetParent(room, false);
            var bounds = boundsGo.AddComponent<BoxCollider>();
            bounds.isTrigger = true;
            bounds.size = new Vector3(RoomWidth - 10f, 2f, RoomDepth - 8f);

            // Décor
            Transform props = new GameObject("Props").transform;
            props.SetParent(room, false);
            foreach (Vector3 p in new[] { new Vector3(-5f, 0f, 3.5f), new Vector3(5f, 0f, 3.5f), new Vector3(-5f, 0f, -3f), new Vector3(5f, 0f, -3f) })
                Block("Pillar", props, p + Vector3.up * WallHeight * 0.5f, new Vector3(1f, WallHeight * 0.5f, 1f), a.pillarMat, fadeable: true, PrimitiveType.Cylinder);

            Block("Crate", props, new Vector3(-7.4f, 0.5f, 5.4f), Vector3.one, a.woodMat, fadeable: true).transform.rotation = Quaternion.Euler(0f, 12f, 0f);
            Block("Crate", props, new Vector3(-6.3f, 0.4f, 5.7f), Vector3.one * 0.8f, a.woodMat, fadeable: true).transform.rotation = Quaternion.Euler(0f, -20f, 0f);
            Block("Crate", props, new Vector3(-7.3f, 1.25f, 5.4f), Vector3.one * 0.5f, a.woodMat, fadeable: true).transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            Block("Crate", props, new Vector3(7.5f, 0.5f, -5.5f), Vector3.one, a.woodMat, fadeable: true).transform.rotation = Quaternion.Euler(0f, -8f, 0f);

            Block("Rock", props, new Vector3(7.2f, 0.25f, 5.6f), new Vector3(1.4f, 0.8f, 1.1f), a.rockMat, fadeable: false, PrimitiveType.Sphere);
            Block("Rock", props, new Vector3(6.2f, 0.15f, 6.1f), new Vector3(0.7f, 0.5f, 0.6f), a.rockMat, fadeable: false, PrimitiveType.Sphere);
            Block("Rock", props, new Vector3(-7.6f, 0.2f, -5.8f), new Vector3(1f, 0.6f, 0.9f), a.rockMat, fadeable: false, PrimitiveType.Sphere);

            CorruptionPuddle(props, new Vector3(-3f, 0f, -5f), a);
            CorruptionPuddle(props, new Vector3(4.5f, 0f, 0.5f), a);

            foreach (float x in new[] { -6f, 0f, 6f })
                Torch(props, new Vector3(x, 0f, RoomDepth * 0.5f), a);

            AmbientSpores(room, a);

            // Personnages
            GameObject player = CreatePlayer(a);
            CreateDummy(a);

            // Caméra
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.05f);
            camGo.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var follow = camGo.AddComponent<CameraFollow>();
            follow.target = player.transform;
            follow.roomBounds = bounds;
            follow.SnapToTarget();

            var occlusion = camGo.AddComponent<WallOcclusion>();
            occlusion.target = player.transform;
            occlusion.targetSize = new Vector2(1.4f, 1f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            Selection.activeGameObject = player;
        }

        static GameObject CreatePlayer(BuildContext a)
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.05f, -3f);

            var cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.5f, 0f);
            cc.height = 1f;
            cc.radius = 0.35f;
            cc.skinWidth = 0.05f;
            cc.stepOffset = 0.25f;

            player.AddComponent<PlayerControls>();
            var controller = player.AddComponent<PlayerController>();
            controller.stats = a.axolotl;
            var attack = player.AddComponent<PlayerAttack>();
            attack.projectilePrefab = a.projectile;
            attack.muzzleVfxPrefab = a.muzzleVfx;

            CreateQuadrupedRig(player, controller, a);
            FlatQuad("BlobShadow", player.transform, a.shadowMat, 1.1f, new Vector3(0f, 0.02f, 0f), faceUp: true);

            var aimPivot = new GameObject("AimIndicator").transform;
            aimPivot.SetParent(player.transform, false);
            aimPivot.localPosition = new Vector3(0f, 0.03f, 0f);
            FlatQuad("Arrow", aimPivot, a.aimMat, 0.45f, new Vector3(0f, 0f, 1f), faceUp: true);
            controller.aimIndicator = aimPivot;

            var dashGo = new GameObject("DashParticles");
            dashGo.transform.SetParent(player.transform, false);
            dashGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            ParticleSystem dash = AddParticles(dashGo, a.particleAlpha);
            var main = dash.main;
            main.duration = 0.15f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            Color dashColor = a.axolotl.placeholderColor;
            dashColor.a = 0.6f;
            main.startColor = dashColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = dash.emission;
            emission.rateOverTime = 140f;
            var shape = dash.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            ShrinkAndFade(dash);
            controller.dashParticles = dash;

            return player;
        }

        static void CreateDummy(BuildContext a)
        {
            var dummy = new GameObject("TrainingDummy");
            dummy.transform.position = new Vector3(0f, 0f, 3f);

            var col = dummy.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.85f, 0f);
            col.height = 1.7f;
            col.radius = 0.5f;

            var td = dummy.AddComponent<TrainingDummy>();
            td.spritePunch = CreateSpriteVisual(dummy.transform, a, DummyColor, new Vector3(1.1f, 1.6f, 1f), out SpriteRenderer sr);
            td.spriteRenderer = sr;
            td.hitVfxPrefab = a.hitVfx;
            FlatQuad("BlobShadow", dummy.transform, a.shadowMat, 1.5f, new Vector3(0f, 0.02f, 0f), faceUp: true);

            var spawner = dummy.AddComponent<DamageNumberSpawner>();
            spawner.prefab = a.damageNumber;
            td.damageNumbers = spawner;

            var statsGo = new GameObject("StatsText");
            statsGo.transform.SetParent(dummy.transform, false);
            statsGo.transform.localPosition = new Vector3(1.6f, 1.4f, 0f);
            TextMeshPro stats = SetupText(statsGo, a, 3f, "Total 0\nDPS 0");
            stats.fontStyle = FontStyles.Bold;
            stats.horizontalAlignment = HorizontalAlignmentOptions.Left;
            stats.color = new Color(0.85f, 0.9f, 0.85f);
            statsGo.AddComponent<Billboard>();
            td.statsText = stats;
        }

        /// <summary>
        /// Visual (billboard + squash) > Rig (flip, tri des parties, animateur) > pivots Body / Head / Tail / Legs.
        /// Chaque pivot contient un carré "_Art" : c'est lui qu'on remplacera par les vrais sprites.
        /// </summary>
        static void CreateQuadrupedRig(GameObject player, PlayerController controller, BuildContext a)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(player.transform, false);
            visual.AddComponent<Billboard>();
            controller.spritePunch = visual.AddComponent<ScalePunch>();

            var rigGo = new GameObject("Rig");
            rigGo.transform.SetParent(visual.transform, false);
            rigGo.AddComponent<SortingGroup>();
            var rig = rigGo.AddComponent<CharacterRig>();
            rig.flipRoot = rigGo.transform;
            Transform root = rigGo.transform;

            Color body = a.axolotl.placeholderColor;
            rig.body = RigPart("Body", root, new Vector3(0f, 0.55f, 0f), 0f, new Vector2(0.8f, 0.48f), new Vector2(0f, -0.24f), body, 0, a);
            rig.head = RigPart("Head", root, new Vector3(0.3f, 0.7f, 0f), 0f, new Vector2(0.42f, 0.38f), new Vector2(0.18f, -0.17f), Tint(body, 1.12f), 2, a);
            AddSquare("Eye", rig.head, new Vector2(0.08f, 0.1f), new Vector2(0.28f, 0.02f), 0f, new Color(0.12f, 0.05f, 0.12f), 3, a);
            var gill = new Color(0.95f, 0.35f, 0.6f);
            AddSquare("Gill_Top", rig.head, new Vector2(0.07f, 0.2f), new Vector2(0.06f, 0.14f), 20f, gill, 1, a);
            AddSquare("Gill_Back", rig.head, new Vector2(0.07f, 0.18f), new Vector2(0f, 0.06f), 55f, gill, 1, a);
            rig.tail = RigPart("Tail", root, new Vector3(-0.36f, 0.62f, 0f), -15f, new Vector2(0.44f, 0.14f), new Vector2(-0.22f, -0.07f), Tint(body, 0.95f), -1, a);

            rig.legs = new[]
            {
                Leg("Leg_FrontFar", root, new Vector3(0.3f, 0.4f, 0f), front: true, far: true, Tint(body, 0.7f), -2, a),
                Leg("Leg_BackFar", root, new Vector3(-0.18f, 0.4f, 0f), front: false, far: true, Tint(body, 0.7f), -2, a),
                Leg("Leg_FrontNear", root, new Vector3(0.24f, 0.36f, 0f), front: true, far: false, Tint(body, 0.92f), 1, a),
                Leg("Leg_BackNear", root, new Vector3(-0.24f, 0.36f, 0f), front: false, far: false, Tint(body, 0.92f), 1, a),
            };

            var animator = rigGo.AddComponent<CharacterAnimator>();
            animator.rig = rig;
            animator.profile = a.quadrupedProfile;
            player.AddComponent<PlayerAnimationDriver>().characterAnimator = animator;
        }

        static CharacterRig.Leg Leg(string name, Transform parent, Vector3 hip, bool front, bool far, Color color, int order, BuildContext a)
        {
            Transform pivot = RigPart(name, parent, hip, 0f, new Vector2(0.14f, 0.34f), new Vector2(0f, -0.34f), color, order, a);
            return new CharacterRig.Leg { pivot = pivot, front = front, far = far };
        }

        static Transform RigPart(string name, Transform parent, Vector3 pivotPosition, float angle, Vector2 size, Vector2 artOffset, Color color, int order, BuildContext a)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = pivotPosition;
            pivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            AddSquare(name + "_Art", pivot, size, artOffset, 0f, color, order, a);
            return pivot;
        }

        /// <summary>Carré placeholder (le sprite a son pivot en bas au centre : offset = position du bas du carré).</summary>
        static SpriteRenderer AddSquare(string name, Transform parent, Vector2 size, Vector2 offset, float angle, Color color, int order, BuildContext a)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = a.square;
            renderer.color = color;
            renderer.sharedMaterial = a.spriteMat;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = false;
            return renderer;
        }

        static Color Tint(Color c, float factor) =>
            new Color(Mathf.Clamp01(c.r * factor), Mathf.Clamp01(c.g * factor), Mathf.Clamp01(c.b * factor), 1f);

        /// <summary>Visual (billboard) > Sprite (carré placeholder, futur rig 2D Animation).</summary>
        static ScalePunch CreateSpriteVisual(Transform parent, BuildContext a, Color color, Vector3 scale, out SpriteRenderer renderer)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);
            visual.AddComponent<Billboard>();

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(visual.transform, false);
            spriteGo.transform.localScale = scale;
            renderer = spriteGo.AddComponent<SpriteRenderer>();
            renderer.sprite = a.square;
            renderer.color = color;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.sharedMaterial = a.spriteMat;
            return spriteGo.AddComponent<ScalePunch>();
        }

        static GameObject Block(string name, Transform parent, Vector3 position, Vector3 scale, Material mat, bool fadeable, PrimitiveType type = PrimitiveType.Cube)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            // Selon la version d'Unity, CreatePrimitive n'ajoute pas toujours de collider 3D : on s'assure qu'il y en a un.
            if (go.GetComponent<Collider>() == null)
            {
                switch (type)
                {
                    case PrimitiveType.Sphere: go.AddComponent<SphereCollider>(); break;
                    case PrimitiveType.Capsule: go.AddComponent<CapsuleCollider>(); break;
                    default: go.AddComponent<BoxCollider>(); break;
                }
            }
            if (fadeable) go.AddComponent<FadeableWall>();
            return go;
        }

        static GameObject FlatQuad(string name, Transform parent, Material mat, float size, Vector3 localPosition, bool faceUp)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPosition;
            quad.transform.localRotation = faceUp ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            quad.transform.localScale = Vector3.one * size;
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return quad;
        }

        static void CorruptionPuddle(Transform parent, Vector3 position, BuildContext a)
        {
            GameObject puddle = Block("CorruptionPuddle", parent, position + Vector3.up * 0.01f, new Vector3(2.4f, 0.01f, 1.8f), a.corruptionMat, fadeable: false, PrimitiveType.Cylinder);
            Object.DestroyImmediate(puddle.GetComponent<Collider>());

            var bubblesGo = new GameObject("CorruptionBubbles");
            bubblesGo.transform.SetParent(parent, false);
            bubblesGo.transform.localPosition = position + Vector3.up * 0.05f;
            ParticleSystem ps = AddParticles(bubblesGo, a.particleAdd);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.2f, 1f, 0.8f), new Color(0.35f, 0.1f, 0.6f, 0.8f));
            var emission = ps.emission;
            emission.rateOverTime = 8f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.6f, 0.05f, 1.1f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            ShrinkAndFade(ps);

            var lightGo = new GameObject("CorruptionGlow");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = position + Vector3.up * 0.6f;
            var light = lightGo.AddComponent<Light>();
            light.type = UnityEngine.LightType.Point;
            light.color = new Color(0.65f, 0.25f, 1f);
            light.range = 4f;
            light.intensity = 1.5f;
            lightGo.AddComponent<LightFlicker>().amplitude = 0.15f;
        }

        static void Torch(Transform parent, Vector3 wallPosition, BuildContext a)
        {
            Vector3 basePos = wallPosition + new Vector3(0f, 1.8f, -0.12f);
            Block("TorchBracket", parent, basePos, new Vector3(0.18f, 0.5f, 0.24f), a.woodMat, fadeable: false);

            var fireGo = new GameObject("TorchFire");
            fireGo.transform.SetParent(parent, false);
            fireGo.transform.localPosition = basePos + new Vector3(0f, 0.3f, -0.05f);
            fireGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem ps = AddParticles(fireGo, a.particleAdd);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.85f, 0.35f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 28f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.06f;
            ShrinkAndFade(ps);

            var lightGo = new GameObject("TorchLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = basePos + new Vector3(0f, 0.5f, -0.6f);
            var light = lightGo.AddComponent<Light>();
            light.type = UnityEngine.LightType.Point;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.range = 7f;
            light.intensity = 3f;
            TorchShadowInstaller.Configure(light);
            lightGo.AddComponent<LightFlicker>();
        }

        static void AmbientSpores(Transform parent, BuildContext a)
        {
            var go = new GameObject("AmbientSpores");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            ParticleSystem ps = AddParticles(go, a.particleAdd);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 1f, 0.6f, 0.7f), new Color(0.6f, 1f, 0.8f, 0.5f));
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(RoomWidth, 2.5f, RoomDepth);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.3f;
            ShrinkAndFade(ps);
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
