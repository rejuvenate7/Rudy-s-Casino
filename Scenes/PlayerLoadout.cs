using UnityEngine;


// player loadout class for giving the player different outfits and fight style anims and walk cycle anims
[CreateAssetMenu(fileName = "NewPlayerLoadout", menuName = "Game/Player Loadout")]
public class PlayerLoadout : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable ID used by menus and save data, such as butler_loadout.")]
    [SerializeField] private string loadoutId = "new_loadout";

    [Header("Outfit Animation")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite[] walkFrames;
    [Min(0.01f)][SerializeField] private float secondsPerWalkFrame = 0.15f;

    [Header("Fighting Style")]
    [Tooltip("Must match a Style Id configured on PlayerCombatController, such as punch or platter.")]
    [SerializeField] private string attackStyleId = "punch";

    public string LoadoutId { get { return loadoutId; } }
    public Sprite IdleSprite { get { return idleSprite; } }
    public Sprite[] WalkFrames { get { return walkFrames; } }
    public float SecondsPerWalkFrame { get { return Mathf.Max(0.01f, secondsPerWalkFrame); } }
    public string AttackStyleId { get { return attackStyleId; } }
    public bool HasOutfit
    {
        get { return idleSprite != null || (walkFrames != null && walkFrames.Length > 0); }
    }
}
