using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    public enum GameState
    {
        Menu,
        Battle,
        Paused
    }

    public static GameStateManager Instance { get; private set; }

    [Header("Core")]
    public GameState currentState = GameState.Menu;
    public WaveSpawner waveSpawner;
    public HeroSlotManager heroSlotManager;
    public GameObject heroPanel;

    [Header("UI Groups")]
    [Tooltip("Объект-родитель для интерфейса меню. Можно оставить пустым до создания меню.")]
    public GameObject menuUI;

    [Tooltip("Объект-родитель для боевого HUD. Можно оставить пустым до группировки HUD.")]
    public GameObject battleUI;

    [Tooltip("Панель паузы. Пока можно оставить пустой.")]
    public GameObject pauseUI;

    [Tooltip("Кнопка паузы, видна только во время боя.")]
    public GameObject pauseButton;

    [Tooltip("Кнопка начала боя с мечами. Видна только в меню.")]
    public GameObject playButton;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        EnterMenu();
    }

    public void EnterMenu()
    {
        currentState = GameState.Menu;
        Time.timeScale = 1f;

        if (waveSpawner != null)
            waveSpawner.StopBattle();

        RestoreCastle();

        if (heroPanel != null)
        {
            HeroPanelController controller = heroPanel.GetComponent<HeroPanelController>();
            if (controller != null)
                controller.ClosePanel();
            else
                heroPanel.SetActive(false);
        }

        if (heroSlotManager != null)
            heroSlotManager.SetMenuSlotVisibility(true);

        if (menuUI != null)
            menuUI.SetActive(true);

        if (battleUI != null)
            battleUI.SetActive(false);

        if (pauseUI != null)
            pauseUI.SetActive(false);

        if (pauseButton != null)
            pauseButton.SetActive(false);

        if (playButton != null)
            playButton.SetActive(true);
    }

    // Привяжем к кнопке с двумя мечами.
    public void StartBattle()
    {
        currentState = GameState.Battle;
        Time.timeScale = 1f;

        RestoreCastle();

        if (heroSlotManager != null)
            heroSlotManager.SetMenuSlotVisibility(false);

        if (menuUI != null)
            menuUI.SetActive(false);

        if (battleUI != null)
            battleUI.SetActive(true);

        if (pauseUI != null)
            pauseUI.SetActive(false);

        if (pauseButton != null)
            pauseButton.SetActive(true);

        if (playButton != null)
            playButton.SetActive(false);

        if (heroPanel != null)
        {
            HeroPanelController controller = heroPanel.GetComponent<HeroPanelController>();
            if (controller != null)
                controller.ClosePanel();
            else
                heroPanel.SetActive(false);
        }

        if (waveSpawner != null)
            waveSpawner.BeginBattle();
    }

    public void PauseBattle()
    {
        if (currentState != GameState.Battle)
            return;

        currentState = GameState.Paused;
        Time.timeScale = 0f;

        if (pauseUI != null)
            pauseUI.SetActive(true);

        if (pauseButton != null)
            pauseButton.SetActive(false);
    }

    public void ResumeBattle()
    {
        if (currentState != GameState.Paused)
            return;

        currentState = GameState.Battle;
        Time.timeScale = 1f;

        if (pauseUI != null)
            pauseUI.SetActive(false);

        if (pauseButton != null)
            pauseButton.SetActive(true);
    }

    public void RestartBattle()
    {
        if (currentState != GameState.Paused)
            return;

        Time.timeScale = 1f;
        currentState = GameState.Battle;

        if (pauseUI != null)
            pauseUI.SetActive(false);

        if (pauseButton != null)
            pauseButton.SetActive(true);

        RestoreCastle();

        if (waveSpawner != null)
            waveSpawner.RestartCurrentWave();
    }

    void RestoreCastle()
    {
        Castle castle = FindAnyObjectByType<Castle>();
        if (castle != null)
            castle.RestoreToFull();
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        EnterMenu();
    }
}
