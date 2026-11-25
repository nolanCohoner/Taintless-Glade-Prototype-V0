using System;
using System.Collections.Generic;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Réglages du joueur (volumes, affichage, confort de jeu), sauvegardés dans les PlayerPrefs.
    /// Les valeurs se modifient en direct ; <see cref="Save"/> les enregistre, <see cref="Revert"/> recharge celles enregistrées.
    /// </summary>
    public static class GameSettings
    {
        public enum WindowMode { Fenetre = 0, PleinEcranFenetre = 1, PleinEcran = 2 }

        const string Prefix = "settings.";

        // --- Audio (0..1). Le volume général pilote AudioListener ; musique / effets / interface servent aux futurs sons. ---
        public static float MasterVolume = 1f;
        public static float MusicVolume = 0.8f;
        public static float SfxVolume = 1f;
        public static float UiVolume = 1f;

        // --- Affichage ---
        public static int ResolutionWidth;
        public static int ResolutionHeight;
        public static WindowMode Mode = WindowMode.PleinEcranFenetre;
        public static bool VSync = true;
        public static int QualityLevel;

        // --- Jeu ---
        /// <summary>Multiplicateur des secousses de caméra (0 = aucune).</summary>
        public static float ScreenShake = 1f;
        public static bool DamageNumbers = true;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Changed = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Load();
            Apply();
        }

        public static void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat(Prefix + "master", 1f);
            MusicVolume = PlayerPrefs.GetFloat(Prefix + "music", 0.8f);
            SfxVolume = PlayerPrefs.GetFloat(Prefix + "sfx", 1f);
            UiVolume = PlayerPrefs.GetFloat(Prefix + "ui", 1f);
            Resolution current = Screen.currentResolution;
            ResolutionWidth = PlayerPrefs.GetInt(Prefix + "resW", current.width);
            ResolutionHeight = PlayerPrefs.GetInt(Prefix + "resH", current.height);
            Mode = (WindowMode)PlayerPrefs.GetInt(Prefix + "mode", (int)WindowMode.PleinEcranFenetre);
            VSync = PlayerPrefs.GetInt(Prefix + "vsync", 1) == 1;
            QualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            ScreenShake = PlayerPrefs.GetFloat(Prefix + "shake", 1f);
            DamageNumbers = PlayerPrefs.GetInt(Prefix + "dmg", 1) == 1;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "master", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "music", MusicVolume);
            PlayerPrefs.SetFloat(Prefix + "sfx", SfxVolume);
            PlayerPrefs.SetFloat(Prefix + "ui", UiVolume);
            PlayerPrefs.SetInt(Prefix + "resW", ResolutionWidth);
            PlayerPrefs.SetInt(Prefix + "resH", ResolutionHeight);
            PlayerPrefs.SetInt(Prefix + "mode", (int)Mode);
            PlayerPrefs.SetInt(Prefix + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "quality", QualityLevel);
            PlayerPrefs.SetFloat(Prefix + "shake", ScreenShake);
            PlayerPrefs.SetInt(Prefix + "dmg", DamageNumbers ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Annule les changements non enregistrés.</summary>
        public static void Revert()
        {
            Load();
            Apply();
        }

        public static void ResetAudio()
        {
            MasterVolume = 1f; MusicVolume = 0.8f; SfxVolume = 1f; UiVolume = 1f;
            Apply();
        }

        public static void ResetDisplay()
        {
            Resolution native = Screen.currentResolution;
            ResolutionWidth = native.width;
            ResolutionHeight = native.height;
            Mode = WindowMode.PleinEcranFenetre;
            VSync = true;
            QualityLevel = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, QualitySettings.names.Length - 1);
            Apply();
        }

        public static void ResetGameplay()
        {
            ScreenShake = 1f;
            DamageNumbers = true;
            Apply();
        }

        public static void Apply()
        {
            AudioListener.volume = Mathf.Clamp01(MasterVolume);
            if (QualityLevel >= 0 && QualityLevel < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != QualityLevel)
                QualitySettings.SetQualityLevel(QualityLevel, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;

            if (ResolutionWidth > 0 && ResolutionHeight > 0)
            {
                FullScreenMode fs = Mode == WindowMode.Fenetre ? FullScreenMode.Windowed
                    : Mode == WindowMode.PleinEcran ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow;
                if (Screen.width != ResolutionWidth || Screen.height != ResolutionHeight || Screen.fullScreenMode != fs)
                    Screen.SetResolution(ResolutionWidth, ResolutionHeight, fs);
            }
            Changed?.Invoke();
        }

        /// <summary>Résolutions proposées (sans doublons, du plus petit au plus grand, au moins 960x540).</summary>
        public static List<Vector2Int> AvailableResolutions()
        {
            var list = new List<Vector2Int>();
            foreach (Resolution r in Screen.resolutions)
            {
                if (r.width < 960 || r.height < 540) continue;
                var v = new Vector2Int(r.width, r.height);
                if (!list.Contains(v)) list.Add(v);
            }
            var current = new Vector2Int(ResolutionWidth, ResolutionHeight);
            if (ResolutionWidth > 0 && !list.Contains(current)) list.Add(current);
            if (list.Count == 0) list.Add(new Vector2Int(1920, 1080));
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }
    }
}
