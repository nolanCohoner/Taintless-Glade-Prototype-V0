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

        public Vector3 FlipRootScale { get; private set; } = Vector3.one;
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
