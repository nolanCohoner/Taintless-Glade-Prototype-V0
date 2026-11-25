using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TaintlessGlade
{
    /// <summary>
    /// Icônes d'input (pack Kenney « Input Prompts Pixel 1-bit ») : boutons de souris et de manette.
    /// Les touches de clavier sont dessinées par le menu (cadre + texte Fredoka). Asset : Resources/InputGlyphLibrary.
    /// </summary>
    [CreateAssetMenu(menuName = "Taintless Glade/Input Glyph Library", fileName = "InputGlyphLibrary")]
    public class InputGlyphLibrary : ScriptableObject
    {
        static InputGlyphLibrary _instance;
        public static InputGlyphLibrary Instance => _instance != null ? _instance : (_instance = Resources.Load<InputGlyphLibrary>("InputGlyphLibrary"));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        public Sprite mouse, mouseLeft, mouseRight, mouseMiddle;
        public Sprite padSouth, padEast, padWest, padNorth;
        public Sprite padDpad;
    }

    /// <summary>Un emplacement de contrôle affiché : une icône (si elle existe) et/ou un texte pour une touche.</summary>
    public struct Glyph
    {
        public Sprite icon;
        public string label;
        public bool empty;
    }

    public static class InputGlyphs
    {
        static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "space", "Espace" }, { "leftShift", "Maj G" }, { "rightShift", "Maj D" },
            { "leftCtrl", "Ctrl G" }, { "rightCtrl", "Ctrl D" }, { "leftAlt", "Alt G" }, { "rightAlt", "Alt D" },
            { "tab", "Tab" }, { "enter", "Entrée" }, { "escape", "Échap" }, { "backspace", "Retour" },
            { "upArrow", "Haut" }, { "downArrow", "Bas" }, { "leftArrow", "Gauche" }, { "rightArrow", "Droite" },
            { "backquote", "²" },
        };

        static readonly Dictionary<string, string> PadLabels = new Dictionary<string, string>
        {
            { "rightTrigger", "RT" }, { "leftTrigger", "LT" }, { "rightShoulder", "RB" }, { "leftShoulder", "LB" },
            { "leftStick", "Stick G" }, { "rightStick", "Stick D" }, { "leftStickPress", "L3" }, { "rightStickPress", "R3" },
            { "start", "Start" }, { "select", "Select" },
            { "dpad/up", "Croix haut" }, { "dpad/down", "Croix bas" }, { "dpad/left", "Croix gauche" }, { "dpad/right", "Croix droite" },
        };

        public static Glyph Describe(string path)
        {
            if (string.IsNullOrEmpty(path)) return new Glyph { empty = true, label = "—" };
            InputGlyphLibrary lib = InputGlyphLibrary.Instance;

            if (path.StartsWith("<Mouse>/"))
            {
                string key = path.Substring("<Mouse>/".Length);
                switch (key)
                {
                    case "leftButton": return new Glyph { icon = lib != null ? lib.mouseLeft : null, label = "Clic G" };
                    case "rightButton": return new Glyph { icon = lib != null ? lib.mouseRight : null, label = "Clic D" };
                    case "middleButton": return new Glyph { icon = lib != null ? lib.mouseMiddle : null, label = "Molette" };
                    default: return new Glyph { icon = lib != null ? lib.mouse : null, label = "Souris" };
                }
            }

            if (path.StartsWith("<Gamepad>/"))
            {
                string key = path.Substring("<Gamepad>/".Length);
                switch (key)
                {
                    case "buttonSouth": return new Glyph { icon = lib != null ? lib.padSouth : null, label = "A" };
                    case "buttonEast": return new Glyph { icon = lib != null ? lib.padEast : null, label = "B" };
                    case "buttonWest": return new Glyph { icon = lib != null ? lib.padWest : null, label = "X" };
                    case "buttonNorth": return new Glyph { icon = lib != null ? lib.padNorth : null, label = "Y" };
                }
                if (PadLabels.TryGetValue(key, out string pad)) return new Glyph { label = pad };
                return new Glyph { label = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice) };
            }

            if (path.StartsWith("<Keyboard>/"))
            {
                string key = path.Substring("<Keyboard>/".Length);
                if (Labels.TryGetValue(key, out string label)) return new Glyph { label = label };
                string readable = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
                return new Glyph { label = readable.Length <= 2 ? readable.ToUpperInvariant() : readable };
            }

            return new Glyph { label = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice) };
        }
    }
}
