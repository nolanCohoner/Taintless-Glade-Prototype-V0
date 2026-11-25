using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TaintlessGlade
{
    /// <summary>
    /// Choix de la salle : grille 3 x 2 avec des flèches pour changer de page.
    /// Les cases sans salle sont verrouillées (« ??? »). Un clic sur une salle lance la partie.
    /// </summary>
    public class RoomSelectView : MonoBehaviour
    {
        public MenuController controller;
        public RectTransform grid;
        public MenuButton previousButton;
        public MenuButton nextButton;
        public TMP_Text pageLabel;

        public const int Columns = 3, Rows = 2;
        const float CardW = 420f, CardH = 320f, Gap = 30f;

        class Card
        {
            public MenuButton button;
            public Image thumb, lockIcon;
            public TMP_Text status;
            public RoomCatalog.Room room;
        }

        readonly Card[] _cards = new Card[Columns * Rows];
        RoomCatalog _catalog;
        int _page;
        bool _built;

        public MenuButton FirstButton => _cards[0] != null ? _cards[0].button : null;

        void Awake() => Build();

        void Start()
        {
            if (previousButton != null) previousButton.Clicked += () => ChangePage(-1);
            if (nextButton != null) nextButton.Clicked += () => ChangePage(1);
        }

        void OnEnable()
        {
            Build();
            _page = 0;
            Refresh();
        }

        void Build()
        {
            if (_built || grid == null) return;
            _built = true;
            _catalog = GameSession.Rooms;
            MenuTheme theme = MenuTheme.Instance;

            float totalW = Columns * CardW + (Columns - 1) * Gap;
            float totalH = Rows * CardH + (Rows - 1) * Gap;
            for (int i = 0; i < _cards.Length; i++)
            {
                int col = i % Columns, row = i / Columns;
                var card = new Card();
                MenuButton b = MenuButton.Create("Room_" + i, grid, "", new Vector2(CardW, CardH), 34f, alwaysBorder: true, align: TextAlignmentOptions.Left);
                MenuUi.Place(b.transform as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-totalW * 0.5f + CardW * 0.5f + col * (CardW + Gap), totalH * 0.5f - CardH * 0.5f - row * (CardH + Gap)), new Vector2(CardW, CardH));
                b.slide = 0f;
                b.hoverScale = 0.045f;
                card.button = b;

                card.thumb = MenuUi.Image("Thumb", b.content, theme.pixel, new Color(0.1f, 0.12f, 0.13f, 1f));
                MenuUi.Place(card.thumb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(CardW - 52f, CardH - 112f));
                card.thumb.preserveAspect = false;
                card.thumb.transform.SetAsFirstSibling();
                b.fill.transform.SetAsFirstSibling();

                card.lockIcon = MenuUi.Image("Lock", b.content, theme.lockIcon, new Color(1f, 1f, 1f, 0.55f));
                MenuUi.Place(card.lockIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -26f - (CardH - 112f) * 0.5f), new Vector2(72f, 72f));
                card.lockIcon.preserveAspect = true;

                RectTransform label = b.label.rectTransform;
                label.anchorMin = new Vector2(0f, 0f);
                label.anchorMax = new Vector2(1f, 0f);
                label.pivot = new Vector2(0.5f, 0f);
                label.offsetMin = new Vector2(28f, 16f);
                label.offsetMax = new Vector2(-28f, 66f);
                b.label.alignment = TextAlignmentOptions.BottomLeft;

                card.status = MenuUi.Text("Status", b.content, "", 24f, theme.accent, TextAlignmentOptions.BottomRight);
                MenuUi.Place(card.status.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 18f), new Vector2(200f, 40f));

                int slot = i;
                b.Clicked += () => OnCard(slot);
                _cards[i] = card;
            }
        }

        void ChangePage(int direction)
        {
            int pages = _catalog != null ? _catalog.PageCount : 1;
            int next = Mathf.Clamp(_page + direction, 0, pages - 1);
            if (next == _page) return;
            _page = next;
            Refresh();
            for (int i = 0; i < _cards.Length; i++) _cards[i].button.Pop(6f + i);
            controller.Rustle(0.6f);
        }

        void Refresh()
        {
            if (!_built) return;
            MenuTheme theme = MenuTheme.Instance;
            int pages = _catalog != null ? _catalog.PageCount : 1;

            for (int i = 0; i < _cards.Length; i++)
            {
                Card c = _cards[i];
                int index = _page * _cards.Length + i;
                c.room = _catalog != null && index < _catalog.rooms.Count ? _catalog.rooms[index] : null;
                bool available = c.room != null && !string.IsNullOrEmpty(c.room.sceneName);

                c.button.Locked = !available;
                c.button.Text = available ? c.room.title : "???";
                c.status.text = available ? "Jouer" : "Bientôt";
                c.status.color = available ? theme.accent : theme.textDim;
                c.lockIcon.gameObject.SetActive(!available);
                c.thumb.sprite = available && c.room.thumbnail != null ? c.room.thumbnail : theme.pixel;
                c.thumb.color = available ? (c.room.thumbnail != null ? Color.white : new Color(0.16f, 0.2f, 0.2f, 1f)) : new Color(0f, 0f, 0f, 0.65f);
            }

            if (previousButton != null) previousButton.Locked = _page <= 0;
            if (nextButton != null) nextButton.Locked = _page >= pages - 1;
            if (pageLabel != null) pageLabel.text = (_page + 1) + " / " + pages;
        }

        void OnCard(int slot)
        {
            Card c = _cards[slot];
            if (c.room == null || string.IsNullOrEmpty(c.room.sceneName)) return;
            controller.StartGame(c.room);
        }

        void Update()
        {
            if (!controller.IsScreen(MenuScreen.Rooms) || controller.IsBusy) return;
            int direction = 0;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.pageDownKey.wasPressedThisFrame) direction = 1;
                if (kb.pageUpKey.wasPressedThisFrame) direction = -1;
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.rightShoulder.wasPressedThisFrame) direction = 1;
                if (pad.leftShoulder.wasPressedThisFrame) direction = -1;
            }
            if (direction != 0) ChangePage(direction);
        }
    }
}
