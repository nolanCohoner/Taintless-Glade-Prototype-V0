using System;
using System.Collections.Generic;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>Salles proposées dans le menu de démarrage. Asset : Resources/RoomCatalog.</summary>
    [CreateAssetMenu(menuName = "Taintless Glade/Room Catalog", fileName = "RoomCatalog")]
    public class RoomCatalog : ScriptableObject
    {
        [Serializable]
        public class Room
        {
            public string id;
            public string title;
            [TextArea] public string description;
            [Tooltip("Nom de la scène (doit être dans les Build Settings).")]
            public string sceneName;
            public Sprite thumbnail;
        }

        public List<Room> rooms = new List<Room>();
        [Tooltip("Nombre minimum de cases dans la grille (les cases libres s'affichent verrouillées). Une page = 6 cases (3 x 2).")]
        public int minSlots = 12;

        public const int PageSize = 6;

        public int SlotCount => Mathf.Max(minSlots, Mathf.CeilToInt(rooms.Count / (float)PageSize) * PageSize);
        public int PageCount => Mathf.Max(1, Mathf.CeilToInt(SlotCount / (float)PageSize));
        public Room Find(string id) => rooms.Find(r => r.id == id);
    }
}
