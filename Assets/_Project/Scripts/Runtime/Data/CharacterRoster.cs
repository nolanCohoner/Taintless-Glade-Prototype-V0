using System.Collections.Generic;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>Liste ordonnée des personnages du jeu (jouables ou « bientôt disponibles »). Asset : Resources/CharacterRoster.</summary>
    [CreateAssetMenu(menuName = "Taintless Glade/Character Roster", fileName = "CharacterRoster")]
    public class CharacterRoster : ScriptableObject
    {
        public List<CharacterStats> characters = new List<CharacterStats>();

        public CharacterStats Find(string id)
        {
            foreach (CharacterStats c in characters)
                if (c != null && c.id == id) return c;
            return null;
        }

        public CharacterStats FirstPlayable()
        {
            foreach (CharacterStats c in characters)
                if (c != null && c.playable) return c;
            return characters.Count > 0 ? characters[0] : null;
        }

        public int IndexOf(CharacterStats stats) => characters.IndexOf(stats);
    }
}
