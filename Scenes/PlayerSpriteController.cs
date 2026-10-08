using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]

// for moving the player sprite around and configuring a walk cycle animation
public class PlayerSpriteController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Walk Animation")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite[] walkFrames;

    [Tooltip("Seconds before changing to the next walk frame.")]
    [Min(0.01f)]
    [SerializeField] private float secondsPerFrame = 0.15f;

    [Header("Footstep Audio")]
    [Tooltip("Dedicated non-looping AudioSource for footsteps. If left empty, one is added automatically.")]
    [SerializeField] private AudioSource footstepAudioSource;

    [Tooltip("Individual step clips. A random clip plays whenever the walk sprite changes.")]
    [SerializeField] private AudioClip[] footstepSounds;

    [Range(0f, 1f)]
    [SerializeField] private float footstepVolume = 1f;

    [Tooltip("Avoid playing the same footstep twice in a row when more than one clip is assigned.")]
    [SerializeField] private bool avoidImmediateFootstepRepeat = true;

    private SpriteRenderer spriteRenderer;
    private int currentFrame;
    private float animationTimer;
    private bool wasMoving;
    private int lastFootstepIndex = -1;
    private CombatHealth health;
    private bool useArenaBounds;
    private float arenaMinX;
    private float arenaMaxX;
    private bool attackPoseActive;
    private Sprite attackPose;
    private bool defeatedPoseActive;
    private Sprite defeatedPose;
    private Vector3 scaleBeforeDefeatedPose;
    private bool hasScaleBeforeDefeatedPose;
    private float externalPushRemainingX;
    private float externalPushTimeRemaining;
    public int FacingDirection { get { return spriteRenderer != null && spriteRenderer.flipX ? -1 : 1; } }
    public PlayerLoadout EquippedLoadout { get; private set; }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        health = GetComponent<CombatHealth>();
        ConfigureFootstepAudioSource();

        if (idleSprite != null)
            spriteRenderer.sprite = idleSprite;
    }

    private void Update()
    {
        if (health == null)
            health = GetComponent<CombatHealth>();
        if (defeatedPoseActive)
        {
            if (defeatedPose != null)
                spriteRenderer.sprite = defeatedPose;
            return;
        }
        if (attackPoseActive)
        {
            if (attackPose != null)
                spriteRenderer.sprite = attackPose;
            return;
        }
        float horizontalInput = health != null && health.IsDead ? 0f : ReadHorizontalInput();


        Vector3 previousPosition = transform.position;
        float movementX = horizontalInput * moveSpeed * Time.deltaTime;
        movementX += ConsumeExternalPush(Time.deltaTime);
        Vector3 position = previousPosition + Vector3.right * movementX;
        if (useArenaBounds)
            position.x = Mathf.Clamp(position.x, arenaMinX, arenaMaxX);
        transform.position = position;
        bool changedPosition = Mathf.Abs(position.x - previousPosition.x) > 0.00001f;
        bool isWalking = changedPosition && Mathf.Abs(horizontalInput) > 0.001f;

        if (isWalking)
        {
            spriteRenderer.flipX = horizontalInput < 0f;
        }

        // Bullet pushback changes position without triggering Frank's walk
        // cycle or playing a footstep for every hit.
        UpdateWalkAnimation(isWalking);
    }

    public void SetArenaBounds(float leftX, float rightX)
    {
        arenaMinX = Mathf.Min(leftX, rightX);
        arenaMaxX = Mathf.Max(leftX, rightX);
        useArenaBounds = true;
    }

    public void ClearArenaBounds() { useArenaBounds = false; }


    public bool ApplyOutfit(PlayerLoadout loadout)
    {
        if (loadout == null)
            return false;

        if (loadout.HasOutfit)
        {
            if (loadout.IdleSprite != null)
                idleSprite = loadout.IdleSprite;

            if (loadout.WalkFrames != null && loadout.WalkFrames.Length > 0)
                walkFrames = loadout.WalkFrames;

            secondsPerFrame = loadout.SecondsPerWalkFrame;
        }

        EquippedLoadout = loadout;
        currentFrame = 0;
        animationTimer = 0f;
        wasMoving = false;

        if (!attackPoseActive && !defeatedPoseActive && spriteRenderer != null)
            UpdateWalkAnimation(false);

        return true;
    }

    public void SetAttackPose(Sprite sprite)
    {
        if (defeatedPoseActive)
            return;

        UpdateWalkAnimation(false);
        if (footstepAudioSource != null)
            footstepAudioSource.Stop();
        attackPoseActive = true;
        attackPose = sprite;
        if (spriteRenderer != null && sprite != null)
            spriteRenderer.sprite = sprite;
    }

    public void ClearAttackPose()
    {
        if (!attackPoseActive)
            return;
        attackPoseActive = false;
        attackPose = null;
        if (spriteRenderer != null)
            UpdateWalkAnimation(false);
    }

    public void ApplyHorizontalPush(float direction, float distance, float duration)
    {
        if (defeatedPoseActive || (health != null && health.IsDead))
            return;

        float pushDirection = Mathf.Abs(direction) > 0.001f
            ? Mathf.Sign(direction)
            : -FacingDirection;
        externalPushRemainingX += pushDirection * Mathf.Max(0f, distance);
        externalPushTimeRemaining = Mathf.Max(
            externalPushTimeRemaining,
            Mathf.Max(0.01f, duration)
        );
    }

    public void SetDefeatedPose(Sprite sprite, float scaleMultiplier = 1f)
    {
        if (sprite == null)
            return;

        if (!defeatedPoseActive)
        {
            scaleBeforeDefeatedPose = transform.localScale;
            hasScaleBeforeDefeatedPose = true;
        }

        float scale = Mathf.Max(0.01f, scaleMultiplier);
        Vector3 normalScale = hasScaleBeforeDefeatedPose
            ? scaleBeforeDefeatedPose
            : transform.localScale;
        transform.localScale = new Vector3(
            normalScale.x * scale,
            normalScale.y * scale,
            normalScale.z
        );

        defeatedPoseActive = true;
        defeatedPose = sprite;
        attackPoseActive = false;
        attackPose = null;
        externalPushRemainingX = 0f;
        externalPushTimeRemaining = 0f;

        PlayerCombatController combat = GetComponent<PlayerCombatController>();
        if (combat != null)
            combat.CancelAttack(true);
        if (footstepAudioSource != null)
            footstepAudioSource.Stop();
        if (spriteRenderer != null)
            spriteRenderer.sprite = defeatedPose;
    }

    public void ClearDefeatedPose()
    {
        if (hasScaleBeforeDefeatedPose)
            transform.localScale = scaleBeforeDefeatedPose;

        defeatedPoseActive = false;
        defeatedPose = null;
        hasScaleBeforeDefeatedPose = false;
        externalPushRemainingX = 0f;
        externalPushTimeRemaining = 0f;
        if (spriteRenderer != null)
            UpdateWalkAnimation(false);
    }

    private float ConsumeExternalPush(float deltaTime)
    {
        if (externalPushTimeRemaining <= 0f ||
            Mathf.Abs(externalPushRemainingX) <= 0.00001f)
        {
            externalPushRemainingX = 0f;
            externalPushTimeRemaining = 0f;
            return 0f;
        }

        float elapsed = Mathf.Min(Mathf.Max(0f, deltaTime), externalPushTimeRemaining);
        float movement = externalPushRemainingX *
                         (elapsed / externalPushTimeRemaining);
        externalPushRemainingX -= movement;
        externalPushTimeRemaining -= elapsed;
        return movement;
    }

    private void OnDisable()
    {
        PlayerCombatController combat = GetComponent<PlayerCombatController>();
        if (combat != null)
            combat.CancelAttack(true);

        externalPushRemainingX = 0f;
        externalPushTimeRemaining = 0f;

        if (spriteRenderer != null && !defeatedPoseActive)
            UpdateWalkAnimation(false);
        if (footstepAudioSource != null)
            footstepAudioSource.Stop();
    }

    private float ReadHorizontalInput()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard =
            UnityEngine.InputSystem.Keyboard.current;

        if (keyboard == null)
            return 0f;

        float input = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            input -= 1f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            input += 1f;

        return input;
