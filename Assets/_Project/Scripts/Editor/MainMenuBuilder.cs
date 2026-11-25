using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TaintlessGlade.EditorTools
{
    /// <summary>
    /// Tools > Taintless Glade > Menu > Construire le menu principal :
    /// génère en un clic tout le menu de démarrage (police Fredoka, images du feuillage, thème, personnages, salles,
    /// scène MainMenu avec son décor 3D et son interface) et règle le mode Play pour toujours démarrer dessus.
    /// Relancer l'outil régénère la scène (les stats déjà créées sont conservées).
    /// </summary>
    public static class MainMenuBuilder
    {
        const string Root = "Assets/_Project";
        const string ResourcesDir = Root + "/Resources";
        const string DataDir = Root + "/Data";
        const string KenneyBorders = Root + "/Art/kenney_fantasy-ui-borders/PNG/Default/";
        const string KenneyCursors = Root + "/Art/kenney_cursor-pack/PNG/Basic/Default/";
        const string KenneyInput = Root + "/Art/kenney_input-prompts-pixel-1-bit/Tiles (White)/";
        const string MaterialDir = Root + "/Art/Materials/";

        static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        // ------------------------------------------------------------------ Point d'entrée

        [MenuItem("Tools/Taintless Glade/Menu/Construire le menu principal")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                EditorUtility.DisplayProgressBar("Menu", "Police Fredoka...", 0.05f);
                TMP_FontAsset font = FredokaFontInstaller.GetOrCreateFontAsset();
                if (font == null) return;
                FredokaFontInstaller.SetAsDefaultFont(font);
                FredokaFontInstaller.ApplyEverywhere();

                EditorUtility.DisplayProgressBar("Menu", "Images du menu...", 0.25f);
                if (!File.Exists(MenuArtGenerator.FoliagePath("TopRight", "Back")) || !File.Exists(MenuArtGenerator.SilhouettePath("fox")))
                    MenuArtGenerator.GenerateAll();
                AssetDatabase.Refresh();

                EditorUtility.DisplayProgressBar("Menu", "Thème et données...", 0.45f);
                EnsureFolder(ResourcesDir);
                EnsureFolder(DataDir);
                MenuTheme theme = CreateTheme(font);
                CreateGlyphLibrary();
                CharacterRoster roster = CreateRoster();
                CreateRoomCatalog();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayProgressBar("Menu", "Scène...", 0.7f);
                BuildScene(theme, roster);

                AddScenesToBuildSettings();
                PlayModeStartScene.Apply();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            Debug.Log("[Menu] Menu principal généré : " + PlayModeStartScene.MenuScenePath + " — appuie sur Play (le jeu démarre maintenant toujours sur le menu).");
        }

        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ------------------------------------------------------------------ Imports

        static Sprite ImportSprite(string path, float ppu = 100f, int border = 0, bool point = true, SpriteAlignment alignment = SpriteAlignment.Center)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Menu] Image introuvable : " + path);
                return null;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool ok = importer.textureType == TextureImporterType.Sprite
                      && importer.spriteImportMode == SpriteImportMode.Single
                      && Mathf.Approximately(importer.spritePixelsPerUnit, ppu)
                      && importer.spriteBorder == new Vector4(border, border, border, border)
                      && importer.filterMode == (point ? FilterMode.Point : FilterMode.Bilinear)
                      && settings.spriteAlignment == (int)alignment
                      && !importer.mipmapEnabled;
            if (!ok)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = ppu;
                importer.spriteBorder = new Vector4(border, border, border, border);
                importer.mipmapEnabled = false;
                importer.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)alignment;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Texture2D ImportCursor(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Menu] Curseur introuvable : " + path);
                return null;
            }
            if (importer.textureType != TextureImporterType.Cursor || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Cursor;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ Thème, glyphes, données

        static MenuTheme CreateTheme(TMP_FontAsset font)
        {
            var theme = LoadOrCreate<MenuTheme>(ResourcesDir + "/MenuTheme.asset");
            theme.font = font;
            theme.panelTint = new Color(0.04f, 0.06f, 0.07f, 0.96f);
            theme.panelFill = ImportSprite(KenneyBorders + "Panel/panel-001.png", 100f, 16);
            theme.panelBorder = ImportSprite(KenneyBorders + "Border/panel-border-001.png", 100f, 16);
            theme.buttonBorder = ImportSprite(KenneyBorders + "Border/panel-border-000.png", 100f, 16);
            theme.divider = ImportSprite(KenneyBorders + "Divider/divider-000.png");
            theme.dividerFade = ImportSprite(KenneyBorders + "Divider Fade/divider-fade-000.png");
            theme.softCircle = ImportSprite(MenuArtGenerator.OutDir + "/UI_SoftCircle.png", 100f, 0, false);
            theme.pixel = ImportSprite(MenuArtGenerator.OutDir + "/UI_Pixel.png", 100f, 0, true);

            theme.arrowLeft = ImportSprite(KenneyCursors + "arrow_w.png");
            theme.arrowRight = ImportSprite(KenneyCursors + "arrow_e.png");
            theme.lockIcon = ImportSprite(KenneyCursors + "lock.png");
            theme.doorIcon = ImportSprite(KenneyCursors + "door_enter.png");

            // Quel curseur où : flèche dans les menus, main sur un élément cliquable, viseur en jeu, sablier au chargement, interdit si verrouillé.
            theme.cursorMenu = ImportCursor(KenneyCursors + "cursor_none.png");
            theme.menuHotspot = new Vector2(10f, 6f);
            theme.cursorHover = ImportCursor(KenneyCursors + "hand_point.png");
            theme.hoverHotspot = new Vector2(9f, 5f);
            theme.cursorGame = ImportCursor(KenneyCursors + "target_round_a.png");
            theme.gameHotspot = new Vector2(16f, 16f);
            theme.cursorBusy = ImportCursor(KenneyCursors + "busy_hourglass.png");
            theme.busyHotspot = new Vector2(16f, 16f);
            theme.cursorForbidden = ImportCursor(KenneyCursors + "disabled.png");
            theme.forbiddenHotspot = new Vector2(16f, 16f);

            EditorUtility.SetDirty(theme);
            return theme;
        }

        static void CreateGlyphLibrary()
        {
            var lib = LoadOrCreate<InputGlyphLibrary>(ResourcesDir + "/InputGlyphLibrary.asset");
            // Indices de la feuille de tuiles Kenney (34 colonnes) : 76-79 = souris, 4-7 = boutons A B X Y.
            Sprite Tile(int index) => ImportSprite(KenneyInput + "tile_" + index.ToString("0000") + ".png", 16f);
            lib.mouse = Tile(76);
            lib.mouseLeft = Tile(77);
            lib.mouseRight = Tile(78);
            lib.mouseMiddle = Tile(79);
            lib.padSouth = Tile(4);
            lib.padEast = Tile(5);
            lib.padWest = Tile(6);
            lib.padNorth = Tile(7);
            EditorUtility.SetDirty(lib);
        }

        class CharacterDef
        {
            public string id, name, perkTitle, perkText, weaponName, weaponText, bonusHp;
            public bool playable;
            public int hp;
            public float dash, speed, shot, length, attack, luck;
            public Color color;
        }

        static readonly CharacterDef[] Characters =
        {
            new CharacterDef { id = "axolotl", name = "Axolotl", playable = true, hp = 3, dash = 2f, speed = 2f, shot = 2f, length = 2.75f, attack = 4f, luck = 0f,
                color = new Color(1f, 0.6f, 0.78f), perkTitle = "Régénération passive", perkText = "Récupère de la vie petit à petit, sans rien faire.",
                weaponName = "Crachat", weaponText = "Tir à distance. Maintenir pour charger un tir plus puissant." },
            new CharacterDef { id = "fox", name = "Renard", hp = 3, dash = 2f, speed = 2f, shot = 2f, length = 0.5f, attack = 3f, luck = 1f,
                color = new Color(0.95f, 0.5f, 0.2f), perkTitle = "Attaque variée", perkText = "Deux façons d'attaquer selon qu'il bouge ou non.",
                weaponName = "Queue de renard", weaponText = "À l'arrêt : frappe autour de lui, mais plus il touche d'ennemis, plus les dégâts baissent. En mouvement : attaque en cône." },
            new CharacterDef { id = "rabbit", name = "Lapin", hp = 2, dash = 2f, speed = 3f, shot = 2.5f, length = 0.5f, attack = 3f, luck = 1f,
                color = new Color(0.9f, 0.85f, 0.8f), perkTitle = "Haute vitesse", perkText = "Très rapide, avec un dash plus long.",
                weaponName = "Coup de pied", weaponText = "Chance d'assommer l'ennemi." },
            new CharacterDef { id = "bear", name = "Ours", hp = 3, dash = 1f, speed = 1f, shot = 2f, length = 0.5f, attack = 6f, luck = 0f, bonusHp = "2 PV de pierre",
                color = new Color(0.5f, 0.35f, 0.25f), perkTitle = "Tank", perkText = "Lent, attaques lentes, très résistant, énormes dégâts.",
                weaponName = "Coup de griffe", weaponText = "Attaque au corps à corps lente et dévastatrice." },
            new CharacterDef { id = "bat", name = "Chauve-souris", hp = 3, dash = 2f, speed = 2f, shot = 1f, length = 2f, attack = 3f, luck = 0f, bonusHp = "3 PV de feuilles",
                color = new Color(0.4f, 0.3f, 0.5f), perkTitle = "Vol", perkText = "Survole les obstacles.",
                weaponName = "Ultra-son", weaponText = "Onde qui rebondit sur les murs." },
            new CharacterDef { id = "fish", name = "Poisson", hp = 0, dash = 1f, speed = 2f, shot = 4f, length = 4f, attack = 3f, luck = 0f, bonusHp = "Bocal : 3 coups",
                color = new Color(0.3f, 0.7f, 0.9f), perkTitle = "Le bocal", perkText = "Meurt si son bocal est touché trois fois ; le bocal se réinitialise à chaque changement de salle. (Pourrait aller dans l'eau.)",
                weaponName = "Crachat puissant", weaponText = "Repousse les ennemis. Personnage difficile." },
        };

        static CharacterRoster CreateRoster()
        {
            var roster = LoadOrCreate<CharacterRoster>(ResourcesDir + "/CharacterRoster.asset");
            roster.characters.Clear();

            foreach (CharacterDef def in Characters)
            {
                string path = DataDir + "/Stats_" + (def.id == "axolotl" ? "Axolotl" : char.ToUpperInvariant(def.id[0]) + def.id.Substring(1)) + ".asset";
                var stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(path);
                if (stats == null)
                {
                    stats = ScriptableObject.CreateInstance<CharacterStats>();
                    stats.maxHp = def.hp;
                    stats.dashSpeed = def.dash;
                    stats.speed = def.speed;
                    stats.shotSpeed = def.shot;
                    stats.shotLength = def.length;
                    stats.attack = def.attack;
                    stats.luck = def.luck;
                    stats.placeholderColor = def.color;
                    AssetDatabase.CreateAsset(stats, path);
                }

                // Texte et identité : toujours remis à jour (les chiffres des stats existantes ne sont jamais écrasés).
                stats.id = def.id;
                stats.displayName = def.name;
                stats.playable = def.playable;
                stats.perkTitle = def.perkTitle;
                stats.perkText = def.perkText;
                stats.weaponName = def.weaponName;
                stats.weaponText = def.weaponText;
                stats.bonusHp = def.bonusHp;
                stats.silhouette = def.playable ? null : ImportSprite(MenuArtGenerator.SilhouettePath(def.id), 320f, 0, false, SpriteAlignment.BottomCenter);
                EditorUtility.SetDirty(stats);
                roster.characters.Add(stats);
            }
            EditorUtility.SetDirty(roster);
            return roster;
        }

        static void CreateRoomCatalog()
        {
            var catalog = LoadOrCreate<RoomCatalog>(ResourcesDir + "/RoomCatalog.asset");
            catalog.rooms.Clear();
            catalog.rooms.Add(new RoomCatalog.Room
            {
                id = "test_room",
                title = "Salle de test",
                description = "Entraînement : un mannequin et une salle libre.",
                sceneName = "TestRoom",
                thumbnail = ImportSprite(MenuArtGenerator.RoomThumbPath, 100f, 0, false),
            });
            catalog.minSlots = 12;
            EditorUtility.SetDirty(catalog);
        }

        // ------------------------------------------------------------------ Scène

        class SceneContext
        {
            public MenuTheme theme;
            public Material sprite, floor, wall, shadow, particle;
            public RigAnimationProfile profile;
        }

        static Material LoadMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + name + ".mat");

        static void BuildScene(MenuTheme theme, CharacterRoster roster)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ctx = new SceneContext
            {
                theme = theme,
                sprite = LoadMaterial("M_Sprite"),
                floor = LoadMaterial("M_Floor"),
                wall = LoadMaterial("M_Wall"),
                shadow = LoadMaterial("M_BlobShadow"),
                particle = LoadMaterial("M_ParticleAdditive"),
                profile = AssetDatabase.LoadAssetAtPath<RigAnimationProfile>(DataDir + "/AnimProfile_Quadruped.asset"),
            };
            if (ctx.profile == null)
            {
                ctx.profile = ScriptableObject.CreateInstance<RigAnimationProfile>();
                AssetDatabase.CreateAsset(ctx.profile, DataDir + "/AnimProfile_Quadruped.asset");
            }

            // Ambiance
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.36f, 0.46f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.28f, 0.24f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.1f, 0.14f);

            var sunGo = new GameObject("Directional Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = UnityEngine.LightType.Directional;
            sun.color = new Color(0.75f, 0.82f, 1f);
            sun.intensity = 0.55f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
            RenderSettings.sun = sun;

            var volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "/Settings/TestRoomVolumeProfile.asset");
            if (volumeProfile != null)
            {
                var volume = new GameObject("Global Volume").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = volumeProfile;
            }

            // Décor 3D
            Transform stageRoot = new GameObject("Stage").transform;
            BuildEnvironment(stageRoot, ctx);
            Camera cam = BuildCamera();

            var stage = stageRoot.gameObject.AddComponent<MenuStage>();
            stage.cam = cam;
            var slots = new List<MenuStage.Slot>();
            foreach (CharacterStats stats in roster.characters)
                slots.Add(BuildSlot(stageRoot, stats, ctx));
            stage.slots = slots.ToArray();

            // Interface
            BuildEventSystem();
            BuildUi(ctx, stage);

            EditorSceneManager.SaveScene(scene, PlayModeStartScene.MenuScenePath);
        }

        static void BuildEnvironment(Transform parent, SceneContext ctx)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(0f, -0.25f, 2f);
            floor.transform.localScale = new Vector3(40f, 0.5f, 30f);
            if (ctx.floor != null) floor.GetComponent<MeshRenderer>().sharedMaterial = ctx.floor;
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "BackWall";
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = new Vector3(0f, 4f, 9f);
            wall.transform.localScale = new Vector3(40f, 10f, 0.6f);
            if (ctx.wall != null) wall.GetComponent<MeshRenderer>().sharedMaterial = ctx.wall;
            Object.DestroyImmediate(wall.GetComponent<Collider>());

            // Torche chaude à gauche, lueur froide à droite.
            var torch = new GameObject("TorchLight").AddComponent<Light>();
            torch.transform.SetParent(parent, false);
            torch.transform.localPosition = new Vector3(-2.6f, 2.4f, 0.2f);
            torch.type = UnityEngine.LightType.Point;
            torch.color = new Color(1f, 0.62f, 0.3f);
            torch.range = 10f;
            torch.intensity = 4f;
            torch.shadows = LightShadows.Soft;
            torch.gameObject.AddComponent<LightFlicker>();

            var cool = new GameObject("CoolLight").AddComponent<Light>();
            cool.transform.SetParent(parent, false);
            cool.transform.localPosition = new Vector3(3f, 1.4f, 1.8f);
            cool.type = UnityEngine.LightType.Point;
            cool.color = new Color(0.4f, 0.55f, 0.95f);
            cool.range = 9f;
            cool.intensity = 1.6f;

            // Spores qui flottent.
            var go = new GameObject("AmbientSpores");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 1.3f, 1f);
            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (ctx.particle != null) renderer.sharedMaterial = ctx.particle;
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 1f, 0.6f, 0.7f), new Color(0.6f, 1f, 0.8f, 0.5f));
            var emission = ps.emission;
            emission.rateOverTime = 16f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(10f, 3f, 8f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.3f;
            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = gradient;
        }

        static Camera BuildCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.04f);
            camGo.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camGo.transform.position = new Vector3(0.1f, 1.6f, -3.3f);
            camGo.transform.LookAt(new Vector3(0f, 0.62f, 0f));
            return cam;
        }

        static MenuStage.Slot BuildSlot(Transform stageRoot, CharacterStats stats, SceneContext ctx)
        {
            var root = new GameObject("Slot_" + stats.id).transform;
            root.SetParent(stageRoot, false);
            var slot = new MenuStage.Slot { stats = stats, root = root };

            // Ombre au sol.
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shadow.name = "BlobShadow";
            shadow.transform.SetParent(root, false);
            shadow.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.transform.localScale = Vector3.one * 1.3f;
            Object.DestroyImmediate(shadow.GetComponent<Collider>());
            var shadowRenderer = shadow.GetComponent<MeshRenderer>();
            if (ctx.shadow != null) shadowRenderer.sharedMaterial = ctx.shadow;
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            visual.gameObject.AddComponent<Billboard>();
            slot.visual = visual;
            slot.punch = visual.gameObject.AddComponent<ScalePunch>();

            if (stats.playable)
            {
                // Le vrai personnage : même rig et mêmes animations que dans le jeu (idle, yeux qui suivent la souris...).
                var rigGo = new GameObject("Rig");
                rigGo.transform.SetParent(visual, false);
                rigGo.AddComponent<SortingGroup>();
                var rig = rigGo.AddComponent<CharacterRig>();
                rig.flipRoot = rigGo.transform;
                rig.body = Pivot("Body", rigGo.transform);
                rig.head = Pivot("Head", rigGo.transform);
                rig.tail = Pivot("Tail", rigGo.transform);
                rig.legs = new[]
                {
                    Leg("Leg_FrontFar", rigGo.transform, true, true),
                    Leg("Leg_BackFar", rigGo.transform, false, true),
                    Leg("Leg_FrontNear", rigGo.transform, true, false),
                    Leg("Leg_BackNear", rigGo.transform, false, false),
                };
                var animator = rigGo.AddComponent<CharacterAnimator>();
                animator.rig = rig;
                animator.profile = ctx.profile;
                AxolotlSpriteInstaller.Apply(rig);
                slot.animator = animator;
            }
            else
            {
                // Personnage à venir : silhouette noire (image générée par MenuArtGenerator).
                var sr = new GameObject("Silhouette").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(visual, false);
                sr.sprite = stats.silhouette;
                sr.color = Color.white;
                if (ctx.sprite != null) sr.sharedMaterial = ctx.sprite;
                sr.shadowCastingMode = ShadowCastingMode.Off;
                sr.receiveShadows = false;
            }

            // Titre au-dessus : blanc si jouable, rouge sinon.
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(root, false);
            titleGo.transform.localPosition = new Vector3(0f, stats.playable ? 1.55f : 1.45f, 0f);
            titleGo.AddComponent<Billboard>();
            var title = titleGo.AddComponent<TextMeshPro>();
            title.font = ctx.theme.font;
            title.fontSize = 2.4f;
            title.alignment = TextAlignmentOptions.Center;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.rectTransform.sizeDelta = new Vector2(8f, 2f);
            title.fontStyle = FontStyles.Bold;
            if (stats.playable)
            {
                title.text = stats.displayName;
                title.color = Color.white;
            }
            else
            {
                title.text = stats.displayName + "\n<size=55%><color=#C03030>Bientôt disponible</color></size>";
                title.color = new Color(0.92f, 0.2f, 0.22f);
            }
            slot.title = title;
            return slot;
        }

        static Transform Pivot(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static CharacterRig.Leg Leg(string name, Transform parent, bool front, bool far) =>
            new CharacterRig.Leg { pivot = Pivot(name, parent), front = front, far = far };

        static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // ------------------------------------------------------------------ Interface

        static void BuildUi(SceneContext ctx, MenuStage stage)
        {
            MenuTheme theme = ctx.theme;

            var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRect = canvasGo.GetComponent<RectTransform>();

            var controllerGo = new GameObject("MenuController");
            var controller = controllerGo.AddComponent<MenuController>();
            controller.stage = stage;

            // --- Feuillage : calques du fond d'abord, puis ceux de devant ---
            RectTransform foliageRoot = MenuUi.Rect("Foliage", canvasRect);
            MenuUi.Stretch(foliageRoot);
            var foliage = foliageRoot.gameObject.AddComponent<FoliageFrame>();
            var layers = new List<FoliageFrame.Layer>();
            float delay = 0f;
            foreach (string layerName in MenuArtGenerator.LayerNames)
            {
                foreach (string massName in MenuArtGenerator.MassNames)
                {
                    Sprite sprite = ImportSprite(MenuArtGenerator.FoliagePath(massName, layerName), 100f, 0, false);
                    bool top = massName.StartsWith("Top");
                    bool isRight = massName.EndsWith("Right");
                    Vector2 corner = new Vector2(isRight ? 1f : 0f, top ? 1f : 0f);
                    Image image = MenuUi.Image("Foliage_" + massName + "_" + layerName, foliageRoot, sprite, Color.white);
                    RectTransform rt = image.rectTransform;
                    rt.anchorMin = rt.anchorMax = corner;
                    rt.pivot = corner;
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = Reference;
                    bool front = layerName == "Front";
                    layers.Add(new FoliageFrame.Layer
                    {
                        rect = rt,
                        outward = new Vector2(isRight ? 1f : -1f, top ? 1f : -1f),
                        depth = front ? 0.95f : 0.3f,
                        introDelay = delay,
                    });
                    delay += 0.07f;
                }
            }
            foliage.layers = layers.ToArray();
            controller.foliage = foliage;

            // --- Particules d'UI (spores + gerbes de clic) ---
            RectTransform particles = MenuUi.Rect("UiParticles", canvasRect);
            MenuUi.Stretch(particles);
            particles.gameObject.AddComponent<UiParticles>();

            // --- Titre (haut droite) ---
            RectTransform titlePanelRect = MenuUi.Rect("TitlePanel", canvasRect);
            MenuUi.Place(titlePanelRect, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-470f, -235f), new Vector2(860f, 340f));
            var titlePanel = AddMenuPanel(titlePanelRect, new Vector2(0f, 220f), 0.8f);
            TextMeshProUGUI title = MenuUi.Text("Title", titlePanelRect, "Taintless\nGlade", 150f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            MenuUi.Stretch(title.rectTransform);
            title.lineSpacing = -22f;
            title.enableAutoSizing = false;
            controller.titlePanel = titlePanel;
            controller.titleRect = title.rectTransform;

            // --- Menu principal (bas gauche) ---
            RectTransform mainRect = MenuUi.Rect("MainPanel", canvasRect);
            MenuUi.Place(mainRect, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(760f, 560f));
            controller.mainPanel = AddMenuPanel(mainRect, new Vector2(-260f, 0f), 0.92f);
            string[] mainNames = { "Démarrer", "Personnage", "Paramètres", "Quitter" };
            var mainButtons = new MenuButton[mainNames.Length];
            for (int i = 0; i < mainNames.Length; i++)
            {
                MenuButton b = MenuButton.Create("Button_" + mainNames[i], mainRect, mainNames[i], new Vector2(430f, 80f), 50f);
                MenuUi.Place(b.transform as RectTransform, Vector2.zero, Vector2.zero, new Vector2(70f, 50f + (mainNames.Length - 1 - i) * 92f), new Vector2(430f, 80f));
                mainButtons[i] = b;
            }
            controller.playButton = mainButtons[0];
            controller.charactersButton = mainButtons[1];
            controller.settingsButton = mainButtons[2];
            controller.quitButton = mainButtons[3];

            var backs = new List<MenuButton>();

            // --- Sélection du personnage ---
            RectTransform charRect = MenuUi.Rect("CharactersPanel", canvasRect);
            MenuUi.Stretch(charRect);
            controller.charactersPanel = AddMenuPanel(charRect, Vector2.zero, 0.97f);
            TextMeshProUGUI charHeader = MenuUi.Text("Header", charRect, "Choix du personnage", 68f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            MenuUi.Place(charHeader.rectTransform, new Vector2(0.37f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(1100f, 96f));

            RectTransform statsRect = MenuUi.Panel("Stats", charRect, new Vector2(640f, 830f));
            MenuUi.Place(statsRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, -10f), new Vector2(640f, 830f));

            MenuButton left = ArrowButton("ArrowLeft", charRect, theme.arrowLeft, new Vector2(0.12f, 0.5f), new Vector2(0f, -10f));
            MenuButton right = ArrowButton("ArrowRight", charRect, theme.arrowRight, new Vector2(0.6f, 0.5f), new Vector2(0f, -10f));
            MenuButton confirm = MenuButton.Create("Confirm", charRect, "Choisir", new Vector2(560f, 90f), 48f, true, TextAlignmentOptions.Center);
            confirm.slide = 0f;
            MenuUi.Place(confirm.transform as RectTransform, new Vector2(0.37f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(560f, 90f));
            confirm.label.rectTransform.offsetMin = new Vector2(12f, 0f);
            backs.Add(BackButton(charRect));

            var charView = charRect.gameObject.AddComponent<CharacterSelectView>();
            charView.controller = controller;
            charView.stage = stage;
            charView.statsRoot = statsRect;
            charView.leftArrow = left;
            charView.rightArrow = right;
            charView.confirmButton = confirm;
            controller.characterView = charView;

            // --- Choix de la salle ---
            RectTransform roomsRect = MenuUi.Panel("RoomsPanel", canvasRect, new Vector2(1640f, 940f));
            MenuUi.Place(roomsRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(1640f, 940f));
            controller.roomsPanel = AddMenuPanel(roomsRect, new Vector2(0f, -80f), 0.9f);
            TextMeshProUGUI roomsHeader = MenuUi.Text("Header", roomsRect, "Choisir une salle", 62f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            MenuUi.Place(roomsHeader.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -26f), new Vector2(900f, 90f));
            Image roomsLine = MenuUi.Divider("HeaderLine", roomsRect, 1500f);
            MenuUi.Place(roomsLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), roomsLine.rectTransform.sizeDelta);

            RectTransform grid = MenuUi.Rect("Grid", roomsRect);
            MenuUi.Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1320f, 680f));
            MenuButton prev = ArrowButton("PagePrevious", roomsRect, theme.arrowLeft, new Vector2(0.5f, 0.5f), new Vector2(-755f, -30f));
            MenuButton next = ArrowButton("PageNext", roomsRect, theme.arrowRight, new Vector2(0.5f, 0.5f), new Vector2(755f, -30f));
            TextMeshProUGUI pageLabel = MenuUi.Text("PageLabel", roomsRect, "1 / 1", 30f, theme.textDim, TextAlignmentOptions.Center);
            MenuUi.Place(pageLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(300f, 44f));
            backs.Add(BackButton(roomsRect, new Vector2(60f, 34f)));

            var roomView = roomsRect.gameObject.AddComponent<RoomSelectView>();
            roomView.controller = controller;
            roomView.grid = grid;
            roomView.previousButton = prev;
            roomView.nextButton = next;
            roomView.pageLabel = pageLabel;
            controller.roomView = roomView;

            // --- Paramètres ---
            RectTransform settingsRect = MenuUi.Panel("SettingsPanel", canvasRect, new Vector2(1500f, 920f));
            MenuUi.Place(settingsRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 920f));
            controller.settingsPanel = AddMenuPanel(settingsRect, new Vector2(0f, -80f), 0.9f);
            var settingsView = settingsRect.gameObject.AddComponent<SettingsView>();
            settingsView.controller = controller;
            settingsView.panel = settingsRect;
            controller.settingsView = settingsView;

            controller.backButtons = backs.ToArray();

            // --- Fondu noir (entrée / sortie du menu), toujours au-dessus ---
            Image fade = MenuUi.Image("Fade", canvasRect, theme.pixel, Color.black);
            MenuUi.Stretch(fade.rectTransform);
            var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 1f;
            fadeGroup.blocksRaycasts = false;
            controller.fade = fadeGroup;
        }

        static MenuPanel AddMenuPanel(RectTransform rect, Vector2 hiddenOffset, float hiddenScale)
        {
            rect.gameObject.AddComponent<CanvasGroup>();
            var panel = rect.gameObject.AddComponent<MenuPanel>();
            panel.hiddenOffset = hiddenOffset;
            panel.hiddenScale = hiddenScale;
            return panel;
        }

        static MenuButton ArrowButton(string name, Transform parent, Sprite icon, Vector2 anchor, Vector2 position)
        {
            MenuButton b = MenuButton.Create(name, parent, "", new Vector2(100f, 120f), 28f, true, TextAlignmentOptions.Center);
            b.slide = 0f;
            MenuUi.Place(b.transform as RectTransform, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(100f, 120f));
            Image img = MenuUi.Image("Icon", b.content, icon, Color.white);
            MenuUi.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
            img.preserveAspect = true;
            return b;
        }

        static MenuButton BackButton(Transform parent, Vector2? position = null)
        {
            MenuButton b = MenuButton.Create("Back", parent, "Retour", new Vector2(300f, 74f), 40f, true, TextAlignmentOptions.Center);
            b.slide = 0f;
            MenuUi.Place(b.transform as RectTransform, Vector2.zero, Vector2.zero, position ?? new Vector2(70f, 56f), new Vector2(300f, 74f));
            b.label.rectTransform.offsetMin = new Vector2(12f, 0f);
            return b;
        }

        // ------------------------------------------------------------------ Build Settings

        static void AddScenesToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == PlayModeStartScene.MenuScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(PlayModeStartScene.MenuScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
