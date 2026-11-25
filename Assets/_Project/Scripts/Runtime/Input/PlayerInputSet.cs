using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TaintlessGlade
{
    public enum BindSlot { Primary = 0, Secondary = 1, Gamepad = 2 }

    /// <summary>Une ligne du menu des contrôles : une action du jeu avec ses trois emplacements (clavier/souris x2, manette).</summary>
    public sealed class ControlRow
    {
        public string label;
        public InputAction action;
        /// <summary>Index de binding par emplacement (-1 = aucun).</summary>
        public readonly int[] bindingIndex = { -1, -1, -1 };
        /// <summary>Texte affiché pour un emplacement non modifiable (ex. « Stick G »).</summary>
        public string lockedGamepadLabel;
    }

    /// <summary>
    /// Toutes les actions du joueur, définies en code, avec trois emplacements par action
    /// (principal, secondaire, manette). Les remplacements du joueur sont sauvegardés dans les PlayerPrefs
    /// et appliqués à chaque création. Utilisé par PlayerControls (jeu) et par le menu des contrôles (réaffectation).
    /// </summary>
    public sealed class PlayerInputSet : IDisposable
    {
        const string PrefsKey = "controls.overrides.v1";

        public readonly InputAction Move, AimStick, Attack, Dash, Run, ColorToggle, Bullet;
        public readonly List<ControlRow> Rows = new List<ControlRow>();
        readonly List<InputAction> _all = new List<InputAction>();

        public PlayerInputSet()
        {
            Move = Make("Move", InputActionType.Value);
            AimStick = Make("AimStick", InputActionType.Value);
            Attack = Make("Attack", InputActionType.Button);
            Dash = Make("Dash", InputActionType.Button);
            Run = Make("Run", InputActionType.Button);
            ColorToggle = Make("SpitColor", InputActionType.Button);
            Bullet = Make("BulletStyle", InputActionType.Button);

            // Déplacement : deux composites (WASD / flèches) dont chaque direction est réaffectable, + stick gauche fixe.
            string[] dirNames = { "Haut", "Bas", "Gauche", "Droite" };
            string[] dirPart = { "Up", "Down", "Left", "Right" };
            string[] wasd = { "w", "s", "a", "d" };
            string[] arrows = { "upArrow", "downArrow", "leftArrow", "rightArrow" };
            int first = Move.bindings.Count;
            var c1 = Move.AddCompositeBinding("2DVector");
            for (int i = 0; i < 4; i++) c1 = c1.With(dirPart[i], "<Keyboard>/" + wasd[i]);
            int second = Move.bindings.Count;
            var c2 = Move.AddCompositeBinding("2DVector");
            for (int i = 0; i < 4; i++) c2 = c2.With(dirPart[i], "<Keyboard>/" + arrows[i]);
            Move.AddBinding("<Gamepad>/leftStick");
            for (int i = 0; i < 4; i++)
            {
                var row = new ControlRow { label = "Aller " + dirNames[i].ToLowerInvariant(), action = Move, lockedGamepadLabel = "Stick G" };
                row.bindingIndex[(int)BindSlot.Primary] = first + 1 + i;
                row.bindingIndex[(int)BindSlot.Secondary] = second + 1 + i;
                Rows.Add(row);
            }

            AimStick.AddBinding("<Gamepad>/rightStick");

            AddSimple("Attaquer (cracher)", Attack, "<Mouse>/leftButton", "", "<Gamepad>/rightTrigger");
            AddSimple("Esquive", Dash, "<Keyboard>/space", "<Mouse>/rightButton", "<Gamepad>/buttonSouth");
            AddSimple("Courir", Run, "<Keyboard>/leftShift", "", "<Gamepad>/leftStickPress");
            Run.AddBinding("<Gamepad>/leftShoulder"); // alternative manette, non affichée
            AddSimple("Couleur du crachat", ColorToggle, "<Keyboard>/c", "", "<Gamepad>/buttonNorth");
            AddSimple("Style de balle", Bullet, "<Keyboard>/v", "", "<Gamepad>/dpad/right");

            LoadSaved();
        }

        InputAction Make(string name, InputActionType type)
        {
            var action = new InputAction(name, type);
            _all.Add(action);
            return action;
        }

        void AddSimple(string label, InputAction action, string primary, string secondary, string gamepad)
        {
            var row = new ControlRow { label = label, action = action };
            row.bindingIndex[(int)BindSlot.Primary] = action.bindings.Count;
            action.AddBinding(primary);
            row.bindingIndex[(int)BindSlot.Secondary] = action.bindings.Count;
            action.AddBinding(secondary);
            row.bindingIndex[(int)BindSlot.Gamepad] = action.bindings.Count;
            action.AddBinding(gamepad);
            Rows.Add(row);
        }

        public void Enable() { foreach (InputAction a in _all) a.Enable(); }
        public void Disable() { foreach (InputAction a in _all) a.Disable(); }

        public void Dispose()
        {
            foreach (InputAction a in _all) a.Dispose();
            _all.Clear();
        }

        // ------------------------------------------------------------------ Lecture d'un emplacement

        public static string EffectivePath(ControlRow row, BindSlot slot)
        {
            int index = row.bindingIndex[(int)slot];
            return index < 0 ? "" : row.action.bindings[index].effectivePath ?? "";
        }

        public bool IsOverridden(ControlRow row, BindSlot slot)
        {
            int index = row.bindingIndex[(int)slot];
            return index >= 0 && row.action.bindings[index].overridePath != null;
        }

        public void SetPath(ControlRow row, BindSlot slot, string path)
        {
            int index = row.bindingIndex[(int)slot];
            if (index >= 0) row.action.ApplyBindingOverride(index, path);
        }

        // ------------------------------------------------------------------ Sauvegarde

        /// <summary>Enregistre les remplacements (format « Action:index=chemin; »).</summary>
        public void Save()
        {
            var sb = new StringBuilder();
            foreach (InputAction action in _all)
            {
                var bindings = action.bindings;
                for (int i = 0; i < bindings.Count; i++)
                    if (bindings[i].overridePath != null)
                        sb.Append(action.name).Append(':').Append(i).Append('=').Append(bindings[i].overridePath).Append(';');
            }
            PlayerPrefs.SetString(PrefsKey, sb.ToString());
            PlayerPrefs.Save();
        }

        public void LoadSaved()
        {
            foreach (InputAction a in _all) a.RemoveAllBindingOverrides();
            string data = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(data)) return;

            foreach (string entry in data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = entry.IndexOf(':');
                int equals = entry.IndexOf('=');
                if (colon <= 0 || equals < colon) continue;
                string actionName = entry.Substring(0, colon);
                if (!int.TryParse(entry.Substring(colon + 1, equals - colon - 1), out int index)) continue;
                string path = entry.Substring(equals + 1);

                InputAction action = _all.Find(a => a.name == actionName);
                if (action != null && index >= 0 && index < action.bindings.Count)
                    action.ApplyBindingOverride(index, path);
            }
        }

        /// <summary>Remet tous les contrôles par défaut (à enregistrer avec Save).</summary>
        public void ResetToDefaults()
        {
            foreach (InputAction a in _all) a.RemoveAllBindingOverrides();
        }

        /// <summary>Recharge les contrôles enregistrés (annule les changements non enregistrés).</summary>
        public void Revert() => LoadSaved();
    }
}
