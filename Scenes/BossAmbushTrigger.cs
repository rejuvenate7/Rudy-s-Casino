using UnityEngine;

// for triggering the boss ambush in the final boss fight vs bob, where you walk across the half of the level
// and it automatically triggers a boss fight
[AddComponentMenu("Game/Bosses/Boss Ambush Trigger")]
public class BossAmbushTrigger : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("Optional. If empty, the first PlayerSpriteController in the scene is used.")]
    [SerializeField] private Transform player;
    [Tooltip("Enable when the player approaches this object's X position from the left.")]
    [SerializeField] private bool playerApproachesFromLeft = true;
    [Tooltip("Offset added to this object's world X position.")]
    [SerializeField] private float triggerXOffset;

    [Header("Ambush")]
    [Tooltip("The ambush can trigger only while this exact background sprite is displayed. Leave empty to allow it on every background.")]
    [SerializeField] private Sprite requiredBackground;
    [Tooltip("The same scene without the NPC painted into the background.")]
    [SerializeField] private Sprite backgroundWithoutNpc;
    [Tooltip("Boss prefab, spawn position, dialogue, music, and defeat sequence settings.")]
    [SerializeField]
    private GameManager.BossFightSettings bossFight = new GameManager.BossFightSettings();

    private bool triggered;
    private bool requiredBackgroundWasActive;
    private bool hasReachedApproachSide;
    private bool wasOnApproachSide;

    private void Update()
    {
        if (triggered || GameManager.Instance == null)
            return;

        if (!IsRequiredBackgroundActive())
        {
            ResetCrossingState();
            return;
        }

        FindPlayerIfNeeded();
        if (player == null)
            return;

        float triggerX = transform.position.x + triggerXOffset;
        bool isOnApproachSide = playerApproachesFromLeft ? player.position.x < triggerX : player.position.x > triggerX;

        if (!requiredBackgroundWasActive)
        {
            requiredBackgroundWasActive = true;
            hasReachedApproachSide = isOnApproachSide;
            wasOnApproachSide = isOnApproachSide;
            return;
        }

        if (GameManager.Instance.IsTransitionInProgress)
        {
            if (isOnApproachSide)
                hasReachedApproachSide = true;

            wasOnApproachSide = isOnApproachSide;
            return;
        }

        if (isOnApproachSide)
            hasReachedApproachSide = true;

        bool crossedTrigger = hasReachedApproachSide && wasOnApproachSide && !isOnApproachSide;

        wasOnApproachSide = isOnApproachSide;

        if (crossedTrigger)
            TriggerAmbush();
    }

    public void TriggerAmbush()
    {
        if (triggered || GameManager.Instance == null)
            return;
        if (!IsRequiredBackgroundActive())
        {
            Debug.LogWarning(
                "Boss Ambush Trigger was called while its Required Background was not active.",
                this
            );
            return;
        }
        if (GameManager.Instance.IsTransitionInProgress)
        {
            Debug.LogWarning(
                "Boss Ambush Trigger is waiting for the current transition to finish.",
                this
            );
            return;
        }
        if (bossFight == null || bossFight.bossPrefab == null)
        {
            Debug.LogWarning("Boss Ambush Trigger needs a Boss Prefab.", this);
            return;
        }

        triggered = true;

        if (backgroundWithoutNpc != null)
            GameManager.Instance.SetBackground(backgroundWithoutNpc);

        GameManager.Instance.StartBossFight(bossFight);
        enabled = false;
    }

    private void ResetCrossingState()
    {
        requiredBackgroundWasActive = false;
        hasReachedApproachSide = false;
        wasOnApproachSide = false;
    }

    private bool IsRequiredBackgroundActive()
    {
        return requiredBackground == null ||
               (GameManager.Instance != null &&
                GameManager.Instance.CurrentBackground == requiredBackground);
    }

    private void FindPlayerIfNeeded()
    {
        if (player != null)
            return;

        PlayerSpriteController controller = FindObjectOfType<PlayerSpriteController>();
        if (controller != null)
            player = controller.transform;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        float triggerX = transform.position.x + triggerXOffset;
        Gizmos.DrawLine(
            new Vector3(triggerX, transform.position.y - 5f, transform.position.z),
            new Vector3(triggerX, transform.position.y + 5f, transform.position.z)
        );
    }
}
