using UnityEngine;

[ExecuteAlways]
public class HeroSlotManager : MonoBehaviour
{
    public const int TotalSlots = 12;
    public const int Columns = 3;
    public const int Rows = 4;

    [Header("References")]
    public Transform castle;
    public Hero bowMaster;
    public Hero[] additionalHeroes;
    public HeroPanelController heroPanel;

    [Header("Hero Slots 1-12")]
    public Transform slot1;
    public Transform slot2;
    public Transform slot3;
    public Transform slot4;
    public Transform slot5;
    public Transform slot6;
    public Transform slot7;
    public Transform slot8;
    public Transform slot9;
    public Transform slot10;
    public Transform slot11;
    public Transform slot12;

    [Header("Unlocking")]
    [Tooltip("Сколько слотов сейчас открыто. 1-3 доступны с начала. Позже это значение будет повышать улучшение замка.")]
    [Range(1, TotalSlots)] public int unlockedSlots = 1;

    [Header("Grid On Castle")]
    [Tooltip("Небольшой внутренний отступ сетки от краёв замка, в долях размера замка.")]
    [Range(0f, 0.2f)] public float horizontalPadding = 0.04f;
    [Range(0f, 0.2f)] public float verticalPadding = 0.04f;

    [Header("Manual Slot Grid (for final castle art)")]
    [Tooltip("Включи, чтобы вручную совместить 12 рамок с окнами/площадками нарисованного замка.")]
    public bool useManualGrid = false;
    [Tooltip("Смещение центра всей сетки относительно Transform замка.")]
    public Vector2 gridOffset = Vector2.zero;
    [Tooltip("Расстояние между центрами соседних слотов по X и Y.")]
    public Vector2 slotSpacing = new Vector2(0.65f, 0.72f);
    [Tooltip("Размер одной рамки в мировых единицах.")]
    public Vector2 manualSlotSize = new Vector2(0.55f, 0.62f);

    [Header("Hero Size")]
    public bool autoFitHeroHeight = true;
    [Tooltip("Высота обычного героя относительно высоты одной ячейки.")]
    [Range(0.4f, 1.2f)] public float heroHeightInCell = 0.85f;

    [Header("Slot Visual") ]
    [Range(0f, 0.5f)] public float emptySlotAlpha = 0.10f;
    [Range(0.4f, 1f)] public float emptySlotFill = 0.86f;
    public int slotSortingOrder = 20;

    private readonly SpriteRenderer[] slotVisuals = new SpriteRenderer[TotalSlots];
    private bool showEmptySlotsInMenu = true;
    private float cellWidth = 0.83f;
    private float cellHeight = 1.125f;

    private Vector3 lastCastlePosition;
    private Vector3 lastCastleScale;
    private float lastHorizontalPadding = -1f;
    private float lastVerticalPadding = -1f;
    private float lastHeroHeightInCell = -1f;
    private int lastUnlockedSlots = -1;
    private bool lastUseManualGrid;
    private Vector2 lastGridOffset;
    private Vector2 lastSlotSpacing;
    private Vector2 lastManualSlotSize;
    private bool editorInitialized;
    private WaveSpawner waveSpawner;

    [Header("Early Progress Unlocks")]
    [Tooltip("Становится true после прохождения 3-й волны. Позже к этому привяжем кнопку прокачки замка.")]
    public bool castleUpgradeUnlocked = false;
    [Tooltip("Становится true после прохождения 3-й волны. Позже к этому привяжем покупку городских лучников.")]
    public bool cityArchersUnlocked = false;

    void Start()
    {
        RebuildLayout();

        if (!Application.isPlaying)
            return;

        waveSpawner = FindAnyObjectByType<WaveSpawner>();
        if (waveSpawner != null)
        {
            waveSpawner.OnWaveCompleted -= HandleWaveCompleted;
            waveSpawner.OnWaveCompleted += HandleWaveCompleted;
            // At startup recalculate slots from actual progress instead of keeping
            // a stale Inspector value from a previous test.
            int completedWave = waveSpawner.EffectiveCompletedWave;
            int waveSlots = completedWave >= 3 ? 3 : completedWave >= 2 ? 2 : 1;
            int castleSlots = 1;
            Castle castleComponent = FindAnyObjectByType<Castle>();
            if (castleComponent != null && castleComponent.castleLevel >= 20)
                castleSlots = Mathf.Clamp(4 + (castleComponent.castleLevel - 20) / 5, 4, TotalSlots);
            unlockedSlots = Mathf.Max(waveSlots, castleSlots);
            ApplyEarlyProgress(completedWave);
            RefreshSlots();
        }
    }

    void OnDestroy()
    {
        if (waveSpawner != null)
            waveSpawner.OnWaveCompleted -= HandleWaveCompleted;
    }

