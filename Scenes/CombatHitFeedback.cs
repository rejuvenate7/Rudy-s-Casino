using UnityEngine;

[DisallowMultipleComponent]
// for handling what happens when a player/enemy gets hit, uses the white flash shader 
public class CombatHitFeedback : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [Tooltip("shader.")]
    [SerializeField] private Shader whiteFlashShader;
    [Min(0f)][SerializeField] private float flashSeconds = 0.2f;
    [SerializeField] private AudioClip defaultLandedSound;
    [Range(0f, 1f)][SerializeField] private float defaultSoundVolume = 1f;
    [SerializeField] private GameObject defaultHitVfxPrefab;
    [Min(0.01f)][SerializeField] private float defaultVfxLifetime = 2f;

    private Material flashMaterial;
    private Material previousMaterial;
    private bool flashing;
    private float flashEnds;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void PlayHit(Vector3 position, AudioClip overrideSound, float volume,
        GameObject overrideVfx, float vfxLifetime)
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SpriteRenderer>();
        AudioClip clip = overrideSound != null ? overrideSound : defaultLandedSound;
        float soundVolume = overrideSound != null ? volume : defaultSoundVolume;
        if (clip != null)
        {
            GameObject soundObject = new GameObject("Landed hit sound");
            soundObject.transform.position = position;
            AudioSource source = soundObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.PlayOneShot(clip, Mathf.Clamp01(soundVolume));
            Destroy(soundObject, clip.length + 0.1f);
        }

        GameObject vfx = overrideVfx != null ? overrideVfx : defaultHitVfxPrefab;
        if (vfx != null)
            Destroy(Instantiate(vfx, position, Quaternion.identity),
                Mathf.Max(0.01f, overrideVfx != null ? vfxLifetime : defaultVfxLifetime));

        if (targetRenderer == null || flashSeconds <= 0f)
            return;
        if (flashMaterial == null)
        {
            if (whiteFlashShader == null)
                whiteFlashShader = Shader.Find("Custom/CombatWhiteFlash");
            if (whiteFlashShader == null)
                return;
            flashMaterial = new Material(whiteFlashShader);
        }
        if (!flashing)
            previousMaterial = targetRenderer.sharedMaterial;
        targetRenderer.sharedMaterial = flashMaterial;
        flashing = true;
        flashEnds = Time.time + flashSeconds;
    }

    private void Update()
    {
        if (flashing && Time.time >= flashEnds)
            RestoreMaterial();
    }

    private void RestoreMaterial()
    {
        if (flashing && targetRenderer != null && targetRenderer.sharedMaterial == flashMaterial)
            targetRenderer.sharedMaterial = previousMaterial;
        flashing = false;
    }

    private void OnDisable() { RestoreMaterial(); }
    private void OnDestroy()
    {
        RestoreMaterial();
        if (flashMaterial != null)
            Destroy(flashMaterial);
    }
}
