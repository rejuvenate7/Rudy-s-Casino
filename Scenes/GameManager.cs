using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// the main game manager class which manages everything in the game, such as, player boss fights, background changes
// talk point setup, cutscenes... etc
public class GameManager : MonoBehaviour
{
    public enum TalkPointActionType
    {
        Dialogue,
        ChangeBackground,
        DialogueAndChangeBackground,
        SoundEffectOnly,
        HealPlayer,
        OpenFightStyleInventory,
        ExitElevator
    }

    [Serializable]
    public class BossKilledEvent : UnityEvent<string> { }

    [Serializable]
    private class BossProgressSave
    {
        public List<string> ids = new List<string>();
    }
    [Serializable]
    public class TalkPoint
    {
        [Tooltip("A descriptive name used only to keep the Inspector organized.")]
        public string pointName = "NPC";

        [Header("Interaction Action")]
        [Tooltip("Determines what happens when the player presses E at this point.")]
        public TalkPointActionType actionType = TalkPointActionType.Dialogue;

        [Tooltip("Text shown inside this point's prompt, such as E, E (Talk), or E (Loot). Leave empty to use the global Interaction Prompt.")]
        public string promptText = "E";

        [Tooltip("Optional per-point prompt box size. Leave an axis at 0 to calculate it automatically.")]
        public Vector2 customPromptBoxSize = Vector2.zero;

        [Tooltip("After this point is activated once, hide it and prevent it from being used again during this play session.")]
        public bool useOnlyOnce;

        [Tooltip("Optional scene marker. If assigned, prompts appear at this Transform.")]
        public Transform location;

        [Tooltip("Used when Location is not assigned.")]
        public Vector2 worldPosition;

        [Tooltip("How close the player must be before the E prompt appears.")]
        [Min(0.05f)]
        public float interactionRange = 1.25f;

        [Tooltip("Offset from the talk point to the E prompt, in world units.")]
        public Vector2 promptOffset = new Vector2(0f, 0.6f);

        [Header("Background Action")]
        [Tooltip("Background applied immediately for Change Background actions.")]
        public Sprite backgroundOnInteract;

        [Header("Player Loadout Reward")]
        [Tooltip("Optional outfit and fighting style equipped when this point is activated. Ideal for E (Loot) points.")]
        public PlayerLoadout playerLoadoutOnInteract;

        [Header("Interaction Sound")]
        [Tooltip("Optional sound effect played once whenever this point is activated.")]
        public AudioClip interactionSound;

        [Range(0f, 1f)]
        public float interactionSoundVolume = 1f;

        [Header("Dialogue Action")]

        [TextArea(2, 6)]
        public string dialogueText = "Example NPC dialogue.";

        [Tooltip("How many seconds the dialogue takes to reveal from left to right. After it is fully revealed, it remains visible for the global Full Text Hold Duration.")]
        [Min(0.1f)]
        public float dialogueDuration = 3f;

        [Tooltip("Optional voice line played once when this talk point is activated.")]
        public AudioClip voiceLine;

        [Tooltip("Volume multiplier for this talk point's voice line.")]
        [Range(0f, 1f)]
        public float voiceLineVolume = 1f;

        [Tooltip("Display this NPC's dialogue in italics.")]
        public bool italicizeDialogue = false;

        [Header("Elevator After Interaction")]
        [Tooltip("Optional Elevator Name to enter after this interaction finishes. If Point Name is Elevator and only one elevator is configured, that elevator is used automatically.")]
        public string elevatorAfterInteraction;

        [Header("Talk Point Set After Interaction")]
        [Tooltip("Optional. Activates this Talk Point Set after the interaction finishes, such as lvl1_4. Dialogue actions wait until the dialogue and voice line end.")]
        public string talkPointSetAfterInteraction;

        [Header("Cutscene After Interaction")]
        [Tooltip("Optional. Dialogue actions wait for the dialogue and voice line; other actions start the cutscene immediately.")]
        public string cutsceneAfterDialogue;

        [Tooltip("Prevents this talk point from replaying its cutscene after it has already run once.")]
        public bool playCutsceneOnlyOnce = true;

        [NonSerialized]
        public bool cutsceneHasPlayed;

        [NonSerialized]
        public bool hasBeenUsed;

        public Vector3 GetWorldPosition()
        {
            if (location != null)
                return location.position;

            return new Vector3(worldPosition.x, worldPosition.y, 0f);
        }
    }

    [Serializable]
    public class TalkPointSet
    {
        [Tooltip("The name passed to SetButtons, such as lvl1_1 or casino_floor.")]
        public string setName;

        public TalkPoint[] talkPoints;
    }

    [Serializable]
    public class ElevatorSequence
    {
        [Tooltip("The name entered in a talk point's Elevator After Interaction field.")]
        public string elevatorName = "Level1Elevator";

        [Header("Inside Elevator")]
        [Tooltip("Normal elevator background with no chance NPC present.")]
        public Sprite elevatorBackground;

        [Tooltip("Where the player is placed after entering the elevator.")]
        public Vector3 playerStartPosition = new Vector3(0f, -2f, 0f);

        [Tooltip("Optional Player Loadout used only for its idle/walk outfit when entering this elevator. The currently equipped fighting style is preserved. Leave empty to keep the current outfit.")]
        public PlayerLoadout playerOutfitOnEnter;

        [Tooltip("Multiplies the player's normal scale while inside this elevator. For example, 1.5 makes the player 50% larger. The original scale is restored on exit.")]
        [Min(0.01f)]
        public float playerScaleMultiplier = 1.5f;

        [Tooltip("The player cannot walk left or right beyond these world-space X positions.")]
        public Vector2 playerXBounds = new Vector2(-4f, 4f);

        [Min(0.05f)]
        public float interactionRange = 1.25f;

        public Vector2 promptOffset = new Vector2(0f, 0.6f);

        [Header("Heal Talk Point")]
        public Vector2 healPointPosition = new Vector2(-2.5f, -2f);
        public string healPrompt = "E (Heal)";
        public AudioClip healSound;
        [Range(0f, 1f)] public float healSoundVolume = 1f;

        [Header("Inventory Talk Point")]
        public Vector2 inventoryPointPosition = new Vector2(0f, -2f);
        public string inventoryPrompt = "E (Inventory)";

        [Header("Exit Talk Point")]
        public Vector2 exitPointPosition = new Vector2(2.5f, -2f);
        public string exitPrompt = "E (Exit)";

        [Header("Chance NPC")]
        [Tooltip("0 never appears, 1 always appears, and 0.5 is a fifty-percent chance each time this elevator is entered.")]
        [Range(0f, 1f)] public float npcAppearanceChance;

        [Tooltip("Elevator background containing the NPC. No NPC GameObject is spawned.")]
        public Sprite npcElevatorBackground;

        [TextArea(2, 6)]
        public string npcDialogue;

        [Tooltip("How many seconds the dialogue takes to reveal from left to right.")]
        [Min(0f)] public float npcDialogueDuration = 3f;
        public bool italicizeNpcDialogue;
        public AudioClip npcVoiceLine;
        [Range(0f, 1f)] public float npcVoiceVolume = 1f;

        [Header("Destination Level")]
        [Tooltip("Background shown after the player uses E (Exit).")]
        public Sprite destinationBackground;

        [Tooltip("Talk Point Set enabled in the destination level.")]
        public string destinationTalkPointSet;

        [Tooltip("Optional name from Background Music Tracks. Leave empty to keep the current music.")]
        public string destinationMusicTrack;

        public bool setDestinationPlayerPosition = true;
        public Vector3 destinationPlayerPosition = new Vector3(-6f, -2f, 0f);

        [Header("Optional Elevator Music")]
        [Tooltip("Optional name from Background Music Tracks. Leave empty to keep the current music while inside.")]
        public string elevatorMusicTrack;

        [Header("Transitions")]
        [Min(0f)] public float enterFadeOutDuration = 0.6f;
        [Min(0f)] public float enterFadeInDuration = 0.6f;
        [Min(0f)] public float exitFadeOutDuration = 0.6f;
        [Min(0f)] public float exitFadeInDuration = 0.6f;
    }

    [Serializable]
    public class BackgroundMusicTrack
    {
        [Tooltip("The name used when calling ChangeBackgroundMusic, such as level1 or casino.")]
        public string trackName = "level1";

        [Tooltip("The MP3 or other AudioClip played for this music track.")]
        public AudioClip musicClip;

        [Tooltip("The playback volume for this individual track.")]
        [Range(0f, 1f)]
        public float volume = 0.5f;
    }

    [Serializable]
    public class CutsceneShot
    {
        [Tooltip("A descriptive name used only to keep the Inspector organized.")]
        public string shotName = "Shot";

        [Tooltip("The background displayed for this shot.")]
        public Sprite background;

        [Tooltip("How long this background remains visible after its transition and sound begin.")]
        [Min(0f)]
        public float displayDuration = 2f;

        [Tooltip("Total fade-out/fade-in time used when moving from this shot to the next one.")]
        [Min(0f)]
        public float transitionToNextDuration = 1f;

        [Tooltip("Optional sound played once after this background has faded in.")]
        public AudioClip soundEffect;

        [Range(0f, 1f)]
        public float soundVolume = 1f;
    }

    [Serializable]
    public class BossFightSettings
    {
        [Tooltip("Optional. Assign a boss PREFAB to start a fight after this cutscene.")]
        public BossController bossPrefab;
        [Tooltip("Optional scene marker. Otherwise Spawn Position is used.")]
        public Transform spawnPoint;
        public Vector3 spawnPosition = new Vector3(4f, -2f, 0f);
        [TextArea(2, 6)]
        public string introDialogue = "You are not leaving here!";
        [Tooltip("How many seconds the boss introduction takes to reveal from left to right.")]
        [Min(0f)] public float introDuration = 3f;
        public bool italicizeIntro;
        public AudioClip introVoiceLine;
        [Range(0f, 1f)] public float introVoiceVolume = 1f;
        public bool resetPlayerHealth = true;
        [Tooltip("Keep the player's X within the boss's Arena X Limits during the fight.")]
        public bool confinePlayerToArena = true;
        [Tooltip("Optional name from Background Music Tracks.")]
        public string backgroundMusicTrack;

        [Header("After Boss Defeat")]
        [Tooltip("Fade to black before invoking On Boss Defeated, then fade back into gameplay.")]
        public bool fadeAfterBossDefeated = true;

        [Tooltip("How long the screen takes to fade to black after the boss dies.")]
        [Min(0f)] public float bossDefeatedFadeOutDuration = 0.8f;

        [Tooltip("How long the new background takes to fade in after On Boss Defeated runs.")]
        [Min(0f)] public float bossDefeatedFadeInDuration = 0.8f;

        [Tooltip("Invoked while the screen is black. Use this to change the background, music, talk points, or level state.")]
        public UnityEvent onBossDefeated = new UnityEvent();

        [Header("After Player Defeat")]
        [Tooltip("Optional sprite shown immediately when the player reaches 0 HP. Assign Frank's kneeling/bloody sprite here for Bob's fight.")]
        public Sprite playerDefeatedSprite;

        [Tooltip("Multiplier applied to Frank's current scale while showing the defeated sprite. 1 keeps the normal size, 0.75 is smaller, and 1.25 is larger.")]
        [Min(0.01f)] public float playerDefeatedSpriteScale = 1f;

        [TextArea(2, 6)]
        [Tooltip("Optional line spoken by the boss after the player reaches 0 HP.")]
        public string playerDefeatedDialogue;

        [Tooltip("How many seconds the defeat dialogue takes to reveal from left to right.")]
        [Min(0f)] public float playerDefeatedDialogueDuration = 2.5f;
        public bool italicizePlayerDefeatedDialogue;
        public AudioClip playerDefeatedVoiceLine;
        [Range(0f, 1f)] public float playerDefeatedVoiceVolume = 1f;

        [Tooltip("Optional Cutscene Name played after the defeat dialogue and voice line finish.")]
        public string cutsceneAfterPlayerDefeated;

        [Tooltip("Invoked immediately when the player reaches 0 HP, before the optional dialogue and cutscene.")]
        public UnityEvent onPlayerDefeated = new UnityEvent();
    }

    [Serializable]
    public class CutsceneSequence
    {
        [Tooltip("The name entered in a Talk Point's Cutscene After Dialogue field.")]
        public string cutsceneName = "NewCutscene";

        [Header("Cutscene Music")]
        [Tooltip("Optional Track Name from Background Music Tracks. The cutscene switches to it immediately when it starts. Leave empty to keep the current music.")]
        public string musicTrackOnStart;

