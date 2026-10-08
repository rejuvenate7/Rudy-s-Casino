using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// handles the health of a enemy or player
[DisallowMultipleComponent]
public class CombatHealth : MonoBehaviour
{
    public enum CombatTeam { Player, Enemy, Neutral }
    [Header("Allegiance / NPCs")]
    [SerializeField] private CombatTeam team = CombatTeam.Enemy;
    [SerializeField] private bool disappearOnDeath = true;
    private static readonly HashSet<CombatHealth> activeCharacters = new HashSet<CombatHealth>();
    public static IEnumerable<CombatHealth> ActiveCharacters { get { return activeCharacters; } }
    public CombatTeam Team { get { return team; } }
    [Min(1)][SerializeField] private int maxHealth = 100;
    [Tooltip("Seconds of protection after a successful hit.")]
    [Min(0f)][SerializeField] private float hitInvulnerabilitySeconds = 0.5f;
    [Tooltip("The character's 2D hitbox. Its center is also the default aim point.")]
    [SerializeField] private Collider2D hitbox;
    [SerializeField] private UnityEvent onDamaged = new UnityEvent();
    [SerializeField] private UnityEvent onDied = new UnityEvent();

    public int CurrentHealth { get; private set; }
    public int MaxHealth { get { return Mathf.Max(1, maxHealth); } }
    public bool IsDead { get { return CurrentHealth <= 0; } }
    public bool IsInvulnerable { get; set; }
    public Collider2D Hitbox { get { return hitbox; } }
    public event Action<CombatHealth> Died;

    private float nextDamageTime = float.NegativeInfinity;
    private CombatHitFeedback feedback;

    public Vector2 AimPosition
    {
        get { return hitbox != null ? (Vector2)hitbox.bounds.center : (Vector2)transform.position; }
    }

    private void Awake()
    {
        if (hitbox == null)
            hitbox = GetComponentInChildren<Collider2D>();
        feedback = GetComponent<CombatHitFeedback>();
        if (feedback == null)
            feedback = gameObject.AddComponent<CombatHitFeedback>();
        if (GetComponent<NpcHealthLabel>() == null)
            gameObject.AddComponent<NpcHealthLabel>();
        ResetHealth();
    }

    private void OnEnable() { activeCharacters.Add(this); }
    private void OnDisable() { activeCharacters.Remove(this); }
    public void SetTeam(CombatTeam newTeam) { team = newTeam; }
    public void SetDisappearOnDeath(bool shouldDisappear)
    {
        disappearOnDeath = shouldDisappear;
    }

    public bool CanDamage(CombatHealth victim)
    {
        return victim != null && victim != this && !IsDead && isActiveAndEnabled &&
            !victim.IsDead && victim.isActiveAndEnabled && team != CombatTeam.Neutral &&
            victim.team != CombatTeam.Neutral && team != victim.team;
    }

    public bool TakeDamage(int amount)
    {
        return TakeDamage(amount, AimPosition, null, 1f, null, 2f);
    }

    public bool TakeDamage(int amount, Vector3 hitPosition, AudioClip hitSound,
        float hitVolume, GameObject hitVfx, float hitVfxDuration)
    {
        if (amount <= 0 || IsDead || IsInvulnerable || !isActiveAndEnabled ||
            Time.time < nextDamageTime)
            return false;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        nextDamageTime = Time.time + Mathf.Max(0f, hitInvulnerabilitySeconds);
        bool diedFromThisHit = IsDead;



        if (feedback != null)
            feedback.PlayHit(hitPosition, hitSound, hitVolume, hitVfx, hitVfxDuration);

        if (diedFromThisHit)
        {

            if (Died != null)
                Died(this);
        }

        onDamaged.Invoke();
        if (diedFromThisHit)
        {
            onDied.Invoke();
            if (team == CombatTeam.Enemy && disappearOnDeath)
                Destroy(gameObject);
        }
        return true;
    }

    public void ResetHealth()
    {
        CurrentHealth = MaxHealth;
        nextDamageTime = float.NegativeInfinity;
    }

    public void Heal(int amount)
    {
        if (!IsDead && amount > 0)
            CurrentHealth = (int)Math.Min((long)MaxHealth, (long)CurrentHealth + amount);
    }
}
