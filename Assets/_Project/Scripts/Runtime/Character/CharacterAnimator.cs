using UnityEngine;

namespace TaintlessGlade
{
    /// <summary>
    /// Animation procédurale d'un CharacterRig : repos, marche, course, attaque, dash, visée, demi-tour.
    /// Ne connaît pas le joueur : n'importe quel script (joueur, IA ennemie...) remplit les entrées publiques.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class CharacterAnimator : MonoBehaviour
    {
        public CharacterRig rig;
        public RigAnimationProfile profile;

        // --- Entrées (monde) ---
        public Vector3 Velocity { get; set; }
        public Vector3 AimDirection { get; set; } = Vector3.forward;
        public float WalkSpeed { get; set; } = 5f;
        public float RunSpeed { get; set; } = 8f;
        public bool IsDashing { get; set; }

        /// <summary>+1 = regarde à droite de l'écran, -1 = à gauche.</summary>
        public int Facing => _facing;

        int _facing = 1;
        float _flip = 1f;
        float _turnFrom = 1f;
        float _turnT = 1f;
        float _phase;
        float _move, _run, _dash;
        float _attackT = 1f;
        float _headAngle, _headVelocity, _headBob;
        float _tailAngle;

        const float TwoPi = Mathf.PI * 2f;

        public void PlayAttack() => _attackT = 0f;

        void LateUpdate()
        {
            if (rig == null || profile == null) return;
            RigAnimationProfile p = profile;
            float dt = Time.deltaTime;

            // Passage en "espace écran" : x = droite de l'écran, y = haut de l'écran (= vers le fond de la salle).
            Camera cam = Camera.main;
            Vector3 right = cam != null ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;
            Vector3 forward = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            var velocity = new Vector2(Vector3.Dot(Velocity, right), Vector3.Dot(Velocity, forward));
            var aim = new Vector2(Vector3.Dot(AimDirection, right), Vector3.Dot(AimDirection, forward)).normalized;

            UpdateFacing(aim, dt);

            // --- Poids de locomotion ---
            float speed = velocity.magnitude;
            float targetMove = IsDashing ? 0f : Mathf.Clamp01(speed / Mathf.Max(WalkSpeed * 0.8f, 0.01f));
            float targetRun = IsDashing ? 0f : Mathf.Clamp01((speed - WalkSpeed * 1.05f) / Mathf.Max(RunSpeed - WalkSpeed * 1.05f, 0.01f));
            _move = Mathf.MoveTowards(_move, targetMove, dt * 6f);
            _run = Mathf.MoveTowards(_run, targetRun, dt * 4f);
            _dash = Mathf.MoveTowards(_dash, IsDashing ? 1f : 0f, dt * 14f);

            float frequency = Mathf.Lerp(p.walkCyclesPerSecond, p.runCyclesPerSecond, _run);
            _phase = Mathf.Repeat(_phase + dt * frequency * TwoPi * _move, TwoPi);

            float attack = 0f;
            if (_attackT < 1f)
            {
                _attackT = Mathf.Min(1f, _attackT + dt / Mathf.Max(p.attackDuration, 0.01f));
                attack = Mathf.Sin(_attackT * Mathf.PI);
            }

            float aimPitch = Mathf.Atan2(aim.y, Mathf.Abs(aim.x)) * Mathf.Rad2Deg;
            float movePitch = speed > 0.1f ? velocity.y / speed * p.moveHeadInfluence : 0f;

            // --- Corps ---
            float bob = Mathf.Abs(Mathf.Sin(_phase)) * Mathf.Lerp(p.walkBodyBob, p.runBodyBob, _run) * _move;
            float breath = Mathf.Sin(Time.time * p.breathSpeed) * p.breathAmount * (1f - _move);
            var bodyOffset = new Vector3(-attack * p.bodyRecoil, bob, 0f);
            float bodyAngle = -p.runBodyLean * _run * _move + aimPitch * p.bodyAimInfluence * (1f - _dash) + p.dashBodyAngle * _dash;
            var bodyScale = new Vector3(
                1f + attack * p.attackSquash + _dash * p.dashStretch - breath * 0.5f,
                1f - attack * p.attackSquash * 0.7f - _dash * p.dashStretch * 0.5f + breath,
                1f);
            CharacterRig.Apply(rig.body, rig.BodyRest, bodyOffset, bodyAngle, bodyScale);

            // --- Tête : suit la visée + la direction de déplacement, rebond en retard sur le corps ---
            float headTarget = Mathf.Clamp(aimPitch * p.aimHeadInfluence + movePitch, -p.maxHeadAngle, p.maxHeadAngle);
            _headAngle = Mathf.SmoothDampAngle(_headAngle, headTarget, ref _headVelocity, p.headSmoothTime);
            _headBob = Mathf.Lerp(_headBob, bob * p.headBobFollow, 1f - Mathf.Exp(-dt / Mathf.Max(p.headLag, 0.001f)));
            Vector3 headOffset = Attach(rig.HeadRest.position, bodyOffset, bodyAngle, bodyScale)
                                 + new Vector3(attack * p.headThrust, _headBob - bob + breath * 0.5f, 0f);
            CharacterRig.Apply(rig.head, rig.HeadRest, headOffset, bodyAngle + _headAngle, Vector3.one * (1f + attack * 0.08f));

            // --- Queue : balancement léger au repos, fort en course, tendue en dash ---
            float moveAmplitude = Mathf.Lerp(p.walkTailAmplitude, p.runTailAmplitude, _run);
            float wave = Mathf.Lerp(Mathf.Sin(Time.time * p.idleTailFrequency * TwoPi) * p.idleTailAmplitude,
                                    Mathf.Sin(_phase + 1.2f) * moveAmplitude, _move);
            float tailTarget = wave * (1f - _dash) + p.runTailLift * _run * _move + p.attackTailFlick * attack + p.dashTailLift * _dash;
            _tailAngle = Mathf.Lerp(_tailAngle, tailTarget, 1f - Mathf.Exp(-dt * p.tailResponsiveness));
            Vector3 tailOffset = Attach(rig.TailRest.position, bodyOffset, bodyAngle, bodyScale);
            // Queue orientée vers -X : un angle négatif la relève.
            CharacterRig.Apply(rig.tail, rig.TailRest, tailOffset, bodyAngle - _tailAngle, Vector3.one);

            // --- Pattes : trot en diagonale (avant-proche avec arrière-éloignée), repliées en dash ---
            float swing = Mathf.Lerp(p.walkLegSwing, p.runLegSwing, _run) * _move;
            float liftHeight = Mathf.Lerp(p.walkLegLift, p.runLegLift, _run) * _move;
            for (int i = 0; i < rig.legs.Length && i < rig.LegRest.Length; i++)
            {
                CharacterRig.Leg leg = rig.legs[i];
                float legPhase = _phase + (leg.front ? 0f : Mathf.PI) + (leg.far ? Mathf.PI : 0f);
                float angle = Mathf.Sin(legPhase) * swing;
                float lift = Mathf.Max(0f, Mathf.Cos(legPhase)) * liftHeight;
                angle = Mathf.Lerp(angle, (leg.front ? 1f : -1f) * p.dashLegTuck, _dash);

                Vector3 hipOffset = Attach(rig.LegRest[i].position, bodyOffset, bodyAngle, bodyScale) * p.hipFollow + Vector3.up * lift;
                CharacterRig.Apply(leg.pivot, rig.LegRest[i], hipOffset, angle + bodyAngle * 0.5f, Vector3.one);
            }
        }

        void UpdateFacing(Vector2 aim, float dt)
        {
            if (Mathf.Abs(aim.x) > profile.facingDeadZone)
            {
                int wanted = aim.x > 0f ? 1 : -1;
                if (wanted != _facing)
                {
                    _facing = wanted;
                    _turnFrom = _flip;
                    _turnT = 0f;
                }
            }

            _turnT = Mathf.Min(1f, _turnT + dt / Mathf.Max(profile.turnDuration, 0.001f));
            _flip = Mathf.Lerp(_turnFrom, _facing, Mathf.SmoothStep(0f, 1f, _turnT));
            float stretch = 1f + Mathf.Sin(_turnT * Mathf.PI) * profile.turnStretch;

            if (rig.flipRoot != null)
            {
                Vector3 s = rig.FlipRootScale;
                rig.flipRoot.localScale = new Vector3(s.x * _flip, s.y * stretch, s.z);
            }
        }

        /// <summary>
        /// Décalage d'un point "accroché" au corps quand celui-ci bouge, tourne et se déforme
        /// (équivaut à un parentage, sans hériter de l'écrasement non uniforme du corps).
        /// </summary>
        Vector3 Attach(Vector3 restPosition, Vector3 bodyOffset, float bodyAngle, Vector3 bodyScale)
        {
            Vector3 relative = restPosition - rig.BodyRest.position;
            Vector3 transformed = Quaternion.Euler(0f, 0f, bodyAngle) * Vector3.Scale(relative, bodyScale);
            return bodyOffset + transformed - relative;
        }
    }
}