        [Tooltip("How long the screen takes to fade to black before the first shot.")]
        [Min(0f)]
        public float openingFadeOutDuration = 0.8f;

        [Tooltip("How long the first shot takes to fade in from black.")]
        [Min(0f)]
        public float openingFadeInDuration = 0.8f;

        public CutsceneShot[] shots;

        [Header("Return To Level")]
        [Tooltip("The normal level background restored after the final cutscene shot.")]
        public Sprite returnBackground;

        [Tooltip("If enabled, all talk points are removed when this cutscene ends. This overrides Return Talk Point Set Name.")]
        public bool clearAllTalkPointsOnReturn = false;

        [Tooltip("Optional talk-point set enabled when gameplay resumes. Leave empty to restore the previous set.")]
        public string returnTalkPointSetName;

        [Tooltip("How long the final shot takes to fade to black.")]
        [Min(0f)]
        public float endingFadeOutDuration = 0.8f;

        [Tooltip("How long the level takes to fade back in.")]
        [Min(0f)]
        public float endingFadeInDuration = 0.8f;

        [Header("Boss Fight After Cutscene")]
        public BossFightSettings bossFight = new BossFightSettings();

        [Header("Game Ending")]
        [Tooltip("After this cutscene, keep the player hidden, clear interactions, and permanently lock gameplay. Return Background can be used as the final end screen.")]
        public bool endGameAfterCutscene;
    }

    public static GameManager Instance { get; private set; }

    [Header("Boss Defeat Progress")]
    [Tooltip("Boss IDs defeated in this playthrough. Use HasDefeatedBoss to query them.")]
    [SerializeField] private List<string> defeatedBossIds = new List<string>();
    [Tooltip("Optional: save defeated IDs between app launches, as well as between levels.")]
    [SerializeField] private bool persistBossProgress;
    [SerializeField] private string bossProgressSaveKey = "Frank.DefeatedBosses.v1";
    [Tooltip("Invoked once when an ID is first recorded. Receives that boss's ID.")]
    [SerializeField] private BossKilledEvent onKilledBossEvent = new BossKilledEvent();
    public IReadOnlyList<string> DefeatedBossIds { get { return defeatedBossIds.AsReadOnly(); } }

    [Header("Background Sprites")]
    [SerializeField] private Sprite lvl1_1;
    [SerializeField] private Sprite lvl1_2;
    [SerializeField] private Sprite lvl1_3;
    [SerializeField] private Sprite lvl1_4;

    [Header("Start Menu")]
    [Tooltip("Show the Rudy's Casino title menu when the game launches.")]
    [SerializeField] private bool showStartMenuOnLaunch = true;

    [Tooltip("The single background shown behind the title menu. Leave empty to use lvl1_1.")]
    [SerializeField] private Sprite startMenuBackground;

    [Tooltip("Screen-space blur radius used for the title and game-over backgrounds.")]
    [Range(0f, 16f)]
    [SerializeField] private float menuBackgroundBlurRadius = 5f;

    [SerializeField] private string startMenuTitle = "RUDY'S CASINO";
    [Tooltip("Talk Point Set activated after PLAY is pressed.")]
    [SerializeField] private string startGameTalkPointSetName = "lvl1_1";
    [Min(24)][SerializeField] private int startMenuTitleFontSize = 72;
    [Min(16)][SerializeField] private int menuButtonFontSize = 34;
    [SerializeField] private Vector2 menuButtonSize = new Vector2(360f, 72f);
    [SerializeField] private Color startMenuDimColor = new Color(0f, 0f, 0f, 0.52f);
    [SerializeField] private Color startMenuTitleColor = Color.white;
    [SerializeField] private Color menuButtonColor = new Color(0.04f, 0.04f, 0.04f, 0.9f);
    [SerializeField] private Color menuButtonTextColor = Color.white;

    [Header("Mouse Cursor")]
    [Tooltip("Hide the cursor during gameplay and only show it while a mouse-driven menu is open.")]
    [SerializeField] private bool hideCursorDuringGameplay = true;

    [Header("Game Over Screen")]
    [SerializeField] private string gameOverTitle = "GAME OVER";
    [SerializeField] private string gameOverMessage = "You failed to infiltrate Rudy Mafia...";
    [Tooltip("Optional TTF/OTF font used only by the Game Over title and message. Leave empty to use Unity's default font.")]
    [SerializeField] private Font gameOverFont;
    [Min(24)][SerializeField] private int gameOverFontSize = 86;
    [Min(14)][SerializeField] private int gameOverMessageFontSize = 30;
    [SerializeField] private Color gameOverTextColor = new Color(0.95f, 0.04f, 0.04f, 1f);
    [SerializeField] private Color gameOverMessageColor = new Color(1f, 0.78f, 0.78f, 1f);
    [SerializeField] private Color gameOverTintColor = new Color(0.28f, 0f, 0f, 0.52f);
    [Tooltip("A thin solid outline keeps the text readable without the pixelated multi-sample glow.")]
    [Range(0f, 3f)][SerializeField] private float gameOverTextOutlineThickness = 1f;
    [SerializeField] private Color gameOverTextOutlineColor = new Color(0.08f, 0f, 0f, 0.95f);
    [Tooltip("Seconds used to smoothly blur and tint the final cutscene shot.")]
    [Min(0f)][SerializeField] private float gameOverFadeInDuration = 0.8f;

    [Header("Player Interaction")]
    [Tooltip("Drag the Player here, or give the Player GameObject the Player tag.")]
    [SerializeField] private Transform player;

    [Tooltip("Optional. If empty, the camera tagged MainCamera is used.")]
    [SerializeField] private Camera interactionCamera;

    [Tooltip("Recommended because this game moves the player only left and right.")]
    [SerializeField] private bool useHorizontalDistanceOnly = true;

    [Header("Player Health Display")]
    [SerializeField] private bool showPlayerHealth = true;
    [SerializeField] private Vector2 healthTextTopLeft = new Vector2(30f, 25f);
    [Min(10)][SerializeField] private int healthFontSize = 36;
    [SerializeField] private Color healthTextColor = new Color(1f, 1f, 1f, 0.8f);

    [Header("Talk Point Sets")]
    [SerializeField] private TalkPointSet[] talkPointSets;

    [Header("Elevators")]
    [Tooltip("Reusable elevator configurations. A level talk point enters one by matching Elevator After Interaction to Elevator Name.")]
    [SerializeField] private ElevatorSequence[] elevatorSequences;

    [Header("Fight Style Inventory Appearance")]
    [SerializeField] private Vector2 inventoryPanelSize = new Vector2(760f, 560f);
    [Min(18)][SerializeField] private int inventoryTitleFontSize = 38;
    [Min(14)][SerializeField] private int inventoryButtonFontSize = 28;
    [SerializeField] private Color inventoryPanelColor = new Color(0.03f, 0.03f, 0.03f, 0.96f);
    [SerializeField] private Color inventoryTextColor = Color.white;

    [Header("Prompt Appearance")]
    [SerializeField] private string interactionPrompt = "E";
    [SerializeField] private int promptFontSize = 26;
    [SerializeField] private Vector2 promptBoxSize = new Vector2(50f, 50f);
    [SerializeField] private Color promptTextColor = Color.white;

    [Header("Dialogue Appearance")]
    [SerializeField] private int dialogueFontSize = 36;
    [SerializeField] private Vector2 dialogueBoxSize = new Vector2(1100f, 220f);
    [SerializeField] private float dialogueBottomMargin = 45f;
    [SerializeField] private Vector2 dialogueTextPadding = new Vector2(45f, 25f);
    [SerializeField] private Color dialogueBoxColor = new Color(0.03f, 0.03f, 0.03f, 0.94f);
    [SerializeField] private Color dialogueTextColor = Color.white;

    [Tooltip("How long fully revealed dialogue remains on screen before disappearing.")]
    [Min(0f)]
    [SerializeField] private float fullTextHoldDuration = 2f;

    [Header("Dialogue Audio")]
    [Tooltip("A non-looping 2D AudioSource used by every talk point voice line.")]
    [SerializeField] private AudioSource voiceLineAudioSource;

    [Header("Interaction Sound Effects")]
    [Tooltip("A non-looping 2D AudioSource used by talk-point action sounds. If empty, one is created automatically.")]
    [SerializeField] private AudioSource interactionSoundAudioSource;

    [Header("Background Music")]
    [Tooltip("Dedicated 2D AudioSource for continuously looping level music. If empty, one is created automatically.")]
    [SerializeField] private AudioSource backgroundMusicAudioSource;

    [Tooltip("Every background music track that can be selected by name.")]
    [SerializeField] private BackgroundMusicTrack[] backgroundMusicTracks;

    [Tooltip("Automatically begin playing music when the GameManager starts.")]
    [SerializeField] private bool playBackgroundMusicOnStart = true;

    [Tooltip("Track Name to play at startup. Leave empty to use the first configured track.")]
    [SerializeField] private string startingBackgroundMusicTrack = "level1";

    [Header("Cutscenes")]
    [Tooltip("Reusable background cutscenes. Match Cutscene Name with a talk point's Cutscene After Dialogue value.")]
    [SerializeField] private CutsceneSequence[] cutsceneSequences;

    [Tooltip("A non-looping 2D AudioSource used for cutscene shot sound effects.")]
    [SerializeField] private AudioSource cutsceneAudioSource;

    [Header("Boss Fight Debug Shortcut")]
    [Tooltip("While the game is running, press the top-row 0 or numpad 0 key to start the configured boss fight immediately.")]
    [SerializeField] private bool enableBossFightDebugShortcut = true;

    [Tooltip("The Cutscene Name whose Boss Fight After Cutscene settings are used by the 0-key shortcut.")]
    [SerializeField] private string debugBossCutsceneName = "ButlerBoss";

    [Tooltip("Use that cutscene's Return Background as the arena background when the shortcut is pressed.")]
    [SerializeField] private bool debugUseReturnBackground = true;

    [Header("NPC Walk-Away Transition")]
    [Tooltip("Only dialogue started from this Talk Point Set counts toward the one-time walk-away transition.")]
    [SerializeField] private string npcWalkAwayTalkPointSetName = "lvl1_1";

    [Tooltip("Number of qualifying first-level conversations before the walk-away transition begins.")]
    [Min(1)]
    [SerializeField] private int npcWalkAwayDialogueCount = 2;

    [Tooltip("AudioSource used to play the footsteps. Disable Play On Awake on it.")]
    [SerializeField] private AudioSource footstepAudioSource;

    [Tooltip("A single footstep or a recording containing several footsteps.")]
    [SerializeField] private AudioClip footstepSound;

    [Tooltip("Enable this when Footstep Sound contains only one short step.")]
    [SerializeField] private bool loopFootstepSound;

    [Tooltip("Minimum time the screen remains black while footsteps play.")]
    [Min(0f)]
    [SerializeField] private float footstepPlayTime = 2.5f;

    [Min(0f)]
    [SerializeField] private float fadeToBlackDuration = 0.8f;

    [Min(0f)]
    [SerializeField] private float fadeFromBlackDuration = 0.8f;

    [Tooltip("The background shown after the NPC has walked away.")]
    [SerializeField] private string backgroundAfterNpcLeaves = "lvl1_2";

    [SerializeField] private bool lockPlayerDuringTransition = true;

    private BackgroundController backgroundController;

    private TalkPoint[] activeTalkPoints;
    private string activeTalkPointSetName;
    private TalkPoint nearestTalkPoint;
    private TalkPoint displayedDialogue;
    private float displayedDialogueRevealStartTime;
    private float displayedDialogueRevealDuration;
    private float dialogueEndTime;
    private bool buttonsEnabled;
    private float fadeAlpha;
    private bool transitionInProgress;
    private bool gameEnded;
    private BossController activeBoss;
    private BossFightSettings activeBossSettings;
    private CombatHealth bossFightPlayerHealth;
    private PlayerSpriteController bossFightPlayerController;
    private TalkPoint bossIntroDialogue;
    private float bossDialogueRevealStartTime;
    private float bossDialogueRevealDuration;
    private GUIStyle healthTextStyle;
    private PlayerLoadout activePlayerLoadout;
    private PlayerLoadout activePlayerOutfitOverride;
    private ElevatorSequence activeElevator;
    private TalkPoint elevatorNpcDialogue;
    private bool elevatorExitUnlocked;
    private bool inventoryOpen;
    private Vector2 inventoryScrollPosition;
    private PlayerSpriteController inventoryPlayerController;
    private bool inventoryPlayerControllerWasEnabled;
    private Vector3 playerScaleBeforeElevator;
    private bool hasSavedPlayerScaleForElevator;
    private bool startMenuVisible;
    private PlayerSpriteController startMenuPlayerController;
    private bool startMenuPlayerControllerWasEnabled;
    private bool startMenuPlayerStateSaved;
    private float gameOverFadeStartTime;