    void HandleWaveCompleted(int completedWave)
    {
        ApplyEarlyProgress(completedWave);
    }

    void ApplyEarlyProgress(int completedWave)
    {
        // Слот 1 открыт сразу. Слот 2 — после волны 2, слот 3 — после волны 3.
        // Слоты 4-12 этот код не открывает: позже ими управляет только прокачка замка.
        int waveSlots = completedWave >= 3 ? 3 : completedWave >= 2 ? 2 : 1;
        if (unlockedSlots < waveSlots)
            SetUnlockedSlots(waveSlots);

        bool earlySystemsUnlocked = completedWave >= 3;
        castleUpgradeUnlocked = earlySystemsUnlocked;
        cityArchersUnlocked = earlySystemsUnlocked;
    }

    void OnEnable()
    {
        // ExecuteAlways позволяет видеть правильную расстановку прямо в редакторе,
        // ещё до нажатия Play.
        editorInitialized = false;
    }

    void Update()
    {
        if (Application.isPlaying)
        {
            // Поддержка быстрого теста: если currentWave вручную поставлен, например, на 10,
            // сразу считаем ранние разблокировки так, будто предыдущие волны уже пройдены.
            if (waveSpawner == null)
                waveSpawner = FindAnyObjectByType<WaveSpawner>();

            if (waveSpawner != null)
                ApplyEarlyProgress(waveSpawner.EffectiveCompletedWave);

            return;
        }

        if (castle == null)
            return;

        bool layoutChanged = !editorInitialized ||
                             castle.position != lastCastlePosition ||
                             castle.lossyScale != lastCastleScale ||
                             !Mathf.Approximately(horizontalPadding, lastHorizontalPadding) ||
                             !Mathf.Approximately(verticalPadding, lastVerticalPadding) ||
                             !Mathf.Approximately(heroHeightInCell, lastHeroHeightInCell) ||
                             unlockedSlots != lastUnlockedSlots ||
                             useManualGrid != lastUseManualGrid ||
                             gridOffset != lastGridOffset ||
                             slotSpacing != lastSlotSpacing ||
                             manualSlotSize != lastManualSlotSize;

        if (layoutChanged)
            RebuildLayout();
    }

    void RebuildLayout()
    {
        if (castle == null)
            return;

        EnsureAllSlotsExist();
        PositionSlotsOnCastle();
        SetupAllSlotVisuals();
        PositionInstalledHeroes();
        RefreshSlots();

        lastCastlePosition = castle.position;
        lastCastleScale = castle.lossyScale;
        lastHorizontalPadding = horizontalPadding;
        lastVerticalPadding = verticalPadding;
        lastHeroHeightInCell = heroHeightInCell;
        lastUnlockedSlots = unlockedSlots;
        lastUseManualGrid = useManualGrid;
        lastGridOffset = gridOffset;
        lastSlotSpacing = slotSpacing;
        lastManualSlotSize = manualSlotSize;
        editorInitialized = true;
    }

    void PositionInstalledHeroes()
    {
        // Bow Master стартует в первом слоте.
        if (bowMaster != null && bowMaster.isInstalled)
        {
            if (bowMaster.installedSlot <= 0 || bowMaster.installedSlot > TotalSlots)
                bowMaster.installedSlot = 1;

            MoveHeroToSlot(bowMaster, bowMaster.installedSlot);
        }

        if (additionalHeroes != null)
        {
            foreach (Hero hero in additionalHeroes)
            {
                if (hero != null && hero.isInstalled && hero.installedSlot >= 1 && hero.installedSlot <= TotalSlots)
                    MoveHeroToSlot(hero, hero.installedSlot);
            }
        }
    }

    void EnsureAllSlotsExist()
    {
        Transform[] slots = GetSlotsArray();

        for (int i = 0; i < TotalSlots; i++)
        {
            if (slots[i] != null)
                continue;

            string slotName = "HeroSlot" + (i + 1);
            GameObject existing = GameObject.Find(slotName);

            if (existing != null)
            {
                slots[i] = existing.transform;
            }
            else
            {
                GameObject created = new GameObject(slotName);
                slots[i] = created.transform;
            }
        }

        ApplySlotsArray(slots);
    }

