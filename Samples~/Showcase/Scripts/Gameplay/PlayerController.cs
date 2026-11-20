using UnityEngine;
using SfxShowcase.Core;

namespace SfxShowcase.Gameplay
{
    /// <summary>
    /// The player: a hand-written kinematic character controller for a momentum platformer. Nothing here uses
    /// Rigidbody2D dynamics; the kinematic body only exists so that the level's triggers see the player.
    ///
    /// How it works, in the order the code below is laid out:
    /// 1. ON THE GROUND the player has one number, the ground speed, measured along the surface. Input, friction and
    ///    the slope change that number; the position then moves along the surface tangent. Two floor sensors (rays that
    ///    point into the ground, whichever way the ground faces) keep the player on the surface and give its angle, so
    ///    walls and the ceiling of a loop are just ground at another angle. A wall sensor points ahead.
    /// 2. IN THE AIR the player has an ordinary velocity: gravity, a little steering, a jump that is cut short when the
    ///    button is released. Sensors pointing down, sideways and up land the player or slide it along what it touches.
    /// 3. Landing turns the air velocity into ground speed by projecting it onto the surface; leaving the ground turns
    ///    ground speed back into a velocity along the tangent.
    /// 4. Too slow on a steep surface and the ground lets go: speed is what holds the player on a wall.
    ///
    /// Everything it tells the rest of the game goes through <see cref="GameEvents"/> and <see cref="IPlayerState"/>.
    /// All numbers are in <see cref="PlayerTuning"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerController : PlayerBase
    {
        /// <summary>Slower than this counts as standing still (for standing firm on a slope and for which way to face).</summary>
        const float StandstillSpeed = 0.05f;

        [Tooltip("Every number that shapes the movement.")]
        [SerializeField] PlayerTuning tuning = new PlayerTuning();

        [Tooltip("The art kit: used for the dust, the Burst puff and the scattered Chimes.")]
        [SerializeField] ArtKitBase art;

        [Tooltip("The level: where a run starts and how deep a pit is.")]
        [SerializeField] LevelDescriptor level;

        [Tooltip("The run: it holds the Chime count and says whether the game is being played.")]
        [SerializeField] GameSession session;

        [Tooltip("The kinematic body that carries the player through the physics world, so triggers fire.")]
        [SerializeField] Rigidbody2D body;

        [Tooltip("The trigger circle the level's pieces touch; its radius follows the Tuck.")]
        [SerializeField] CircleCollider2D touchCollider;

        // The simulated state. The position is the centre of the collision circle.
        Vector2 position;
        Vector2 velocity;
        float groundSpeed;
        Vector2 groundNormal = Vector2.up;
        GroundSurface standingSurface;
        bool grounded;
        float radius;
        TerrainPlane plane = TerrainPlane.A;
        int facing = 1;

        // What the player is doing.
        bool tucked;
        bool jumping;
        bool charging;
        float charge;
        bool skidding;
        bool knockedBack;
        bool finished;
        bool controlGranted;
        float finishBraking;
        float strideDistance;

        // Timers, seconds remaining.
        float controlLockTimer;
        float invulnerableTimer;
        float respawnTimer;

        // Where a respawn happens.
        int checkpointIndex = -1;
        Vector2 checkpointPosition;

        // Input, sampled every frame and consumed by the fixed step.
        readonly DeviceInput deviceInput = new DeviceInput();
        readonly GroundSensor sensor = new GroundSensor();
        float moveInput;
        bool downHeld;
        bool jumpHeld;
        bool previousJumpHeld;
        bool jumpPressLatched;

        // ---- What the rest of the game may read (IPlayerState) ---------------------------------------------------

        /// <inheritdoc/>
        public override Vector2 Position => transform.position;
        /// <inheritdoc/>
        public override Vector2 Velocity => velocity;
        /// <inheritdoc/>
        public override float GroundSpeed => grounded ? groundSpeed : 0f;
        /// <inheritdoc/>
        public override float TopSpeed => tuning.topSpeed;
        /// <inheritdoc/>
        public override bool IsGrounded => grounded;
        /// <inheritdoc/>
        public override float GroundAngle => grounded ? Mathf.Atan2(-groundNormal.x, groundNormal.y) * Mathf.Rad2Deg : 0f;
        /// <inheritdoc/>
        public override bool IsRolling => tucked;
        /// <inheritdoc/>
        public override bool IsCharging => charging;
        /// <inheritdoc/>
        public override float Charge01 => charging ? charge : 0f;
        /// <inheritdoc/>
        public override int Facing => facing;
        /// <inheritdoc/>
        public override bool IsAttacking => tucked || charging;
        /// <inheritdoc/>
        public override bool IsInvulnerable => invulnerableTimer > 0f || respawnTimer > 0f;
        /// <inheritdoc/>
        public override bool HasControl => controlGranted && !finished && respawnTimer <= 0f;
        /// <inheritdoc/>
        public override TerrainPlane Plane => plane;

        /// <summary>The numbers that shape the movement.</summary>
        public PlayerTuning Tuning => tuning;

        /// <summary>Where the controls are read from: <see cref="PlayerBase.InputOverride"/> when set, otherwise the keyboard and gamepad.</summary>
        public IPlayerInput ActiveInput => InputOverride ?? deviceInput;

        /// <summary>The direction of positive ground speed: the ground normal turned a quarter clockwise (+x on flat ground).</summary>
        Vector2 Tangent => new Vector2(groundNormal.y, -groundNormal.x);

        /// <summary>Input acts on the character: in control, and not flying back from a hit.</summary>
        bool CanAct => HasControl && !knockedBack;

        /// <summary>Holding down while too slow to tuck: the player stays put (and can start a Wind-Up).</summary>
        bool IsCrouching => grounded && !tucked && downHeld && Mathf.Abs(groundSpeed) < tuning.tuckStartSpeed;

        // ---- Setup -----------------------------------------------------------------------------------------------

        /// <summary>Connects the player to what it needs; called once by <see cref="GameplayInstaller"/> while the scene is built.</summary>
        public void Wire(ArtKitBase artKit, LevelDescriptor levelDescriptor, GameSession gameSession,
                         Rigidbody2D kinematicBody, CircleCollider2D triggerCollider)
        {
            art = artKit;
            level = levelDescriptor;
            session = gameSession;
            body = kinematicBody;
            touchCollider = triggerCollider;
        }

        /// <summary>Gives or takes the controls; the session calls this when the run starts. A Jump already held is not a new press.</summary>
        public void SetControl(bool granted)
        {
            controlGranted = granted;
            previousJumpHeld = true;
            jumpPressLatched = false;
        }

        /// <summary>Back to the start of the level for a new run: no checkpoint, standing at the spawn point.</summary>
        public void ResetForNewRun()
        {
            finished = false;
            checkpointIndex = -1;
            PlaceAt(level.SpawnPoint);
        }

        void Awake()
        {
            position = transform.position;
            radius = tuning.standingRadius;
        }

        void Start()
        {
            // Not in Awake: when the installer runs in play mode, Awake comes before Wire has set the collider.
            touchCollider.radius = radius;
        }

        // ---- The frame -------------------------------------------------------------------------------------------

        void Update()
        {
            ReadInput();
        }

        void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;
            ReadInput();

            if (respawnTimer > 0f)
            {
                respawnTimer -= deltaTime;
                if (respawnTimer <= 0f) CompleteRespawn();
                return;
            }

            invulnerableTimer = Mathf.Max(0f, invulnerableTimer - deltaTime);
            if (grounded) controlLockTimer = Mathf.Max(0f, controlLockTimer - deltaTime);

            HandleActions(deltaTime);
            Simulate(deltaTime);
            CountFootsteps(deltaTime);

            if (position.y < level.KillY) BeginRespawn();
            body.MovePosition(position);
        }

