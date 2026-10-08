using System.Collections.Generic;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Sur la caméra : lance des rayons vers une grille de points répartis sur la silhouette du joueur.
    /// Un FadeableWall ne se dissout que s'il se trouve réellement ENTRE la caméra et le sprite
    /// et qu'il en cache une part suffisante. Un décor derrière ou à côté du joueur n'est jamais touché.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class WallOcclusion : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Taille de la silhouette du perso (largeur, hauteur) dans le plan de l'écran, depuis ses pieds.")]
        public Vector2 targetSize = new Vector2(1.2f, 1f);

        [Tooltip("Part du sprite qui doit être cachée par un élément pour qu'il se dissolve (0.3 = 30 %).")]
        [Range(0.05f, 1f)] public float minHiddenFraction = 0.3f;
        [Tooltip("Résolution de la grille de points testés sur le sprite (3 = 3x3 rayons).")]
        [Range(2, 5)] public int gridResolution = 3;
        [Tooltip("Marge intérieure de la grille, pour ignorer les bords du sprite.")]
        [Range(0f, 0.4f)] public float gridInset = 0.15f;
        public LayerMask mask = ~0;

        readonly RaycastHit[] _hits = new RaycastHit[16];
        readonly Dictionary<FadeableWall, int> _blockedCounts = new();
        readonly HashSet<FadeableWall> _wallsOnRay = new();

        void LateUpdate()
        {
            if (target == null) return;

            _blockedCounts.Clear();
            Vector3 from = transform.position;
            int total = 0;

            for (int y = 0; y < gridResolution; y++)
            {
                for (int x = 0; x < gridResolution; x++)
                {
                    float u = Mathf.Lerp(gridInset, 1f - gridInset, x / (gridResolution - 1f));
                    float v = Mathf.Lerp(gridInset, 1f - gridInset, y / (gridResolution - 1f));
                    CastToward(from, SamplePoint(u, v));
                    total++;
                }
            }

            foreach (KeyValuePair<FadeableWall, int> pair in _blockedCounts)
            {
                if ((float)pair.Value / total >= minHiddenFraction)
                    pair.Key.MarkOccluding();
            }
        }

        // Point de la silhouette, dans le même plan que les sprites "billboard" (orientés comme la caméra).
        Vector3 SamplePoint(float u, float v)
        {
            return target.position
                   + transform.right * ((u - 0.5f) * targetSize.x)
                   + transform.up * (v * targetSize.y);
        }

        void CastToward(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.01f) return;

            // Le rayon s'arrête juste avant le sprite : seul ce qui est devant le joueur peut être touché.
            int count = Physics.RaycastNonAlloc(from, delta / distance, _hits, distance - 0.05f, mask, QueryTriggerInteraction.Ignore);

            _wallsOnRay.Clear();
            for (int i = 0; i < count; i++)
            {
                FadeableWall wall = _hits[i].collider.GetComponentInParent<FadeableWall>();
                if (wall != null && _wallsOnRay.Add(wall))
                    _blockedCounts[wall] = _blockedCounts.TryGetValue(wall, out int n) ? n + 1 : 1;
            }
        }
    }
}
