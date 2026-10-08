using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;


// tommy gun attack, which configures the attack speed/audio/when the bullet vfx spawns of the gun used in the final boss fight
[AddComponentMenu("Game/Combat/Tommy Gun Burst Attack")]
public class TommyGunBurstAttack : BossAttack
{
    [Header("Tommy Gun Burst")]
    [Min(1)][SerializeField] private int shotsPerBurst = 10;
    [Min(0.02f)][SerializeField] private float secondsBetweenShots = 0.09f;
    [Min(0)][SerializeField] private int damagePerBullet = 10;
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Burst Audio (Played Once)")]
    [Tooltip("One complete Tommy-gun recording played once when the burst starts. It is not repeated for each bullet. Leave the inherited Attack Sound empty to avoid two sounds playing together.")]
    [FormerlySerializedAs("shotSound")]
    [SerializeField] private AudioClip burstSound;
    [FormerlySerializedAs("shotVolume")]
    [Range(0f, 1f)][SerializeField] private float burstVolume = 0.8f;

    [Header("Procedural Bullet VFX")]
    [Min(0.02f)][SerializeField] private float tracerDuration = 0.08f;
    [Min(0.002f)][SerializeField] private float tracerCoreWidth = 0.025f;
    [Min(0.002f)][SerializeField] private float tracerGlowWidth = 0.08f;
    [Min(0.01f)][SerializeField] private float muzzleFlashLength = 0.32f;
    [SerializeField] private Color tracerCoreColor = Color.white;
    [SerializeField] private Color tracerGlowColor = new Color(1f, 0.55f, 0.08f, 0.8f);
    [SerializeField] private Material tracerMaterial;
    [SerializeField] private int sortingOrderOffset = 12;

    [Header("Procedural Muzzle Smoke")]
    [SerializeField] private bool showMuzzleSmoke = true;
    [Range(1, 12)][SerializeField] private int smokeParticlesPerShot = 3;
    [Min(0.05f)][SerializeField] private float smokeLifetime = 0.55f;
    [Min(0f)][SerializeField] private float smokeSpeed = 0.35f;
    [Min(0.01f)][SerializeField] private float smokeStartSize = 0.18f;
    [SerializeField] private Color smokeColor = new Color(0.72f, 0.72f, 0.72f, 0.5f);
    [Tooltip("Optional particle material. Empty uses a built-in fallback material.")]
    [SerializeField] private Material smokeMaterial;

    [Header("Player Hit Pushback")]
    [Tooltip("World-space distance Frank is smoothly pushed by each bullet contact.")]
    [Min(0f)][SerializeField] private float hitPushDistance = 0.12f;
    [Tooltip("Seconds used to apply each hit's push instead of moving Frank instantly.")]
    [Min(0.01f)][SerializeField] private float hitPushDuration = 0.22f;
    [Tooltip("Keep pushback and blood responsive on every bullet contact, even while CombatHealth's brief hit invulnerability prevents repeated health loss.")]
    [SerializeField] private bool reactToEveryBulletContact = true;

    [Header("Blood Splatter On Hit")]
    [SerializeField] private bool showBloodSplatter = true;
    [Tooltip("Optional custom blood VFX prefab. Leave empty to use the built-in procedural blood particles.")]
    [SerializeField] private GameObject bloodVfxPrefab;
    [Tooltip("Offset from the victim's aim point where the blood begins.")]
    [SerializeField] private Vector2 bloodSpawnOffset = Vector2.zero;
    [Range(1, 40)][SerializeField] private int bloodParticleCount = 8;
    [Min(0.05f)][SerializeField] private float bloodLifetime = 0.55f;
    [Min(0f)][SerializeField] private float bloodSpeed = 2.4f;
    [Min(0.01f)][SerializeField] private float bloodParticleSize = 0.11f;
    [SerializeField] private Color bloodColor = new Color(0.72f, 0.015f, 0.02f, 0.95f);
    [SerializeField] private Color darkBloodColor = new Color(0.22f, 0.005f, 0.008f, 0.95f);
    [Tooltip("Optional particle material. Empty uses a built-in sprite material.")]
    [SerializeField] private Material bloodMaterial;
    [SerializeField] private int bloodSortingOrderOffset = 20;

    private Coroutine burstRoutine;
    private Material generatedBloodMaterial;
    private Texture2D generatedBloodTexture;

    public override bool IsConfigured { get { return shotsPerBurst > 0; } }

    public override float RecoverySeconds
    {
        get
        {
            float burstDuration = Mathf.Max(0, shotsPerBurst - 1) *
                                  Mathf.Max(0.02f, secondsBetweenShots);
            return Mathf.Max(base.RecoverySeconds, burstDuration + 0.05f);
        }
    }

    private void Reset()
    {
        attackRange = 12f;
        windupSeconds = 0.25f;
        recoverySeconds = 1f;
    }

