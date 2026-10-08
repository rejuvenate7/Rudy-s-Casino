using System.Collections.Generic;
using UnityEngine;

// used for spawning boss projectiles
public class BossProjectile : MonoBehaviour
{
    private ICombatActor owner;
    private Vector2 direction;
    private float speed;
    private float radius;
    private int damage;
    private float lifetime;
    private float spin;
    private bool initialized;
    private AudioClip hitSound;
    private float hitVolume;
    private GameObject hitVfx;
    private float hitVfxLifetime;
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(8);
    private ContactFilter2D contactFilter;

    public void Initialize(ICombatActor source, Vector2 travelDirection,
        float travelSpeed, float hitRadius, int hitDamage, float secondsToLive,
        float spinDegreesPerSecond, LayerMask hitLayers, AudioClip impactSound,
        float impactVolume, GameObject impactVfx, float impactVfxDuration)
    {
        owner = source;
        direction = travelDirection.sqrMagnitude > 0.0001f
            ? travelDirection.normalized : Vector2.right * source.FacingDirection;
        speed = Mathf.Max(0.01f, travelSpeed);
        radius = Mathf.Max(0.01f, hitRadius);
        damage = Mathf.Max(0, hitDamage);
        lifetime = Mathf.Max(0.01f, secondsToLive);
        spin = spinDegreesPerSecond;
        hitSound = impactSound;
        hitVolume = Mathf.Clamp01(impactVolume);
        hitVfx = impactVfx;
        hitVfxLifetime = Mathf.Max(0.01f, impactVfxDuration);
        contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(hitLayers);
        contactFilter.useTriggers = true;
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized)
            return;
        if (owner == null || owner.ActorTransform == null || owner.Health == null ||
            !owner.CanUseAttacks)
        {
            Destroy(gameObject);
            return;
        }
        if (Time.deltaTime <= 0f)
            return;

        float stepTime = Mathf.Min(Time.deltaTime, lifetime);
        float distance = speed * stepTime;
        Vector2 start = transform.position;



        Physics2D.SyncTransforms();
        hits.Clear();
        Physics2D.CircleCast(start, radius, direction, contactFilter, hits, distance);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
                continue;
            CombatHealth target = hit.collider.GetComponentInParent<CombatHealth>();
            if (!owner.Health.CanDamage(target))
                continue;

            Vector3 impactPosition = new Vector3(hit.point.x, hit.point.y, transform.position.z);
            target.TakeDamage(damage, impactPosition, hitSound, hitVolume, hitVfx, hitVfxLifetime);

            Destroy(gameObject);
            return;
        }

        transform.position += (Vector3)(direction * distance);
        transform.Rotate(0f, 0f, spin * stepTime);
        lifetime -= stepTime;
        if (lifetime <= 0f)
            Destroy(gameObject);
    }

}
