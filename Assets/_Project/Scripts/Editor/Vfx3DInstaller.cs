using System.IO;
using UnityEditor;
using UnityEngine;

namespace TaintlessGlade.EditorTools
{
    /// <summary>
    /// Crée / complète Resources/Vfx3DLibrary.asset avec les prefabs de Free Quick Effects Vol.1
    /// et le sort du pack Full Opaque Spell qui sert de balle.
    /// Se lance tout seul au chargement de l'éditeur ; le menu permet de tout réassigner si besoin.
    /// </summary>
    [InitializeOnLoad]
    public static class Vfx3DInstaller
    {
        const string PrefabDir = "Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Prefabs/";
        const string SpellShotPath = "Assets/FullOpaqueSpell/Prefabs/VFX/Spell_SpellManager/VFX_Arcanic_TrackingShot_Main.prefab";
        const string AssetDir = "Assets/_Project/Resources";
        const string AssetPath = AssetDir + "/Vfx3DLibrary.asset";

        static Vfx3DInstaller()
        {
            EditorApplication.delayCall += () => { Install(false); InstallHovl(false); InstallBolt(false); };
        }

        [MenuItem("Taintless Glade/Réassigner la bibliothèque d'effets 3D")]
        static void Reinstall()
        {
            Install(true);
            InstallHovl(true);
            InstallBolt(true);
        }

        static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + name + ".prefab");

        static Material LoadMat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/Materials/" + name + ".mat");

        static GameObject[] LoadAll(params string[] names)
        {
            var list = new GameObject[names.Length];
            for (int i = 0; i < names.Length; i++) list[i] = Load(names[i]);
            return list;
        }

        const string HovlDir = "Assets/Hovl Studio/Magic effects pack/Prefabs/";
        // Étincelles qui entourent la bulle 2D du crachat (Plexus et Star aura testés : trop éloignés d'une balle).
        static readonly string[] HovlBullets = { "Sparks/Sparks flashing pink" };
        // Explosion + étoiles + éclats ; le dernier sert de dissipation en fin de portée.
        static readonly string[] HovlImpacts = { "Hits and explosions/Explosion", "Hits and explosions/Star hit", "Sparks/Sparks explode pink" };

        static bool Same(GameObject[] a, GameObject[] b)
        {
            if (a == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static GameObject[] LoadHovl(string[] paths)
        {
            var list = new GameObject[paths.Length];
            for (int i = 0; i < paths.Length; i++) list[i] = AssetDatabase.LoadAssetAtPath<GameObject>(HovlDir + paths[i] + ".prefab");
            return System.Array.TrueForAll(list, p => p != null) ? list : null;
        }

        /// <summary>Les matériaux du pack Hovl sont en shader built-in (roses en URP) : on les bascule sur le shader de particules du projet, qui garde texture, teinte HDR et blend.</summary>
        static void ConvertToUrp(GameObject[] prefabs)
        {
            Shader target = Shader.Find("TaintlessGlade/Simple Particle");
            if (target == null) return;
            int converted = 0;
            foreach (GameObject prefab in prefabs)
            {
                foreach (string dep in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(prefab), true))
                {
                    if (!dep.EndsWith(".mat")) continue;
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(dep);
                    if (mat == null || mat.shader.name.StartsWith("Universal Render Pipeline")) continue;
                    bool changed = false;
                    if (mat.shader != target)
                    {
                        mat.shader = target;
                        mat.renderQueue = 3000;
                        changed = true;
                    }
                    // Les textures du pack sans canal alpha (fond noir, forme dans la luminosité) sont faites pour le blend additif.
                    Texture tex = mat.GetTexture("_MainTex");
                    var importer = tex != null ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter : null;
                    if (importer != null && !importer.DoesSourceTextureHaveAlpha() && mat.GetFloat("_DstBlend") != (float)UnityEngine.Rendering.BlendMode.One)
                    {
                        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                        changed = true;
                    }
                    if (!changed) continue;
                    EditorUtility.SetDirty(mat);
                    converted++;
                }
            }
            if (converted > 0) Debug.Log("[Taintless Glade] " + converted + " matériau(x) du pack Hovl ajustés pour URP (shader Simple Particle, additif si texture sans alpha).");
        }

        static void InstallHovl(bool force)
        {
            var lib = AssetDatabase.LoadAssetAtPath<Vfx3DLibrary>(AssetPath);
            if (lib == null) return;
            GameObject[] bullets = LoadHovl(HovlBullets), impacts = LoadHovl(HovlImpacts);
            if (bullets == null || impacts == null) return; // pack Hovl absent.
            ConvertToUrp(bullets);
            ConvertToUrp(impacts);
            AssetDatabase.SaveAssets();
            if (!force && Same(lib.hovlBullets, bullets) && Same(lib.hovlImpacts, impacts)) return;
            lib.hovlBullets = bullets;
            lib.hovlImpacts = impacts;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Taintless Glade] Balles et impacts du pack Hovl assignés dans la bibliothèque d'effets 3D.");
        }

