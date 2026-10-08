using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(CombatHealth))]

// boss controller script to attach to bosses, controls everything about the boss
public class BossController : MonoBehaviour, ICombatActor
{
    [Header("Defeat Tracking")]
    [Tooltip("Unique stable ID, such as butler. Used by GameManager and player style unlocks.")]
    [SerializeField] private string bossId = "new_boss";
    public string BossId { get { return bossId; } }
    [Header("Sprites")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private Sprite attackSprite;
    [Min(0.01f)][SerializeField] private float secondsPerWalkFrame = 0.15f;
    [SerializeField] private bool artworkFacesRight = true;

    [Header("Movement")]
    [Tooltip("Keeps this boss fixed at its spawn position while retaining the normal attack AI. Useful for stationary gun bosses.")]
    [SerializeField] private bool stationaryDuringFight;
    [Min(0f)][SerializeField] private float moveSpeed = 2.5f;
    [Tooltip("Absolute world X limits of the arena, left then right.")]
    [SerializeField] private Vector2 arenaXLimits = new Vector2(-8f, 8f);
    [Tooltip("Seconds to commit to one move before choosing a new direction.")]
    [SerializeField] private Vector2 movementDecisionSeconds = new Vector2(0.5f, 1.2f);
    [Range(0f, 1f)][SerializeField] private float approachChance = 0.65f;
    [Range(0f, 1f)][SerializeField] private float pauseChance = 0.15f;
    [Tooltip("Inside this distance, the boss is more likely to step away.")]
    [Min(0f)][SerializeField] private float preferredDistance = 2.5f;

    [Header("Attack")]
    [Tooltip("Assign a PlatterThrowAttack, TaserLightningAttack, TommyGunBurstAttack, MeleeBossAttack, or your own BossAttack subclass.")]
    [SerializeField] private BossAttack attackBehaviour;
    [Tooltip("Prevents the boss from taking damage after the fight begins. Use this for an intentionally undefeatable encounter.")]
    [SerializeField] private bool invulnerableDuringFight;
    [Tooltip("Random wait AFTER recovery before the next attack can begin.")]
    [SerializeField] private Vector2 attackCooldownSeconds = new Vector2(1.2f, 2.2f);
    [Min(0f)][SerializeField] private float firstAttackDelay = 0.8f;
    [Tooltip("Optional child at the hand when artwork faces right. X is mirrored when facing left.")]
    [SerializeField] private Transform attackOrigin;
    [Tooltip("World-unit offset from the boss pivot if Attack Origin is empty.")]
    [SerializeField] private Vector2 attackOriginOffset = new Vector2(0.6f, 0.4f);

    [Header("Walking Sounds")]
    [SerializeField] private AudioClip[] footstepSounds;
    [Range(0f, 1f)][SerializeField] private float footstepVolume = 0.8f;
    [SerializeField] private AudioSource footstepAudioSource;
    [Tooltip("Separate source for throws/attacks. Empty sources are created automatically.")]
    [SerializeField] private AudioSource attackAudioSource;

    public bool IsFighting { get; private set; }
    public int FacingDirection { get; private set; }
    public SpriteRenderer Renderer { get; private set; }
    public CombatHealth Health { get; private set; }
    public Transform ActorTransform { get { return transform; } }
    public bool CanUseAttacks
    {
        get { return isActiveAndEnabled && IsFighting && Health != null && !Health.IsDead; }
    }
    public float ArenaMinX { get { return Mathf.Min(arenaXLimits.x, arenaXLimits.y); } }
    public float ArenaMaxX { get { return Mathf.Max(arenaXLimits.x, arenaXLimits.y); } }
    public bool HasConfiguredAttack
    {
        get { return attackBehaviour != null && attackBehaviour.isActiveAndEnabled && attackBehaviour.IsConfigured; }
    }

    private enum FightState { Moving, Windup, Recovery }
    private FightState state;
    private CombatHealth target;
    private float stateTimer;
    private float moveTargetX;
    private float nextAttackTime;
    private Vector2 attackAimPosition;
    private float walkTimer;
    private int walkFrame;
    private bool wasWalking;
    private AudioClip lastStep;

    private void Awake()
    {
        Renderer = GetComponent<SpriteRenderer>();
        Health = GetComponent<CombatHealth>();
        Health.SetTeam(CombatHealth.CombatTeam.Enemy);
        Health.IsInvulnerable = true;
        Health.Died += HandleDeath;
        if (attackBehaviour == null)
            attackBehaviour = GetComponent<BossAttack>();
        ConfigureSource(ref footstepAudioSource);
        if (attackAudioSource == footstepAudioSource)
            attackAudioSource = null;
        ConfigureSource(ref attackAudioSource);
        FacingDirection = 1;
        ShowIdle();
    }

    // start boss fight
    public bool BeginFight(CombatHealth playerHealth)
    {
        if (playerHealth == null || playerHealth.IsDead || Health.IsDead ||
            !isActiveAndEnabled)
            return false;
        if (!HasConfiguredAttack)
        {
            Debug.LogWarning("Boss needs an enabled, configured Attack Behaviour such as Platter Throw or Taser Lightning.", this);
            return false;
        }

        target = playerHealth;
        Health.IsInvulnerable = invulnerableDuringFight;
        IsFighting = true;
        nextAttackTime = Time.time + Mathf.Max(0f, firstAttackDelay);
        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x, ArenaMinX, ArenaMaxX);
        transform.position = position;
        ChooseMovement();
        return true;
    }

