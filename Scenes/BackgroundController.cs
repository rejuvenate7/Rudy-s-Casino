using UnityEngine;

// controls the background of each level/entire game
public class BackgroundController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private Camera targetCamera;

    [Header("Bars Outside Background")]
    [Tooltip("Forces the unused camera area around a fitted background to use a solid color.")]
    [SerializeField] private bool useSolidColorBars = true;

    [SerializeField] private Color barColor = Color.black;

    public Sprite CurrentBackground
    {
        get
        {
            return backgroundRenderer != null ? backgroundRenderer.sprite : null;
        }
    }

    private void Awake()
    {
        if (backgroundRenderer == null)
            backgroundRenderer = GetComponent<SpriteRenderer>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        ApplyBarAppearance();
    }

    private void Start()
    {
        GameManager.Instance.RegisterBackground(this);
        FitBackgroundToCamera();
    }

    public void SetBackground(Sprite newBackground)
    {
        if (newBackground == null)
            return;

        backgroundRenderer.sprite = newBackground;
        FitBackgroundToCamera();
    }

    // auto fit the background to the camera depending on what resolution sprite was supplied
    private void FitBackgroundToCamera()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        ApplyBarAppearance();

        if (targetCamera == null || !targetCamera.orthographic || backgroundRenderer.sprite == null)
        {
            return;
        }

        backgroundRenderer.transform.localScale = Vector3.one;

        float cameraHeight = targetCamera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * targetCamera.aspect;

        Vector2 spriteSize = backgroundRenderer.sprite.bounds.size;

        float widthScale = cameraWidth / spriteSize.x;
        float heightScale = cameraHeight / spriteSize.y;

        float requiredScale = Mathf.Min(widthScale, heightScale);

        backgroundRenderer.transform.localScale = new Vector3(requiredScale, requiredScale, 1f);

        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 backgroundPosition = backgroundRenderer.transform.position;

        backgroundRenderer.transform.position = new Vector3(
            cameraPosition.x,
            cameraPosition.y,
            backgroundPosition.z
        );
    }

    private void ApplyBarAppearance()
    {
        if (!useSolidColorBars || targetCamera == null)
            return;

        targetCamera.clearFlags = CameraClearFlags.SolidColor;
        targetCamera.backgroundColor = barColor;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.UnregisterBackground(this);
    }
}
