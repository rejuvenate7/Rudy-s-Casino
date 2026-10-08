using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

// boss attack script for attaching to bosses, to configure attributes about the boss
public abstract class BossAttack : MonoBehaviour
{
    [Header("Attack Timing")]
    [Min(0.05f)][SerializeField] protected float attackRange = 6f;
    [Tooltip("The attack sprite is held this long before the weapon is released.")]
    [Min(0f)][SerializeField] protected float windupSeconds = 0.6f;
    [Tooltip("The same attack sprite stays visible this long after release.")]
    [Min(0f)][SerializeField] protected float recoverySeconds = 0.35f;

    [Header("Attack Sound / Hit Feedback")]
    [Tooltip("Swing or throw sound. Plays once when released, including a miss.")]
    [FormerlySerializedAs("throwSound")]
    [SerializeField] private AudioClip attackSound;
    [FormerlySerializedAs("throwVolume")]
    [Range(0f, 1f)][SerializeField] private float soundVolume = 1f;
    [Tooltip("Sound for a damaging hit. Empty uses the victim's default hit sound.")]
    [FormerlySerializedAs("impactSound")]
    [SerializeField] private AudioClip landedSound;
    [FormerlySerializedAs("impactVolume")]
    [Range(0f, 1f)][SerializeField] private float landedVolume = 1f;
    [SerializeField] private GameObject releaseVfxPrefab;
    [FormerlySerializedAs("impactVfxPrefab")]
    [SerializeField] private GameObject landedVfxPrefab;
    [Min(0.01f)][SerializeField] private float vfxLifetime = 2f;

    [Header("Optional VFX / Custom Script Hooks")]
    [Tooltip("Connect a ParticleSystem.Play or a method on your own VFX component.")]
    [SerializeField] private UnityEvent onWindup = new UnityEvent();
    [SerializeField] private UnityEvent onRelease = new UnityEvent();
    [SerializeField] private UnityEvent onAttackFinished = new UnityEvent();
    [SerializeField] private UnityEvent onCancelled = new UnityEvent();

    public float Range { get { return Mathf.Max(0.05f, attackRange); } }
    public float WindupSeconds { get { return Mathf.Max(0f, windupSeconds); } }
    public virtual float RecoverySeconds { get { return Mathf.Max(0f, recoverySeconds); } }
    public AudioClip LandedSound { get { return landedSound; } }
    public float LandedVolume { get { return Mathf.Clamp01(landedVolume); } }
    public GameObject LandedVfxPrefab { get { return landedVfxPrefab; } }
    public float VfxLifetime { get { return Mathf.Max(0.01f, vfxLifetime); } }

    public virtual bool IsConfigured { get { return true; } }

    public void BeginWindup() { onWindup.Invoke(); }

    public void Release(ICombatActor owner, CombatHealth target, Vector2 aimPosition)
    {
        if (owner == null || owner.ActorTransform == null || !owner.CanUseAttacks)
            return;
        owner.PlaySound(attackSound, Mathf.Clamp01(soundVolume));
        if (releaseVfxPrefab != null)
            Destroy(Instantiate(releaseVfxPrefab, owner.GetAttackPosition(), Quaternion.identity), VfxLifetime);
        PerformAttack(owner, target, aimPosition);
        onRelease.Invoke();
    }

    public void FinishAnimation() { onAttackFinished.Invoke(); }

    public virtual void CancelAttack()
    {
        onCancelled.Invoke();
    }


    public virtual void ClearActiveEffects() { }

    // deal damage to the owner
    protected bool DealDamage(ICombatActor owner, CombatHealth victim, int damage, Vector3 position)
    {
        return owner != null && owner.Health != null && owner.Health.CanDamage(victim) &&
            victim.TakeDamage(damage, position, landedSound, LandedVolume, landedVfxPrefab, VfxLifetime);
    }

    protected abstract void PerformAttack(ICombatActor owner, CombatHealth target,
        Vector2 aimPosition);
}
