using System.Collections.Generic;
using UnityEngine;

// platter throw attack, for the murr boss fight and later given to the player
public class PlatterThrowAttack : BossAttack
{
    [Header("Platter")]
    [SerializeField] private Sprite platterSprite;
    [Tooltip("Rendered width in world units; height keeps the image's aspect ratio.")]
    [Min(0.01f)][SerializeField] private float platterWidth = 1f;
    [Min(0.01f)][SerializeField] private float projectileSpeed = 7f;
    [Min(0.01f)][SerializeField] private float hitRadius = 0.15f;
    [Min(0)][SerializeField] private int damage = 10;
    [Min(0.01f)][SerializeField] private float projectileLifetime = 4f;
    [SerializeField] private float spinDegreesPerSecond = 360f;
    [Tooltip("Include opponents' Collider2D layers. Same-team characters are ignored.")]
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private int sortingOrderOffset = 1;

    private readonly List<BossProjectile> projectiles = new List<BossProjectile>();

    public override bool IsConfigured { get { return platterSprite != null; } }

    protected override void PerformAttack(ICombatActor owner, CombatHealth target,
        Vector2 aimPosition)
    {
        if (platterSprite == null)
            return;

        Vector3 origin = owner.GetAttackPosition();
        Vector2 direction = aimPosition - (Vector2)origin;
        GameObject platter = new GameObject("Thrown platter");
        platter.transform.position = origin;
        float scale = Mathf.Max(0.01f, platterWidth) /
                      Mathf.Max(0.001f, platterSprite.bounds.size.x);
        platter.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = platter.AddComponent<SpriteRenderer>();
        renderer.sprite = platterSprite;
        renderer.sortingLayerID = owner.Renderer.sortingLayerID;
        renderer.sortingOrder = owner.Renderer.sortingOrder + sortingOrderOffset;

        BossProjectile projectile = platter.AddComponent<BossProjectile>();
        projectile.Initialize(owner, direction, projectileSpeed, hitRadius, damage,
            projectileLifetime, spinDegreesPerSecond, hitLayers, LandedSound, LandedVolume,
            LandedVfxPrefab, VfxLifetime);
        projectiles.RemoveAll(item => item == null);
        projectiles.Add(projectile);

    }

    public override void ClearActiveEffects()
    {
        foreach (BossProjectile projectile in projectiles)
            if (projectile != null)
                Destroy(projectile.gameObject);
        projectiles.Clear();
    }
}