    public BossController ActiveBoss { get { return activeBoss; } }
    public PlayerLoadout ActivePlayerLoadout { get { return activePlayerLoadout; } }
    public Sprite CurrentBackground
    {
        get
        {
            return backgroundController != null
                ? backgroundController.CurrentBackground
                : null;
        }
    }
    public bool IsBossFightActive { get { return activeBoss != null && activeBoss.IsFighting; } }
    public bool IsTransitionInProgress { get { return transitionInProgress; } }
    public bool IsGameEnded { get { return gameEnded; } }
    public bool IsPlayerCombatLocked
    {
        get
        {
            return startMenuVisible || gameEnded || transitionInProgress || displayedDialogue != null ||
                   bossIntroDialogue != null || inventoryOpen;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadBossProgress();
        ConfigureBackgroundMusicAudioSource();
        ConfigureInteractionSoundAudioSource();
        startMenuVisible = showStartMenuOnLaunch;
        ApplyCursorState();
    }

    private void Start()
    {
        SetBackground("lvl1_1");

        if (playBackgroundMusicOnStart)
        {
            if (!string.IsNullOrWhiteSpace(startingBackgroundMusicTrack))
            {
                ChangeBackgroundMusic(startingBackgroundMusicTrack);
            }
            else if (backgroundMusicTracks != null && backgroundMusicTracks.Length > 0)
            {
                PlayBackgroundMusicTrack(backgroundMusicTracks[0], false);
            }
        }

        if (startMenuVisible)
            EnterStartMenuState();
    }

    private int lvl1_firstDialogRan = 0;
    private bool lvl1_firstDialogFinished = false;

    private void Update()
    {
        if (startMenuVisible)
        {
            nearestTalkPoint = null;
            return;
        }

        if (gameEnded)
        {
            nearestTalkPoint = null;
            return;
        }

        if (!inventoryOpen && enableBossFightDebugShortcut && WasBossFightDebugPressed())
        {
            StartDebugBossFight();
            return;
        }

        if (displayedDialogue != null && Time.unscaledTime >= dialogueEndTime)
        {
            TalkPoint finishedTalkPoint = displayedDialogue;
            bool finishedElevatorNpcDialogue = finishedTalkPoint == elevatorNpcDialogue;
            displayedDialogue = null;
            CompleteTalkPointInteraction(finishedTalkPoint);

            if (finishedElevatorNpcDialogue && activeElevator != null)
            {
                elevatorNpcDialogue = null;
                elevatorExitUnlocked = true;
                BuildElevatorTalkPoints();
            }
        }

        if (inventoryOpen)
        {
            nearestTalkPoint = null;
            return;
        }

        if (transitionInProgress || activeBoss != null)
        {
            nearestTalkPoint = null;
            return;
        }

        if (!buttonsEnabled || activeTalkPoints == null || activeTalkPoints.Length == 0)
        {
            nearestTalkPoint = null;
            return;
        }

        FindPlayerAndCameraIfNeeded();

        if (player == null)
        {
            nearestTalkPoint = null;
            return;
        }

        nearestTalkPoint = FindNearestTalkPointInRange();

        if (nearestTalkPoint != null &&
            displayedDialogue == null &&
            WasInteractPressed())
        {
            ActivateTalkPoint(nearestTalkPoint);
        }

        if (lvl1_firstDialogRan >= Mathf.Max(1, npcWalkAwayDialogueCount) &&
            !lvl1_firstDialogFinished && displayedDialogue == null &&
            !transitionInProgress)
        {
            lvl1_firstDialogFinished = true;
            StartCoroutine(FirstNpcWalksAwayTransition());
        }
    }

    private void LateUpdate()
    {
        // Keep one script authoritative so another component or a focus change
        // cannot accidentally leave the cursor visible during gameplay.
        bool shouldBeVisible = ShouldShowCursor();
        if (Cursor.visible != shouldBeVisible || Cursor.lockState != CursorLockMode.None)
            ApplyCursorState();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ApplyCursorState();
    }

    private void OnGUI()
    {
        if (startMenuVisible)
        {
            DrawStartMenu();
            return;
        }

        if (gameEnded)
        {
            DrawGameOverScreen();
            return;
        }

        DrawPlayerHealth();
        if (buttonsEnabled)
        {
            FindPlayerAndCameraIfNeeded();

            if (interactionCamera != null)
            {
                if (nearestTalkPoint != null && displayedDialogue == null)
                {
                    Vector3 promptPosition = nearestTalkPoint.GetWorldPosition() +
                                             (Vector3)nearestTalkPoint.promptOffset;
                    string promptText = GetTalkPointPromptText(nearestTalkPoint);

                    DrawWorldSpaceBox(
                        promptPosition,
                        promptText,
                        GetTalkPointPromptBoxSize(nearestTalkPoint, promptText),
                        promptFontSize,
                        promptTextColor,
                        false
                    );
                }

                if (displayedDialogue != null)
                {
                    DrawBottomDialogueBox(
                        displayedDialogue,
                        displayedDialogueRevealStartTime,
                        displayedDialogueRevealDuration
                    );
                }
            }
        }


        if (bossIntroDialogue != null)
        {
            DrawBottomDialogueBox(
                bossIntroDialogue,
                bossDialogueRevealStartTime,
                bossDialogueRevealDuration
            );
        }

        if (inventoryOpen)
            DrawFightStyleInventory();



        DrawFadeOverlay();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            CloseFightStyleInventory();
            EndBossFight();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Instance = null;
        }
    }

    public void RegisterBackground(BackgroundController controller)
    {
        backgroundController = controller;

        if (lvl1_1 != null)
            backgroundController.SetBackground(lvl1_1);
    }

    public void UnregisterBackground(BackgroundController controller)
    {
        if (backgroundController == controller)
            backgroundController = null;
    }

    public void RegisterPlayer(Transform playerTransform)
    {
        player = playerTransform;

        if (startMenuVisible)
            LockPlayerForStartMenu();

        if (player != null &&
            (activePlayerLoadout != null || activePlayerOutfitOverride != null))
        {
            StartCoroutine(ReapplyLoadoutAfterPlayerRegistration(
                player,
                activePlayerLoadout,
                activePlayerOutfitOverride
            ));
        }
    }

    private void EnterStartMenuState()
    {
        startMenuVisible = true;
        nearestTalkPoint = null;
        HideButtons();
        LockPlayerForStartMenu();
        ApplyCursorState();
    }

    private void LockPlayerForStartMenu()
    {
        FindPlayerAndCameraIfNeeded();
        if (player == null)
            return;

        PlayerSpriteController controller = player.GetComponent<PlayerSpriteController>();
        if (controller != null &&
            (!startMenuPlayerStateSaved || startMenuPlayerController != controller))
        {
            startMenuPlayerController = controller;
            startMenuPlayerControllerWasEnabled = controller.enabled;
            startMenuPlayerStateSaved = true;
        }

        if (controller != null)
            controller.enabled = false;

        PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
        if (combat != null)
            combat.CancelAttack(true);
    }

    private void RestoreStartMenuPlayer()
    {
        if (startMenuPlayerStateSaved && startMenuPlayerController != null)
            startMenuPlayerController.enabled = startMenuPlayerControllerWasEnabled;

        startMenuPlayerController = null;
        startMenuPlayerStateSaved = false;
    }

