using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TaintlessGlade.EditorTools
{
    /// <summary>
    /// Remplace les carrés placeholder du rig du joueur par les calques de l'Axolotl (Art/Textures/Asxolot).
    /// Modifie la scène ouverte sur place (pivots, références du CharacterRig et animateur conservés) :
    /// pas besoin de régénérer la salle de test.
    /// Tous les calques font 1400x1000 et partagent le même canevas : on les empile tels quels,
    /// chacun décalé pour que le pivot du GameObject tombe sur l'articulation voulue.
    /// </summary>
    public static class AxolotlSpriteInstaller
    {
        const string Dir = "Assets/_Project/Art/Textures/Asxolot/";
        const string SpriteMaterialPath = "Assets/_Project/Art/Materials/M_Sprite.mat";

        // Canevas source (pixels, origine en haut à gauche).
        const float CanvasW = 1400f, CanvasH = 1000f;
        const float PixelsPerUnit = 700f;       // corps ≈ 0,93 u de large, perso ≈ 1,15 u de haut
        const float OriginX = 640f;             // centre du corps -> x local 0
        const float GroundY = 968f;             // bas des pattes -> y local 0

        static readonly string[] HeadTentacles = { "Red", "Orange", "Yellow", "Green", "Blue", "Purple" };
        static readonly string[] TailTentacles = { "Red", "Orange", "Yellow", "Green", "Blue", "Purple" };

        // Racines des tentacules (là où elles touchent la tête / la queue), même ordre que les tableaux de couleurs.
        static readonly Vector2[] HeadTentacleRoots =
        {
            new Vector2(540f, 330f),   // Red
            new Vector2(500f, 470f),   // Orange
            new Vector2(540f, 610f),   // Yellow
            new Vector2(1010f, 330f),  // Green
            new Vector2(1060f, 470f),  // Blue
            new Vector2(1020f, 590f),  // Purple
        };

        static readonly Vector2[] TailTentacleRoots =
        {
            new Vector2(340f, 705f),   // Red
            new Vector2(240f, 665f),   // Orange
            new Vector2(185f, 610f),   // Yellow
            new Vector2(205f, 545f),   // Green
            new Vector2(265f, 610f),   // Blue
            new Vector2(340f, 650f),   // Purple
        };

        // Variété fixe (pas de hasard : le résultat est le même à chaque application) : aucune tentacule ne bat au même rythme.
        static readonly float[] FrequencyVariety = { 1f, 0.82f, 1.19f, 0.91f, 1.24f, 0.77f, 1.1f, 0.88f, 1.21f, 0.8f, 1.05f, 0.95f };
        static readonly float[] ResponsivenessVariety = { 1f, 0.75f, 1.2f, 0.9f, 0.65f, 1.1f, 0.85f, 1.25f, 0.7f, 1f, 0.8f, 1.15f };

        struct EyeSpec
        {
            public string side, ballTexture, irisTexture;
            public Vector2 ballCenter, irisCenter;
        }

        static readonly EyeSpec[] Eyes =
        {
            new EyeSpec { side = "Left", ballTexture = "eyeBallLeft", irisTexture = "eyeIrisLeft", ballCenter = new Vector2(1004f, 484f), irisCenter = new Vector2(1028f, 484f) },
            new EyeSpec { side = "Right", ballTexture = "eyeBallRight", irisTexture = "eyeIrisRight", ballCenter = new Vector2(586f, 488f), irisCenter = new Vector2(572f, 490f) },
        };

        // Arc arc-en-ciel (dessin 400x1400 : corde à gauche, ventre à droite = côté de la cible). 560 px/unité -> ~2 unités de haut,
        // nettement plus grand que le perso (~1,15 u) pour rester bien lisible dans un rogue-like.
        const string BowTexturePath = "Assets/_Project/Art/Textures/Weapons/arc Arc en ciel.png";
        const float BowPixelsPerUnit = 560f;
        static readonly Vector2 BowCanvas = new Vector2(400f, 1400f);
        static readonly Vector2 BowGrip = new Vector2(265f, 730f); // poignée (px, origine en haut à gauche)

        // Carré rouge de secours si la texture de l'arc manque : largeur x hauteur en unités.
        static readonly Vector2 BowSize = new Vector2(0.1f, 0.62f);

        static readonly Vector2 BodyJoint = new Vector2(640f, 940f);
        static readonly Vector2 HeadJoint = new Vector2(790f, 650f);
        static readonly Vector2 TailJoint = new Vector2(365f, 700f);

        // paw.png est dessinée à la place de la patte avant proche : son articulation (haut de la patte) est ici.
        // Les pattes sont remontées de ce nombre de pixels (canevas) par rapport au dessin d'origine.
        const float PawRaise = 26f;
        static readonly Vector2 PawDrawnJoint = new Vector2(1044f, 790f);
        static readonly Color FarTint = new Color(0.82f, 0.9f, 0.95f, 1f);

        [MenuItem("Tools/Taintless Glade/Appliquer les sprites de l'Axolotl au joueur")]
        public static void ApplyToScene()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            CharacterRig rig = player != null ? player.GetComponentInChildren<CharacterRig>(true) : null;
            if (rig == null)
            {
                EditorUtility.DisplayDialog("Axolotl", "Aucun joueur avec un CharacterRig dans la scène ouverte (ouvre TestRoom).", "OK");
                return;
            }

            Apply(rig);
            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            Debug.Log("[Axolotl] Sprites appliqués. Pense à sauvegarder la scène (Ctrl+S).");
        }

        public static void Apply(CharacterRig rig)
        {
            ConfigureImports();

            Material material = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            Undo.RegisterFullObjectHierarchyUndo(rig.gameObject, "Appliquer sprites Axolotl");

            // --- Corps ---
            Place(rig.body, BodyJoint);
            Clear(rig.body);
            AddLayer("Body_Art", rig.body, "Body", BodyJoint, -3, Color.white, material);

            // --- Tête : tentacules (derrière), crâne, yeux ---
            Place(rig.head, HeadJoint);
            Clear(rig.head);
            var tentacles = new List<CharacterRig.Tentacle>();
            for (int i = 0; i < HeadTentacles.Length; i++)
                tentacles.Add(AddTentacle("HeadTentacle" + HeadTentacles[i], rig.head, HeadJoint, HeadTentacleRoots[i], -2, i * 0.9f, i, material));
            AddLayer("Head_Art", rig.head, "Head", HeadJoint, 2, Color.white, material);

            // Yeux : la boule d'oeil écrit le stencil, l'iris (qui suit la souris) ne se voit que dans la boule.
            Material ballMaterial = GetEyeMaterial("M_EyeBall", stencilComp: 8, stencilPass: 2, alphaClip: 1f);
            Material irisMaterial = GetEyeMaterial("M_EyeIris", stencilComp: 3, stencilPass: 0, alphaClip: 0f);
            var eyes = new List<CharacterRig.Eye>();
            foreach (EyeSpec e in Eyes)
            {
                AddLayer("EyeBall_" + e.side, rig.head, e.ballTexture, HeadJoint, 3, Color.white, ballMaterial);
                var pivot = new GameObject("EyeIris_" + e.side).transform;
                Undo.RegisterCreatedObjectUndo(pivot.gameObject, "Axolotl eye");
                pivot.SetParent(rig.head, false);
                Vector3 center = ToLocal(e.ballCenter) - ToLocal(HeadJoint);
                pivot.localPosition = center;
                AddLayer("EyeIris_Art", pivot, e.irisTexture, e.irisCenter, 4, Color.white, irisMaterial);
                eyes.Add(new CharacterRig.Eye { iris = pivot, center = center });
            }
            rig.eyes = eyes.ToArray();

            // --- Queue : tentacules (derrière) + nageoire ---
            Place(rig.tail, TailJoint);
            Clear(rig.tail);
            for (int i = 0; i < TailTentacles.Length; i++)
                tentacles.Add(AddTentacle("TailTentacle" + TailTentacles[i], rig.tail, TailJoint, TailTentacleRoots[i], -5, i * 0.9f + 2f, HeadTentacles.Length + i, material));
            AddLayer("Tail_Art", rig.tail, "Tail", TailJoint, -1, Color.white, material);
            rig.tentacles = tentacles.ToArray();

            // --- Pattes : paw.png dupliqué 4 fois (le pivot = le haut de la patte) ---
            foreach (CharacterRig.Leg leg in rig.legs)
            {
                if (leg == null || leg.pivot == null) continue;
                float hipX = leg.front ? (leg.far ? 894f : 1044f) : (leg.far ? 434f : 584f);
                float hipY = (leg.far ? 780f : 790f) - PawRaise;
                var joint = new Vector2(hipX, hipY);
                // paw.png est dessinée à x = 1044 : on la décale pour la ramener à hipX.
                Place(leg.pivot, joint);
                Clear(leg.pivot);
                AddLayer(leg.pivot.name + "_Art", leg.pivot, "paw", PawDrawnJoint, leg.far ? 0 : 1, leg.far ? FarTint : Color.white, material);
            }

            // --- Arme : boules de crachat (halo + 3 boules animées par SpitOrbs), tenues par la patte avant proche ---
            Transform oldBow = rig.transform.Find("Bow");
            if (oldBow != null) Undo.DestroyObjectImmediate(oldBow.gameObject);
            var bow = new GameObject("Bow").transform;
            Undo.RegisterCreatedObjectUndo(bow.gameObject, "Axolotl bow");
            bow.SetParent(rig.transform, false);
            SpitWeaponInstaller.Build(bow);
            rig.bow = bow;
            rig.irisTravel = 30f / PixelsPerUnit;
            rig.bowReach = 0.42f;

            // --- Gouttes de sueur ---
            var sweat = rig.GetComponent<SweatEmitter>();
            if (sweat == null) sweat = Undo.AddComponent<SweatEmitter>(rig.gameObject);
            sweat.material = material;

            rig.CaptureRestPose();
            EditorUtility.SetDirty(rig);
        }

        /// <summary>Pivot de tentacule posé sur sa racine (le parent bouge avec la tête / la queue), le calque est dans un enfant.</summary>
        static CharacterRig.Tentacle AddTentacle(string name, Transform parent, Vector2 parentJoint, Vector2 root, int order, float phase, int index, Material material)
        {
            var pivot = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(pivot.gameObject, "Axolotl tentacle");
            pivot.SetParent(parent, false);
            pivot.localPosition = ToLocal(root) - ToLocal(parentJoint);
            AddLayer(name + "_Art", pivot, name, root, order, Color.white, material);
            // Pointe vers la droite si la racine est du côté droit du point d'attache.
            return new CharacterRig.Tentacle
            {
                pivot = pivot, direction = root.x >= parentJoint.x ? 1f : -1f, phase = phase, amplitude = 1f,
                frequency = FrequencyVariety[index % FrequencyVariety.Length], responsiveness = ResponsivenessVariety[index % ResponsivenessVariety.Length]
            };
        }

        /// <summary>Rectangle rouge debout, centré sur le pivot "Bow" (pivot du sprite carré = bas-centre).</summary>
        static void AddBowSquare(Transform bow, Material material)
        {
            var go = new GameObject("Bow_Art");
            Undo.RegisterCreatedObjectUndo(go, "Axolotl bow");
            go.transform.SetParent(bow, false);
            var sr = go.AddComponent<SpriteRenderer>();

            var bowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BowTexturePath);
            if (bowSprite != null)
            {
                // Le pivot "Bow" est la poignée : on décale le dessin (pivot au centre du canevas) pour la poser dessus.
                sr.sprite = bowSprite;
                go.transform.localPosition = new Vector3((BowCanvas.x * 0.5f - BowGrip.x) / BowPixelsPerUnit, (BowGrip.y - BowCanvas.y * 0.5f) / BowPixelsPerUnit, 0f);
            }
            else
            {
                // Texture absente : carré rouge générique.
                Debug.LogWarning("[Axolotl] Texture de l'arc introuvable : " + BowTexturePath);
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Textures/T_PlaceholderSquare.png");
                sr.color = new Color(0.9f, 0.1f, 0.1f, 1f);
                go.transform.localPosition = new Vector3(0f, -BowSize.y * 0.5f, 0f);
                go.transform.localScale = new Vector3(BowSize.x, BowSize.y, 1f);
            }
            if (material != null) sr.sharedMaterial = material;
            sr.sortingOrder = 5;
            sr.shadowCastingMode = ShadowCastingMode.On;
            sr.receiveShadows = false;
        }

        /// <summary>Matériau sprite sans test de profondeur : l'arc (2 u de haut) ne disparaît plus dans le sol quand il descend sous les pieds.</summary>
        internal static Material GetOverlayMaterial()
        {
            const string path = "Assets/_Project/Art/Materials/M_SpriteOverlay.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("TaintlessGlade/Sprite Character"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_ZTest", 8f); // Always
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material GetEyeMaterial(string name, float stencilComp, float stencilPass, float alphaClip)
        {
            string path = "Assets/_Project/Art/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("TaintlessGlade/Sprite Character");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            // Comp : 8 = Always, 3 = Equal. Pass : 0 = Keep, 2 = Replace.
            material.SetFloat("_StencilRef", 1f);
            material.SetFloat("_StencilComp", stencilComp);
            material.SetFloat("_StencilPass", stencilPass);
            material.SetFloat("_AlphaClip", alphaClip);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Place(Transform pivot, Vector2 joint)
        {
            pivot.localPosition = ToLocal(joint);
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;
        }

        static void Clear(Transform pivot)
        {
            for (int i = pivot.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(pivot.GetChild(i).gameObject);
        }

        static Vector3 ToLocal(Vector2 px) => new Vector3((px.x - OriginX) / PixelsPerUnit, (GroundY - px.y) / PixelsPerUnit, 0f);

        /// <summary>Décalage du calque (centré sur le canevas) pour que l'articulation tombe sur le pivot parent.</summary>
        static Vector3 ArtOffset(Vector2 joint) =>
            new Vector3((CanvasW * 0.5f - joint.x) / PixelsPerUnit, (joint.y - CanvasH * 0.5f) / PixelsPerUnit, 0f);

        static Transform AddLayer(string name, Transform parent, string texture, Vector2 joint, int order, Color color, Material material)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + texture + ".png");
            if (sprite == null) Debug.LogWarning("[Axolotl] Sprite introuvable : " + texture);

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Axolotl layer");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = ArtOffset(joint);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingOrder = order;
            sr.shadowCastingMode = ShadowCastingMode.On;
            sr.receiveShadows = false;
            return go.transform;
        }

        static void ConfigureImports()
        {
            var names = new List<string> { "Body", "Head", "Tail", "paw", "eyeBallLeft", "eyeBallRight", "eyeIrisLeft", "eyeIrisRight" };
            foreach (string c in HeadTentacles) names.Add("HeadTentacle" + c);
            foreach (string c in TailTentacles) names.Add("TailTentacle" + c);

            foreach (string n in names)
                ConfigureSprite(Dir + n + ".png", PixelsPerUnit);

            ConfigureSprite(BowTexturePath, BowPixelsPerUnit);
        }

        internal static void ConfigureSprite(string path, float pixelsPerUnit)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("[Axolotl] Texture introuvable : " + path); return; }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool ok = importer.textureType == TextureImporterType.Sprite
                      && importer.spriteImportMode == SpriteImportMode.Single
                      && Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit)
                      && settings.spriteAlignment == (int)SpriteAlignment.Center;
            if (ok) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
