using System.Collections.Generic;
using UnityEngine;


// lightning vfx for taser weapon
[DisallowMultipleComponent]
public class LightningArcVfx : MonoBehaviour
{
    private LineRenderer glowLine;
    private LineRenderer coreLine;
    private readonly List<LineRenderer> branches = new List<LineRenderer>();
    private Vector3 startPoint;
    private Vector3 endPoint;
    private float remainingLifetime;
    private float totalLifetime;
    private float flickerInterval;
    private float flickerTimer;
    private float jaggedness;
    private float branchLength;
    private int segmentCount;
    private int branchCount;
    private Color coreColor;
    private Color glowColor;
    private Material generatedMaterial;
    private bool initialized;

    public void Initialize(
        Vector3 start,
        Vector3 end,
        float lifetime,
        int segments,
        float boltJaggedness,
        float secondsPerFlicker,
        float coreWidth,
        float glowWidth,
        Color boltCoreColor,
        Color boltGlowColor,
        int forks,
        float forkLength,
        int sortingLayerId,
        int sortingOrder,
        Material suppliedCoreMaterial,
        Material suppliedGlowMaterial
    )
    {
        startPoint = start;
        endPoint = end;
        totalLifetime = Mathf.Max(0.02f, lifetime);
        remainingLifetime = totalLifetime;
        segmentCount = Mathf.Clamp(segments, 4, 64);
        jaggedness = Mathf.Max(0f, boltJaggedness);
        flickerInterval = Mathf.Max(0.01f, secondsPerFlicker);
        branchCount = Mathf.Clamp(forks, 0, 8);
        branchLength = Mathf.Clamp(forkLength, 0.05f, 0.8f);
        coreColor = boltCoreColor;
        glowColor = boltGlowColor;

        Material fallbackMaterial = null;
        if (suppliedCoreMaterial == null || suppliedGlowMaterial == null)
            fallbackMaterial = CreateFallbackMaterial();

        glowLine = CreateLine(
            "Lightning Glow",
            Mathf.Max(0.005f, glowWidth),
            suppliedGlowMaterial != null ? suppliedGlowMaterial : fallbackMaterial,
            sortingLayerId,
            sortingOrder
        );
        coreLine = CreateLine(
            "Lightning Core",
            Mathf.Max(0.005f, coreWidth),
            suppliedCoreMaterial != null ? suppliedCoreMaterial : fallbackMaterial,
            sortingLayerId,
            sortingOrder + 1
        );

        float forkWidth = Mathf.Max(0.003f, coreWidth * 0.6f);
        for (int i = 0; i < branchCount; i++)
        {
            branches.Add(CreateLine(
                "Lightning Branch " + (i + 1),
                forkWidth,
                suppliedCoreMaterial != null ? suppliedCoreMaterial : fallbackMaterial,
                sortingLayerId,
                sortingOrder + 1
            ));
        }

        initialized = true;
        RebuildBolt();
        ApplyFade(1f);
    }

    private void Update()
    {
        if (!initialized)
            return;

        if (Time.deltaTime <= 0f)
            return;

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        flickerTimer -= Time.deltaTime;
        if (flickerTimer <= 0f)
            RebuildBolt();


        float normalizedLife = remainingLifetime / totalLifetime;
        ApplyFade(Mathf.Clamp01(normalizedLife / 0.35f));
    }

    private void RebuildBolt()
    {
        flickerTimer = flickerInterval;

        Vector3[] points = new Vector3[segmentCount + 1];
        Vector3 boltVector = endPoint - startPoint;
        Vector3 perpendicular = new Vector3(-boltVector.y, boltVector.x, 0f).normalized;

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            float endpointDamping = Mathf.Sin(t * Mathf.PI);
            float displacement = Random.Range(-jaggedness, jaggedness) * endpointDamping;
            points[i] = Vector3.Lerp(startPoint, endPoint, t) + perpendicular * displacement;
        }

        SetLinePoints(glowLine, points);
        SetLinePoints(coreLine, points);
        RebuildBranches(points, perpendicular);
    }

    private void RebuildBranches(Vector3[] mainPoints, Vector3 perpendicular)
    {
        float mainLength = Vector3.Distance(startPoint, endPoint);

        for (int i = 0; i < branches.Count; i++)
        {
            LineRenderer branch = branches[i];
            int startIndex = Random.Range(1, Mathf.Max(2, mainPoints.Length - 2));
            Vector3 branchStart = mainPoints[startIndex];
            float side = Random.value < 0.5f ? -1f : 1f;
            float length = mainLength * branchLength * Random.Range(0.45f, 1f);
            Vector3 forward = (endPoint - startPoint).normalized;
            Vector3 branchEnd = branchStart +
                (forward * Random.Range(0.15f, 0.5f) + perpendicular * side).normalized * length;

            const int branchSegments = 5;
            Vector3[] branchPoints = new Vector3[branchSegments + 1];
            for (int point = 0; point <= branchSegments; point++)
            {
                float t = point / (float)branchSegments;
                float taperingJitter = (1f - t) * jaggedness * 0.55f;
                branchPoints[point] = Vector3.Lerp(branchStart, branchEnd, t) +
                    perpendicular * Random.Range(-taperingJitter, taperingJitter);
            }
            SetLinePoints(branch, branchPoints);
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
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.startWidth = width;
        line.endWidth = width * 0.65f;
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
            name = "Runtime Lightning Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return generatedMaterial;
    }

    private static void SetLinePoints(LineRenderer line, Vector3[] points)
    {
        if (line == null)
            return;
        line.positionCount = points.Length;
        line.SetPositions(points);
    }

    private void ApplyFade(float alpha)
    {
        SetLineColor(glowLine, WithAlpha(glowColor, glowColor.a * alpha));
        SetLineColor(coreLine, WithAlpha(coreColor, coreColor.a * alpha));
        Color branchColor = WithAlpha(coreColor, coreColor.a * alpha * 0.8f);
        foreach (LineRenderer branch in branches)
            SetLineColor(branch, branchColor);
    }

    private static void SetLineColor(LineRenderer line, Color color)
    {
        if (line == null)
            return;
        line.startColor = color;
        line.endColor = WithAlpha(color, 0f);
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
    }
}