    public void PlayFromStartMenu()
    {
        if (!startMenuVisible)
            return;

        startMenuVisible = false;
        RestoreStartMenuPlayer();
        ApplyCursorState();

        if (!string.IsNullOrWhiteSpace(startGameTalkPointSetName))
            SetButtons(startGameTalkPointSetName.Trim());
        else
            ShowButtons();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EnterGameOverState()
    {
        gameEnded = true;
        gameOverFadeStartTime = Time.unscaledTime;
        transitionInProgress = false;
        fadeAlpha = 0f;
        nearestTalkPoint = null;
        ClearButtons();

        FindPlayerAndCameraIfNeeded();
        if (player != null)
        {
            PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
            if (combat != null)
                combat.CancelAttack(true);

            PlayerSpriteController controller = player.GetComponent<PlayerSpriteController>();
            if (controller != null)
                controller.enabled = false;
        }

        ApplyCursorState();
    }

    private bool ShouldShowCursor()
    {
        return !hideCursorDuringGameplay || startMenuVisible || gameEnded || inventoryOpen;
    }

    private void ApplyCursorState()
    {
        Cursor.visible = ShouldShowCursor();

        // This game only needs the pointer hidden, not locked to the center.
        // Mouse-driven menus remain immediately usable when they open.
        Cursor.lockState = CursorLockMode.None;
    }


    public bool EquipPlayerLoadout(PlayerLoadout loadout)
    {
        if (loadout == null)
            return false;

        FindPlayerAndCameraIfNeeded();
        PlayerCombatController combat = player != null
            ? player.GetComponent<PlayerCombatController>()
            : null;

        if (combat == null)
        {
            Debug.LogWarning("Cannot equip player loadout because the Player needs PlayerCombatController.");
            return false;
        }

        if (!combat.EquipLoadout(loadout))
        {
            Debug.LogWarning(
                "Could not equip loadout '" + loadout.LoadoutId +
                "'. Check its Attack Style Id, the Player's Fighting Styles, and Required Boss Id."
            );
            return false;
        }

        activePlayerLoadout = loadout;
        activePlayerOutfitOverride = null;
        return true;
    }



    public bool ApplyPlayerOutfit(PlayerLoadout outfitLoadout)
    {
        if (outfitLoadout == null || !outfitLoadout.HasOutfit)
            return false;

        FindPlayerAndCameraIfNeeded();
        PlayerSpriteController movement = player != null
            ? player.GetComponent<PlayerSpriteController>()
            : null;

        if (movement == null || !movement.ApplyOutfit(outfitLoadout))
        {
            Debug.LogWarning(
                "Could not apply player outfit '" + outfitLoadout.LoadoutId +
                "'. Check that the Player has PlayerSpriteController and that the loadout has outfit sprites."
            );
            return false;
        }

        activePlayerOutfitOverride = outfitLoadout;
        return true;
    }



    public bool EquipPlayerAttackStyle(string styleId)
    {
        FindPlayerAndCameraIfNeeded();
        PlayerCombatController combat = player != null
            ? player.GetComponent<PlayerCombatController>()
            : null;

        if (combat == null || !combat.EquipStyle(styleId))
        {
            Debug.LogWarning("Could not equip player attack style '" + styleId + "'.");
            return false;
        }

        return true;
    }

    private IEnumerator ReapplyLoadoutAfterPlayerRegistration(
        Transform registeredPlayer,
        PlayerLoadout loadout,
        PlayerLoadout outfitOverride)
    {

        yield return null;

        if (player != registeredPlayer)
            yield break;

        PlayerCombatController combat = registeredPlayer.GetComponent<PlayerCombatController>();
        if (combat != null && loadout != null)
            combat.EquipLoadout(loadout);

        if (outfitOverride != null)
        {
            PlayerSpriteController movement =
                registeredPlayer.GetComponent<PlayerSpriteController>();
            if (movement != null)
                movement.ApplyOutfit(outfitOverride);
        }
    }

    public void SetBackground(Sprite newBackground)
    {
        if (backgroundController == null)
        {
            Debug.LogWarning("No BackgroundController exists in the current scene.");
            return;
        }

        backgroundController.SetBackground(newBackground);
    }

    public void SetBackground(string backgroundName)
    {
        switch (backgroundName)
        {
            case "lvl1_1":
                SetBackground(lvl1_1);
                SetButtons("lvl1_1");
                break;

            case "lvl1_2":
                SetBackground(lvl1_2);
                SetButtons("lvl1_2");
                break;

            case "lvl1_3":
                SetBackground(lvl1_3);
                break;

            case "lvl1_4":
                SetBackground(lvl1_4);
                break;

            default:
                Debug.LogWarning("Unknown background name: " + backgroundName);
                break;
        }
    }

    private void ConfigureBackgroundMusicAudioSource()
    {
        if (backgroundMusicAudioSource == null)
            backgroundMusicAudioSource = gameObject.AddComponent<AudioSource>();

        backgroundMusicAudioSource.playOnAwake = false;
        backgroundMusicAudioSource.loop = true;
        backgroundMusicAudioSource.spatialBlend = 0f;
    }

    private void ConfigureInteractionSoundAudioSource()
    {
        if (interactionSoundAudioSource == null)
            interactionSoundAudioSource = gameObject.AddComponent<AudioSource>();

        interactionSoundAudioSource.playOnAwake = false;
        interactionSoundAudioSource.loop = false;
        interactionSoundAudioSource.spatialBlend = 0f;
    }


    public void ChangeBackgroundMusic(string trackName)
    {
        BackgroundMusicTrack track = FindBackgroundMusicTrack(trackName);

        if (track == null)
        {
            Debug.LogWarning("No background music track named '" + trackName + "' was found.");
            return;
        }

        PlayBackgroundMusicTrack(track, false);
    }


    public void RestartBackgroundMusic(string trackName)
    {
        BackgroundMusicTrack track = FindBackgroundMusicTrack(trackName);

        if (track == null)
        {
            Debug.LogWarning("No background music track named '" + trackName + "' was found.");
            return;
        }

        PlayBackgroundMusicTrack(track, true);
    }

    public void StopBackgroundMusic()
    {
        if (backgroundMusicAudioSource != null)
            backgroundMusicAudioSource.Stop();
    }

    public void PauseBackgroundMusic()
    {
        if (backgroundMusicAudioSource != null)
            backgroundMusicAudioSource.Pause();
    }

    public void ResumeBackgroundMusic()
    {
        if (backgroundMusicAudioSource != null)
            backgroundMusicAudioSource.UnPause();
    }

    private BackgroundMusicTrack FindBackgroundMusicTrack(string trackName)
    {
        if (backgroundMusicTracks == null || string.IsNullOrWhiteSpace(trackName))
            return null;

        foreach (BackgroundMusicTrack track in backgroundMusicTracks)
        {
            if (track != null &&
                string.Equals(track.trackName, trackName, StringComparison.OrdinalIgnoreCase))
            {
                return track;
            }
        }

        return null;
    }

    private void PlayBackgroundMusicTrack(BackgroundMusicTrack track, bool restart)
    {
        if (track == null || track.musicClip == null)
        {
            Debug.LogWarning("The selected background music track has no Music Clip assigned.");
            return;
        }

        ConfigureBackgroundMusicAudioSource();

        if (!restart &&
            backgroundMusicAudioSource.clip == track.musicClip &&
            backgroundMusicAudioSource.isPlaying)
        {
            return;
        }

        backgroundMusicAudioSource.Stop();
        backgroundMusicAudioSource.clip = track.musicClip;
        backgroundMusicAudioSource.volume = Mathf.Clamp01(track.volume);
        backgroundMusicAudioSource.loop = true;
        backgroundMusicAudioSource.Play();
    }


    public void SetButtons(string setName)
    {
        if (talkPointSets == null)
        {
            activeTalkPointSetName = null;
            Debug.LogWarning("No talk point sets have been configured.");
            HideButtons();
            return;
        }

        foreach (TalkPointSet pointSet in talkPointSets)
        {
            if (pointSet != null &&
                string.Equals(pointSet.setName, setName, StringComparison.OrdinalIgnoreCase))
            {
                SetButtons(pointSet.talkPoints);
                activeTalkPointSetName = pointSet.setName;
                return;
            }
        }

        activeTalkPointSetName = null;
        Debug.LogWarning("No talk point set named '" + setName + "' was found.");
        HideButtons();
    }


    public void SetButtons(TalkPoint[] newTalkPoints)
    {
        activeTalkPointSetName = null;
        activeTalkPoints = newTalkPoints;
        displayedDialogue = null;
        nearestTalkPoint = null;
        buttonsEnabled = activeTalkPoints != null && activeTalkPoints.Length > 0;
    }


    public void HideButtons()
    {
        buttonsEnabled = false;
        nearestTalkPoint = null;
        displayedDialogue = null;
    }

    public void ShowButtons()
    {
        buttonsEnabled = activeTalkPoints != null && activeTalkPoints.Length > 0;
    }


    public void ClearButtons()
    {
        HideButtons();
        activeTalkPointSetName = null;
        activeTalkPoints = null;
    }


    public void SetBackgroundAndButtons(string name)
    {
        SetBackground(name);
        SetButtons(name);
    }


    public void SetBackgroundAndButtons(string backgroundName, string buttonSetName)
    {
        SetBackground(backgroundName);
        SetButtons(buttonSetName);
    }

    private TalkPoint FindNearestTalkPointInRange()
    {
        TalkPoint closest = null;
        float closestDistance = float.MaxValue;

        foreach (TalkPoint point in activeTalkPoints)
        {
            if (point == null || (point.useOnlyOnce && point.hasBeenUsed))
                continue;

            Vector3 pointPosition = point.GetWorldPosition();
            float distance;

            if (useHorizontalDistanceOnly)
                distance = Mathf.Abs(player.position.x - pointPosition.x);
            else
                distance = Vector2.Distance(player.position, pointPosition);

            if (distance <= point.interactionRange && distance < closestDistance)
            {
                closest = point;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private void ActivateTalkPoint(TalkPoint talkPoint)
    {
        if (talkPoint == null || (talkPoint.useOnlyOnce && talkPoint.hasBeenUsed))
            return;



        if (talkPoint.playerLoadoutOnInteract != null &&
            !EquipPlayerLoadout(talkPoint.playerLoadoutOnInteract))
        {
            return;
        }

        talkPoint.hasBeenUsed = true;
        PlayInteractionSound(talkPoint);

        bool changesBackground =
            talkPoint.actionType == TalkPointActionType.ChangeBackground ||
            talkPoint.actionType == TalkPointActionType.DialogueAndChangeBackground;
        bool showsDialogue =
            talkPoint.actionType == TalkPointActionType.Dialogue ||
            talkPoint.actionType == TalkPointActionType.DialogueAndChangeBackground;

        if (changesBackground)
        {
            if (talkPoint.backgroundOnInteract != null)
                SetBackground(talkPoint.backgroundOnInteract);
            else
                Debug.LogWarning(
                    "Talk point '" + talkPoint.pointName +
                    "' is a background action but has no Background On Interact assigned."
                );
        }

        switch (talkPoint.actionType)
        {
            case TalkPointActionType.HealPlayer:
                HealPlayerToMaximum();
                break;

            case TalkPointActionType.OpenFightStyleInventory:
                OpenFightStyleInventory();
                break;

            case TalkPointActionType.ExitElevator:
                ExitElevator();
                nearestTalkPoint = null;
                return;
        }

        if (showsDialogue)
        {
            displayedDialogue = talkPoint;
            displayedDialogueRevealStartTime = Time.unscaledTime;
            displayedDialogueRevealDuration = Mathf.Max(0f, talkPoint.dialogueDuration);

            float voiceLineDuration = talkPoint.voiceLine != null
                ? talkPoint.voiceLine.length
                : 0f;

            dialogueEndTime = Time.unscaledTime + Mathf.Max(
                displayedDialogueRevealDuration + Mathf.Max(0f, fullTextHoldDuration),
                voiceLineDuration
            );

            PlayVoiceLine(talkPoint);

            if (!lvl1_firstDialogFinished && IsNpcWalkAwayTalkPointSetActive())
                lvl1_firstDialogRan += 1;
        }
        else
        {
            CompleteTalkPointInteraction(talkPoint);
        }

        if (talkPoint.useOnlyOnce)
            nearestTalkPoint = null;
    }

    private bool IsNpcWalkAwayTalkPointSetActive()
    {
        return !string.IsNullOrWhiteSpace(npcWalkAwayTalkPointSetName) &&
               !string.IsNullOrWhiteSpace(activeTalkPointSetName) &&
               string.Equals(
                   activeTalkPointSetName,
                   npcWalkAwayTalkPointSetName.Trim(),
                   StringComparison.OrdinalIgnoreCase
               );
    }

    private string GetTalkPointPromptText(TalkPoint talkPoint)
    {
        if (talkPoint != null && !string.IsNullOrWhiteSpace(talkPoint.promptText))
            return talkPoint.promptText;

        return interactionPrompt;
    }

    private Vector2 GetTalkPointPromptBoxSize(TalkPoint talkPoint, string text)
    {
        float estimatedTextWidth = Mathf.Max(0f, (text ?? string.Empty).Length) *
                                   promptFontSize * 0.62f + 24f;
        float automaticWidth = Mathf.Max(promptBoxSize.x, estimatedTextWidth);

        if (talkPoint == null)
            return new Vector2(automaticWidth, promptBoxSize.y);

        float width = talkPoint.customPromptBoxSize.x > 0f
            ? talkPoint.customPromptBoxSize.x
            : automaticWidth;
        float height = talkPoint.customPromptBoxSize.y > 0f
            ? talkPoint.customPromptBoxSize.y
            : promptBoxSize.y;

        return new Vector2(width, height);
    }

    private void FindPlayerAndCameraIfNeeded()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }

        if (interactionCamera == null)
            interactionCamera = Camera.main;
    }

    private bool WasInteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard =
            UnityEngine.InputSystem.Keyboard.current;

        return keyboard != null && keyboard.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    private bool WasBossFightDebugPressed()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard =
            UnityEngine.InputSystem.Keyboard.current;

        return keyboard != null &&
               (keyboard.digit0Key.wasPressedThisFrame ||
                keyboard.numpad0Key.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Alpha0) ||
               Input.GetKeyDown(KeyCode.Keypad0);
#endif
    }

    private void PlayInteractionSound(TalkPoint talkPoint)
    {
        if (talkPoint == null || talkPoint.interactionSound == null)
            return;

        ConfigureInteractionSoundAudioSource();
        interactionSoundAudioSource.PlayOneShot(
            talkPoint.interactionSound,
            Mathf.Clamp01(talkPoint.interactionSoundVolume)
        );
    }

    private void PlayVoiceLine(TalkPoint talkPoint)
    {
        if (talkPoint == null || talkPoint.voiceLine == null)
            return;

        if (voiceLineAudioSource == null)
        {
            Debug.LogWarning(
                "Talk point '" + talkPoint.pointName +
                "' has a voice line, but Voice Line Audio Source is not assigned."
            );
            return;
        }


        voiceLineAudioSource.PlayOneShot(
            talkPoint.voiceLine,
            Mathf.Clamp01(talkPoint.voiceLineVolume)
        );
    }

    private void CompleteTalkPointInteraction(TalkPoint talkPoint)
    {
        if (talkPoint == null)
            return;

        string elevatorName = GetElevatorNameForTalkPoint(talkPoint);
        if (!string.IsNullOrWhiteSpace(elevatorName))
        {
            EnterElevator(elevatorName);
            return;
        }



        if (!string.IsNullOrWhiteSpace(talkPoint.talkPointSetAfterInteraction))
            SetButtons(talkPoint.talkPointSetAfterInteraction.Trim());

        TryStartTalkPointCutscene(talkPoint);
    }

    private string GetElevatorNameForTalkPoint(TalkPoint talkPoint)
    {
        if (talkPoint == null)
            return null;

        if (!string.IsNullOrWhiteSpace(talkPoint.elevatorAfterInteraction))
            return talkPoint.elevatorAfterInteraction.Trim();

        if (!string.Equals(talkPoint.pointName, "elevator", StringComparison.OrdinalIgnoreCase))
            return null;

        if (elevatorSequences != null && elevatorSequences.Length == 1 &&
            elevatorSequences[0] != null)
        {
            return elevatorSequences[0].elevatorName;
        }

        Debug.LogWarning(
            "Talk point 'Elevator' needs Elevator After Interaction when more than one elevator is configured."
        );
        return null;
    }

    private ElevatorSequence FindElevator(string elevatorName)
    {
        if (elevatorSequences == null || string.IsNullOrWhiteSpace(elevatorName))
            return null;

        foreach (ElevatorSequence sequence in elevatorSequences)
        {
            if (sequence != null &&
                string.Equals(
                    sequence.elevatorName,
                    elevatorName.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return sequence;
            }
        }

        return null;
    }


    public void EnterElevator(string elevatorName)
    {
        if (transitionInProgress || activeBoss != null)
        {
            Debug.LogWarning("The elevator cannot be entered during another transition or boss fight.");
            return;
        }

        ElevatorSequence sequence = FindElevator(elevatorName);
        if (sequence == null)
        {
            Debug.LogWarning("No elevator named '" + elevatorName + "' was found.");
            return;
        }

        if (inventoryOpen)
            CloseFightStyleInventory();

        StartCoroutine(EnterElevatorRoutine(sequence));
    }

    private IEnumerator EnterElevatorRoutine(ElevatorSequence sequence)
    {
        transitionInProgress = true;
        HideButtons();
        FindPlayerAndCameraIfNeeded();

        PlayerSpriteController playerController = player != null
            ? player.GetComponent<PlayerSpriteController>()
            : null;
        bool playerControllerWasEnabled = playerController != null &&
                                          playerController.enabled;

        if (playerController != null)
            playerController.enabled = false;

        yield return FadeScreenTo(1f, Mathf.Max(0f, sequence.enterFadeOutDuration));

        activeElevator = sequence;
        elevatorNpcDialogue = null;

        bool npcAppeared = sequence.npcAppearanceChance > 0f &&
                           UnityEngine.Random.value <= Mathf.Clamp01(sequence.npcAppearanceChance);

        Sprite insideBackground = npcAppeared && sequence.npcElevatorBackground != null
            ? sequence.npcElevatorBackground
            : sequence.elevatorBackground;

        if (insideBackground != null)
            SetBackground(insideBackground);
        else
            Debug.LogWarning("Elevator '" + sequence.elevatorName + "' has no inside background assigned.");

        if (!string.IsNullOrWhiteSpace(sequence.elevatorMusicTrack))
            ChangeBackgroundMusic(sequence.elevatorMusicTrack.Trim());

        if (sequence.playerOutfitOnEnter != null &&
            !ApplyPlayerOutfit(sequence.playerOutfitOnEnter))
        {
            Debug.LogWarning(
                "Elevator '" + sequence.elevatorName +
                "' could not apply its Player Outfit On Enter."
            );
        }

        if (player != null)
        {
            playerScaleBeforeElevator = player.localScale;
            hasSavedPlayerScaleForElevator = true;

            float elevatorScale = Mathf.Max(0.01f, sequence.playerScaleMultiplier);
            player.localScale = new Vector3(
                playerScaleBeforeElevator.x * elevatorScale,
                playerScaleBeforeElevator.y * elevatorScale,
                playerScaleBeforeElevator.z
            );

            player.position = sequence.playerStartPosition;

            if (playerController != null)
            {
                playerController.SetArenaBounds(
                    sequence.playerXBounds.x,
                    sequence.playerXBounds.y
                );

                Vector3 clampedPosition = player.position;
                clampedPosition.x = Mathf.Clamp(
                    clampedPosition.x,
                    Mathf.Min(sequence.playerXBounds.x, sequence.playerXBounds.y),
                    Mathf.Max(sequence.playerXBounds.x, sequence.playerXBounds.y)
                );
                player.position = clampedPosition;
            }
        }

        bool npcHasDialogue = npcAppeared &&
                              (!string.IsNullOrWhiteSpace(sequence.npcDialogue) ||
                               sequence.npcVoiceLine != null);
        elevatorExitUnlocked = !npcHasDialogue;

        yield return FadeScreenTo(0f, Mathf.Max(0f, sequence.enterFadeInDuration));

        if (playerController != null)
            playerController.enabled = playerControllerWasEnabled;

        transitionInProgress = false;
        BuildElevatorTalkPoints();

        if (npcHasDialogue)
            StartElevatorNpcDialogue(sequence);
    }

    private void StartElevatorNpcDialogue(ElevatorSequence sequence)
    {
        elevatorNpcDialogue = new TalkPoint
        {
            pointName = "Elevator Visitor",
            actionType = TalkPointActionType.Dialogue,
            dialogueText = sequence.npcDialogue,
            dialogueDuration = Mathf.Max(0f, sequence.npcDialogueDuration),
            italicizeDialogue = sequence.italicizeNpcDialogue,
            voiceLine = sequence.npcVoiceLine,
            voiceLineVolume = sequence.npcVoiceVolume
        };

        displayedDialogue = elevatorNpcDialogue;
        displayedDialogueRevealStartTime = Time.unscaledTime;
        displayedDialogueRevealDuration = Mathf.Max(
            0f,
            elevatorNpcDialogue.dialogueDuration
        );
        float voiceDuration = sequence.npcVoiceLine != null
            ? sequence.npcVoiceLine.length
            : 0f;
        dialogueEndTime = Time.unscaledTime + Mathf.Max(
            displayedDialogueRevealDuration + Mathf.Max(0f, fullTextHoldDuration),
            voiceDuration
        );
        PlayVoiceLine(elevatorNpcDialogue);
    }

    private void BuildElevatorTalkPoints()
    {
        if (activeElevator == null)
        {
            ClearButtons();
            return;
        }

        List<TalkPoint> points = new List<TalkPoint>
        {
            CreateElevatorTalkPoint(
                "Elevator Heal",
                TalkPointActionType.HealPlayer,
                activeElevator.healPrompt,
                activeElevator.healPointPosition,
                activeElevator.healSound,
                activeElevator.healSoundVolume
            ),
            CreateElevatorTalkPoint(
                "Elevator Inventory",
                TalkPointActionType.OpenFightStyleInventory,
                activeElevator.inventoryPrompt,
                activeElevator.inventoryPointPosition,
                null,
                1f
            )
        };

        if (elevatorExitUnlocked)
        {
            points.Add(CreateElevatorTalkPoint(
                "Elevator Exit",
                TalkPointActionType.ExitElevator,
                activeElevator.exitPrompt,
                activeElevator.exitPointPosition,
                null,
                1f
            ));
        }

        SetButtons(points.ToArray());
    }

    private TalkPoint CreateElevatorTalkPoint(
        string pointName,
        TalkPointActionType actionType,
        string prompt,
        Vector2 position,
        AudioClip sound,
        float soundVolume)
    {
        return new TalkPoint
        {
            pointName = pointName,
            actionType = actionType,
            promptText = prompt,
            worldPosition = position,
            interactionRange = activeElevator != null
                ? Mathf.Max(0.05f, activeElevator.interactionRange)
                : 1.25f,
            promptOffset = activeElevator != null
                ? activeElevator.promptOffset
                : new Vector2(0f, 0.6f),
            interactionSound = sound,
            interactionSoundVolume = Mathf.Clamp01(soundVolume)
        };
    }

    private void HealPlayerToMaximum()
    {
        FindPlayerAndCameraIfNeeded();
        CombatHealth health = player != null
            ? player.GetComponent<CombatHealth>()
            : null;

        if (health == null)
        {
            Debug.LogWarning("E (Heal) needs CombatHealth on the Player.");
            return;
        }

        health.ResetHealth();
    }

    public void OpenFightStyleInventory()
    {
        if (inventoryOpen || transitionInProgress || activeBoss != null)
            return;

        FindPlayerAndCameraIfNeeded();
        PlayerCombatController combat = player != null
            ? player.GetComponent<PlayerCombatController>()
            : null;

        if (combat == null)
        {
            Debug.LogWarning("The fight-style inventory needs PlayerCombatController on the Player.");
            return;
        }

        combat.CancelAttack(true);
        inventoryPlayerController = player.GetComponent<PlayerSpriteController>();
        inventoryPlayerControllerWasEnabled = inventoryPlayerController != null &&
                                              inventoryPlayerController.enabled;
        if (inventoryPlayerController != null)
            inventoryPlayerController.enabled = false;

        inventoryScrollPosition = Vector2.zero;
        inventoryOpen = true;
        nearestTalkPoint = null;
        ApplyCursorState();
    }

    public void CloseFightStyleInventory()
    {
        if (!inventoryOpen)
            return;

        inventoryOpen = false;

        if (inventoryPlayerController != null)
            inventoryPlayerController.enabled = inventoryPlayerControllerWasEnabled;

        inventoryPlayerController = null;
        ApplyCursorState();
    }

    public void ExitElevator()
    {
        if (activeElevator == null)
        {
            Debug.LogWarning("There is no active elevator to exit.");
            return;
        }

        if (!elevatorExitUnlocked)
        {
            Debug.LogWarning("Finish the elevator NPC dialogue before exiting.");
            return;
        }

        if (transitionInProgress)
            return;

        if (inventoryOpen)
            CloseFightStyleInventory();

        StartCoroutine(ExitElevatorRoutine(activeElevator));
    }

    private IEnumerator ExitElevatorRoutine(ElevatorSequence sequence)
    {
        transitionInProgress = true;
        HideButtons();
        FindPlayerAndCameraIfNeeded();

        PlayerSpriteController playerController = player != null
            ? player.GetComponent<PlayerSpriteController>()
            : null;
        bool playerControllerWasEnabled = playerController != null &&
                                          playerController.enabled;
        if (playerController != null)
            playerController.enabled = false;

        yield return FadeScreenTo(1f, Mathf.Max(0f, sequence.exitFadeOutDuration));

        if (playerController != null)
            playerController.ClearArenaBounds();

        if (sequence.destinationBackground != null)
            SetBackground(sequence.destinationBackground);
        else
            Debug.LogWarning("Elevator '" + sequence.elevatorName + "' has no destination background assigned.");

        if (!string.IsNullOrWhiteSpace(sequence.destinationMusicTrack))
            ChangeBackgroundMusic(sequence.destinationMusicTrack.Trim());

        if (player != null && sequence.setDestinationPlayerPosition)
            player.position = sequence.destinationPlayerPosition;

        if (player != null && hasSavedPlayerScaleForElevator)
            player.localScale = playerScaleBeforeElevator;

        hasSavedPlayerScaleForElevator = false;

        activeElevator = null;
        elevatorNpcDialogue = null;
        elevatorExitUnlocked = false;

        if (!string.IsNullOrWhiteSpace(sequence.destinationTalkPointSet))
            SetButtons(sequence.destinationTalkPointSet.Trim());
        else
            ClearButtons();

        yield return FadeScreenTo(0f, Mathf.Max(0f, sequence.exitFadeInDuration));

        if (playerController != null)
            playerController.enabled = playerControllerWasEnabled;

        transitionInProgress = false;
    }

    private void DrawFightStyleInventory()
    {
        float width = Mathf.Min(inventoryPanelSize.x, Screen.width - 40f);
        float height = Mathf.Min(inventoryPanelSize.y, Screen.height - 40f);
        width = Mathf.Max(300f, width);
        height = Mathf.Max(260f, height);

        Rect panelRect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height
        );

        Color previousColor = GUI.color;
        GUI.color = inventoryPanelColor;
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = inventoryTitleFontSize,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = inventoryTextColor;
        GUI.Label(
            new Rect(panelRect.x + 20f, panelRect.y + 15f, panelRect.width - 40f, 55f),
            "Select Fighting Style",
            titleStyle
        );

        FindPlayerAndCameraIfNeeded();
        PlayerCombatController combat = player != null
            ? player.GetComponent<PlayerCombatController>()
            : null;
        string[] styleIds = combat != null
            ? combat.GetUnlockedStyleIds()
            : new string[0];

        Rect scrollViewRect = new Rect(
            panelRect.x + 35f,
            panelRect.y + 85f,
            panelRect.width - 70f,
            panelRect.height - 170f
        );
        float rowHeight = 64f;
        float contentHeight = Mathf.Max(scrollViewRect.height, styleIds.Length * rowHeight);
        Rect contentRect = new Rect(0f, 0f, scrollViewRect.width - 20f, contentHeight);
        inventoryScrollPosition = GUI.BeginScrollView(
            scrollViewRect,
            inventoryScrollPosition,
            contentRect
        );

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = inventoryButtonFontSize,
            fontStyle = FontStyle.Bold
        };
        buttonStyle.normal.textColor = inventoryTextColor;
        buttonStyle.hover.textColor = inventoryTextColor;
        buttonStyle.active.textColor = inventoryTextColor;

        for (int i = 0; i < styleIds.Length; i++)
        {
            string styleId = styleIds[i];
            bool isEquipped = combat != null &&
                              string.Equals(
                                  combat.EquippedStyleId,
                                  styleId,
                                  StringComparison.OrdinalIgnoreCase);
            string label = styleId + (isEquipped ? "  (Equipped)" : string.Empty);

            if (GUI.Button(
                    new Rect(0f, i * rowHeight, contentRect.width, rowHeight - 10f),
                    label,
                    buttonStyle))
            {
                EquipPlayerAttackStyle(styleId);
            }
        }

        if (styleIds.Length == 0)
        {
            GUI.Label(
                new Rect(0f, 0f, contentRect.width, 60f),
                "No fighting styles are available.",
                titleStyle
            );
        }

        GUI.EndScrollView();

        if (GUI.Button(
                new Rect(
                    panelRect.x + panelRect.width * 0.25f,
                    panelRect.yMax - 70f,
                    panelRect.width * 0.5f,
                    48f
                ),
                "Close",
                buttonStyle))
        {
            CloseFightStyleInventory();
        }
    }

