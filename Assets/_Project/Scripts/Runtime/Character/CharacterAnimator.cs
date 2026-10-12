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

        /// <summary>Direction de visée telle qu'elle apparaît À L'ÉCRAN (x = droite, y = haut). Zéro = déduite de AimDirection.</summary>
        public Vector2 AimScreen { get; set; }
        /// <summary>Vecteur écran (pixels) de la tête vers le point regardé (souris) : pilote les iris.</summary>
        public Vector2 LookPixels { get; set; }

        /// <summary>+1 = regarde à droite de l'écran, -1 = à gauche.</summary>
        public int Facing => _facing;
        /// <summary>0..1 : effort visible (course rapide, dash) -> gouttes de sueur.</summary>
        public float Exertion { get; private set; }
        public bool IsDashingVisual => _dash > 0.05f;
        /// <summary>0..1 : le tir part bientôt (anticipation : le corps se contracte, la corde se tend).</summary>
        public float AttackWindup { get; set; }

        int _facing = 1;
        float _prevBobY, _bobVelocity;
        float[] _tentacleAngle = System.Array.Empty<float>();
        float _flip = 1f;
        float _turnFrom = 1f;
        float _turnT = 1f;
        float _phase;
        float _move, _run, _dash;
        float _attackT = 1f;
        float _headAngle, _headVelocity, _headBob;
        float _tailAngle;
        float _windup;
        float _stepStrideMul = 1f, _stepLiftMul = 1f, _stepSpeedMul = 1f;
        float _dashCrouchT = 1f;
        bool _wasDashing;
        float _idleTimer = 3f, _idleT = 1f, _idleDuration = 1f, _idleSign = 1f, _idleEnvelope;
        int _idleEvent;
        float[] _tentaclePhases = System.Array.Empty<float>();

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
            var aim = AimScreen.sqrMagnitude > 1e-4f
                ? AimScreen.normalized
                : new Vector2(Vector3.Dot(AimDirection, right), Vector3.Dot(AimDirection, forward)).normalized;

            UpdateFacing(aim, dt);

            // --- Poids de locomotion ---
            float speed = velocity.magnitude;
            float targetMove = IsDashing ? 0f : Mathf.Clamp01(speed / Mathf.Max(WalkSpeed * 0.8f, 0.01f));
            float targetRun = IsDashing ? 0f : Mathf.Clamp01((speed - WalkSpeed * 1.05f) / Mathf.Max(RunSpeed - WalkSpeed * 1.05f, 0.01f));
            _move = Mathf.MoveTowards(_move, targetMove, dt * 6f);
            _run = Mathf.MoveTowards(_run, targetRun, dt * 4f);
            _dash = Mathf.MoveTowards(_dash, IsDashing ? 1f : 0f, dt * 14f);

            float frequency = Mathf.Lerp(p.walkCyclesPerSecond, p.runCyclesPerSecond, _run);
            float nextPhase = _phase + dt * frequency * TwoPi * _move * _stepSpeedMul;
            if (nextPhase >= TwoPi)
            {
                // Chaque foulée est légèrement différente (longueur, hauteur, vitesse) : pas de boucle parfaite.
                float v = p.stepVariation;
                _stepStrideMul = 1f + Random.Range(-v, v);
                _stepLiftMul = 1f + Random.Range(-v * 1.3f, v * 1.3f);
                _stepSpeedMul = 1f + Random.Range(-v * 0.5f, v * 0.5f);
            }
            _phase = Mathf.Repeat(nextPhase, TwoPi);

            float attack = 0f;
            if (_attackT < 1f)
            {
                _attackT = Mathf.Min(1f, _attackT + dt / Mathf.Max(p.attackDuration, 0.01f));
                attack = Mathf.Sin(_attackT * Mathf.PI);
            }

            // Anticipation du tir : monte vite avant le coup, retombe d'un coup quand il part.
            _windup = Mathf.MoveTowards(_windup, AttackWindup, dt * (AttackWindup > _windup ? 14f : 30f));
            float windup = _windup * (1f - _dash);

            // Anticipation du dash : une courte contraction avant l'étirement.
            if (IsDashing && !_wasDashing) _dashCrouchT = 0f;
            _wasDashing = IsDashing;
            float crouch = 0f;
            if (_dashCrouchT < 1f)
            {
                _dashCrouchT = Mathf.Min(1f, _dashCrouchT + dt / Mathf.Max(p.dashAnticipation, 0.01f));
                crouch = Mathf.Sin(_dashCrouchT * Mathf.PI);
            }

            UpdateIdleEvent(p, dt, attack);

            float aimPitch = Mathf.Atan2(aim.y, Mathf.Abs(aim.x)) * Mathf.Rad2Deg;
            float movePitch = speed > 0.1f ? velocity.y / speed * p.moveHeadInfluence : 0f;

            // --- Corps ---
            // Rebond asymétrique : la montée est plus lente que la chute, avec un petit temps suspendu en haut.
            float warped = _phase + p.stepAsymmetry * Mathf.Sin(2f * _phase);
            float arc = Mathf.Abs(Mathf.Sin(warped));
            float bounce = 2f * arc - arc * arc;
            float bob = bounce * Mathf.Lerp(p.walkBodyBob, p.runBodyBob, _run) * _move * _stepLiftMul;
            float breath = Mathf.Sin(Time.time * p.breathSpeed) * p.breathAmount * (1f - _move);
            var bodyOffset = new Vector3(-attack * p.bodyRecoil - windup * p.windupPull - crouch * p.windupPull * 1.5f, bob - windup * 0.02f - crouch * 0.03f, 0f);
            _bobVelocity = Mathf.Lerp(_bobVelocity, (bob - _prevBobY) / Mathf.Max(dt, 0.0001f), 1f - Mathf.Exp(-dt * 20f));
            _prevBobY = bob;
            Exertion = IsDashing || _dash > 0.05f ? 1f : Mathf.Clamp01((_run * _move - 0.55f) / 0.35f);
            float bodyAngle = -p.runBodyLean * _run * _move + Mathf.Sin(_phase) * p.bodyRoll * _move + aimPitch * p.bodyAimInfluence * (1f - _dash) + p.dashBodyAngle * _dash + windup * 5f + crouch * 7f;
            var bodyScale = new Vector3(
                1f + attack * p.attackSquash + _dash * p.dashStretch - breath * 0.5f + windup * 0.05f + crouch * 0.12f,
                1f - attack * p.attackSquash * 0.7f - _dash * p.dashStretch * 0.5f + breath + (bounce - 0.5f) * p.stepStretch * _move - windup * 0.06f - crouch * 0.18f,
                1f);
            CharacterRig.Apply(rig.body, rig.BodyRest, bodyOffset, bodyAngle, bodyScale);

            // --- Tête : suit la visée + la direction de déplacement, rebond en retard sur le corps ---
            float headTarget = Mathf.Clamp(aimPitch * p.aimHeadInfluence + movePitch, -p.maxHeadAngle, p.maxHeadAngle)
                               + (_idleEvent == 0 ? _idleSign * 9f * _idleEnvelope : 0f);
            _headAngle = Mathf.SmoothDampAngle(_headAngle, headTarget, ref _headVelocity, p.headSmoothTime);
            _headBob = Mathf.Lerp(_headBob, bob * p.headBobFollow, 1f - Mathf.Exp(-dt / Mathf.Max(p.headLag, 0.001f)));
            Vector3 headOffset = Attach(rig.HeadRest.position, bodyOffset, bodyAngle, bodyScale)
                                 + new Vector3(attack * p.headThrust - windup * 0.05f, _headBob - bob + breath * 0.5f, 0f);
            CharacterRig.Apply(rig.head, rig.HeadRest, headOffset, bodyAngle + _headAngle, Vector3.one * (1f + attack * 0.08f));

            // --- Queue : balancement léger au repos, fort en course, tendue en dash ---
            float moveAmplitude = Mathf.Lerp(p.walkTailAmplitude, p.runTailAmplitude, _run);
            float wave = Mathf.Lerp(Mathf.Sin(Time.time * p.idleTailFrequency * TwoPi) * p.idleTailAmplitude,
                                    Mathf.Sin(_phase + 1.2f) * moveAmplitude, _move);
            float tailTarget = wave * (1f - _dash) + p.runTailLift * _run * _move + p.attackTailFlick * attack + p.dashTailLift * _dash + windup * 8f
                              + (_idleEvent == 1 ? _idleSign * 24f * _idleEnvelope : 0f);
            _tailAngle = Mathf.Lerp(_tailAngle, tailTarget, 1f - Mathf.Exp(-dt * p.tailResponsiveness));
            Vector3 tailOffset = Attach(rig.TailRest.position, bodyOffset, bodyAngle, bodyScale);
            // Queue orientée vers -X : un angle négatif la relève.
            CharacterRig.Apply(rig.tail, rig.TailRest, tailOffset, bodyAngle - _tailAngle, Vector3.one);

            // --- Pattes : petits pas rebondissants (le pied glisse en arrière au sol, se lève et revient devant).
            // Marche = trot en diagonale ; course = bonds (les deux pattes avant, puis les deux arrière) ; dash = pattes étirées.
            float stride = Mathf.Lerp(p.walkStride, p.runStride, _run) * _move * _stepStrideMul;
            float liftHeight = Mathf.Lerp(p.walkLegLift, p.runLegLift, _run) * _move * _stepLiftMul;
            float gallop = p.runGallop * _run;
            // Visée dans le référentiel local du rig (le rig est retourné en X quand il regarde à gauche).
            float flipSign = _flip < 0f ? -1f : 1f;
            var aimLocal = new Vector2(aim.x * flipSign, aim.y);
            float drawEnvelope = Mathf.Max(attack, windup * 0.9f);

            bool haveHand = false;
            Vector3 handCenter = default;

            for (int i = 0; i < rig.legs.Length && i < rig.LegRest.Length; i++)
            {
                CharacterRig.Leg leg = rig.legs[i];
                float backShift = Mathf.Lerp(Mathf.PI, Mathf.PI * 0.8f, gallop);
                float farShift = Mathf.Lerp(Mathf.PI, 0.7f, gallop);
                float legPhase = _phase + (leg.front ? 0f : backShift) + (leg.far ? farShift : 0f);
                float stepForward = Mathf.Cos(legPhase);
                float swingUp = Mathf.Max(0f, -Mathf.Sin(legPhase));
                float x = stepForward * stride;
                float lift = swingUp * liftHeight;
                float angle = -Mathf.Sin(legPhase) * p.stepTilt * _move;
                // Au repos, de temps en temps, la patte arrière proche tape le sol.
                if (_idleEvent == 3 && !leg.front && !leg.far)
                    lift += Mathf.Abs(Mathf.Sin(_idleT * Mathf.PI * 3f)) * 0.045f * _idleEnvelope;

                bool bowHand = leg.front && !leg.far && rig.bow != null;
                bool drawHand = leg.front && leg.far && rig.bow != null;
                if (bowHand)
                {
                    // La patte se tend vers la visée (sans pivoter comme un bras) et reste presque immobile.
                    float hold = p.bowHold * (1f - _dash);
                    x *= 1f - 0.7f * hold;
                    lift *= 1f - 0.5f * hold;
                    Vector2 reach = new Vector2(aimLocal.x, Mathf.Clamp(aimLocal.y, -0.4f, 1f)) * (p.bowPawReach * hold);
                    x += reach.x;
                    lift += reach.y;
                    angle = Mathf.Lerp(angle, Mathf.Clamp(Mathf.Atan2(aimLocal.x, -aimLocal.y) * Mathf.Rad2Deg, -10f, 60f) * 0.45f, hold);
                }
                else if (drawHand)
                {
                    x -= drawEnvelope * p.drawPawPull;
                    angle = Mathf.Lerp(angle, -p.drawPawAngle, drawEnvelope);
                }

                // Dash : bond, pattes avant tendues devant, arrière derrière.
                x = Mathf.Lerp(x, (leg.front ? 1f : -1f) * p.dashLegStretch, _dash);
                lift = Mathf.Lerp(lift, p.dashLegLift, _dash);
                angle = Mathf.Lerp(angle, (leg.front ? 1f : -1f) * -p.dashLegTuck * 0.4f, _dash);

                Vector3 hipOffset = Attach(rig.LegRest[i].position, bodyOffset, bodyAngle, bodyScale) * p.hipFollow
                                    + new Vector3(x, lift, 0f);
                CharacterRig.Apply(leg.pivot, rig.LegRest[i], hipOffset, angle + bodyAngle * 0.5f, Vector3.one);

                if (bowHand)
                {
                    haveHand = true;
                    handCenter = rig.LegRest[i].position + hipOffset + Vector3.down * 0.14f;
                }
            }

            // --- Arme : devant la patte qui la tient, perpendiculaire à la visée, recule au tir ---
            if (rig.bow != null && haveHand)
            {
                Vector3 pos = handCenter + (Vector3)(aimLocal * (rig.bowReach - attack * p.bowKick - windup * p.bowKick * 0.7f));
                pos.z = rig.bow.localPosition.z;
                rig.bow.localPosition = pos;
                rig.bow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aimLocal.y, aimLocal.x) * Mathf.Rad2Deg);
            }
            AnimateTentacles(p, dt, attack, windup);
            AnimateEyes(p, dt, bodyAngle + _headAngle, flipSign);
        }

        /// <summary>Onde douce au repos, plus ample en mouvement / tir / dash, traînée derrière le rebond du corps.</summary>
        void AnimateTentacles(RigAnimationProfile p, float dt, float attack, float windup)
        {
            CharacterRig.Tentacle[] tentacles = rig.tentacles;
            if (tentacles.Length == 0) return;
            if (_tentacleAngle.Length != tentacles.Length) _tentacleAngle = new float[tentacles.Length];
            if (_tentaclePhases.Length != tentacles.Length) _tentaclePhases = new float[tentacles.Length];

            float energy = Mathf.Clamp01(_move * 0.6f + _run * 0.4f);
            float amplitude = p.tentacleIdleAmplitude + energy * p.tentacleMoveAmplitude
                              + attack * p.tentacleAttackAmplitude + _dash * p.tentacleDashAmplitude
                              + (_idleEvent == 2 ? 11f * _idleEnvelope : 0f);
            amplitude *= 1f - 0.45f * windup;
            float frequency = Mathf.Lerp(p.tentacleIdleFrequency, p.tentacleMoveFrequency, Mathf.Max(energy, _dash));

            for (int i = 0; i < tentacles.Length && i < rig.TentacleRest.Length; i++)
            {
                CharacterRig.Tentacle t = tentacles[i];
                if (t.pivot == null) continue;
                // Chaque tentacule a sa propre fréquence et son propre retard : elles ne bougent jamais à l'unisson.
                _tentaclePhases[i] = Mathf.Repeat(_tentaclePhases[i] + dt * frequency * t.frequency * TwoPi, TwoPi * 1000f);
                float follow = 1f - Mathf.Exp(-dt * p.tentacleResponsiveness * t.responsiveness);
                float target = Mathf.Sin(_tentaclePhases[i] + t.phase) * amplitude * t.amplitude
                               - t.direction * _bobVelocity * p.tentacleDrag
                               + t.direction * _dash * p.tentacleDashLift;
                _tentacleAngle[i] = Mathf.Lerp(_tentacleAngle[i], target, follow);
                t.pivot.localRotation = Quaternion.Euler(0f, 0f, rig.TentacleRest[i] + _tentacleAngle[i]);
            }
        }

        /// <summary>Les iris pointent vers la souris (direction exacte à l'écran) ; la boule d'oeil les masque au-delà de son contour.</summary>
        void AnimateEyes(RigAnimationProfile p, float dt, float headAngle, float flipSign)
        {
            if (rig.eyes.Length == 0) return;

            float distance = LookPixels.magnitude;
            Vector2 look = distance > 0.5f ? LookPixels / distance * Mathf.SmoothStep(0f, 1f, distance / Mathf.Max(p.irisFullDistance, 1f)) : Vector2.zero;
            var local = new Vector2(look.x * flipSign, look.y);
            // On annule la rotation de la tête pour que le regard reste fidèle à l'écran.
            float c = Mathf.Cos(-headAngle * Mathf.Deg2Rad), s = Mathf.Sin(-headAngle * Mathf.Deg2Rad);
            local = new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);

            float follow = 1f - Mathf.Exp(-dt * p.irisSpeed);
            foreach (CharacterRig.Eye eye in rig.eyes)
            {
                if (eye.iris == null) continue;
                Vector3 current = eye.iris.localPosition;
                Vector2 target = eye.center + local * rig.irisTravel;
                eye.iris.localPosition = new Vector3(Mathf.Lerp(current.x, target.x, follow), Mathf.Lerp(current.y, target.y, follow), current.z);
            }
        }

        /// <summary>
        /// Petits événements aléatoires au repos (inclinaison de tête, coup de queue, tentacules ébouriffées, patte qui tape)
        /// pour éviter l'idle en boucle parfaite. Les clignements des yeux sont laissés aux dessins.
        /// </summary>
        void UpdateIdleEvent(RigAnimationProfile p, float dt, float attack)
        {
            bool still = _move < 0.05f && !IsDashing && attack < 0.01f && _windup < 0.05f;
            if (_idleT < 1f)
            {
                _idleT = Mathf.Min(1f, _idleT + dt / _idleDuration);
                _idleEnvelope = Mathf.Sin(_idleT * Mathf.PI) * (1f - _move) * (1f - _windup);
            }
            else
            {
                _idleEnvelope = 0f;
            }

            if (!still) return;
            _idleTimer -= dt;
            if (_idleTimer > 0f || _idleT < 1f) return;

            _idleEvent = Random.Range(0, 4);
            _idleSign = Random.value < 0.5f ? -1f : 1f;
            _idleDuration = Random.Range(0.7f, 1.3f);
            _idleT = 0f;
            _idleTimer = Random.Range(p.idleEventMinDelay, p.idleEventMaxDelay);
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
