using System.Collections.Generic;
using UnityEngine;


// taser lightning attack, for the butler boss fight and later player unlocks
[AddComponentMenu("Game/Combat/Taser Lightning Attack")]
public class TaserLightningAttack : BossAttack
{
    [Header("Taser Damage")]
    [Min(0)][SerializeField] private int damage = 15;
    [Tooltip("Forgiveness around the center of the lightning ray, in world units.")]
    [Min(0.01f)][SerializeField] private float hitRadius = 0.35f;
    [Tooltip("Only opponents on these layers can be hit.")]
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Procedural Lightning VFX")]
    [Tooltip("How long the animated bolt remains visible after the taser fires.")]
    [Min(0.02f)][SerializeField] private float lightningDuration = 0.22f;
    [Range(4, 64)][SerializeField] private int lightningSegments = 20;
    [Tooltip("Maximum sideways displacement of the animated bolt.")]
    [Min(0f)][SerializeField] private float jaggedness = 0.22f;
    [Tooltip("Seconds between new randomized lightning shapes.")]
    [Min(0.01f)][SerializeField] private float flickerInterval = 0.025f;
    [Min(0.005f)][SerializeField] private float coreWidth = 0.045f;
    [Min(0.005f)][SerializeField] private float glowWidth = 0.18f;
    [SerializeField] private Color coreColor = Color.white;
    [SerializeField] private Color glowColor = new Color(0.15f, 0.75f, 1f, 0.8f);
    [Range(0, 8)][SerializeField] private int branchCount = 3;
    [Range(0.05f, 0.8f)][SerializeField] private float branchLength = 0.28f;
    [SerializeField] private int sortingOrderOffset = 10;

    [Header("Optional Lightning Materials")]
    [Tooltip("Optional additive LineRenderer material. Empty uses a generated built-in material.")]
    [SerializeField] private Material coreMaterial;
    [Tooltip("Optional additive glow material. Empty reuses the core/default material.")]
    [SerializeField] private Material glowMaterial;

    private readonly List<LightningArcVfx> activeBolts = new List<LightningArcVfx>();

    public override bool IsConfigured { get { return true; } }

    private void Reset()
    {
        attackRange = 6f;
        windupSeconds = 0.45f;
        recoverySeconds = 0.3f;
    }

    protected override void PerformAttack(
        ICombatActor owner,
        CombatHealth target,
        Vector2 aimPosition
    )
    {
        if (owner == null || owner.ActorTransform == null || owner.Health == null)
            return;

        Physics2D.SyncTransforms();

        Vector2 origin = owner.GetAttackPosition();
        Vector2 direction = aimPosition - origin;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.right * owner.FacingDirection;
        direction.Normalize();

        CombatHealth victim = FindZapVictim(owner, target, origin, direction);
        Vector2 end = victim != null
            ? victim.AimPosition
            : origin + direction * Range;

        SpawnLightning(owner, origin, end);

        if (victim != null)
            DealDamage(owner, victim, damage, end);
    }

    private CombatHealth FindZapVictim(
        ICombatActor owner,
        CombatHealth preferredTarget,
        Vector2 origin,
        Vector2 direction
    )
    {
        if (IsValidVictim(owner, preferredTarget, origin, direction))
            return preferredTarget;

        CombatHealth nearest = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (CombatHealth candidate in CombatHealth.ActiveCharacters)
        {
            if (!IsValidVictim(owner, candidate, origin, direction))
                continue;

            float distanceAlongBolt = Vector2.Dot(
                candidate.AimPosition - origin,
                direction
            );
            if (distanceAlongBolt < nearestDistance)
            {
                nearestDistance = distanceAlongBolt;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private bool IsValidVictim(
        ICombatActor owner,
        CombatHealth candidate,
        Vector2 origin,
        Vector2 direction
    )
    {
        if (candidate == null || !owner.Health.CanDamage(candidate))
            return false;

        GameObject layerObject = candidate.Hitbox != null
            ? candidate.Hitbox.gameObject
            : candidate.gameObject;
        if ((hitLayers.value & (1 << layerObject.layer)) == 0)
            return false;

        Vector2 toCandidate = candidate.AimPosition - origin;
        float distanceAlongBolt = Vector2.Dot(toCandidate, direction);
        if (distanceAlongBolt < 0f || distanceAlongBolt > Range)
            return false;

        Vector2 closestPoint = origin + direction * distanceAlongBolt;
        float allowedRadius = Mathf.Max(0.01f, hitRadius);
        if (candidate.Hitbox != null)
        {
            Vector2 extents = candidate.Hitbox.bounds.extents;
            allowedRadius += Mathf.Min(extents.x, extents.y);
        }

        return Vector2.Distance(closestPoint, candidate.AimPosition) <= allowedRadius;
    }

    private void SpawnLightning(
        ICombatActor owner,
        Vector3 origin,
        Vector3 end
    )
    {
        GameObject boltObject = new GameObject("Taser Lightning VFX");
        LightningArcVfx bolt = boltObject.AddComponent<LightningArcVfx>();

        int sortingLayerId = owner.Renderer != null
            ? owner.Renderer.sortingLayerID
            : 0;
        int sortingOrder = owner.Renderer != null
            ? owner.Renderer.sortingOrder + sortingOrderOffset
            : sortingOrderOffset;

        bolt.Initialize(
            origin,
            end,
            lightningDuration,
            lightningSegments,
            jaggedness,
            flickerInterval,
            coreWidth,
            glowWidth,
            coreColor,
            glowColor,
            branchCount,
            branchLength,
            sortingLayerId,
            sortingOrder,
            coreMaterial,
            glowMaterial
        );

        activeBolts.RemoveAll(item => item == null);
        activeBolts.Add(bolt);
    }

    public override void ClearActiveEffects()
    {
        foreach (LightningArcVfx bolt in activeBolts)
        {
            if (bolt != null)
                Destroy(bolt.gameObject);
        }
        activeBolts.Clear();
    }
}