    private void TryStartTalkPointCutscene(TalkPoint talkPoint)
    {
        if (talkPoint == null || string.IsNullOrWhiteSpace(talkPoint.cutsceneAfterDialogue))
            return;

        if (talkPoint.playCutsceneOnlyOnce && talkPoint.cutsceneHasPlayed)
            return;

        CutsceneSequence sequence = FindCutscene(talkPoint.cutsceneAfterDialogue);

        if (sequence == null)
        {
            Debug.LogWarning(
                "Talk point '" + talkPoint.pointName +
                "' refers to missing cutscene '" + talkPoint.cutsceneAfterDialogue + "'."
            );
            return;
        }

        talkPoint.cutsceneHasPlayed = true;



        if (lvl1_firstDialogRan >= Mathf.Max(1, npcWalkAwayDialogueCount))
            lvl1_firstDialogFinished = true;

        StartCoroutine(RunCutscene(sequence));
    }

    private CutsceneSequence FindCutscene(string cutsceneName)
    {
        if (cutsceneSequences == null)
            return null;

        foreach (CutsceneSequence sequence in cutsceneSequences)
        {
            if (sequence != null &&
                string.Equals(sequence.cutsceneName, cutsceneName, StringComparison.OrdinalIgnoreCase))
            {
                return sequence;
            }
        }

        return null;
    }


