using System.Collections.Generic;
using UnityEngine;

// melee attack 
public class MeleeBossAttack : BossAttack
{
    [Min(0)][SerializeField] protected int damage = 10;
    [Tooltip("Maximum vertical separation between the characters' hitbox centers.")]
    [Min(0.01f)][SerializeField] protected float verticalReach = 1.5f;
    [SerializeField] private LayerMask hitLayers = ~0;
    private readonly List<Collider2D> overlaps = new List<Collider2D>(8);

    protected virtual void Reset() { attackRange = 1.3f; }

    protected override void PerformAttack(ICombatActor owner, CombatHealth target,
        Vector2 aimPosition)
    {
        if (owner.Health == null)
            return;
        Physics2D.SyncTransforms();
        Vector2 start = owner.Health.AimPosition;
        Vector2 center = start + Vector2.right * owner.FacingDirection * (Range * 0.5f);
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(hitLayers);
        filter.useTriggers = true;
        overlaps.Clear();
        Physics2D.OverlapBox(center, new Vector2(Range, Mathf.Max(0.01f, verticalReach) * 2f),
            0f, filter, overlaps);


        CombatHealth nearest = null;
        float closest = float.PositiveInfinity;
        foreach (Collider2D collider in overlaps)
        {
            CombatHealth candidate = collider.GetComponentInParent<CombatHealth>();
            if (!owner.Health.CanDamage(candidate))
                continue;
            Vector2 delta = candidate.AimPosition - start;
            if (delta.x * owner.FacingDirection < -0.05f)
                continue;
            if (delta.sqrMagnitude < closest)
            {
                closest = delta.sqrMagnitude;
                nearest = candidate;
            }
        }
        if (nearest != null)
            DealDamage(owner, nearest, damage, nearest.AimPosition);
    }
}