        static GameObject LoadHovlOne(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(HovlDir + path + ".prefab");

        static GameObject[] Clean(params GameObject[] list) => System.Array.FindAll(list, p => p != null);

        /// <summary>Balle du crachat magique (traînée Free Quick Effects + étincelles / explosions Hovl) et effets du dash téléportation.</summary>
        static void InstallBolt(bool force)
        {
            var lib = AssetDatabase.LoadAssetAtPath<Vfx3DLibrary>(AssetPath);
            if (lib == null) return;
            GameObject core = Load("vfx_Projectile_02");
            GameObject sparks = LoadHovlOne("Sparks/Sparks flashing blue");
            if (core == null || sparks == null) return; // packs absents.

            // Les matériaux de Free Quick Effects pointent vers un shader built-in qui ne s'affiche pas en URP : balle, muzzle et impacts restaient invisibles.
            ConvertToUrp(Clean(core, Load("vfx_Projectile_01"), Load("vfx_Impact_01"), Load("vfx_MuzzleFlash_01"), lib.muzzle, lib.impact));
            AssetDatabase.SaveAssets();

            GameObject starAura = LoadHovlOne("Character auras/Star aura");
            GameObject buff = LoadHovlOne("Character auras/Buff");
            GameObject redExplosion = LoadHovlOne("AoE effects/Red energy explosion");
            GameObject sparksBlue = LoadHovlOne("Sparks/Sparks blue");
            GameObject explosion2 = Load("vfx_Explosion_02");
            GameObject orb = LoadHovlOne("Environment/Teleport");
            ConvertToUrp(Clean(starAura, buff, redExplosion, sparksBlue, explosion2, orb));
            const string bubblePath = "Assets/_Project/Art/Textures/Weapons/Cracha Arc en ciel Shattered/Cracha Arc en ciel bulle 1.png";
            AxolotlSpriteInstaller.ConfigureSprite(bubblePath, 1000f);
            var bubble = AssetDatabase.LoadAssetAtPath<Sprite>(bubblePath);
            const string ringPath = "Assets/_Project/Art/Textures/Weapons/Cracha Arc en ciel Shattered/Cracha Arc en ciel Fond.png";
            AxolotlSpriteInstaller.ConfigureSprite(ringPath, 1000f);
            var ring = AssetDatabase.LoadAssetAtPath<Sprite>(ringPath);
            AssetDatabase.SaveAssets();

            if (!force && lib.boltCore != null && lib.boltExplosion != null && lib.boltExplosion.Length > 0 && lib.dashTrail != null && lib.dashTrail.Length > 0
                && lib.boltOrb != null && (bubble == null || lib.boltBubble == bubble) && (ring == null || lib.boltRing == ring) && lib.chargeAura != null && lib.chargeAura.Length == 1 && lib.boltChargedExplosion != null && lib.boltChargedExplosion.Length > 0) return;

            GameObject[] launch = Clean(LoadHovlOne("Sparks/Sparks explode blue"));
            GameObject[] bounce = Clean(LoadHovlOne("Sparks/Sparks explode blue"), LoadHovlOne("Hits and explosions/Star hit"));
            GameObject[] explosion = Clean(LoadHovlOne("Hits and explosions/Explosion"), LoadHovlOne("Hits and explosions/Star hit"), LoadHovlOne("Sparks/Sparks explode blue"));
            GameObject[] dashStart = Clean(LoadHovlOne("Sparks/Sparks explode blue"), LoadHovlOne("Smoke effects/Dust puff"));
            GameObject[] dashTrail = Clean(core, sparks);
            GameObject[] dashEnd = Clean(LoadHovlOne("Sparks/Sparks explode blue"), LoadHovlOne("Hits and explosions/Star hit"));

            ConvertToUrp(Clean(sparks, LoadHovlOne("Sparks/Sparks explode blue"), LoadHovlOne("Hits and explosions/Star hit"), LoadHovlOne("Hits and explosions/Explosion"), LoadHovlOne("Smoke effects/Dust puff")));
            AssetDatabase.SaveAssets();

            lib.boltCore = core;
            lib.boltSparks = sparks;
            lib.boltLaunch = launch;
            lib.boltBounce = bounce;
            lib.boltExplosion = explosion;
            lib.boltFlash = Load("vfx_Impact_01");
            lib.boltSparksExtra = sparksBlue;
            lib.boltAura = starAura;
            lib.boltOrb = orb;
            lib.boltBubble = bubble;
            lib.boltRing = ring;
            lib.chargeAura = Clean(starAura);
            lib.boltChargedExplosion = Clean(redExplosion, explosion2);
            lib.dashStart = dashStart;
            lib.dashTrail = dashTrail;
            lib.dashEnd = dashEnd;
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Taintless Glade] Crachat magique et dash téléportation assignés dans la bibliothèque d'effets 3D.");
        }

        static void Install(bool force)
        {
            if (Load("vfx_Projectile_01") == null) return; // pack absent : le jeu garde ses effets 2D.

            var lib = AssetDatabase.LoadAssetAtPath<Vfx3DLibrary>(AssetPath);
            bool created = lib == null;
            if (created)
            {
                Directory.CreateDirectory(AssetDir);
                lib = ScriptableObject.CreateInstance<Vfx3DLibrary>();
                AssetDatabase.CreateAsset(lib, AssetPath);
            }
            else if (!force && lib.spellShot != null && lib.muzzle != null && lib.impact != null && lib.sweat != null) return;

            lib.spellShot = AssetDatabase.LoadAssetAtPath<GameObject>(SpellShotPath);
            lib.muzzle = Load("vfx_MuzzleFlash_01");
            lib.impact = Load("vfx_Impact_01");

            lib.sweat = Load("vfx_Rain_01");
            lib.sweatDropMaterial = LoadMat("Circle01_AB");

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Taintless Glade] Bibliothèque d'effets 3D " + (created ? "créée" : "mise à jour") + " : " + AssetPath);
        }
    }
}