    public void PlayCutscene(string cutsceneName)
    {
        if (transitionInProgress)
        {
            Debug.LogWarning("A transition or cutscene is already running.");
            return;
        }

        CutsceneSequence sequence = FindCutscene(cutsceneName);

        if (sequence == null)
        {
            Debug.LogWarning("No cutscene named '" + cutsceneName + "' was found.");
            return;
        }

        StartCoroutine(RunCutscene(sequence));
    }

    private IEnumerator RunCutscene(
        CutsceneSequence sequence,
        bool preserveCurrentBoss = false)
    {
        if (preserveCurrentBoss)
        {


            UnregisterBossDeaths();
            if (activeBoss != null)
                activeBoss.StopFight();
        }
        else
        {
            EndBossFight();
        }
        transitionInProgress = true;

        if (!string.IsNullOrWhiteSpace(sequence.musicTrackOnStart))
            ChangeBackgroundMusic(sequence.musicTrackOnStart.Trim());

        bool buttonsWereEnabled = buttonsEnabled;
        TalkPoint[] previousTalkPoints = activeTalkPoints;
        HideButtons();

        FindPlayerAndCameraIfNeeded();

        GameObject playerObject = player != null ? player.gameObject : null;
        bool playerWasActive = playerObject != null && playerObject.activeSelf;
        Vector3 playerPositionBeforeCutscene = player != null
            ? player.position
            : Vector3.zero;

        PlayerSpriteController playerController = player != null
            ? player.GetComponent<PlayerSpriteController>()
            : null;
        bool playerControllerWasEnabled = playerController != null &&
                                          playerController.enabled;



        if (playerController != null)
            playerController.enabled = false;



        bool useOpeningFade = sequence.openingFadeOutDuration > 0f ||
                              sequence.openingFadeInDuration > 0f;

        if (useOpeningFade)
        {
            if (sequence.openingFadeOutDuration > 0f)
                yield return FadeScreenTo(1f, sequence.openingFadeOutDuration);
            else
                fadeAlpha = 1f;
        }
        else
        {
            fadeAlpha = 0f;
        }

        if (preserveCurrentBoss && activeBoss != null)
        {



            activeBoss.gameObject.SetActive(false);
        }

        if (playerObject != null)
            playerObject.SetActive(false);

        if (sequence.shots == null || sequence.shots.Length == 0)
        {
            Debug.LogWarning("Cutscene '" + sequence.cutsceneName + "' has no shots.");
        }
        else
        {
            for (int i = 0; i < sequence.shots.Length; i++)
            {
                CutsceneShot shot = sequence.shots[i];

                if (shot == null)
                    continue;

                if (i > 0)
                {
                    CutsceneShot previousShot = sequence.shots[i - 1];
                    float transitionDuration = previousShot != null
                        ? Mathf.Max(0f, previousShot.transitionToNextDuration)
                        : 0f;

                    if (transitionDuration <= 0f)
                    {



                        fadeAlpha = 0f;

                        if (shot.background != null)
                            SetBackground(shot.background);
                    }
                    else
                    {
                        float halfTransition = transitionDuration * 0.5f;

                        yield return FadeScreenTo(1f, halfTransition);

                        if (shot.background != null)
                            SetBackground(shot.background);

                        yield return FadeScreenTo(0f, halfTransition);
                    }
                }
                else
                {
                    if (shot.background != null)
                        SetBackground(shot.background);

                    if (useOpeningFade && sequence.openingFadeInDuration > 0f)
                        yield return FadeScreenTo(0f, sequence.openingFadeInDuration);
                    else
                        fadeAlpha = 0f;
                }

                PlayCutsceneSound(shot);

                if (shot.displayDuration > 0f)
                    yield return new WaitForSecondsRealtime(shot.displayDuration);
            }
        }

        if (sequence.endGameAfterCutscene)
        {



            ClearButtons();
            fadeAlpha = 0f;
            EnterGameOverState();
            yield break;
        }

        bool useEndingFade = sequence.endingFadeOutDuration > 0f ||
                             sequence.endingFadeInDuration > 0f;

        if (useEndingFade)
        {
            if (sequence.endingFadeOutDuration > 0f)
                yield return FadeScreenTo(1f, sequence.endingFadeOutDuration);
            else
                fadeAlpha = 1f;
        }
        else
        {
            fadeAlpha = 0f;
        }

        if (sequence.returnBackground != null)
            SetBackground(sequence.returnBackground);
        else
            Debug.LogWarning("Cutscene '" + sequence.cutsceneName + "' has no Return Background.");




        if (playerObject != null && playerWasActive)
        {
            playerObject.transform.position = playerPositionBeforeCutscene;
            playerObject.SetActive(true);
        }



        bool bossPrepared = PrepareBossFight(sequence.bossFight);

        if (useEndingFade && sequence.endingFadeInDuration > 0f)
            yield return FadeScreenTo(0f, sequence.endingFadeInDuration);
        else
            fadeAlpha = 0f;

        if (bossPrepared)
            yield return ShowBossIntroduction(sequence.bossFight);


        if (playerController != null)
            playerController.enabled = playerControllerWasEnabled;

        transitionInProgress = false;
        if (bossPrepared && BeginPreparedBossFight())
        {
            if (sequence.clearAllTalkPointsOnReturn)
                ClearButtons();
            else
            {
                if (!string.IsNullOrWhiteSpace(sequence.returnTalkPointSetName))
                    SetButtons(sequence.returnTalkPointSetName);
                else
                    activeTalkPoints = previousTalkPoints;
                HideButtons();
            }
            yield break;
        }


        if (sequence.clearAllTalkPointsOnReturn)
        {
            ClearButtons();
        }
        else if (!string.IsNullOrWhiteSpace(sequence.returnTalkPointSetName))
        {
            SetButtons(sequence.returnTalkPointSetName);
        }
        else
        {
            activeTalkPoints = previousTalkPoints;

            if (buttonsWereEnabled)
                ShowButtons();
        }

        transitionInProgress = false;
    }



    public void StartDebugBossFight()
    {
        if (transitionInProgress)
        {
            Debug.LogWarning("Wait for the current transition before using the boss debug shortcut.");
            return;
        }

        if (string.IsNullOrWhiteSpace(debugBossCutsceneName))
        {
            Debug.LogWarning("Boss debug shortcut needs a Debug Boss Cutscene Name.");
            return;
        }

        CutsceneSequence sequence = FindCutscene(debugBossCutsceneName);
        if (sequence == null)
        {
            Debug.LogWarning(
                "Boss debug shortcut could not find a cutscene named '" +
                debugBossCutsceneName + "'."
            );
            return;
        }

        if (sequence.bossFight == null || sequence.bossFight.bossPrefab == null)
        {
            Debug.LogWarning(
                "Cutscene '" + sequence.cutsceneName +
                "' has no Boss Prefab in Boss Fight After Cutscene."
            );
            return;
        }

        displayedDialogue = null;
        bossIntroDialogue = null;
        nearestTalkPoint = null;
        if (voiceLineAudioSource != null)
            voiceLineAudioSource.Stop();

        if (debugUseReturnBackground && sequence.returnBackground != null)
            SetBackground(sequence.returnBackground);

        StartBossFight(sequence.bossFight);
    }


    public void StartBossFight(BossFightSettings settings)
    {
        if (transitionInProgress)
        {
            Debug.LogWarning("Wait for the current transition before starting another boss fight.");
            return;
        }
        StartCoroutine(StartBossFightRoutine(settings));
    }

