using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Fait apparaître les chiffres de dégâts au-dessus d'une cible.
    /// En mode combo, les coups rapprochés (dans comboWindow) s'additionnent sur le même chiffre.
    /// </summary>
    public class DamageNumberSpawner : MonoBehaviour
    {
        public DamageNumber prefab;
        public Vector3 offset = new Vector3(0f, 2f, 0f);
        public float randomSpread = 0.35f;

        [Header("Combo")]
        public bool accumulateCombos = true;
        [Tooltip("Délai max entre deux coups pour qu'ils s'additionnent.")]
        public float comboWindow = 0.8f;

        DamageNumber _current;

        public void Show(float amount)
        {
            if (prefab == null || !GameSettings.DamageNumbers) return;

            if (accumulateCombos && _current != null && _current.IsAccumulating)
            {
                _current.AddHit(amount);
                return;
            }

            Vector2 jitter = Random.insideUnitCircle * randomSpread;
            Vector3 position = transform.position + offset + new Vector3(jitter.x, jitter.y * 0.5f, 0f);
            DamageNumber number = Instantiate(prefab, position, Quaternion.identity);
            number.Init(amount, accumulateCombos ? comboWindow : 0f);
            _current = accumulateCombos ? number : null;
        }
    }
}
