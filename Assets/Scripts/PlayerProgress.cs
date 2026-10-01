using TMPro;
using UnityEngine;

public class PlayerProgress : MonoBehaviour
{
    public static PlayerProgress Instance { get; private set; }

    [Header("Save System")]
    [Tooltip("OFF = developer/testing mode: save is neither loaded nor written. ON = normal player persistence.")]
    public bool enablePersistentSave = false;
    [Tooltip("Developer button-like switch: enable before Play to delete the current local save once, then it turns itself off.")]
    public bool resetSaveOnPlay = false;

    [Header("Currency")]
    public int gold = 0;
    public int emeralds = 0;

    [Header("Experience")]
    public int level = 1;
    public int currentExperience = 0;
    public int baseExperienceToLevel = 100;
    public int experienceIncreasePerLevel = 50;

    [Header("UI")]
    public TMP_Text goldText;
    public TMP_Text emeraldText;
    public TMP_Text levelText;
    public RectTransform xpFill;

    private float fullXpBarWidth;
    private Vector2 fullXpBarPosition;
    private int lastInspectorGold = int.MinValue;
    private int lastInspectorEmeralds = int.MinValue;
    private int lastInspectorLevel = int.MinValue;
    private int lastInspectorExperience = int.MinValue;

    public int ExperienceToNextLevel =>
        baseExperienceToLevel + (level - 1) * experienceIncreasePerLevel;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (GetComponent<SaveSystem>() == null)
            gameObject.AddComponent<SaveSystem>();
    }

    void Start()
    {
        if (xpFill != null)
        {
            fullXpBarWidth = xpFill.sizeDelta.x;
            fullXpBarPosition = xpFill.anchoredPosition;
        }

        UpdateUI();
        CacheInspectorValues();
    }

    void Update()
    {
        // Useful for development: if currency/XP is changed directly in the
        // Inspector during Play, refresh the HUD immediately.
        if (gold != lastInspectorGold || emeralds != lastInspectorEmeralds ||
            level != lastInspectorLevel || currentExperience != lastInspectorExperience)
        {
            UpdateUI();
            CacheInspectorValues();
        }
    }

    void CacheInspectorValues()
    {
        lastInspectorGold = gold;
        lastInspectorEmeralds = emeralds;
        lastInspectorLevel = level;
        lastInspectorExperience = currentExperience;
    }

    public void AddGold(int amount)
    {
        gold += Mathf.Max(0, amount);
        UpdateUI();
    }

    public void AddEmeralds(int amount)
    {
        emeralds += Mathf.Max(0, amount);
        UpdateUI();
    }

    public void AddExperience(int amount)
    {
        currentExperience += Mathf.Max(0, amount);

        while (currentExperience >= ExperienceToNextLevel)
        {
            int needed = ExperienceToNextLevel;
            currentExperience -= needed;
            level++;
        }

        UpdateUI();
    }

    public bool SpendGold(int amount)
    {
        if (amount < 0 || gold < amount)
            return false;

        gold -= amount;
        UpdateUI();
        return true;
    }

    public bool SpendEmeralds(int amount)
    {
        if (amount < 0 || emeralds < amount)
            return false;

        emeralds -= amount;
        UpdateUI();
        return true;
    }

    public void RefreshUI() => UpdateUI();

    void UpdateUI()
    {
        // Только числа, без подписей.
        if (goldText != null)
            goldText.text = gold.ToString();

        if (emeraldText != null)
            emeraldText.text = emeralds.ToString();

        if (levelText != null)
            levelText.text = level.ToString();

        UpdateXpBar();
    }

    void UpdateXpBar()
    {
        if (xpFill == null)
            return;

        float normalized = ExperienceToNextLevel > 0
            ? (float)currentExperience / ExperienceToNextLevel
            : 0f;

        normalized = Mathf.Clamp01(normalized);

        float newWidth = fullXpBarWidth * normalized;

        Vector2 size = xpFill.sizeDelta;
        size.x = newWidth;
        xpFill.sizeDelta = size;

        Vector2 position = fullXpBarPosition;
        position.x -= xpFill.pivot.x * (fullXpBarWidth - newWidth);
        xpFill.anchoredPosition = position;
    }
}
