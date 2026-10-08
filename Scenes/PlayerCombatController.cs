using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerSpriteController), typeof(CombatHealth))]

// for controlling the fight style of the player, after each boss fight they unlock the fight style of the boss
public class PlayerCombatController : MonoBehaviour, ICombatActor
{
    [Serializable]
    public class FightingStyle
    {
        public string styleId = "platter";
        [Tooltip("A weapon component on THIS Player, never a component on the boss prefab.")]
        public BossAttack attackBehaviour;
        public Sprite attackSprite;
        [Tooltip("Minimum seconds between starting attacks. An animation must also finish first.")]
        [Min(0f)] public float cooldownSeconds = 0.8f;
        [Tooltip("Boss ID that must be defeated to equip this style. Empty means unlocked.")]
        public string requiredBossId;

        [Header("Attack Origin")]
        [Tooltip("Optional child Transform on the Player marking where this style's weapon appears. For example, use separate PlatterOrigin and TaserOrigin children. Leave empty to use the fallback origin below.")]
        public Transform attackOrigin;

        [Tooltip("When Attack Origin is empty, enable this to use a unique offset for this fighting style instead of the player's fallback offset.")]
        public bool useCustomAttackOriginOffset;

        [Tooltip("Style-specific origin in local world units while the player faces right. X mirrors automatically when facing left.")]
        public Vector2 attackOriginOffset = new Vector2(0.6f, 0.4f);
    }

    [Header("Default Unarmed Punch (F)")]
    [SerializeField] private PunchAttack punchAttack;
    [SerializeField] private Sprite punchSprite;
    [Min(0f)][SerializeField] private float punchCooldownSeconds = 0.65f;
    [Header("Future Equippable Styles")]
    [SerializeField] private FightingStyle[] fightingStyles;
    [SerializeField] private string startingStyleId = "punch";
    [Header("Fallback Weapon Origin / Audio")]
    [Tooltip("Used by punch and any fighting style that does not define its own Attack Origin or custom offset.")]
    [SerializeField] private Transform attackOrigin;
    [Tooltip("Fallback origin used when no Transform or style-specific offset is configured.")]
    [SerializeField] private Vector2 attackOriginOffset = new Vector2(0.6f, 0.4f);
    [Tooltip("Dedicated 2D source. Automatically added if empty.")]
    [SerializeField] private AudioSource attackAudioSource;

    private PlayerSpriteController movement;
    public CombatHealth Health { get; private set; }
    public SpriteRenderer Renderer { get; private set; }
    public Transform ActorTransform { get { return transform; } }
    public string EquippedStyleId { get { return equippedStyle != null ? equippedStyle.styleId : ""; } }
    public PlayerLoadout EquippedLoadout { get; private set; }
    public bool IsAttacking { get { return state != AttackState.Idle; } }
    public int FacingDirection { get { return IsAttacking ? attackFacing : movement.FacingDirection; } }
    public bool CanUseAttacks
    {
        get
        {
            return isActiveAndEnabled && movement != null && movement.isActiveAndEnabled &&
                Health != null && !Health.IsDead && Health.isActiveAndEnabled &&
                (GameManager.Instance == null || !GameManager.Instance.IsPlayerCombatLocked);
        }
    }

    private enum AttackState { Idle, Windup, Recovery }
    private AttackState state;
    private FightingStyle equippedStyle;
    private float timer;
    private float nextAttackTime;
    private int attackFacing = 1;
    private Vector2 aimPosition;
    private CombatHealth aimTarget;
    private bool wasAllowed;

    private void Awake()
    {
        movement = GetComponent<PlayerSpriteController>();
        Health = GetComponent<CombatHealth>();
        Renderer = GetComponent<SpriteRenderer>();
        Health.SetTeam(CombatHealth.CombatTeam.Player);
        Health.Died += HandlePlayerDeath;
        if (punchAttack == null)
            punchAttack = GetComponent<PunchAttack>();
        if (punchAttack == null)
        {
            punchAttack = gameObject.AddComponent<PunchAttack>();
            punchAttack.ConfigureDefaultPunch();
        }
        if (attackAudioSource == null)
            attackAudioSource = gameObject.AddComponent<AudioSource>();
        attackAudioSource.playOnAwake = false;
        attackAudioSource.loop = false;
        attackAudioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        if (!EquipStyle(startingStyleId))
            EquipStyle("punch");
    }

