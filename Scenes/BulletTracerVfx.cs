using UnityEngine;


[DisallowMultipleComponent]
// for spawning the bullet vfx for the bob boss fight, includes tracer, muzzle flash and smoke effect
public class BulletTracerVfx : MonoBehaviour
{
    private LineRenderer glowLine;
    private LineRenderer coreLine;
    private LineRenderer muzzleFlashLine;
    private float remainingLifetime;
    private float totalLifetime;
    private float tracerLifetime;
    private Color coreColor;
    private Color glowColor;
    private Material generatedMaterial;
    private Material generatedSmokeMaterial;
    private bool initialized;

    public void Initialize(
        Vector3 start,
        Vector3 end,
        float lifetime,
        float coreWidth,
        float glowWidth,
        float muzzleFlashLength,
        Color bulletCoreColor,
        Color bulletGlowColor,
        int sortingLayerId,
        int sortingOrder,
        Material suppliedMaterial,
        bool showMuzzleSmoke,
        int smokeParticleCount,
        float smokeLifetime,
        float smokeSpeed,
        float smokeStartSize,
        Color smokeColor,
        Material suppliedSmokeMaterial
    )
    {
        tracerLifetime = Mathf.Max(0.02f, lifetime);
        totalLifetime = showMuzzleSmoke
            ? Mathf.Max(tracerLifetime, Mathf.Max(0.05f, smokeLifetime) + 0.15f)
            : tracerLifetime;
        remainingLifetime = totalLifetime;
        coreColor = bulletCoreColor;
        glowColor = bulletGlowColor;

        Material material = suppliedMaterial != null
            ? suppliedMaterial
            : CreateFallbackMaterial();

        glowLine = CreateLine(
            "Bullet Tracer Glow",
            Mathf.Max(0.002f, glowWidth),
            material,
            sortingLayerId,
            sortingOrder
        );
        coreLine = CreateLine(
            "Bullet Tracer Core",
            Mathf.Max(0.002f, coreWidth),
            material,
            sortingLayerId,
            sortingOrder + 1
        );
        muzzleFlashLine = CreateLine(
            "Muzzle Flash",
            Mathf.Max(0.004f, glowWidth * 1.6f),
            material,
            sortingLayerId,
            sortingOrder + 2
        );

        SetPoints(glowLine, start, end);
        SetPoints(coreLine, start, end);

        Vector3 direction = (end - start).sqrMagnitude > 0.0001f
            ? (end - start).normalized
            : Vector3.right;
        SetPoints(
            muzzleFlashLine,
            start - direction * Mathf.Max(0.01f, muzzleFlashLength) * 0.15f,
            start + direction * Mathf.Max(0.01f, muzzleFlashLength)
        );

        if (showMuzzleSmoke)
        {
            CreateMuzzleSmoke(
                start,
                direction,
                smokeParticleCount,
                smokeLifetime,
                smokeSpeed,
                smokeStartSize,
                smokeColor,
                sortingLayerId,
                sortingOrder + 3,
                suppliedSmokeMaterial
            );
        }

        initialized = true;
        ApplyFade(1f);
    }

    private void Update()
    {
        if (!initialized || Time.deltaTime <= 0f)
            return;

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float elapsed = totalLifetime - remainingLifetime;
        ApplyFade(Mathf.Clamp01(1f - elapsed / tracerLifetime));
    }

    private void CreateMuzzleSmoke(
        Vector3 start,
        Vector3 direction,
        int particleCount,
        float lifetime,
        float speed,
        float startSize,
        Color color,
        int sortingLayerId,
        int sortingOrder,
        Material suppliedMaterial
    )
    {
        GameObject smokeObject = new GameObject("Muzzle Smoke");
        smokeObject.transform.SetParent(transform, false);
        smokeObject.transform.position = start;

        ParticleSystem smoke = smokeObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = smoke.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = Mathf.Max(0.05f, lifetime);
        main.startSpeed = 0f;
        main.startSize = Mathf.Max(0.01f, startSize);
        main.startColor = color;
        main.maxParticles = Mathf.Max(1, particleCount);

        ParticleSystem.EmissionModule emission = smoke.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = smoke.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer =
            smokeObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingLayerID = sortingLayerId;
        particleRenderer.sortingOrder = sortingOrder;
        particleRenderer.sharedMaterial = suppliedMaterial != null
            ? suppliedMaterial
            : CreateFallbackSmokeMaterial();

        Vector3 forward = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.right;
        Vector3 sideways = new Vector3(-forward.y, forward.x, 0f);
        int count = Mathf.Max(1, particleCount);
        float baseLifetime = Mathf.Max(0.05f, lifetime);
        float baseSize = Mathf.Max(0.01f, startSize);
        float baseSpeed = Mathf.Max(0f, speed);

        smoke.Play(false);
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams puff = new ParticleSystem.EmitParams
            {
                position = start + forward * Random.Range(0f, baseSize * 0.4f),
                velocity = forward * Random.Range(baseSpeed * 0.35f, baseSpeed) +
                           sideways * Random.Range(-baseSpeed * 0.3f, baseSpeed * 0.3f) +
                           Vector3.up * Random.Range(baseSpeed * 0.1f, baseSpeed * 0.5f),
                startLifetime = baseLifetime * Random.Range(0.8f, 1.2f),
                startSize = baseSize * Random.Range(0.75f, 1.35f),
                startColor = color
            };
            smoke.Emit(puff, 1);
        }
    }

    private LineRenderer CreateLine(
        string objectName,
        float width,
        Material material,
        int sortingLayerId,
        int sortingOrder
    )
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 3;
        line.startWidth = width;
        line.endWidth = width * 0.5f;
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = sortingOrder;
        if (material != null)
            line.sharedMaterial = material;
        return line;
    }

    private Material CreateFallbackMaterial()
    {
        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        generatedMaterial = new Material(shader)
        {
            name = "Runtime Bullet Tracer Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return generatedMaterial;
    }

    private Material CreateFallbackSmokeMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (shader == null)
            return null;

        generatedSmokeMaterial = new Material(shader)
        {
            name = "Runtime Muzzle Smoke Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return generatedSmokeMaterial;
    }

    private static void SetPoints(LineRenderer line, Vector3 start, Vector3 end)
    {
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private void ApplyFade(float alpha)
    {
        SetColor(glowLine, WithAlpha(glowColor, glowColor.a * alpha));
        SetColor(coreLine, WithAlpha(coreColor, coreColor.a * alpha));
        SetColor(muzzleFlashLine, WithAlpha(glowColor, glowColor.a * alpha));
    }

    private static void SetColor(LineRenderer line, Color color)
    {
        if (line == null)
            return;
        line.startColor = color;
        line.endColor = WithAlpha(color, color.a * 0.25f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private void OnDestroy()
    {
        if (generatedMaterial != null)
            Destroy(generatedMaterial);
        if (generatedSmokeMaterial != null)
            Destroy(generatedSmokeMaterial);
    }
}