        /// <summary>
        /// Samples the controls. Called every rendered frame as well as every fixed step, so a quick tap of Jump between
        /// two fixed steps is not lost: the press is latched here and consumed by <see cref="ConsumeJumpPress"/>.
        /// </summary>
        void ReadInput()
        {
            IPlayerInput input = ActiveInput;
            bool held = input.JumpHeld;
            if (held && !previousJumpHeld && HasControl) jumpPressLatched = true;
            previousJumpHeld = held;
            jumpHeld = held;
            downHeld = input.Down;

            float x = input.MoveX;
            moveInput = Mathf.Abs(x) < tuning.moveDeadZone ? 0f : Mathf.Sign(x);
        }

        bool ConsumeJumpPress()
        {
            bool pressed = jumpPressLatched;
            jumpPressLatched = false;
            return pressed;
        }

        /// <summary>
        /// Moves the player through one fixed step in pieces no longer than <see cref="PlayerTuning.maxStepDistance"/>,
        /// checking the sensors after each piece, so the ground is followed closely even at top speed.
        /// </summary>
        void Simulate(float deltaTime)
        {
            float speed = grounded ? Mathf.Abs(groundSpeed) : velocity.magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(speed * deltaTime / tuning.maxStepDistance));
            float stepTime = deltaTime / steps;
            for (int i = 0; i < steps; i++)
            {
                if (grounded) GroundStep(stepTime);
                else AirStep(stepTime);
            }
        }

        // ---- Actions: Jump, Tuck, Wind-Up and Burst --------------------------------------------------------------

        /// <summary>Turns button presses into state changes, once per fixed step, before the movement.</summary>
        void HandleActions(float deltaTime)
        {
            bool jumpPressed = ConsumeJumpPress();
            if (!CanAct)
            {
                CancelWindUp();
                return;
            }
            if (!grounded) return;

            if (charging)
            {
                if (!downHeld) ReleaseBurst();
                else if (jumpPressed) AddCharge();
                else charge *= Mathf.Exp(-tuning.windUpLeak * deltaTime);
                return;
            }

            float speed = Mathf.Abs(groundSpeed);
            if (jumpPressed && downHeld && !tucked && speed < tuning.windUpMaxSpeed)
            {
                charging = true;
                charge = 0f;
                AddCharge();
            }
            else if (jumpPressed) Jump();
            else if (!tucked && downHeld && speed >= tuning.tuckStartSpeed) StartTuck();
            else if (tucked && speed < tuning.tuckEndSpeed) EndTuck();
        }

        /// <summary>Leaves the ground along the surface normal, keeping the speed along the ground: a slope throws the jump with it.</summary>
        void Jump()
        {
            Vector2 launch = Tangent * groundSpeed + groundNormal * tuning.JumpSpeed;
            LeaveGround();
            velocity = launch;
            jumping = true;
            if (!tucked)
            {
                tucked = true;
                SetRadius(tuning.tuckedRadius);
            }
            GameEvents.RaiseJumped();
        }

        void StartTuck()
        {
            tucked = true;
            SetRadius(tuning.tuckedRadius);
            GameEvents.RaiseTuckStarted();
        }

        void EndTuck()
        {
            tucked = false;
            SetRadius(tuning.standingRadius);
        }

        void AddCharge()
        {
            charge = Mathf.Min(1f, charge + 1f / tuning.windUpTaps);
            GameEvents.RaiseWindUpCharged(charge);
        }

        /// <summary>Lets the Wind-Up go: the player shoots off tucked, the way it faces, as fast as the charge allows.</summary>
        void ReleaseBurst()
        {
            float released = charge;
            charging = false;
            charge = 0f;
            groundSpeed = facing * tuning.BurstSpeed(released);
            GameEvents.RaiseBurstReleased(released);
            art.SpawnEffect(EffectKind.BurstPuff, FeetPosition());
            StartTuck();
        }

        void CancelWindUp()
        {
            charging = false;
            charge = 0f;
        }

        // ---- On the ground ---------------------------------------------------------------------------------------

        /// <summary>One piece of ground movement: change the ground speed, stop at walls, move along the surface, follow it.</summary>
        void GroundStep(float deltaTime)
        {
            Vector2 tangent = Tangent;
            ApplyGroundForces(deltaTime, tangent);

            float move = groundSpeed * deltaTime;
            if (move != 0f) move = StopAtWall(tangent, move);
            position += tangent * move;

            FollowGround(tangent);
            if (grounded)
            {
                LoseGripIfTooSlow();
                velocity = Tangent * groundSpeed;
            }
        }

        void ApplyGroundForces(float deltaTime, Vector2 tangent)
        {
            if (charging)
            {
                groundSpeed = 0f;
                return;
            }
            if (finished)
            {
                groundSpeed = Mathf.MoveTowards(groundSpeed, 0f, finishBraking * deltaTime);
                return;
            }

            ApplySlope(deltaTime, tangent);

            bool inputIgnored = !CanAct || controlLockTimer > 0f || IsCrouching;
            float move = inputIgnored ? 0f : moveInput;
            if (tucked) ApplyTuckedControl(move, deltaTime);
            else ApplyRunningControl(move, deltaTime, IsSteeperThanStandFirm(tangent));

            groundSpeed = Mathf.Clamp(groundSpeed, -tuning.topSpeed, tuning.topSpeed);

            if (Mathf.Abs(groundSpeed) > StandstillSpeed) facing = groundSpeed > 0f ? 1 : -1;
            else if (CanAct && moveInput != 0f) facing = moveInput > 0f ? 1 : -1;
        }

        /// <summary>
        /// The slope factor: gravity felt along the ground. The tangent's y is the sine of the slope in the direction of
        /// positive ground speed, so going uphill loses speed and going downhill gains it. Tucked, the factor is larger.
        /// </summary>
        void ApplySlope(float deltaTime, Vector2 tangent)
        {
            float slopeSine = tangent.y;
            bool standsFirm = !tucked && Mathf.Abs(groundSpeed) < StandstillSpeed && !IsSteeperThanStandFirm(tangent);
            if (standsFirm) return;

            float factor = tuning.slopeFactorRunning;
            if (tucked)
            {
                bool uphill = groundSpeed * slopeSine > 0f;
                factor = uphill ? tuning.slopeFactorTuckedUphill : tuning.slopeFactorTuckedDownhill;
            }
            groundSpeed -= factor * slopeSine * deltaTime;
        }

        /// <summary>
        /// Whether the ground here is steeper than <see cref="PlayerTuning.standFirmAngle"/> (walls and ceilings are).
        /// On such ground a player who is not steering gets no friction: the slope carries a coasting runner downhill
        /// and a standing one starts to slide. On gentler ground friction wins and the player can stop on the hillside.
        /// </summary>
        bool IsSteeperThanStandFirm(Vector2 tangent)
        {
            return groundNormal.y <= 0f || Mathf.Abs(tangent.y) >= Mathf.Sin(tuning.standFirmAngle * Mathf.Deg2Rad);
        }

        /// <summary>
        /// Running: accelerate up to cruise, coast down with friction (none where the slope carries the player), brake
        /// hard against the motion.
        /// </summary>
        void ApplyRunningControl(float move, float deltaTime, bool slopeCarries)
        {
            bool braking = move != 0f && move * groundSpeed < 0f;
            if (braking)
            {
                if (!skidding && Mathf.Abs(groundSpeed) >= tuning.skidMinSpeed)
                {
                    skidding = true;
                    GameEvents.RaiseSkidded();
                    art.SpawnEffect(EffectKind.Dust, FeetPosition());
                }
                float before = groundSpeed;
                groundSpeed += move * tuning.braking * deltaTime;
                if (before * groundSpeed < 0f) groundSpeed = 0f;
                return;
            }

            skidding = false;
            if (move == 0f)
            {
                if (!slopeCarries) groundSpeed = Mathf.MoveTowards(groundSpeed, 0f, tuning.friction * deltaTime);
                return;
            }

            // Holding forward builds speed up to cruise. Speed above cruise was earned elsewhere and only leaks away.
            float speed = Mathf.Abs(groundSpeed);
            speed = speed < tuning.cruiseSpeed
                ? Mathf.Min(speed + tuning.acceleration * deltaTime, tuning.cruiseSpeed)
                : Mathf.Max(speed - tuning.overCruiseDrag * deltaTime, tuning.cruiseSpeed);
            groundSpeed = move * speed;
        }

        /// <summary>Tucked: the stick cannot add speed, only brake a little; low friction lets the roll carry.</summary>
        void ApplyTuckedControl(float move, float deltaTime)
        {
            bool braking = move != 0f && move * groundSpeed < 0f;
            if (braking) groundSpeed = Mathf.MoveTowards(groundSpeed, 0f, tuning.tuckBraking * deltaTime);
            groundSpeed = Mathf.MoveTowards(groundSpeed, 0f, tuning.tuckFriction * deltaTime);
        }

        /// <summary>
        /// The wall sensor: a ray ahead along the ground. Ground that is much steeper than what is underfoot is a wall;
        /// the move is shortened so the player stops touching it. Returns the move that is allowed.
        /// </summary>
        float StopAtWall(Vector2 tangent, float move)
        {
            float direction = Mathf.Sign(move);
            float distance = Mathf.Abs(move);
            if (!sensor.CastSolid(position, tangent * direction, radius + distance, plane, out SensorHit wall)) return move;

            bool isWall = Vector2.Dot(wall.Normal, groundNormal) < Mathf.Cos(tuning.wallAngle * Mathf.Deg2Rad);
            if (!isWall) return move;

            groundSpeed = 0f;
            return direction * Mathf.Min(distance, wall.Distance - radius);
        }

        /// <summary>
        /// The two floor sensors: rays from just behind and just ahead of the centre, pointing into the ground whichever
        /// way it faces. Where both find the same stretch of ground, the line between the two points is the new ground
        /// direction, which turns the player smoothly over corners and round loops. Where they disagree (a ledge, a
        /// step) the nearer one wins. Where neither finds anything, the ground has ended.
        /// </summary>
        void FollowGround(Vector2 tangent)
        {
            float reach = radius + tuning.groundSnapDistance;
            float oneWayMinimum = radius - tuning.oneWayTolerance;
            float followDot = Mathf.Cos(tuning.maxGroundTurnAngle * Mathf.Deg2Rad);
            Vector2 intoGround = -groundNormal;
            Vector2 spread = tangent * tuning.footSpread;

            bool backFound = sensor.CastFloor(position - spread, intoGround, reach, plane, standingSurface, oneWayMinimum, out SensorHit back);
            bool frontFound = sensor.CastFloor(position + spread, intoGround, reach, plane, standingSurface, oneWayMinimum, out SensorHit front);
            if (!backFound && !frontFound)
            {
                LeaveGround();
                return;
            }

            if (backFound && frontFound)
            {
                Vector2 along = (front.Point - back.Point).normalized;
                if (Vector2.Dot(along, tangent) >= followDot)
                {
                    groundNormal = new Vector2(-along.y, along.x);
                    position = (back.Point + front.Point) * 0.5f + groundNormal * radius;
                    standingSurface = groundSpeed >= 0f ? front.Surface : back.Surface;
                    return;
                }
            }

            SensorHit nearest = !frontFound || (backFound && back.Distance <= front.Distance) ? back : front;
            position += groundNormal * (radius - nearest.Distance);
            standingSurface = nearest.Surface;
            if (Vector2.Dot(nearest.Normal, groundNormal) >= followDot) groundNormal = nearest.Normal;
        }

        /// <summary>
        /// Speed is what holds the player on steep ground. Too slow on a steep slope: left/right is locked for a moment
        /// and the slope takes over. Too slow on a wall or a ceiling: the player drops off.
        /// </summary>
        void LoseGripIfTooSlow()
        {
            if (Mathf.Abs(groundSpeed) >= tuning.slipSpeed) return;
            if (groundNormal.y >= Mathf.Cos(tuning.slipAngle * Mathf.Deg2Rad)) return;

            controlLockTimer = tuning.slipControlLock;
            if (groundNormal.y < Mathf.Cos(tuning.detachAngle * Mathf.Deg2Rad)) LeaveGround();
        }

        /// <summary>The ground is gone (a ledge, a jump, a hit, a lost grip): ground speed becomes a velocity along the tangent.</summary>
        void LeaveGround()
        {
            if (!grounded) return;
            velocity = Tangent * groundSpeed;
            grounded = false;
            groundSpeed = 0f;
            groundNormal = Vector2.up;
            standingSurface = null;
            skidding = false;
            CancelWindUp();
        }

        // ---- In the air ------------------------------------------------------------------------------------------

        /// <summary>One piece of air movement: steer, fall, move, then let the sensors land the player or slide it along what it touches.</summary>
        void AirStep(float deltaTime)
        {
            ApplyAirForces(deltaTime);
            position += velocity * deltaTime;

            SenseFloorInAir();
            if (grounded) return;

            SlideAlong(position, Vector2.right, true);
            SlideAlong(position, Vector2.left, true);
            Vector2 spread = Vector2.right * tuning.footSpread;
            SlideAlong(position - spread, Vector2.up, false);
            SlideAlong(position + spread, Vector2.up, false);
        }

        void ApplyAirForces(float deltaTime)
        {
            float move = CanAct ? moveInput : 0f;
            if (move != 0f)
            {
                // Steering never pushes past cruise speed, but it does not take away speed earned on the ground either.
                if (move * velocity.x < tuning.cruiseSpeed)
                    velocity.x = Mathf.MoveTowards(velocity.x, move * tuning.cruiseSpeed, tuning.airAcceleration * deltaTime);
                facing = move > 0f ? 1 : -1;
            }

            if (jumping && !jumpHeld && velocity.y > tuning.jumpCutSpeed) velocity.y = tuning.jumpCutSpeed;

            bool nearTopOfRise = velocity.y > 0f && velocity.y < tuning.airDragMaxRiseSpeed;
            if (nearTopOfRise) velocity.x *= Mathf.Exp(-tuning.airDrag * deltaTime);

            velocity.y = Mathf.Max(velocity.y - tuning.gravity * deltaTime, -tuning.maxFallSpeed);
        }

        /// <summary>
        /// The two floor sensors in the air point straight down. A surface is landed on when the circle touches it, the
        /// player is moving into it and it is not too steep; a steeper one is slid along like a wall.
        /// </summary>
        void SenseFloorInAir()
        {
            float reach = radius * 2f;
            float oneWayMinimum = radius - tuning.oneWayTolerance;
            Vector2 spread = Vector2.right * tuning.footSpread;

            bool leftFound = sensor.CastFloor(position - spread, Vector2.down, reach, plane, null, oneWayMinimum, out SensorHit left);
            bool rightFound = sensor.CastFloor(position + spread, Vector2.down, reach, plane, null, oneWayMinimum, out SensorHit right);
            bool leftTouches = leftFound && Touches(left);
            bool rightTouches = rightFound && Touches(right);
            if (!leftTouches && !rightTouches) return;

            SensorHit touched = !rightTouches || (leftTouches && Clearance(left) <= Clearance(right)) ? left : right;
            bool landable = touched.Normal.y >= Mathf.Cos(tuning.maxLandingAngle * Mathf.Deg2Rad);
            if (landable) Land(touched);
            else PushOutAndSlide(touched);
        }

        /// <summary>The distance from the centre to the surface of a hit, measured along the surface's normal.</summary>
        float Clearance(SensorHit hit) => Vector2.Dot(position - hit.Point, hit.Normal);

        /// <summary>Whether the collision circle reaches the surface of a hit while moving into it (not away from it).</summary>
        bool Touches(SensorHit hit) => Clearance(hit) <= radius && Vector2.Dot(velocity, hit.Normal) <= 0f;

        /// <summary>A wall or ceiling sensor in the air: a ray of one radius; what it meets is slid along.</summary>
        void SlideAlong(Vector2 origin, Vector2 direction, bool wallsOnly)
        {
            if (!sensor.CastSolid(origin, direction, radius, plane, out SensorHit hit)) return;

            bool isFloor = hit.Normal.y >= Mathf.Cos(tuning.maxLandingAngle * Mathf.Deg2Rad);
            if (wallsOnly && isFloor) return;
            if (Touches(hit)) PushOutAndSlide(hit);
        }

        /// <summary>Moves the circle out of a surface and removes the part of the velocity that points into it.</summary>
        void PushOutAndSlide(SensorHit hit)
        {
            position += hit.Normal * (radius - Clearance(hit));
            velocity -= hit.Normal * Vector2.Dot(velocity, hit.Normal);
        }

        /// <summary>
        /// Touching down: the air velocity is projected onto the surface and becomes ground speed, so falling onto a
        /// downhill slope turns the fall into speed, and landing flat keeps only the sideways part.
        /// </summary>
        void Land(SensorHit floor)
        {
            float impactSpeed = -Vector2.Dot(velocity, floor.Normal);
            position += floor.Normal * (radius - Clearance(floor));
            groundNormal = floor.Normal;
            standingSurface = floor.Surface;
            grounded = true;
            groundSpeed = Mathf.Clamp(Vector2.Dot(velocity, Tangent), -tuning.topSpeed, tuning.topSpeed);
            jumping = false;

            if (tucked)
            {
                tucked = false;
                SetRadius(tuning.standingRadius);
            }
            if (knockedBack)
            {
                knockedBack = false;
                groundSpeed = 0f;
            }
            velocity = Tangent * groundSpeed;

            GameEvents.RaiseLanded(impactSpeed);
            if (impactSpeed >= tuning.dustImpactSpeed) art.SpawnEffect(EffectKind.Dust, FeetPosition());
        }

        // ---- What the level's pieces do to the player (IPlayerActor) ---------------------------------------------

        /// <inheritdoc/>
        public override void Bounce(Vector2 launchVelocity)
        {
            if (finished || respawnTimer > 0f) return;

            CancelWindUp();
            knockedBack = false;
            jumping = false;
            controlLockTimer = 0f;

            // A Woofer that pushes along the ground (a horizontal one) leaves the player running or rolling on it.
            bool staysOnGround = grounded && Vector2.Dot(launchVelocity, groundNormal) <= tuning.groundedBounceLift;
            if (staysOnGround)
            {
                groundSpeed = Mathf.Clamp(Vector2.Dot(launchVelocity, Tangent), -tuning.topSpeed, tuning.topSpeed);
                velocity = Tangent * groundSpeed;
                return;
            }

            LeaveGround();
            Untuck();
            velocity = launchVelocity;
        }

        /// <inheritdoc/>
        public override bool Hurt(Vector2 sourcePosition)
        {
            if (IsInvulnerable || finished || session.Phase != GamePhase.Playing) return false;

            GameEvents.RaiseHurt();
            int carried = session.DropAllChimes();
            if (carried == 0)
            {
                BeginRespawn();
                return true;
            }

            int scattered = Mathf.Min(carried, DesignTargets.MaxScatteredChimes);
            GameEvents.RaiseChimesScattered(scattered);
            ScatteredChime.SpawnFan(art, this, position, scattered, plane);

            float away = position.x >= sourcePosition.x ? 1f : -1f;
            LeaveGround();
            Untuck();
            velocity = new Vector2(away * tuning.knockBackSpeed, tuning.knockBackLift);
            knockedBack = true;
            jumping = false;
            invulnerableTimer = tuning.invulnerableSeconds;
            return true;
        }

        /// <inheritdoc/>
        public override void Rebound()
        {
            // Only ever a push upwards: a player still rising faster than this (a jump taken beside the Clipper) keeps its rise.
            if (grounded || velocity.y >= tuning.reboundSpeed) return;
            velocity.y = tuning.reboundSpeed;
            jumping = false;
        }

        /// <inheritdoc/>
        public override void AddChimes(int count)
        {
            session.CollectChimes(count);
        }

        /// <inheritdoc/>
        public override void SetCheckpoint(int index, Vector2 respawnPosition)
        {
            if (index <= checkpointIndex) return;
            checkpointIndex = index;
            checkpointPosition = respawnPosition;
        }

        /// <inheritdoc/>
        public override void Finish()
        {
            if (finished || session.Phase != GamePhase.Playing) return;

            finished = true;
            CancelWindUp();
            if (grounded && tucked) EndTuck();

            // Constant braking that stops from the arrival speed in the stopping distance: v² = 2·a·d.
            float arrivalSpeed = velocity.magnitude;
            finishBraking = Mathf.Max(tuning.finishMinBraking, arrivalSpeed * arrivalSpeed / (2f * tuning.finishStopDistance));

            GameEvents.RaiseGoalReached();
            session.EndRun();
        }

        /// <inheritdoc/>
        public override void SetTerrainPlane(TerrainPlane newPlane)
        {
            plane = newPlane == TerrainPlane.Both ? TerrainPlane.A : newPlane;
        }

        /// <inheritdoc/>
        public override void PlaceAt(Vector2 newPosition)
        {
            grounded = false;
            groundSpeed = 0f;
            groundNormal = Vector2.up;
            standingSurface = null;
            velocity = Vector2.zero;
            plane = TerrainPlane.A;
            facing = 1;

            tucked = false;
            jumping = false;
            skidding = false;
            knockedBack = false;
            CancelWindUp();
            SetRadius(tuning.standingRadius);

            controlLockTimer = 0f;
            invulnerableTimer = 0f;
            respawnTimer = 0f;
            strideDistance = 0f;

            position = newPosition;
            transform.position = new Vector3(newPosition.x, newPosition.y, transform.position.z);
            body.position = newPosition;
        }

        // ---- Getting lost and coming back ------------------------------------------------------------------------

        /// <summary>A pit, or a hit with no Chimes: the player freezes, loses its Chimes and reappears after a pause.</summary>
        void BeginRespawn()
        {
            LeaveGround();
            Untuck();
            velocity = Vector2.zero;
            knockedBack = false;
            jumping = false;
            session.DropAllChimes();
            respawnTimer = tuning.respawnDelay;
        }

        void CompleteRespawn()
        {
            Vector2 point = checkpointIndex >= 0
                ? checkpointPosition + Vector2.up * tuning.respawnLift
                : level.SpawnPoint;
            PlaceAt(point);
            invulnerableTimer = tuning.respawnInvulnerableSeconds;
            GameEvents.RaiseRespawned(Sections.At(point.x));
        }

        // ---- Small helpers ---------------------------------------------------------------------------------------

        void Untuck()
        {
            if (!tucked) return;
            tucked = false;
            SetRadius(tuning.standingRadius);
        }

        /// <summary>Changes the collision radius; on the ground the centre moves so the circle keeps touching the surface.</summary>
        void SetRadius(float newRadius)
        {
            if (grounded) position += groundNormal * (newRadius - radius);
            radius = newRadius;
            touchCollider.radius = newRadius;
        }

        /// <summary>Where the circle touches the ground (or its lowest point in the air): where dust appears.</summary>
        Vector2 FeetPosition() => position - groundNormal * radius;

        /// <summary>
        /// Raises one <see cref="GameEvents.Footstep"/> per stride while running. The stride
        /// (<see cref="DesignTargets.StrideLength"/>, which the character's legs step by too) lengthens with speed, so
        /// the cadence rises with speed but stays a rhythm.
        /// </summary>
        void CountFootsteps(float deltaTime)
        {
            float speed = Mathf.Abs(groundSpeed);
            bool running = grounded && !tucked && !charging && speed >= tuning.footstepMinSpeed;
            if (!running)
            {
                strideDistance = 0f;
                return;
            }

            float speed01 = Mathf.Clamp01(speed / tuning.topSpeed);
            float stride = DesignTargets.StrideLength(speed01);
            strideDistance += speed * deltaTime;
            if (strideDistance < stride) return;

            strideDistance -= stride;
            GameEvents.RaiseFootstep(speed01);
        }
    }
}