    protected override void PerformAttack(
        ICombatActor owner,
        CombatHealth target,
        Vector2 aimPosition
    )
    {
        if (burstRoutine != null)
            StopCoroutine(burstRoutine);
        burstRoutine = StartCoroutine(FireBurst(owner, target, aimPosition));
    }

    private IEnumerator FireBurst(
        ICombatActor owner,
        CombatHealth primaryTarget,
        Vector2 fallbackAimPosition
    )
    {
        int shotCount = Mathf.Max(1, shotsPerBurst);

        if (owner == null || owner.ActorTransform == null || owner.Health == null ||
            !owner.CanUseAttacks)
        {
            burstRoutine = null;
            yield break;
        }



        owner.PlaySound(burstSound, Mathf.Clamp01(burstVolume));

        for (int shot = 0; shot < shotCount; shot++)
        {
            if (owner == null || owner.ActorTransform == null || owner.Health == null ||
                !owner.CanUseAttacks)
            {
                break;
            }

            FireOneBullet(owner, primaryTarget, fallbackAimPosition);

            if (shot + 1 < shotCount)
                yield return new WaitForSeconds(Mathf.Max(0.02f, secondsBetweenShots));
        }

        burstRoutine = null;
    }

    private void FireOneBullet(
        ICombatActor owner,
        CombatHealth primaryTarget,
        Vector2 fallbackAimPosition
    )
    {
        Physics2D.SyncTransforms();

        Vector2 origin = owner.GetAttackPosition();
        float targetX = primaryTarget != null && owner.Health.CanDamage(primaryTarget)
            ? primaryTarget.AimPosition.x
            : fallbackAimPosition.x;
        float horizontalDelta = targetX - origin.x;
        float horizontalDirection = Mathf.Abs(horizontalDelta) > 0.001f
            ? Mathf.Sign(horizontalDelta)
            : owner.FacingDirection;



        Vector2 direction = Vector2.right * horizontalDirection;

        CombatHealth victim = FindBulletVictim(owner, origin, direction);
        Vector2 end = victim != null
            ? new Vector2(victim.AimPosition.x, origin.y)
            : origin + direction * Range;

        SpawnBulletVfx(owner, origin, end);

        if (victim != null)
        {
            bool damageApplied = DealDamage(
                owner,
                victim,
                damagePerBullet,
                end
            );
            if (damageApplied || reactToEveryBulletContact)
            {
                ApplyHitReaction(
                    victim,
                    horizontalDirection,
                    end
                );
            }
        }
    }