    // update tick to check what movement ai to use
    private void Update()
    {
        if (!IsFighting || Time.deltaTime <= 0f)
            return;
        if (target == null || target.IsDead || !target.isActiveAndEnabled || Health.IsDead ||
            attackBehaviour == null || !attackBehaviour.isActiveAndEnabled)
        {
            StopFight();
            return;
        }

        if (state == FightState.Windup)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {

                state = FightState.Recovery;
                stateTimer = attackBehaviour.RecoverySeconds;
                attackBehaviour.Release(this, target, attackAimPosition);
            }
            return;
        }
        if (state == FightState.Recovery)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                nextAttackTime = Time.time + RandomInterval(attackCooldownSeconds, 0.05f);
                attackBehaviour.FinishAnimation();
                if (IsFighting)
                    ChooseMovement();
            }
            return;
        }

        float distanceToPlayer = Mathf.Abs(target.transform.position.x - transform.position.x);
        if (Time.time >= nextAttackTime && distanceToPlayer <= attackBehaviour.Range)
        {
            BeginAttack();
            return;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
            ChooseMovement();

        Vector3 position = transform.position;
        float previousX = position.x;
        position.x = Mathf.MoveTowards(position.x, moveTargetX,
            Mathf.Max(0f, moveSpeed) * Time.deltaTime);
        position.x = Mathf.Clamp(position.x, ArenaMinX, ArenaMaxX);
        transform.position = position;
        bool walking = Mathf.Abs(position.x - previousX) > 0.00001f;
        if (walking)
            Face(position.x - previousX);
        AnimateWalk(walking);
    }

    // choose what movement to do
    private void ChooseMovement()
    {
        state = FightState.Moving;
        stateTimer = RandomInterval(movementDecisionSeconds, 0.05f);

        if (stationaryDuringFight)
        {
            moveTargetX = transform.position.x;
            if (footstepAudioSource != null)
                footstepAudioSource.Stop();
            ShowIdle();
            return;
        }

        float delta = target.transform.position.x - transform.position.x;
        float toward = Mathf.Abs(delta) > 0.05f ? Mathf.Sign(delta)
            : (Random.value < 0.5f ? -1f : 1f);
        float distance = Mathf.Abs(delta);
        float direction = 0f;

        if (Random.value >= pauseChance)
        {
            float chanceToApproach = distance < preferredDistance ? 0.25f
                : (distance > attackBehaviour.Range ? 0.9f : approachChance);
            direction = Random.value < chanceToApproach ? toward : -toward;
        }


        moveTargetX = Mathf.Clamp(transform.position.x + direction * moveSpeed * stateTimer,
            ArenaMinX, ArenaMaxX);
        if (direction != 0f && Mathf.Abs(moveTargetX - transform.position.x) < 0.05f)
            moveTargetX = Mathf.Clamp(transform.position.x - direction * moveSpeed * stateTimer,
                ArenaMinX, ArenaMaxX);
        ShowIdle();
    }

    // start attack
    private void BeginAttack()
    {
        Physics2D.SyncTransforms();
        attackAimPosition = target.AimPosition;
        Face(attackAimPosition.x - transform.position.x);
        ShowIdle();
        if (attackSprite != null)
            Renderer.sprite = attackSprite;
        footstepAudioSource.Stop();
        state = FightState.Windup;
        stateTimer = attackBehaviour.WindupSeconds;
        attackBehaviour.BeginWindup();
    }

    // for stopping a boss fight
    public void StopFight()
    {
        bool cancelAnimation = IsFighting && state != FightState.Moving;
        IsFighting = false;
        if (Health != null)
            Health.IsInvulnerable = true;
        if (attackBehaviour != null)
        {
            if (cancelAnimation)
                attackBehaviour.CancelAttack();
            attackBehaviour.ClearActiveEffects();
        }
        if (footstepAudioSource != null)
            footstepAudioSource.Stop();
        ShowIdle();
    }

    private void OnDisable() { StopFight(); }

    // boss death and stop the fight
    private void HandleDeath(CombatHealth defeated)
    {
        StopFight();
        if (GameManager.Instance != null)
            GameManager.Instance.HandleKilledBoss(this);
    }

    private void OnDestroy()
    {
        if (Health != null)
            Health.Died -= HandleDeath;
    }

    private void Face(float horizontalDirection)
    {
        if (Mathf.Abs(horizontalDirection) > 0.001f)
            FacingDirection = horizontalDirection < 0f ? -1 : 1;
        Renderer.flipX = artworkFacesRight ? FacingDirection < 0 : FacingDirection > 0;
    }

    public Vector3 GetAttackPosition()
    {
        Vector3 offset = attackOrigin != null
            ? attackOrigin.position - transform.position : (Vector3)attackOriginOffset;
        offset.x = Mathf.Abs(offset.x) * FacingDirection;
        return transform.position + offset;
    }

    public void FaceTowards(Vector2 position) { Face(position.x - transform.position.x); }

    private void ShowIdle()
    {
        wasWalking = false;
        walkFrame = 0;
        walkTimer = 0f;
        if (Renderer == null)
            return;
        if (idleSprite != null)
            Renderer.sprite = idleSprite;
        else if (walkFrames != null && walkFrames.Length > 0 && walkFrames[0] != null)
            Renderer.sprite = walkFrames[0];
    }

    // animate the boss walking using walk frames
    private void AnimateWalk(bool moving)
    {
        if (!moving)
        {
            ShowIdle();
            return;
        }
        if (walkFrames == null || walkFrames.Length == 0)
            return;
        if (!wasWalking)
        {
            wasWalking = true;
            walkTimer = 0f;
            walkFrame = 0;
            SetWalkFrame();
        }
        walkTimer += Time.deltaTime;
        float frameTime = Mathf.Max(0.01f, secondsPerWalkFrame);
        while (walkTimer >= frameTime)
        {
            walkTimer -= frameTime;
            walkFrame = (walkFrame + 1) % walkFrames.Length;
            SetWalkFrame();
        }
    }

    private void SetWalkFrame()
    {
        if (walkFrames[walkFrame] == null)
            return;
        Renderer.sprite = walkFrames[walkFrame];
        if (footstepSounds == null)
            return;

        AudioClip chosen = null;
        int count = 0;
        foreach (AudioClip clip in footstepSounds)
        {
            if (clip == null || clip == lastStep)
                continue;
            count++;
            if (Random.Range(0, count) == 0)
                chosen = clip;
        }
        if (chosen == null)
            foreach (AudioClip clip in footstepSounds)
                if (clip != null) { chosen = clip; break; }
        if (chosen != null)
        {
            footstepAudioSource.PlayOneShot(chosen, Mathf.Clamp01(footstepVolume));
            lastStep = chosen;
        }
    }

    public void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null && attackAudioSource != null)
            attackAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void ConfigureSource(ref AudioSource source)
    {
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
    }

    private static float RandomInterval(Vector2 limits, float minimum)
    {
        float low = Mathf.Max(minimum, Mathf.Min(limits.x, limits.y));
        return Random.Range(low, Mathf.Max(low, Mathf.Max(limits.x, limits.y)));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 left = new Vector3(ArenaMinX, transform.position.y, transform.position.z);
        Vector3 right = new Vector3(ArenaMaxX, transform.position.y, transform.position.z);
        Gizmos.DrawLine(left, right);
        Gizmos.DrawLine(left + Vector3.up, left - Vector3.up);
        Gizmos.DrawLine(right + Vector3.up, right - Vector3.up);
    }
}