    private void Update()
    {
        if (!CanUseAttacks)
        {
            if (IsAttacking || wasAllowed)
                CancelAttack(true);
            wasAllowed = false;
            return;
        }
        wasAllowed = true;
        if (Time.deltaTime <= 0f)
            return;

        if (state != AttackState.Idle)
        {
            if (equippedStyle == null || equippedStyle.attackBehaviour == null ||
                !equippedStyle.attackBehaviour.isActiveAndEnabled)
            {
                CancelAttack(true);
                return;
            }
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                if (state == AttackState.Windup)
                    ReleaseCurrentAttack();
                else
                {
                    state = AttackState.Idle;
                    movement.ClearAttackPose();
                    equippedStyle.attackBehaviour.FinishAnimation();
                }
            }
        }

        if (WasAttackPressed())
            TryAttack();
    }

    public bool TryAttack()
    {
        if (equippedStyle != null && equippedStyle.styleId == "punch")
        {
            equippedStyle.attackSprite = punchSprite;
            equippedStyle.cooldownSeconds = punchCooldownSeconds;
        }
        if (!CanUseAttacks || Time.deltaTime <= 0f || IsAttacking || Time.time < nextAttackTime ||
            equippedStyle == null || !CanEquip(equippedStyle))
            return false;
        if (equippedStyle.attackSprite == null)
        {
            Debug.LogWarning("Assign the player's attack sprite for style '" + EquippedStyleId + "'.", this);
            return false;
        }

        attackFacing = movement.FacingDirection;
        Physics2D.SyncTransforms();
        aimTarget = FindEnemyInFront(equippedStyle.attackBehaviour.Range);
        aimPosition = aimTarget != null ? aimTarget.AimPosition
            : (Vector2)GetAttackPosition() + Vector2.right * attackFacing * equippedStyle.attackBehaviour.Range;
        state = AttackState.Windup;
        timer = equippedStyle.attackBehaviour.WindupSeconds;
        nextAttackTime = Time.time + Mathf.Max(0f, equippedStyle.cooldownSeconds);
        movement.SetAttackPose(equippedStyle.attackSprite);
        equippedStyle.attackBehaviour.BeginWindup();
        if (state == AttackState.Windup && timer <= 0f && CanUseAttacks)
            ReleaseCurrentAttack();
        return true;
    }

    private void ReleaseCurrentAttack()
    {

        BossAttack attack = equippedStyle.attackBehaviour;
        state = AttackState.Recovery;
        timer = attack.RecoverySeconds;
        attack.Release(this, aimTarget, aimPosition);
    }



    public bool EquipLoadout(PlayerLoadout loadout)
    {
        if (loadout == null || movement == null)
            return false;

        CancelAttack(true);

        if (!string.IsNullOrWhiteSpace(loadout.AttackStyleId) &&
            !EquipStyle(loadout.AttackStyleId))
        {
            return false;
        }

        if (!movement.ApplyOutfit(loadout))
            return false;

        EquippedLoadout = loadout;
        return true;
    }

    public bool CanEquipStyle(string styleId)
    {
        FightingStyle style = FindStyle(styleId);
        return style != null && CanEquip(style);
    }

    public bool EquipStyle(string styleId)
    {
        if (IsAttacking || string.IsNullOrWhiteSpace(styleId))
            return false;
        FightingStyle candidate = FindStyle(styleId);
        if (candidate == null || !CanEquip(candidate))
            return false;
        if (equippedStyle != null && equippedStyle.styleId == candidate.styleId)
            return true;
        ClearWeaponEffects();
        equippedStyle = candidate;

        return true;
    }

    public string[] GetUnlockedStyleIds()
    {
        List<string> ids = new List<string> { "punch" };
        if (fightingStyles != null)
            foreach (FightingStyle style in fightingStyles)
                if (style != null && !string.IsNullOrWhiteSpace(style.styleId) && CanEquip(style) && !ids.Contains(style.styleId))
                    ids.Add(style.styleId);
        return ids.ToArray();
    }

    private FightingStyle FindStyle(string styleId)
    {
        if (string.IsNullOrWhiteSpace(styleId))
            return null;

        string requestedId = styleId.Trim();
        if (string.Equals(requestedId, "punch", StringComparison.OrdinalIgnoreCase))
        {
            return new FightingStyle
            {
                styleId = "punch",
                attackBehaviour = punchAttack,
                attackSprite = punchSprite,
                cooldownSeconds = punchCooldownSeconds,
                requiredBossId = ""
            };
        }

        if (fightingStyles != null)
        {
            foreach (FightingStyle style in fightingStyles)
            {
                if (style != null &&
                    string.Equals(style.styleId, requestedId, StringComparison.OrdinalIgnoreCase))
                {
                    return style;
                }
            }
        }

        return null;
    }

    private bool CanEquip(FightingStyle style)
    {
        return style.attackBehaviour != null && style.attackBehaviour.gameObject == gameObject &&
            style.attackBehaviour.isActiveAndEnabled && style.attackBehaviour.IsConfigured &&
            (string.IsNullOrWhiteSpace(style.requiredBossId) ||
             (GameManager.Instance != null && GameManager.Instance.HasDefeatedBoss(style.requiredBossId)));
    }

    private CombatHealth FindEnemyInFront(float range)
    {
        CombatHealth nearest = null;
        float best = float.PositiveInfinity;
        foreach (CombatHealth enemy in CombatHealth.ActiveCharacters)
        {
            if (!Health.CanDamage(enemy))
                continue;
            Vector2 delta = enemy.AimPosition - Health.AimPosition;
            if (delta.x * attackFacing < 0f || Mathf.Abs(delta.x) > range)
                continue;
            if (delta.sqrMagnitude < best) { best = delta.sqrMagnitude; nearest = enemy; }
        }
        return nearest;
    }

    public Vector3 GetAttackPosition()
    {
        Vector3 offset;

        if (equippedStyle != null && equippedStyle.attackOrigin != null)
        {
            offset = equippedStyle.attackOrigin.position - transform.position;
        }
        else if (equippedStyle != null && equippedStyle.useCustomAttackOriginOffset)
        {
            offset = equippedStyle.attackOriginOffset;
        }
        else if (attackOrigin != null)
        {
            offset = attackOrigin.position - transform.position;
        }
        else
        {
            offset = attackOriginOffset;
        }

        offset.x = Mathf.Abs(offset.x) * FacingDirection;
        return transform.position + offset;
    }

    public void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null && attackAudioSource != null)
            attackAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    public void CancelAttack() { CancelAttack(true); }
    public void CancelAttack(bool clearProjectiles)
    {
        bool wasAttacking = IsAttacking;
        state = AttackState.Idle;
        if (movement != null)
            movement.ClearAttackPose();
        if (wasAttacking && equippedStyle != null && equippedStyle.attackBehaviour != null)
            equippedStyle.attackBehaviour.CancelAttack();
        if (clearProjectiles)
            ClearWeaponEffects();
    }

    private void ClearWeaponEffects()
    {
        foreach (BossAttack attack in GetComponents<BossAttack>())
            attack.ClearActiveEffects();
    }

    private void HandlePlayerDeath(CombatHealth defeated) { CancelAttack(true); }
    private void OnDisable() { CancelAttack(true); }
    private void OnDestroy()
    {
        if (Health != null)
            Health.Died -= HandlePlayerDeath;
    }

    private bool WasAttackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        return keyboard != null && keyboard.fKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F);
#endif
    }
}