    private CombatHealth FindBulletVictim(
        ICombatActor owner,
        Vector2 origin,
        Vector2 direction
    )
    {
        CombatHealth nearest = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (CombatHealth candidate in CombatHealth.ActiveCharacters)
        {
            if (candidate == null || !owner.Health.CanDamage(candidate))
                continue;

            GameObject layerObject = candidate.Hitbox != null
                ? candidate.Hitbox.gameObject
                : candidate.gameObject;
            if ((hitLayers.value & (1 << layerObject.layer)) == 0)
                continue;

            Vector2 toCandidate = candidate.AimPosition - origin;
            float distanceAlongShot = Vector2.Dot(toCandidate, direction);
            if (distanceAlongShot < 0f || distanceAlongShot > Range)
                continue;





            if (distanceAlongShot < nearestDistance)
            {
                nearestDistance = distanceAlongShot;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private void SpawnBulletVfx(
        ICombatActor owner,
        Vector3 origin,
        Vector3 end
    )
    {
        GameObject vfxObject = new GameObject("Tommy Gun Bullet VFX");
        BulletTracerVfx vfx = vfxObject.AddComponent<BulletTracerVfx>();

        int sortingLayerId = owner.Renderer != null
            ? owner.Renderer.sortingLayerID
            : 0;
        int sortingOrder = owner.Renderer != null
            ? owner.Renderer.sortingOrder + sortingOrderOffset
            : sortingOrderOffset;

        vfx.Initialize(
            origin,
            end,
            tracerDuration,
            tracerCoreWidth,
            tracerGlowWidth,
            muzzleFlashLength,
            tracerCoreColor,
            tracerGlowColor,
            sortingLayerId,
            sortingOrder,
            tracerMaterial,
            showMuzzleSmoke,
            smokeParticlesPerShot,
            smokeLifetime,
            smokeSpeed,
            smokeStartSize,
            smokeColor,
            smokeMaterial
        );
    }

    private void ApplyHitReaction(
        CombatHealth victim,
        float shotDirection,
        Vector3 bulletHitPosition)
    {
        if (victim == null)
            return;

        PlayerSpriteController playerController =
            victim.GetComponent<PlayerSpriteController>();
        if (playerController != null && hitPushDistance > 0f)
        {
            playerController.ApplyHorizontalPush(
                shotDirection,
                hitPushDistance,
                hitPushDuration
            );
        }

        if (showBloodSplatter)
            SpawnBloodSplatter(victim, shotDirection, bulletHitPosition);
    }

    private void SpawnBloodSplatter(
        CombatHealth victim,
        float shotDirection,
        Vector3 bulletHitPosition)
    {
        // Use the exact endpoint shared by the bullet tracer and damage hit,
        // rather than the center of Frank's collider.
        Vector3 spawnPosition = bulletHitPosition +
                                (Vector3)bloodSpawnOffset;
        float direction = Mathf.Abs(shotDirection) > 0.001f
            ? Mathf.Sign(shotDirection)
            : 1f;

        if (bloodVfxPrefab != null)
        {
            Quaternion rotation = Quaternion.Euler(
                0f,
                0f,
                direction < 0f ? 180f : 0f
            );
            GameObject customBlood = Instantiate(
                bloodVfxPrefab,
                spawnPosition,
                rotation
            );
            Destroy(customBlood, Mathf.Max(0.05f, bloodLifetime));
            return;
        }

        GameObject bloodObject = new GameObject("Tommy Gun Blood Splatter");
        bloodObject.transform.position = spawnPosition;
        ParticleSystem blood = bloodObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = blood.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = Mathf.Max(0.05f, bloodLifetime);
        main.startSpeed = 0f;
        main.startSize = Mathf.Max(0.01f, bloodParticleSize);
        main.startColor = bloodColor;
        main.maxParticles = Mathf.Max(1, bloodParticleCount);

        ParticleSystem.EmissionModule emission = blood.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = blood.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer =
            bloodObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        particleRenderer.lengthScale = 1.4f;
        particleRenderer.velocityScale = 0.12f;
        particleRenderer.cameraVelocityScale = 0f;
        SpriteRenderer victimRenderer = victim.GetComponentInChildren<SpriteRenderer>();
        if (victimRenderer != null)
        {
            particleRenderer.sortingLayerID = victimRenderer.sortingLayerID;
            particleRenderer.sortingOrder = victimRenderer.sortingOrder +
                                            bloodSortingOrderOffset;
        }
        particleRenderer.sharedMaterial = bloodMaterial != null
            ? bloodMaterial
            : GetOrCreateBloodMaterial();

        int count = Mathf.Max(1, bloodParticleCount);
        float lifetime = Mathf.Max(0.05f, bloodLifetime);
        float speed = Mathf.Max(0f, bloodSpeed);
        float size = Mathf.Max(0.01f, bloodParticleSize);
        Vector3 forward = Vector3.right * direction;

        blood.Play(false);
        for (int i = 0; i < count; i++)
        {
            Color particleColor = Color.Lerp(
                darkBloodColor,
                bloodColor,
                Random.Range(0.25f, 1f)
            );
            ParticleSystem.EmitParams droplet = new ParticleSystem.EmitParams
            {
                position = spawnPosition + new Vector3(
                    Random.Range(-size * 0.25f, size * 0.25f),
                    Random.Range(-size * 0.35f, size * 0.35f),
                    0f
                ),
                velocity = forward * Random.Range(speed * 0.45f, speed) +
                           Vector3.up * Random.Range(-speed * 0.55f, speed * 0.65f),
                startLifetime = lifetime * Random.Range(0.7f, 1.15f),
                startSize = size * Random.Range(0.55f, 1.4f),
                startColor = particleColor
            };
            blood.Emit(droplet, 1);
        }

        Destroy(bloodObject, lifetime * 1.25f + 0.1f);
    }

    private Material GetOrCreateBloodMaterial()
    {
        if (generatedBloodMaterial != null)
            return generatedBloodMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (shader == null)
            return null;

        generatedBloodMaterial = new Material(shader)
        {
            name = "Runtime Blood Splatter Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        generatedBloodTexture = CreateBloodParticleTexture();
        if (generatedBloodTexture != null)
            generatedBloodMaterial.mainTexture = generatedBloodTexture;
        return generatedBloodMaterial;
    }

    private static Texture2D CreateBloodParticleTexture()
    {
        const int textureSize = 32;
        Texture2D texture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false
        )
        {
            name = "Runtime Blood Droplet Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float normalizedX = ((x + 0.5f) / textureSize) * 2f - 1f;
                float normalizedY = ((y + 0.5f) / textureSize) * 2f - 1f;
                float distance = Mathf.Sqrt(
                    normalizedX * normalizedX + normalizedY * normalizedY
                );
                float alpha = Mathf.Clamp01((1f - distance) / 0.18f);
                pixels[y * textureSize + x] = new Color32(
                    255,
                    255,
                    255,
                    (byte)Mathf.RoundToInt(alpha * 255f)
                );
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    public override void CancelAttack()
    {
        if (burstRoutine != null)
        {
            StopCoroutine(burstRoutine);
            burstRoutine = null;
        }
        base.CancelAttack();
    }

    public override void ClearActiveEffects()
    {
        if (burstRoutine != null)
        {
            StopCoroutine(burstRoutine);
            burstRoutine = null;
        }
    }

    private void OnDestroy()
    {
        if (generatedBloodMaterial != null)
            Destroy(generatedBloodMaterial);
        if (generatedBloodTexture != null)
            Destroy(generatedBloodTexture);
    }
}
