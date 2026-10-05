using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class Castle : MonoBehaviour
{
    [Header("Castle Upgrade")]
    [Min(1)] public int castleLevel = 1;
    public float baseMaxHealth = 500f;
    public float baseMaxMana = 100f;
    public float healthPerLevel = 50f;
    public float manaPerLevel = 10f;
    public HeroSlotManager heroSlotManager;

    [Header("Castle upgrade clickable square")]
    [Tooltip("Положение квадрата относительно центра замка. Меняй X/Y в Inspector, чтобы подогнать вручную.")]
    public Vector2 upgradeSlotOffset = new Vector2(-0.9f, 0f);
    [Tooltip("Размер кликабельного квадрата замка. Можно менять вручную в Inspector.")]
    public Vector2 upgradeSlotSize = new Vector2(0.55f, 0.55f);
    [Range(0f, 0.5f)] public float upgradeSlotAlpha = 0.10f;
    public int upgradeSlotSortingOrder = 21;
    public CastleUpgradePanelController upgradePanel;

    private GameObject upgradeSlotObject;
    private SpriteRenderer upgradeSlotRenderer;

    [Header("Health")]
    public float maxHealth = 500f;
    public float currentHealth;
    public RectTransform healthBarFill;
    public TMP_Text healthText;

    [Header("Mana")]
    public float maxMana = 100f;
    public float currentMana;
    public RectTransform manaBarFill;
    public TMP_Text manaText;

    private float fullHealthBarWidth;
    private Vector2 fullHealthBarPosition;
    private float fullManaBarWidth;
    private Vector2 fullManaBarPosition;

    public int UpgradeCost => GetUpgradeCost(castleLevel);

    void Start()
    {
        if (heroSlotManager == null)
            heroSlotManager = FindAnyObjectByType<HeroSlotManager>();

        ApplyLevelStats(false);
        currentHealth = maxHealth;
        currentMana = maxMana;

        CacheBarGeometry();
        NormalizeHudBarGeometry();

        ApplyHeroSlotUnlocks();
        SetupUpgradePanel();
        BuildUpgradeSlot();
        UpdateUI();
    }


    void CacheBarGeometry()
    {
        // Never use the current Fill width as the "full" width. The Fill may have
        // been left at width 0 by a previous run, which made the coloured bar vanish
        // again as soon as Play started. The Background is the stable full-size source.
        if (healthBarFill != null)
        {
            RectTransform bg = FindRectTransformByName("CastleHPBackground");
            fullHealthBarWidth = bg != null ? bg.rect.width : healthBarFill.rect.width;
            if (fullHealthBarWidth <= 0.01f) fullHealthBarWidth = 240f;
            fullHealthBarPosition = healthBarFill.anchoredPosition;
        }

        if (manaBarFill != null)
        {
            RectTransform bg = FindRectTransformByName("ManaBackground");
            fullManaBarWidth = bg != null ? bg.rect.width : manaBarFill.rect.width;
            if (fullManaBarWidth <= 0.01f) fullManaBarWidth = 240f;
            fullManaBarPosition = manaBarFill.anchoredPosition;
        }
    }


    void NormalizeHudBarGeometry()
    {
        // Make each coloured Fill use exactly the same rectangle as its Background.
        // This fixes scene setups where the Fill's anchors/offsets were changed and
        // Play mode made the coloured bar disappear even though its Image color was correct.
        NormalizeHudBar(healthBarFill, "CastleHPBackground");
        NormalizeHudBar(manaBarFill, "ManaBackground");
    }

    void NormalizeHudBar(RectTransform fill, string backgroundName)
    {
        if (fill == null) return;
        RectTransform bg = FindRectTransformByName(backgroundName);
        if (bg == null) return;

        // Keep the user's Image/color/material untouched; only align the rectangle.
        fill.anchorMin = bg.anchorMin;
        fill.anchorMax = bg.anchorMax;
        fill.pivot = bg.pivot;
        fill.anchoredPosition = bg.anchoredPosition;
        fill.sizeDelta = bg.sizeDelta;
        fill.localScale = Vector3.one;
        fill.localRotation = Quaternion.identity;

        // Ensure the coloured fill renders after (on top of) its grey background.
        if (fill.parent == bg.parent)
            fill.SetSiblingIndex(Mathf.Min(bg.GetSiblingIndex() + 1, fill.parent.childCount - 1));

        if (backgroundName == "CastleHPBackground")
        {
            fullHealthBarWidth = bg.rect.width > 0.01f ? bg.rect.width : 240f;
            fullHealthBarPosition = fill.anchoredPosition;
        }
        else
        {
            fullManaBarWidth = bg.rect.width > 0.01f ? bg.rect.width : 240f;
            fullManaBarPosition = fill.anchoredPosition;
        }
    }

    RectTransform FindRectTransformByName(string objectName)
    {
        foreach (RectTransform rt in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (rt != null && rt.gameObject.scene.IsValid() && rt.name == objectName)
                return rt;
        }
        return null;
    }

    // Cost to upgrade FROM currentLevel to currentLevel + 1.
    // L1-9: 10..90, L10-19: 120..300, L20-29: 330..600, etc.
    public static int GetUpgradeCost(int currentLevel)
    {
        currentLevel = Mathf.Max(1, currentLevel);
        int decade = currentLevel / 10;
        int withinDecade = currentLevel % 10;

        if (decade == 0)
            return currentLevel * 10;

        // Cost at the exact decade boundary: L10=120, L20=330, L30=640...
        int boundaryCost = 0;
        for (int d = 1; d <= decade; d++)
        {
            int previousEndCost;
            if (d == 1)
                previousEndCost = 100;
            else
            {
                // End cost of previous decade (level d*10 - 1 -> d*10).
                int prevStart = GetDecadeStartCost(d - 1);
                int prevStep = d * 10;
                previousEndCost = prevStart + 9 * prevStep;
            }
            boundaryCost = previousEndCost + (d + 1) * 10;
        }

        int step = (decade + 1) * 10;
        return boundaryCost + withinDecade * step;
    }

    private static int GetDecadeStartCost(int decade)
    {
        if (decade <= 0) return 10;
        int cost = 120;
        for (int d = 2; d <= decade; d++)
        {
            int previousStep = d * 10;
            int previousEnd = cost + 9 * previousStep;
            cost = previousEnd + (d + 1) * 10;
        }
        return cost;
    }

    public bool CanOpenUpgradePanel
    {
        get
        {
            bool unlocked = heroSlotManager != null && heroSlotManager.castleUpgradeUnlocked;
            bool menu = GameStateManager.Instance == null || GameStateManager.Instance.currentState == GameStateManager.GameState.Menu;
            return unlocked && menu;
        }
    }

    void Update()
    {
        // Keep a REAL preview object alive in Edit Mode too, so it is visible in
        // both Scene and Game views while positioning it from the Inspector.
        if (upgradeSlotObject == null) BuildUpgradeSlot();

        if (upgradeSlotObject != null)
        {
            upgradeSlotObject.transform.position = transform.position + (Vector3)upgradeSlotOffset;
            upgradeSlotObject.transform.localScale = new Vector3(
                Mathf.Max(0.01f, upgradeSlotSize.x),
                Mathf.Max(0.01f, upgradeSlotSize.y),
                1f);

            // In Edit Mode the square is always shown as a placement preview.
            // In Play Mode normal game/menu unlock rules apply.
            upgradeSlotObject.SetActive(!Application.isPlaying || CanOpenUpgradePanel);

            if (upgradeSlotRenderer != null)
                upgradeSlotRenderer.sortingOrder = upgradeSlotSortingOrder;
        }

    }

    void OnValidate()
    {
        upgradeSlotSize.x = Mathf.Max(0.01f, upgradeSlotSize.x);
        upgradeSlotSize.y = Mathf.Max(0.01f, upgradeSlotSize.y);
    }

    // Editor preview: shows the castle upgrade square directly in the Scene view
    // even when the game is not running. Offset/Size changes are visible immediately.
    void OnDrawGizmos()
    {
        Vector3 center = transform.position + (Vector3)upgradeSlotOffset;
        Vector3 size = new Vector3(Mathf.Max(0.01f, upgradeSlotSize.x), Mathf.Max(0.01f, upgradeSlotSize.y), 0.01f);

        Color oldColor = Gizmos.color;
        Gizmos.color = new Color(0f, 0f, 0f, Mathf.Max(0.08f, upgradeSlotAlpha));
        Gizmos.DrawCube(center, size);
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(center, size);
        Gizmos.color = oldColor;
    }

    void SetupUpgradePanel()
    {
        if (upgradePanel != null) { upgradePanel.castle = this; return; }
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!t.gameObject.scene.IsValid() || t.name != "CastleUpgradePanel") continue;
            upgradePanel = t.GetComponent<CastleUpgradePanelController>();
            if (upgradePanel == null) upgradePanel = t.gameObject.AddComponent<CastleUpgradePanelController>();
            upgradePanel.castle = this;
            break;
        }
    }

    void BuildUpgradeSlot()
    {
        Transform existing = transform.Find("CastleUpgradeSlot");
        if (existing == null)
        {
            upgradeSlotObject = new GameObject("CastleUpgradeSlot");
            upgradeSlotObject.transform.SetParent(transform, true);
        }
        else upgradeSlotObject = existing.gameObject;

        upgradeSlotObject.transform.position = transform.position + (Vector3)upgradeSlotOffset;
        upgradeSlotObject.transform.localScale = new Vector3(upgradeSlotSize.x, upgradeSlotSize.y, 1f);

        upgradeSlotRenderer = upgradeSlotObject.GetComponent<SpriteRenderer>();
        if (upgradeSlotRenderer == null) upgradeSlotRenderer = upgradeSlotObject.AddComponent<SpriteRenderer>();
        if (upgradeSlotRenderer.sprite == null) upgradeSlotRenderer.sprite = CreateUpgradeSlotSprite();
        upgradeSlotRenderer.sortingOrder = upgradeSlotSortingOrder;

        if (upgradeSlotObject.GetComponent<BoxCollider2D>() == null)
            upgradeSlotObject.AddComponent<BoxCollider2D>();
        CastleUpgradeSlotClick click = upgradeSlotObject.GetComponent<CastleUpgradeSlotClick>();
        if (click == null) click = upgradeSlotObject.AddComponent<CastleUpgradeSlotClick>();
        click.castle = this;
    }

    Sprite CreateUpgradeSlotSprite()
    {
        const int size = 64, border = 4;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "RuntimeCastleUpgradeSlot";
        Color inside = new Color(0f, 0f, 0f, upgradeSlotAlpha);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, (x < border || x >= size-border || y < border || y >= size-border) ? Color.black : inside);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f,0.5f), size);
    }

    public void OpenUpgradePanel()
    {
        if (!CanOpenUpgradePanel) return;
        SetupUpgradePanel();
        if (upgradePanel != null) upgradePanel.Open(this);
    }

    public bool TryUpgradeCastle()
    {
        if (PlayerProgress.Instance == null)
            return false;

        int cost = UpgradeCost;
        if (!PlayerProgress.Instance.SpendGold(cost))
            return false;

        castleLevel++;
        ApplyLevelStats(true);
        ApplyHeroSlotUnlocks();
        return true;
    }

    public void ApplyLevelStats(bool refill)
    {
        castleLevel = Mathf.Max(1, castleLevel);
        maxHealth = baseMaxHealth + (castleLevel - 1) * healthPerLevel;
        maxMana = baseMaxMana + (castleLevel - 1) * manaPerLevel;

        if (refill)
        {
            currentHealth = maxHealth;
            currentMana = maxMana;
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
        }

        UpdateUI();
    }

    public void ApplyHeroSlotUnlocks()
    {
        if (heroSlotManager == null)
            return;

        // Slots 1-3 are handled by early wave progress.
        // Slot 4 at castle level 20; then one new slot every 5 levels.
        // Castle upgrades must never unlock slots 2-3. Those belong only to
        // wave progression (slot 2 after wave 2, slot 3 after wave 3).
        if (castleLevel < 20)
            return;

        int castleSlots = Mathf.Clamp(4 + (castleLevel - 20) / 5, 4, HeroSlotManager.TotalSlots);
        if (heroSlotManager.unlockedSlots < castleSlots)
            heroSlotManager.SetUnlockedSlots(castleSlots);
    }

    public void TakeDamage(float damage)
    {
        currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);
        UpdateUI();

        if (currentHealth <= 0f)
            Debug.Log("Замок уничтожен!");
    }

    public bool SpendMana(float amount)
    {
        if (amount < 0f || currentMana < amount)
            return false;

        currentMana -= amount;
        UpdateUI();
        return true;
    }

    public void AddHealth(float amount)
    {
        if (amount <= 0f) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        UpdateUI();
    }

    public void AddMana(float amount)
    {
        currentMana = Mathf.Clamp(currentMana + amount, 0f, maxMana);
        UpdateUI();
    }

    public void RestoreManaToFull()
    {
        currentMana = maxMana;
        UpdateUI();
    }

    public void RestoreToFull()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;
        UpdateUI();
    }

    void UpdateUI()
    {
        UpdateBar(healthBarFill, fullHealthBarWidth, fullHealthBarPosition,
            maxHealth > 0f ? currentHealth / maxHealth : 0f);

        UpdateBar(manaBarFill, fullManaBarWidth, fullManaBarPosition,
            maxMana > 0f ? currentMana / maxMana : 0f);

        if (healthText != null)
            healthText.text = Mathf.CeilToInt(currentHealth).ToString();

        if (manaText != null)
            manaText.text = Mathf.CeilToInt(currentMana).ToString();
    }

    void UpdateBar(RectTransform bar, float fullWidth, Vector2 fullPosition, float normalized)
    {
        if (bar == null)
            return;

        normalized = Mathf.Clamp01(normalized);
        float newWidth = fullWidth * normalized;

        Vector2 size = bar.sizeDelta;
        size.x = newWidth;
        bar.sizeDelta = size;

        Vector2 position = fullPosition;
        position.x -= bar.pivot.x * (fullWidth - newWidth);
        bar.anchoredPosition = position;
    }
}
