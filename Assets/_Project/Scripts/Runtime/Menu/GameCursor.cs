using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Curseur de souris du jeu (pack Kenney) : flèche dans les menus, main sur un élément cliquable,
    /// viseur en jeu, sablier pendant un chargement, interdit sur un élément verrouillé. Textures assignées dans MenuTheme.
    /// </summary>
    public static class GameCursor
    {
        public enum Kind { Menu, Hover, Game, Busy, Forbidden }

        static Kind _current = (Kind)(-1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = (Kind)(-1);

        public static void Set(Kind kind)
        {
            if (_current == kind) return;
            MenuTheme theme = MenuTheme.Instance;
            if (theme == null) return;
            _current = kind;

            Texture2D texture;
            Vector2 hotspot;
            switch (kind)
            {
                case Kind.Hover: texture = theme.cursorHover; hotspot = theme.hoverHotspot; break;
                case Kind.Game: texture = theme.cursorGame; hotspot = theme.gameHotspot; break;
                case Kind.Busy: texture = theme.cursorBusy; hotspot = theme.busyHotspot; break;
                case Kind.Forbidden: texture = theme.cursorForbidden; hotspot = theme.forbiddenHotspot; break;
                default: texture = theme.cursorMenu; hotspot = theme.menuHotspot; break;
            }
            Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
