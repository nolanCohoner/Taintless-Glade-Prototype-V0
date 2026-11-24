using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TaintlessGlade.EditorTools
{
    /// <summary>
    /// Monte l'arme crachat de l'Axolotl sur le pivot "Bow" du rig : un halo arc-en-ciel + 3 boules de crachat
    /// (dossier Art/Textures/Weapons/Cracha Arc en ciel Shattered), animées par SpitOrbs.
    /// Les 4 dessins font 800x800 et partagent le même canevas : chaque calque est décalé pour que le pivot
    /// du GameObject tombe au centre de la boule (elle tourne et gonfle autour d'elle-même).
    /// </summary>
    public static class SpitWeaponInstaller
    {
        const string Dir = "Assets/_Project/Art/Textures/Weapons/Cracha Arc en ciel Shattered/";
        const float CanvasW = 800f, CanvasH = 800f;
        const float PixelsPerUnit = 1000f;   // cluster entier ≈ 0,8 u de large

        const string HaloTexture = "Cracha Arc en ciel Fond";
        static readonly Vector2 HaloCenter = new Vector2(423f, 400f);   // centre de l'anneau (px, origine en haut à gauche)

        struct OrbSpec
        {
            public string texture;
            public Vector2 center;       // centre de la boule dans le dessin (px)
            public float frequency, phase, amplitude, inertia, shotDelay;
        }

        // Rythme, amplitude et retard différents pour chaque boule : elles ne flottent jamais à l'unisson.
        static readonly OrbSpec[] Orbs =
        {
            new OrbSpec { texture = "Cracha Arc en ciel bulle 1", center = new Vector2(433f, 329f), frequency = 0.55f, phase = 0f,   amplitude = 0.030f, inertia = 0.7f, shotDelay = 0f },
            new OrbSpec { texture = "Cracha Arc en ciel bulle 2", center = new Vector2(230f, 600f), frequency = 0.78f, phase = 2.1f, amplitude = 0.040f, inertia = 1.0f, shotDelay = 0.2f },
            new OrbSpec { texture = "Cracha Arc en ciel bulle 3", center = new Vector2(675f, 674f), frequency = 0.95f, phase = 4.2f, amplitude = 0.045f, inertia = 1.2f, shotDelay = 0.4f },
        };

        [MenuItem("Tools/Taintless Glade/Appliquer le crachat à l'arme de l'Axolotl")]
        public static void ApplyToScene()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            CharacterRig rig = player != null ? player.GetComponentInChildren<CharacterRig>(true) : null;
            if (rig == null)
            {
                EditorUtility.DisplayDialog("Crachat", "Aucun joueur avec un CharacterRig dans la scène ouverte (ouvre TestRoom).", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(rig.gameObject, "Appliquer le crachat");
            if (rig.bow == null)
            {
                var bow = new GameObject("Bow").transform;
                Undo.RegisterCreatedObjectUndo(bow.gameObject, "Crachat");
                bow.SetParent(rig.transform, false);
                rig.bow = bow;
                rig.bowReach = 0.42f;
            }

            Build(rig.bow);
            EditorUtility.SetDirty(rig);
            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            Debug.Log("[Crachat] Arme appliquée. Pense à sauvegarder la scène (Ctrl+S).");
        }

        /// <summary>Remplace le contenu du pivot "Bow" par le halo et les boules, et branche SpitOrbs dessus.</summary>
        public static void Build(Transform bow)
        {
            ConfigureImports();
            Material material = AxolotlSpriteInstaller.GetOverlayMaterial();

            for (int i = bow.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(bow.GetChild(i).gameObject);

            var art = new GameObject("SpitArt").transform;
            Undo.RegisterCreatedObjectUndo(art.gameObject, "Crachat");
            art.SetParent(bow, false);

            // Anneau derrière, puis les boules de la plus grande à la plus petite.
            Transform halo = AddPiece("Halo", art, HaloTexture, HaloCenter, 5, material, castShadow: false);

            var orbs = new List<SpitOrbs.Orb>();
            for (int i = 0; i < Orbs.Length; i++)
            {
                OrbSpec s = Orbs[i];
                Transform pivot = AddPiece("Orb" + (i + 1), art, s.texture, s.center, 6 + i, material, castShadow: true);
                orbs.Add(new SpitOrbs.Orb
                {
                    pivot = pivot,
                    home = pivot.localPosition,
                    frequency = s.frequency,
                    phase = s.phase,
                    amplitude = s.amplitude,
                    inertia = s.inertia,
                    shotDelay = s.shotDelay,
                });
            }

            var spit = bow.GetComponent<SpitOrbs>();
            if (spit == null) spit = Undo.AddComponent<SpitOrbs>(bow.gameObject);
            spit.animator = bow.GetComponentInParent<CharacterAnimator>();
            spit.art = art;
            spit.halo = halo;
            spit.haloHome = halo.localPosition;
            spit.orbs = orbs.ToArray();
            EditorUtility.SetDirty(spit);
        }

        /// <summary>Pivot posé au centre du dessin ; le sprite (centré sur le canevas) est décalé pour rester à sa place d'origine.</summary>
        static Transform AddPiece(string name, Transform parent, string texture, Vector2 centerPx, int order, Material material, bool castShadow)
        {
            var pivot = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(pivot.gameObject, "Crachat");
            pivot.SetParent(parent, false);
            Vector3 local = ToLocal(centerPx);
            pivot.localPosition = local;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + texture + ".png");
            if (sprite == null) Debug.LogWarning("[Crachat] Sprite introuvable : " + texture);

            var art = new GameObject(name + "_Art");
            Undo.RegisterCreatedObjectUndo(art, "Crachat");
            art.transform.SetParent(pivot, false);
            art.transform.localPosition = -local;

            var sr = art.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingOrder = order;
            sr.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return pivot;
        }

        /// <summary>Position (unités) d'un point du dessin par rapport au centre du canevas.</summary>
        static Vector3 ToLocal(Vector2 px) => new Vector3((px.x - CanvasW * 0.5f) / PixelsPerUnit, (CanvasH * 0.5f - px.y) / PixelsPerUnit, 0f);

        static void ConfigureImports()
        {
            AxolotlSpriteInstaller.ConfigureSprite(Dir + HaloTexture + ".png", PixelsPerUnit);
            foreach (OrbSpec s in Orbs)
                AxolotlSpriteInstaller.ConfigureSprite(Dir + s.texture + ".png", PixelsPerUnit);
        }
    }
}