#else
        float input = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            input -= 1f;

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            input += 1f;

        return input;
#endif
    }

    private void UpdateWalkAnimation(bool isMoving)
    {
        if (!isMoving)
        {
            currentFrame = 0;
            animationTimer = 0f;
            wasMoving = false;

            if (idleSprite != null)
                spriteRenderer.sprite = idleSprite;
            else if (walkFrames != null && walkFrames.Length > 0)
                spriteRenderer.sprite = walkFrames[0];

            return;
        }

        if (walkFrames == null || walkFrames.Length == 0)
            return;


        if (!wasMoving)
        {
            currentFrame = 0;
            animationTimer = 0f;
            spriteRenderer.sprite = walkFrames[0];
            PlayRandomFootstep();
        }

        wasMoving = true;
        animationTimer += Time.deltaTime;

        float frameTime = Mathf.Max(0.01f, secondsPerFrame);

        while (animationTimer >= frameTime)
        {
            animationTimer -= frameTime;
            currentFrame = (currentFrame + 1) % walkFrames.Length;
            spriteRenderer.sprite = walkFrames[currentFrame];
            PlayRandomFootstep();
        }
    }

    private void ConfigureFootstepAudioSource()
    {
        if (footstepAudioSource == null)
            footstepAudioSource = gameObject.AddComponent<AudioSource>();

        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.loop = false;
        footstepAudioSource.spatialBlend = 0f;
    }

    private void PlayRandomFootstep()
    {
        if (footstepSounds == null || footstepSounds.Length == 0)
            return;

        int validClipCount = 0;

        for (int i = 0; i < footstepSounds.Length; i++)
        {
            if (footstepSounds[i] != null)
                validClipCount++;
        }

        if (validClipCount == 0)
            return;

        bool excludePrevious = avoidImmediateFootstepRepeat &&
                               validClipCount > 1 &&
                               lastFootstepIndex >= 0 &&
                               lastFootstepIndex < footstepSounds.Length &&
                               footstepSounds[lastFootstepIndex] != null;

        int selectableClipCount = validClipCount - (excludePrevious ? 1 : 0);
        int selectedRank = Random.Range(0, selectableClipCount);
        int chosenIndex = -1;

        for (int i = 0; i < footstepSounds.Length; i++)
        {
            if (footstepSounds[i] == null)
                continue;

            if (excludePrevious && i == lastFootstepIndex)
                continue;

            if (selectedRank == 0)
            {
                chosenIndex = i;
                break;
            }

            selectedRank--;
        }

        if (chosenIndex < 0)
            return;

        ConfigureFootstepAudioSource();
        footstepAudioSource.PlayOneShot(
            footstepSounds[chosenIndex],
            Mathf.Clamp01(footstepVolume)
        );

        lastFootstepIndex = chosenIndex;
    }
}