    void PositionSlotsOnCastle()
    {
        if (castle == null)
            return;

        Bounds bounds;
        SpriteRenderer castleRenderer = castle.GetComponent<SpriteRenderer>();

        if (castleRenderer != null && castleRenderer.sprite != null)
        {
            bounds = castleRenderer.bounds;
        }
        else
        {
            // Запасной вариант для обычного белого Square.
            Vector3 size = new Vector3(Mathf.Abs(castle.lossyScale.x), Mathf.Abs(castle.lossyScale.y), 0f);
            bounds = new Bounds(castle.position, size);
        }

        Transform[] slots = GetSlotsArray();

        if (useManualGrid)
        {
            cellWidth = Mathf.Max(0.05f, manualSlotSize.x);
            cellHeight = Mathf.Max(0.05f, manualSlotSize.y);

            // gridOffset указывает на центр всей сетки 3x4 относительно Castle.
            Vector2 center = (Vector2)castle.position + gridOffset;
            float leftX = center.x - slotSpacing.x;
            float bottomY = center.y - slotSpacing.y * 1.5f;

            for (int i = 0; i < TotalSlots; i++)
            {
                int row = i / Columns;
                int column = i % Columns;
                float x = leftX + slotSpacing.x * column;
                float y = bottomY + slotSpacing.y * row;

                if (slots[i] != null)
                    slots[i].position = new Vector3(x, y, castle.position.z);
            }

            return;
        }

        float usableWidth = bounds.size.x * (1f - horizontalPadding * 2f);
        float usableHeight = bounds.size.y * (1f - verticalPadding * 2f);
        cellWidth = usableWidth / Columns;
        cellHeight = usableHeight / Rows;

        float left = bounds.center.x - usableWidth * 0.5f;
        float bottom = bounds.center.y - usableHeight * 0.5f;

        for (int i = 0; i < TotalSlots; i++)
        {
            int row = i / Columns;      // 0 = нижний ряд
            int column = i % Columns;   // 0 = левый столбец

            float x = left + cellWidth * (column + 0.5f);
            float y = bottom + cellHeight * (row + 0.5f);

            if (slots[i] != null)
                slots[i].position = new Vector3(x, y, castle.position.z);
        }
    }

    void SetupAllSlotVisuals()
    {
        Transform[] slots = GetSlotsArray();

        for (int i = 0; i < TotalSlots; i++)
            slotVisuals[i] = SetupSlotVisual(slots[i], i + 1);
    }

    public void SetMenuSlotVisibility(bool visible)
    {
        showEmptySlotsInMenu = visible;
        RefreshSlots();
    }

    // Позже Castle Upgrade просто будет вызывать этот метод.
    public void SetUnlockedSlots(int count)
    {
        unlockedSlots = Mathf.Clamp(count, 1, TotalSlots);
        RefreshSlots();
    }


    public void ApplyLoadedProgress()
    {
        int completedWave = waveSpawner != null ? waveSpawner.EffectiveCompletedWave : 0;
        ApplyEarlyProgress(completedWave);

        Castle castleComponent = FindAnyObjectByType<Castle>();
        int waveSlots = completedWave >= 3 ? 3 : completedWave >= 2 ? 2 : 1;
        int castleSlots = 1;
        if (castleComponent != null && castleComponent.castleLevel >= 20)
            castleSlots = Mathf.Clamp(4 + (castleComponent.castleLevel - 20) / 5, 4, TotalSlots);
        unlockedSlots = Mathf.Max(waveSlots, castleSlots);

        PositionInstalledHeroes();
        RefreshSlots();
    }

    public bool IsSlotUnlocked(int slotIndex)
    {
        return slotIndex >= 1 && slotIndex <= Mathf.Clamp(unlockedSlots, 1, TotalSlots);
    }

    public bool IsSlotOccupied(int slotIndex)
    {
        if (bowMaster != null && bowMaster.isInstalled && bowMaster.installedSlot == slotIndex)
            return true;

        if (additionalHeroes != null)
        {
            foreach (Hero hero in additionalHeroes)
            {
                if (hero != null && hero.isInstalled && hero.installedSlot == slotIndex)
                    return true;
            }
        }

        return false;
    }


    public Hero GetInstalledHeroInSlot(int slotIndex)
    {
        if (slotIndex < 1 || slotIndex > TotalSlots) return null;

        if (bowMaster != null && bowMaster.isInstalled && bowMaster.installedSlot == slotIndex)
            return bowMaster;

        if (additionalHeroes != null)
        {
            foreach (Hero hero in additionalHeroes)
                if (hero != null && hero.isInstalled && hero.installedSlot == slotIndex)
                    return hero;
        }

        return null;
    }

    public bool CanInstallToSlot(int slotIndex)
    {
        return IsSlotUnlocked(slotIndex) && !IsSlotOccupied(slotIndex);
    }

    public bool InstallHero(Hero hero, int slotIndex)
    {
        if (hero == null || !hero.isPurchased || !IsSlotUnlocked(slotIndex))
            return false;

        // Установка уже установленного героя в новый слот = перенос, не копия.
        if (IsSlotOccupied(slotIndex) && hero.installedSlot != slotIndex)
            return false;

        hero.installedSlot = slotIndex;
        hero.SetInstalled(true);
        MoveHeroToSlot(hero, slotIndex);
        RefreshSlots();
        return true;
    }

