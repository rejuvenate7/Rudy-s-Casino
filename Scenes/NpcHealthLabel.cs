using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]

// the health nametag below npc and boss name tag above them
public class NpcHealthLabel : MonoBehaviour
{
    [Header("Health Text")]
    [Tooltip("Disable this for an undefeatable boss while keeping its name visible.")]
    [SerializeField] private bool showHealthText = true;
    [SerializeField] private Vector2 worldOffset = new Vector2(0f, -0.25f);
    [Min(10)][SerializeField] private int fontSize = 26;
    [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.85f);

    [Header("Boss Name")]
    [SerializeField] private bool showBossName = true;

    [Tooltip("Name shown above this boss. Leave empty to use the Boss Controller's Boss Id.")]
    [SerializeField] private string bossDisplayName;

    [Tooltip("World-space offset from the top of the boss hitbox or sprite.")]
    [SerializeField] private Vector2 bossNameWorldOffset = new Vector2(0f, 0.35f);

    [Min(10)][SerializeField] private int bossNameFontSize = 32;
    [Min(60f)][SerializeField] private float bossNameBoxWidth = 320f;
    [SerializeField] private Color bossNameTextColor = Color.white;
    [SerializeField] private FontStyle bossNameFontStyle = FontStyle.Bold;

    [Header("Boss Name Outline")]
    [FormerlySerializedAs("showBossNameShadow")]
    [SerializeField] private bool showBossNameOutline = true;

    [Tooltip("Outline thickness in screen pixels.")]
    [Range(1, 6)]
    [SerializeField] private int bossNameOutlineThickness = 2;

    [FormerlySerializedAs("bossNameShadowColor")]
    [SerializeField] private Color bossNameOutlineColor = new Color(0f, 0f, 0f, 0.95f);

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    private CombatHealth health;
    private BossController bossController;
    private SpriteRenderer spriteRenderer;
    private GUIStyle healthStyle;
    private GUIStyle bossNameStyle;
    private GUIStyle bossNameOutlineStyle;

    private void Awake()
    {
        health = GetComponent<CombatHealth>();
        bossController = GetComponent<BossController>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnGUI()
    {
        if (health == null || health.IsDead || health.Team != CombatHealth.CombatTeam.Enemy ||
            (spriteRenderer != null && !spriteRenderer.enabled))
            return;
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;
        Vector3 bottom = transform.position;
        Vector3 top = transform.position;
        if (health.Hitbox != null)
        {
            bottom.x = health.Hitbox.bounds.center.x;
            bottom.y = health.Hitbox.bounds.min.y;
            top.x = health.Hitbox.bounds.center.x;
            top.y = health.Hitbox.bounds.max.y;
        }
        else if (spriteRenderer != null)
        {
            bottom.x = spriteRenderer.bounds.center.x;
            bottom.y = spriteRenderer.bounds.min.y;
            top.x = spriteRenderer.bounds.center.x;
            top.y = spriteRenderer.bounds.max.y;
        }

        int previousDepth = GUI.depth;
        GUI.depth = 10;

        if (showHealthText)
            DrawHealthText(bottom + (Vector3)worldOffset);
        DrawBossName(top + (Vector3)bossNameWorldOffset);

        GUI.depth = previousDepth;
    }

    private void DrawHealthText(Vector3 worldPosition)
    {
        Vector3 screen = targetCamera.WorldToScreenPoint(worldPosition);
        if (!IsOnScreen(screen))
            return;

        if (healthStyle == null)
            healthStyle = new GUIStyle(GUI.skin.label);

        healthStyle.alignment = TextAnchor.UpperCenter;
        healthStyle.fontSize = fontSize;
        healthStyle.fontStyle = FontStyle.Bold;
        SetAllStyleTextColors(healthStyle, textColor);

        GUI.Label(
            new Rect(screen.x - 120f, Screen.height - screen.y, 240f, fontSize * 1.7f),
            health.CurrentHealth + " / " + health.MaxHealth,
            healthStyle
        );
    }

    private void DrawBossName(Vector3 worldPosition)
    {
        if (!showBossName)
            return;

        string nameToDraw = bossDisplayName;
        if (string.IsNullOrWhiteSpace(nameToDraw) && bossController != null)
            nameToDraw = bossController.BossId;
        if (string.IsNullOrWhiteSpace(nameToDraw))
            return;

        Vector3 screen = targetCamera.WorldToScreenPoint(worldPosition);
        if (!IsOnScreen(screen))
            return;

        if (bossNameStyle == null)
            bossNameStyle = new GUIStyle(GUI.skin.label);

        bossNameStyle.alignment = TextAnchor.MiddleCenter;
        bossNameStyle.fontSize = bossNameFontSize;
        bossNameStyle.fontStyle = bossNameFontStyle;
        SetAllStyleTextColors(bossNameStyle, bossNameTextColor);

        float width = Mathf.Max(60f, bossNameBoxWidth);
        float height = bossNameFontSize * 1.7f;
        Rect nameRect = new Rect(
            screen.x - width * 0.5f,
            Screen.height - screen.y - height * 0.5f,
            width,
            height
        );
        string trimmedName = nameToDraw.Trim();

        if (showBossNameOutline)
        {
            if (bossNameOutlineStyle == null)
                bossNameOutlineStyle = new GUIStyle(GUI.skin.label);

            bossNameOutlineStyle.alignment = TextAnchor.MiddleCenter;
            bossNameOutlineStyle.fontSize = bossNameFontSize;
            bossNameOutlineStyle.fontStyle = bossNameFontStyle;
            SetAllStyleTextColors(bossNameOutlineStyle, bossNameOutlineColor);

            int thickness = Mathf.Clamp(bossNameOutlineThickness, 1, 6);
            for (int offsetX = -thickness; offsetX <= thickness; offsetX++)
            {
                for (int offsetY = -thickness; offsetY <= thickness; offsetY++)
                {
                    if (offsetX == 0 && offsetY == 0)
                        continue;


                    if (offsetX * offsetX + offsetY * offsetY > thickness * thickness + 1)
                        continue;

                    Rect outlineRect = new Rect(
                        nameRect.x + offsetX,
                        nameRect.y + offsetY,
                        nameRect.width,
                        nameRect.height
                    );
                    GUI.Label(outlineRect, trimmedName, bossNameOutlineStyle);
                }
            }
        }

        GUI.Label(nameRect, trimmedName, bossNameStyle);
    }

    private static void SetAllStyleTextColors(GUIStyle style, Color color)
    {
        style.normal.textColor = color;
        style.hover.textColor = color;
        style.active.textColor = color;
        style.focused.textColor = color;
        style.onNormal.textColor = color;
        style.onHover.textColor = color;
        style.onActive.textColor = color;
        style.onFocused.textColor = color;
    }

    private bool IsOnScreen(Vector3 screenPosition)
    {
        return screenPosition.z > 0f &&
               screenPosition.x >= 0f && screenPosition.x <= Screen.width &&
               screenPosition.y >= 0f && screenPosition.y <= Screen.height;
    }
}