    private IEnumerator StartBossFightRoutine(BossFightSettings settings)
    {
        EndBossFight();
        if (!PrepareBossFight(settings))
            yield break;
        transitionInProgress = true;
        HideButtons();
        PlayerSpriteController controller = bossFightPlayerController;
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controller != null)
            controller.enabled = false;
        yield return ShowBossIntroduction(settings);
        if (controller != null)
            controller.enabled = controllerWasEnabled;
        transitionInProgress = false;
        BeginPreparedBossFight();
    }

    private bool PrepareBossFight(BossFightSettings settings)
    {
        if (settings == null || settings.bossPrefab == null)
            return false;
        FindPlayerAndCameraIfNeeded();
        CombatHealth health = player != null ? player.GetComponent<CombatHealth>() : null;
        if (health == null || !health.isActiveAndEnabled || health.Hitbox == null ||
            !health.Hitbox.enabled || !health.Hitbox.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("Boss fight needs an active Player with CombatHealth and an enabled Collider2D assigned as its Hitbox.");
            return false;
        }
        if (!settings.resetPlayerHealth && health.IsDead)
        {
            Debug.LogWarning("Cannot start a boss fight with a defeated player. Enable Reset Player Health.");
            return false;
        }

        Vector3 spawn = settings.spawnPoint != null
            ? settings.spawnPoint.position : settings.spawnPosition;
        activeBoss = Instantiate(settings.bossPrefab, spawn, Quaternion.identity);
        activeBoss.gameObject.SetActive(true);
        if (activeBoss.Health != null)
            activeBoss.Health.SetDisappearOnDeath(false);
        if (!activeBoss.isActiveAndEnabled || !activeBoss.HasConfiguredAttack)
        {
            Debug.LogWarning("Boss prefab needs an enabled BossController and a configured Attack Behaviour.");
            EndBossFight();
            return false;
        }
        Vector3 bossPosition = activeBoss.transform.position;
        bossPosition.x = Mathf.Clamp(bossPosition.x, activeBoss.ArenaMinX, activeBoss.ArenaMaxX);
        activeBoss.transform.position = bossPosition;
        activeBoss.FaceTowards(player.position);
        activeBossSettings = settings;
        bossFightPlayerHealth = health;
        bossFightPlayerController = player.GetComponent<PlayerSpriteController>();
        if (bossFightPlayerController != null)
            bossFightPlayerController.ClearDefeatedPose();
        if (settings.resetPlayerHealth)
            health.ResetHealth();
        health.SetTeam(CombatHealth.CombatTeam.Player);
        health.Died += HandlePlayerDefeated;
        if (!string.IsNullOrWhiteSpace(settings.backgroundMusicTrack))
            ChangeBackgroundMusic(settings.backgroundMusicTrack);
        return true;
    }

    private IEnumerator ShowBossIntroduction(BossFightSettings settings)
    {
        if (settings == null || activeBoss == null)
            yield break;
        bool hasText = !string.IsNullOrWhiteSpace(settings.introDialogue);
        if (hasText)
        {
            bossIntroDialogue = new TalkPoint
            {
                dialogueText = settings.introDialogue,
                italicizeDialogue = settings.italicizeIntro
            };
            bossDialogueRevealStartTime = Time.unscaledTime;
            bossDialogueRevealDuration = Mathf.Max(0f, settings.introDuration);
        }
        if (settings.introVoiceLine != null)
        {
            if (voiceLineAudioSource == null)
                voiceLineAudioSource = gameObject.AddComponent<AudioSource>();
            voiceLineAudioSource.playOnAwake = false;
            voiceLineAudioSource.loop = false;
            voiceLineAudioSource.spatialBlend = 0f;
            voiceLineAudioSource.PlayOneShot(settings.introVoiceLine,
                Mathf.Clamp01(settings.introVoiceVolume));
        }
        float duration = Mathf.Max(
            hasText
                ? bossDialogueRevealDuration + Mathf.Max(0f, fullTextHoldDuration)
                : 0f,
            settings.introVoiceLine != null ? settings.introVoiceLine.length : 0f);
        if (duration > 0f)
            yield return new WaitForSecondsRealtime(duration);
        bossIntroDialogue = null;
    }

    private bool BeginPreparedBossFight()
    {
        if (activeBoss == null || !activeBoss.BeginFight(bossFightPlayerHealth))
        {
            EndBossFight();
            return false;
        }
        if (activeBossSettings.confinePlayerToArena && bossFightPlayerController != null)
            bossFightPlayerController.SetArenaBounds(activeBoss.ArenaMinX, activeBoss.ArenaMaxX);
        return true;
    }


    public void HandleKilledBoss(BossController defeated)
    {
        if (defeated == null || defeated.Health == null || !defeated.Health.IsDead)
            return;
        string defeatedId = defeated.BossId;
        bool isActiveEncounterBoss = activeBoss == defeated;
        BossFightSettings settings = isActiveEncounterBoss ? activeBossSettings : null;
        OnKilledBoss(defeatedId);

        if (settings == null)
        {
            if (isActiveEncounterBoss)
                EndBossFight();
            return;
        }

        if (settings.fadeAfterBossDefeated)
        {


            StartCoroutine(RunBossDefeatedTransition(settings, defeated));
        }
        else
        {
            EndBossFight();
            if (settings.onBossDefeated != null)
                settings.onBossDefeated.Invoke();
        }
    }

    private IEnumerator RunBossDefeatedTransition(
        BossFightSettings settings,
        BossController defeatedBoss)
    {
        transitionInProgress = true;
        nearestTalkPoint = null;

        FindPlayerAndCameraIfNeeded();
        PlayerSpriteController playerController = player != null
            ? player.GetComponent<PlayerSpriteController>()
            : null;
        bool playerControllerWasEnabled = playerController != null &&
                                          playerController.enabled;


        if (playerController != null)
            playerController.enabled = false;

        yield return FadeScreenTo(
            1f,
            Mathf.Max(0f, settings.bossDefeatedFadeOutDuration)
        );



        if (activeBoss == defeatedBoss)
            EndBossFight();
        else if (defeatedBoss != null)
            Destroy(defeatedBoss.gameObject);


        if (settings.onBossDefeated != null)
            settings.onBossDefeated.Invoke();

        yield return FadeScreenTo(
            0f,
            Mathf.Max(0f, settings.bossDefeatedFadeInDuration)
        );

        if (playerController != null)
            playerController.enabled = playerControllerWasEnabled;

        transitionInProgress = false;
    }



    public void OnKilledBoss(string bossId)
    {
        if (string.IsNullOrWhiteSpace(bossId) || HasDefeatedBoss(bossId))
            return;
        string id = bossId.Trim().ToLowerInvariant();
        defeatedBossIds.Add(id);
        SaveBossProgress();


        onKilledBossEvent.Invoke(id);
    }

    public bool HasDefeatedBoss(string bossId)
    {
        if (string.IsNullOrWhiteSpace(bossId) || defeatedBossIds == null)
            return false;
        return defeatedBossIds.Exists(id => string.Equals(id, bossId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public void ResetBossProgress()
    {
        defeatedBossIds.Clear();
        if (persistBossProgress)
        {
            PlayerPrefs.DeleteKey(bossProgressSaveKey);
            PlayerPrefs.Save();
        }
    }

    private void SaveBossProgress()
    {
        if (!persistBossProgress)
            return;
        PlayerPrefs.SetString(bossProgressSaveKey, JsonUtility.ToJson(new BossProgressSave { ids = defeatedBossIds }));
        PlayerPrefs.Save();
    }

    private void LoadBossProgress()
    {
        if (defeatedBossIds == null)
            defeatedBossIds = new List<string>();
        if (!persistBossProgress || !PlayerPrefs.HasKey(bossProgressSaveKey))
            return;
        try
        {
            BossProgressSave save = JsonUtility.FromJson<BossProgressSave>(PlayerPrefs.GetString(bossProgressSaveKey));
            if (save != null && save.ids != null)
                foreach (string id in save.ids)
                    if (!string.IsNullOrWhiteSpace(id) && !HasDefeatedBoss(id))
                        defeatedBossIds.Add(id.Trim().ToLowerInvariant());
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not load saved boss progress: " + exception.Message);
        }
    }

    private void HandlePlayerDefeated(CombatHealth defeated)
    {
        BossFightSettings settings = activeBossSettings;
        UnregisterBossDeaths();
        if (activeBoss != null)
            activeBoss.StopFight();
        if (bossFightPlayerController != null)
        {
            bossFightPlayerController.ClearArenaBounds();
            if (settings != null && settings.playerDefeatedSprite != null)
            {
                bossFightPlayerController.SetDefeatedPose(
                    settings.playerDefeatedSprite,
                    settings.playerDefeatedSpriteScale
                );
            }
        }
        if (settings != null && settings.onPlayerDefeated != null)
            settings.onPlayerDefeated.Invoke();

        if (settings != null &&
            (!string.IsNullOrWhiteSpace(settings.playerDefeatedDialogue) ||
             settings.playerDefeatedVoiceLine != null ||
             !string.IsNullOrWhiteSpace(settings.cutsceneAfterPlayerDefeated)))
        {
            StartCoroutine(RunPlayerDefeatedSequence(settings));
        }
    }

    private IEnumerator RunPlayerDefeatedSequence(BossFightSettings settings)
    {
        transitionInProgress = true;
        nearestTalkPoint = null;
        HideButtons();

        if (bossFightPlayerController != null)
            bossFightPlayerController.enabled = false;

        bool hasDialogue = !string.IsNullOrWhiteSpace(settings.playerDefeatedDialogue);
        if (hasDialogue)
        {
            bossIntroDialogue = new TalkPoint
            {
                dialogueText = settings.playerDefeatedDialogue,
                italicizeDialogue = settings.italicizePlayerDefeatedDialogue
            };
            bossDialogueRevealStartTime = Time.unscaledTime;
            bossDialogueRevealDuration = Mathf.Max(
                0f,
                settings.playerDefeatedDialogueDuration
            );
        }

        if (settings.playerDefeatedVoiceLine != null)
        {
            if (voiceLineAudioSource == null)
                voiceLineAudioSource = gameObject.AddComponent<AudioSource>();
            voiceLineAudioSource.playOnAwake = false;
            voiceLineAudioSource.loop = false;
            voiceLineAudioSource.spatialBlend = 0f;
            voiceLineAudioSource.PlayOneShot(
                settings.playerDefeatedVoiceLine,
                Mathf.Clamp01(settings.playerDefeatedVoiceVolume)
            );
        }

        float dialogueTime = hasDialogue
            ? bossDialogueRevealDuration + Mathf.Max(0f, fullTextHoldDuration)
            : 0f;
        float voiceTime = settings.playerDefeatedVoiceLine != null
            ? settings.playerDefeatedVoiceLine.length
            : 0f;
        float waitTime = Mathf.Max(dialogueTime, voiceTime);
        if (waitTime > 0f)
            yield return new WaitForSecondsRealtime(waitTime);

        bossIntroDialogue = null;

        if (string.IsNullOrWhiteSpace(settings.cutsceneAfterPlayerDefeated))
        {
            transitionInProgress = false;
            yield break;
        }

        CutsceneSequence sequence = FindCutscene(settings.cutsceneAfterPlayerDefeated);
        if (sequence == null)
        {
            Debug.LogWarning(
                "No player-defeat cutscene named '" +
                settings.cutsceneAfterPlayerDefeated + "' was found."
            );
            transitionInProgress = false;
            yield break;
        }

        yield return RunCutscene(sequence, true);
    }


    public void EndBossFight()
    {
        if (player != null)
        {
            PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
            if (combat != null)
                combat.CancelAttack(true);
        }
        UnregisterBossDeaths();
        BossController bossToRemove = activeBoss;
        activeBoss = null;
        activeBossSettings = null;
        bossIntroDialogue = null;
        bossFightPlayerHealth = null;
        if (bossFightPlayerController != null)
            bossFightPlayerController.ClearArenaBounds();
        bossFightPlayerController = null;
        if (bossToRemove != null)
        {
            bossToRemove.StopFight();
            Destroy(bossToRemove.gameObject);
        }
    }

    private void UnregisterBossDeaths()
    {
        if (bossFightPlayerHealth != null)
            bossFightPlayerHealth.Died -= HandlePlayerDefeated;
    }

    private void DrawPlayerHealth()
    {
        if (!showPlayerHealth)
            return;
        FindPlayerAndCameraIfNeeded();
        if (player == null || !player.gameObject.activeInHierarchy)
            return;
        CombatHealth health = player.GetComponent<CombatHealth>();
        if (health == null)
            return;
        if (healthTextStyle == null)
            healthTextStyle = new GUIStyle(GUI.skin.label);
        healthTextStyle.fontSize = healthFontSize;
        healthTextStyle.fontStyle = FontStyle.Bold;
        healthTextStyle.normal.textColor = healthTextColor;
        healthTextStyle.alignment = TextAnchor.UpperLeft;
        GUI.Label(new Rect(healthTextTopLeft.x, healthTextTopLeft.y,
            Mathf.Max(100f, Screen.width - healthTextTopLeft.x), healthFontSize * 1.7f),
            health.CurrentHealth + " / " + health.MaxHealth, healthTextStyle);
    }

    private void PlayCutsceneSound(CutsceneShot shot)
    {
        if (shot == null || shot.soundEffect == null)
            return;

        if (cutsceneAudioSource == null)
        {
            Debug.LogWarning(
                "Cutscene shot '" + shot.shotName +
                "' has a sound effect, but Cutscene Audio Source is not assigned."
            );
            return;
        }

        cutsceneAudioSource.PlayOneShot(
            shot.soundEffect,
            Mathf.Clamp01(shot.soundVolume)
        );
    }

    private IEnumerator FirstNpcWalksAwayTransition()
    {
        transitionInProgress = true;



        ClearButtons();

        FindPlayerAndCameraIfNeeded();

        PlayerSpriteController playerController = null;
        bool playerControllerWasEnabled = false;

        if (lockPlayerDuringTransition && player != null)
        {
            playerController = player.GetComponent<PlayerSpriteController>();

            if (playerController != null)
            {
                playerControllerWasEnabled = playerController.enabled;
                playerController.enabled = false;
            }
        }

        yield return FadeScreenTo(1f, fadeToBlackDuration);

        AudioClip clipToPlay = footstepSound;

        if (clipToPlay == null && footstepAudioSource != null)
            clipToPlay = footstepAudioSource.clip;

        float blackScreenTime = Mathf.Max(0f, footstepPlayTime);

        if (footstepAudioSource != null && clipToPlay != null)
        {
            if (loopFootstepSound)
            {
                footstepAudioSource.clip = clipToPlay;
                footstepAudioSource.loop = true;
                footstepAudioSource.Play();
            }
            else
            {
                footstepAudioSource.PlayOneShot(clipToPlay);
                blackScreenTime = Mathf.Max(blackScreenTime, clipToPlay.length);
            }
        }

        if (blackScreenTime > 0f)
            yield return new WaitForSecondsRealtime(blackScreenTime);

        if (footstepAudioSource != null && loopFootstepSound)
        {
            footstepAudioSource.Stop();
            footstepAudioSource.loop = false;
        }



        SetBackground(backgroundAfterNpcLeaves);

        yield return FadeScreenTo(0f, fadeFromBlackDuration);

        if (playerController != null)
            playerController.enabled = playerControllerWasEnabled;

        transitionInProgress = false;
    }

    private IEnumerator FadeScreenTo(float targetAlpha, float duration)
    {
        float startAlpha = fadeAlpha;

        if (duration <= 0f)
        {
            fadeAlpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        fadeAlpha = targetAlpha;
    }

    private void DrawStartMenu()
    {
        DrawStartMenuBackground();
        DrawFullScreenColor(startMenuDimColor);

        float titleHeight = Mathf.Max(90f, startMenuTitleFontSize * 1.5f);
        Rect titleRect = new Rect(
            20f,
            Screen.height * 0.16f,
            Mathf.Max(0f, Screen.width - 40f),
            titleHeight
        );

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Max(24, startMenuTitleFontSize),
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = startMenuTitleColor;

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.Label(new Rect(titleRect.x + 4f, titleRect.y + 5f,
            titleRect.width, titleRect.height), startMenuTitle, titleStyle);
        GUI.color = previousColor;
        GUI.Label(titleRect, startMenuTitle, titleStyle);

        float buttonWidth = Mathf.Min(
            Mathf.Max(180f, menuButtonSize.x),
            Mathf.Max(180f, Screen.width - 40f)
        );
        float buttonHeight = Mathf.Max(48f, menuButtonSize.y);
        float buttonX = (Screen.width - buttonWidth) * 0.5f;
        float playY = Screen.height * 0.56f;

        if (DrawMenuButton(
            new Rect(buttonX, playY, buttonWidth, buttonHeight),
            "PLAY",
            menuButtonColor,
            menuButtonTextColor))
        {
            PlayFromStartMenu();
        }

        if (DrawMenuButton(
            new Rect(buttonX, playY + buttonHeight + 22f, buttonWidth, buttonHeight),
            "EXIT",
            menuButtonColor,
            menuButtonTextColor))
        {
            QuitGame();
        }
    }

    private void DrawStartMenuBackground()
    {
        Sprite background = startMenuBackground != null
            ? startMenuBackground
            : lvl1_1;

        if (background == null)
        {
            DrawFullScreenColor(Color.black);
            return;
        }

        DrawBlurredSpriteFullScreen(
            background,
            new Rect(0f, 0f, Screen.width, Screen.height)
        );
    }

    private void DrawGameOverScreen()
    {
        float reveal = gameOverFadeInDuration <= 0f
            ? 1f
            : Mathf.Clamp01(
                (Time.unscaledTime - gameOverFadeStartTime) /
                gameOverFadeInDuration
            );

        Sprite current = CurrentBackground;
        if (current != null)
        {
            DrawBlurredSpriteFullScreen(
                current,
                new Rect(0f, 0f, Screen.width, Screen.height),
                reveal
            );
        }
        else if (reveal >= 1f)
        {
            DrawFullScreenColor(Color.black);
        }

        DrawFullScreenColor(new Color(0f, 0f, 0f, 0.58f * reveal));
        DrawFullScreenColor(WithMultipliedAlpha(gameOverTintColor, reveal));

        Rect titleRect = new Rect(
            20f,
            Screen.height * 0.28f,
            Mathf.Max(0f, Screen.width - 40f),
            Mathf.Max(110f, gameOverFontSize * 1.6f)
        );
        DrawOutlinedCenteredLabel(
            titleRect,
            gameOverTitle,
            Mathf.Max(24, gameOverFontSize),
            gameOverTextColor,
            gameOverTextOutlineColor,
            Mathf.Clamp(gameOverTextOutlineThickness, 0f, 3f),
            gameOverFont,
            reveal
        );

        Rect messageRect = new Rect(
            30f,
            Screen.height * 0.43f,
            Mathf.Max(0f, Screen.width - 60f),
            Mathf.Max(52f, gameOverMessageFontSize * 1.8f)
        );
        DrawOutlinedCenteredLabel(
            messageRect,
            gameOverMessage,
            Mathf.Max(14, gameOverMessageFontSize),
            gameOverMessageColor,
            gameOverTextOutlineColor,
            Mathf.Clamp(gameOverTextOutlineThickness, 0f, 3f),
            gameOverFont,
            reveal
        );

        float buttonWidth = Mathf.Min(
            Mathf.Max(180f, menuButtonSize.x),
            Mathf.Max(180f, Screen.width - 40f)
        );
        float buttonHeight = Mathf.Max(48f, menuButtonSize.y);
        Rect exitRect = new Rect(
            (Screen.width - buttonWidth) * 0.5f,
            Screen.height * 0.62f,
            buttonWidth,
            buttonHeight
        );

        float buttonReveal = Mathf.Clamp01((reveal - 0.7f) / 0.3f);
        if (buttonReveal > 0f && DrawMenuButton(
                exitRect,
                "EXIT",
                new Color(0.16f, 0.01f, 0.01f, 0.94f * buttonReveal),
                new Color(1f, 1f, 1f, buttonReveal)))
        {
            QuitGame();
        }
    }

    private void DrawBlurredSpriteFullScreen(
        Sprite sprite,
        Rect destination,
        float opacity = 1f)
    {
        if (sprite == null || sprite.texture == null)
            return;

        opacity = Mathf.Clamp01(opacity);
        float radius = Mathf.Max(0f, menuBackgroundBlurRadius);
        Color previousColor = GUI.color;

        if (radius <= 0.01f)
        {
            GUI.color = new Color(1f, 1f, 1f, opacity);
            DrawSpriteCover(sprite, destination);
            GUI.color = previousColor;
            return;
        }



        for (int vertical = -1; vertical <= 1; vertical++)
        {
            for (int horizontal = -1; horizontal <= 1; horizontal++)
            {
                float offsetX = horizontal * radius;
                float offsetY = vertical * radius;
                GUI.color = new Color(1f, 1f, 1f, 0.16f * opacity);
                DrawSpriteCover(
                    sprite,
                    new Rect(
                        destination.x + offsetX - radius,
                        destination.y + offsetY - radius,
                        destination.width + radius * 2f,
                        destination.height + radius * 2f
                    )
                );
            }
        }

        GUI.color = previousColor;
    }

    private static void DrawSpriteCover(Sprite sprite, Rect destination)
    {
        Texture2D texture = sprite.texture;
        Rect source = sprite.rect;
        if (texture == null || source.width <= 0f || source.height <= 0f ||
            destination.width <= 0f || destination.height <= 0f)
        {
            return;
        }

        float destinationAspect = destination.width / destination.height;
        float sourceAspect = source.width / source.height;
        Rect visible = source;

        if (sourceAspect > destinationAspect)
        {
            float visibleWidth = source.height * destinationAspect;
            visible.x += (source.width - visibleWidth) * 0.5f;
            visible.width = visibleWidth;
        }
        else
        {
            float visibleHeight = source.width / destinationAspect;
            visible.y += (source.height - visibleHeight) * 0.5f;
            visible.height = visibleHeight;
        }

        Rect uv = new Rect(
            visible.x / texture.width,
            visible.y / texture.height,
            visible.width / texture.width,
            visible.height / texture.height
        );
        GUI.DrawTextureWithTexCoords(destination, texture, uv, true);
    }

    private bool DrawMenuButton(
        Rect rect,
        string text,
        Color backgroundColor,
        Color textColor)
    {
        bool hovered = rect.Contains(Event.current.mousePosition);
        Color previousColor = GUI.color;
        Color fill = backgroundColor;
        if (hovered)
        {
            fill.r = Mathf.Min(1f, fill.r + 0.08f);
            fill.g = Mathf.Min(1f, fill.g + 0.08f);
            fill.b = Mathf.Min(1f, fill.b + 0.08f);
        }

        GUI.color = fill;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Max(16, menuButtonFontSize),
            fontStyle = FontStyle.Bold
        };
        style.normal.background = null;
        style.hover.background = null;
        style.active.background = null;
        style.focused.background = null;
        style.normal.textColor = textColor;
        style.hover.textColor = textColor;
        style.active.textColor = textColor;
        style.focused.textColor = textColor;

        return GUI.Button(rect, text, style);
    }

    private static void DrawOutlinedCenteredLabel(
        Rect rect,
        string text,
        int fontSize,
        Color textColor,
        Color outlineColor,
        float outlineThickness,
        Font font,
        float opacity)
    {
        opacity = Mathf.Clamp01(opacity);
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold
        };
        if (font != null)
            style.font = font;

        Color previousColor = GUI.color;
        GUI.color = Color.white;
        float thickness = Mathf.Round(Mathf.Clamp(outlineThickness, 0f, 3f));
        if (thickness > 0f)
        {
            style.normal.textColor = WithMultipliedAlpha(outlineColor, opacity);
            Vector2[] offsets =
            {
                new Vector2(-thickness, 0f),
                new Vector2(thickness, 0f),
                new Vector2(0f, -thickness),
                new Vector2(0f, thickness)
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 offset = offsets[i];
                GUI.Label(new Rect(
                    rect.x + offset.x,
                    rect.y + offset.y,
                    rect.width,
                    rect.height
                ), text, style);
            }
        }

        style.normal.textColor = WithMultipliedAlpha(textColor, opacity);
        GUI.Label(rect, text, style);
        GUI.color = previousColor;
    }

    private static Color WithMultipliedAlpha(Color color, float multiplier)
    {
        color.a *= Mathf.Clamp01(multiplier);
        return color;
    }

    private static void DrawFullScreenColor(Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(
            new Rect(0f, 0f, Screen.width, Screen.height),
            Texture2D.whiteTexture
        );
        GUI.color = previousColor;
    }

    private void DrawFadeOverlay()
    {
        if (fadeAlpha <= 0f)
            return;

        Color previousGuiColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(fadeAlpha));
        GUI.DrawTexture(
            new Rect(0f, 0f, Screen.width, Screen.height),
            Texture2D.whiteTexture
        );
        GUI.color = previousGuiColor;
    }

    private void DrawWorldSpaceBox(
        Vector3 worldPosition,
        string text,
        Vector2 boxSize,
        int fontSize,
        Color textColor,
        bool wordWrap)
    {
        Vector3 screenPosition = interactionCamera.WorldToScreenPoint(worldPosition);


        if (screenPosition.z <= 0f)
            return;

        float guiX = screenPosition.x - boxSize.x * 0.5f;
        float guiY = Screen.height - screenPosition.y - boxSize.y * 0.5f;

        Rect promptRect = new Rect(guiX, guiY, boxSize.x, boxSize.y);



        GUI.Box(promptRect, GUIContent.none);

        GUIStyle textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            wordWrap = wordWrap,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            contentOffset = Vector2.zero
        };

        textStyle.normal.textColor = textColor;

        GUI.Label(promptRect, text ?? string.Empty, textStyle);
    }

    private void DrawBottomDialogueBox(
        TalkPoint talkPoint,
        float revealStartTime,
        float revealDuration
    )
    {

        float width = Mathf.Min(dialogueBoxSize.x, Screen.width - 40f);
        float maximumHeight = Mathf.Max(80f, Screen.height - dialogueBottomMargin - 20f);
        float height = Mathf.Min(dialogueBoxSize.y, maximumHeight);

        Rect boxRect = new Rect(
            (Screen.width - width) * 0.5f,
            Screen.height - dialogueBottomMargin - height,
            width,
            height
        );



        Color previousGuiColor = GUI.color;
        GUI.color = dialogueBoxColor;
        GUI.DrawTexture(boxRect, Texture2D.whiteTexture);
        GUI.color = previousGuiColor;

        float horizontalPadding = Mathf.Max(0f, dialogueTextPadding.x);
        float verticalPadding = Mathf.Max(0f, dialogueTextPadding.y);

        Rect textRect = new Rect(
            boxRect.x + horizontalPadding,
            boxRect.y + verticalPadding,
            Mathf.Max(0f, boxRect.width - horizontalPadding * 2f),
            Mathf.Max(0f, boxRect.height - verticalPadding * 2f)
        );

        GUIStyle dialogueStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = dialogueFontSize,
            fontStyle = talkPoint.italicizeDialogue
                ? FontStyle.BoldAndItalic
                : FontStyle.Bold,
            wordWrap = true
        };

        dialogueStyle.normal.textColor = dialogueTextColor;
        string visibleText = GetRevealedDialogueText(
            talkPoint.dialogueText,
            revealStartTime,
            revealDuration
        );
        GUI.Label(
            textRect,
            visibleText,
            dialogueStyle
        );
    }

    private static string GetRevealedDialogueText(
        string fullText,
        float revealStartTime,
        float revealDuration
    )
    {
        if (string.IsNullOrEmpty(fullText))
            return string.Empty;

        if (revealDuration <= 0f)
            return fullText;

        float elapsed = Mathf.Max(0f, Time.unscaledTime - revealStartTime);
        float progress = Mathf.Clamp01(elapsed / revealDuration);
        int visibleCharacterCount = Mathf.Clamp(
            Mathf.FloorToInt(progress * fullText.Length),
            0,
            fullText.Length
        );

        return visibleCharacterCount >= fullText.Length
            ? fullText
            : fullText.Substring(0, visibleCharacterCount);
    }

    private void OnDrawGizmosSelected()
    {
        if (talkPointSets == null)
            return;

        Gizmos.color = Color.yellow;

        foreach (TalkPointSet pointSet in talkPointSets)
        {
            if (pointSet == null || pointSet.talkPoints == null)
                continue;

            foreach (TalkPoint point in pointSet.talkPoints)
            {
                if (point == null)
                    continue;

                Gizmos.DrawWireSphere(point.GetWorldPosition(), 0.15f);
            }
        }
    }
}
