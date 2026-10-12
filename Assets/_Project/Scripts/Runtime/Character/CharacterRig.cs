using System;
using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// "Squelette" modulaire d'un perso 2D : des pivots (corps, tête, queue, pattes) animés par CharacterAnimator.
    /// Chaque pivot peut contenir n'importe quel visuel (carré placeholder, sprite final, ou os d'un rig 2D Animation).
    /// Conventions : le perso regarde vers +X local (la droite), +Y = haut de l'écran.
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        [Serializable]
        public class Leg
        {
            public Transform pivot;
            [Tooltip("Patte avant (sinon arrière).")] public bool front;
            [Tooltip("Patte du côté éloigné de la caméra (déphasée, dessinée derrière le corps).")] public bool far;
        }

        [Serializable]
        public class Tentacle
        {
            public Transform pivot;
            [Tooltip("+1 = la tentacule pointe vers la droite au repos, -1 = vers la gauche.")] public float direction = 1f;
            [Tooltip("Décalage de l'onde pour que les tentacules ne bougent pas toutes en même temps.")] public float phase;
            [Range(0.2f, 2f)] public float amplitude = 1f;
            [Tooltip("Multiplicateur de fréquence propre à cette tentacule (évite qu'elles bougent à l'unisson).")]
            [Range(0.5f, 1.6f)] public float frequency = 1f;
            [Tooltip("Réactivité : une petite valeur = tentacule plus molle, qui traîne.")]
            [Range(0.3f, 2f)] public float responsiveness = 1f;
        }

        [Serializable]
        public class Eye
        {
            [Tooltip("Pivot de l'iris (déplacé dans le référentiel de la tête).")] public Transform iris;
            [Tooltip("Centre de la boule d'oeil, dans le référentiel de la tête.")] public Vector2 center;
        }

        public struct Pose
        {
            public Vector3 position;
            public float angle;
            public Vector3 scale;

            public static Pose Of(Transform t) => t == null ? default : new Pose
            {
                position = t.localPosition,
                angle = Mathf.DeltaAngle(0f, t.localEulerAngles.z),
                scale = t.localScale,
            };
        }

        [Tooltip("Retourné en scale X pour regarder à gauche / à droite.")]
        public Transform flipRoot;
        public Transform body;
        public Transform head;
        public Transform tail;
        public Leg[] legs = Array.Empty<Leg>();
        public Tentacle[] tentacles = Array.Empty<Tentacle>();
        public Eye[] eyes = Array.Empty<Eye>();
        [Tooltip("Course max de l'iris depuis le centre de la boule d'oeil (le reste est masqué par la boule).")]
        public float irisTravel = 0.045f;
        [Tooltip("Arme tenue par la patte avant proche (carré rouge en attendant l'arc arc-en-ciel). Orientée vers la visée.")]
        public Transform bow;
        [Tooltip("Distance entre l'articulation de la patte et le centre de l'arme.")]
        public float bowReach = 0.3f;

        public Vector3 FlipRootScale { get; private set; } = Vector3.one;
        public float[] TentacleRest { get; private set; } = Array.Empty<float>();
        public Pose BodyRest { get; private set; }
        public Pose HeadRest { get; private set; }
        public Pose TailRest { get; private set; }
        public Pose[] LegRest { get; private set; } = Array.Empty<Pose>();

        void Awake() => CaptureRestPose();

        /// <summary>Mémorise la pose actuelle comme pose de repos (à rappeler si on change les pivots en jeu).</summary>
        public void CaptureRestPose()
        {
            if (flipRoot == null) flipRoot = transform;
            FlipRootScale = flipRoot.localScale;
            BodyRest = Pose.Of(body);
            HeadRest = Pose.Of(head);
            TailRest = Pose.Of(tail);
            LegRest = new Pose[legs.Length];
            for (int i = 0; i < legs.Length; i++)
                LegRest[i] = Pose.Of(legs[i].pivot);
            TentacleRest = new float[tentacles.Length];
            for (int i = 0; i < tentacles.Length; i++)
                TentacleRest[i] = tentacles[i].pivot != null ? Mathf.DeltaAngle(0f, tentacles[i].pivot.localEulerAngles.z) : 0f;
        }

        public static void Apply(Transform t, in Pose rest, Vector3 offset, float angle, Vector3 scaleMultiplier)
        {
            if (t == null) return;
            t.localPosition = rest.position + offset;
            t.localRotation = Quaternion.Euler(0f, 0f, rest.angle + angle);
            t.localScale = Vector3.Scale(rest.scale, scaleMultiplier);
        }
    }
}