    public void RemoveHero(Hero hero)
    {
        if (hero == null)
            return;

        hero.SetInstalled(false);
        hero.installedSlot = 0;
        RefreshSlots();
    }

    void MoveHeroToSlot(Hero hero, int slotIndex)
    {
        Transform target = GetSlotTransform(slotIndex);
        if (hero == null || target == null)
            return;

        hero.transform.position = target.position;

        if (autoFitHeroHeight)
            FitHeroHeightToCell(hero);
    }

    void FitHeroHeightToCell(Hero hero)
    {
        SpriteRenderer renderer = hero.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null)
            return;

        float spriteHeight = renderer.sprite.bounds.size.y;
        if (spriteHeight <= 0.0001f)
            return;

        float targetHeight = cellHeight * heroHeightInCell;
        float uniformScale = targetHeight / spriteHeight;

        hero.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
    }

    Transform GetSlotTransform(int slotIndex)
    {
        if (slotIndex < 1 || slotIndex > TotalSlots)
            return null;

        return GetSlotsArray()[slotIndex - 1];
    }

    void RefreshSlots()
    {
        for (int i = 0; i < slotVisuals.Length; i++)
        {
            int slotIndex = i + 1;
            if (slotVisuals[i] == null)
                continue;

            // В редакторе всегда видим все 12 слотов для удобной разработки.
            // В запущенной игре в меню видим ТОЛЬКО уже открытые слоты.
            // Во время боя рамки полностью скрыты.
            bool shouldShow;

            if (!Application.isPlaying)
                shouldShow = true;
            else
                shouldShow = showEmptySlotsInMenu && IsSlotUnlocked(slotIndex);

            bool battle = Application.isPlaying && GameStateManager.Instance != null &&
                          GameStateManager.Instance.currentState == GameStateManager.GameState.Battle;
            bool occupied = IsSlotOccupied(slotIndex);

            // In battle the slot square stays as an invisible click area for ultimates.
            // Its renderer is hidden, so the player never sees the square.
            slotVisuals[i].gameObject.SetActive(shouldShow || (battle && occupied));
            slotVisuals[i].enabled = shouldShow;

            Collider2D col = slotVisuals[i].GetComponent<Collider2D>();
            if (col != null)
                col.enabled = Application.isPlaying &&
                    ((shouldShow && !occupied) || (battle && occupied));
        }
    }

    SpriteRenderer SetupSlotVisual(Transform slot, int slotIndex)
    {
        if (slot == null)
            return null;

        Transform existing = slot.Find("EmptySlotVisual");
        GameObject visual;

        if (existing != null)
            visual = existing.gameObject;
        else
        {
            visual = new GameObject("EmptySlotVisual");
            visual.transform.SetParent(slot, false);
        }

        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(cellWidth * emptySlotFill, cellHeight * emptySlotFill, 1f);

        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = visual.AddComponent<SpriteRenderer>();

        renderer.sprite = CreateOutlinedSquareSprite();
        renderer.color = Color.white;
        renderer.sortingOrder = slotSortingOrder;

        BoxCollider2D collider = visual.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = visual.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        EmptyHeroSlotClick click = visual.GetComponent<EmptyHeroSlotClick>();
        if (click == null)
            click = visual.AddComponent<EmptyHeroSlotClick>();

        click.slotManager = this;
        click.heroPanel = heroPanel;
        click.slotIndex = slotIndex;

        return renderer;
    }

    Transform[] GetSlotsArray()
    {
        return new Transform[]
        {
            slot1, slot2, slot3, slot4, slot5, slot6,
            slot7, slot8, slot9, slot10, slot11, slot12
        };
    }

    void ApplySlotsArray(Transform[] slots)
    {
        slot1 = slots[0];
        slot2 = slots[1];
        slot3 = slots[2];
        slot4 = slots[3];
        slot5 = slots[4];
        slot6 = slots[5];
        slot7 = slots[6];
        slot8 = slots[7];
        slot9 = slots[8];
        slot10 = slots[9];
        slot11 = slots[10];
        slot12 = slots[11];
    }

    Sprite CreateOutlinedSquareSprite()
    {
        const int size = 64;
        const int border = 4;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "RuntimeHeroSlotOutline";
        texture.filterMode = FilterMode.Bilinear;

        Color inside = new Color(0f, 0f, 0f, emptySlotAlpha);
        Color outline = new Color(0f, 0f, 0f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool edge = x < border || x >= size - border || y < border || y >= size - border;
                texture.SetPixel(x, y, edge ? outline : inside);
            }
        }

        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
    }
}
