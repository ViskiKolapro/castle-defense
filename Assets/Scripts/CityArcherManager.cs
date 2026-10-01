using UnityEngine;

[ExecuteAlways]
public class CityArcherManager : MonoBehaviour
{
    [Header("References")]
    public Transform castle;
    public WaveSpawner waveSpawner;
    public GameObject cityArcherPrefab;
    public GameObject projectilePrefab;
    public CityArcherPanelController panel;

    [Header("Two squads")]
    public CityArcherSquad squad1 = new CityArcherSquad();
    public CityArcherSquad squad2 = new CityArcherSquad();

    [Header("20 archer positions - LEFT of castle")]
    [Tooltip("Центр всей сетки 5x4 относительно Castle. Отрицательный X = левее замка.")]
    public Vector2 formationOffset = new Vector2(-2.2f, 0f);
    [Tooltip("Расстояние между отдельными лучниками по горизонтали/вертикали.")]
    public Vector2 formationSpacing = new Vector2(0.45f, 0.65f);

    [Header("Two clickable squad slots")]
    [Tooltip("Позиция квадрата ОТРЯДА 1 (верхние лучники) относительно Castle.")]
    public Vector2 squad1ControlSlotOffset = new Vector2(-3.7f, 0f);
    [Tooltip("Позиция квадрата ОТРЯДА 2 (нижние лучники) относительно Castle.")]
    public Vector2 squad2ControlSlotOffset = new Vector2(-3.7f, -1f);
    public Vector2 controlSlotSize = new Vector2(0.5f, 0.5f);
    [Range(0f, 0.5f)] public float slotAlpha = 0.10f;
    public int slotSortingOrder = 20;

    [Header("Editor preview - 20 archer positions")]
    public bool showFormationPreview = true;
    public Vector2 formationPreviewSize = new Vector2(0.32f, 0.48f);
    [Range(0f, 0.5f)] public float formationPreviewAlpha = 0.08f;
    public int formationPreviewSortingOrder = 19;

    public bool IsUnlocked { get; private set; }

    private readonly Transform[] unitPositions = new Transform[20];
    private readonly GameObject[] spawnedUnits = new GameObject[20];
    private readonly SpriteRenderer[] controlSlotVisuals = new SpriteRenderer[2];
    private readonly SpriteRenderer[] formationPreviewVisuals = new SpriteRenderer[20];
    
    void Start()
    {
        BuildEditorObjects();
        if (!Application.isPlaying) return;

        if (waveSpawner == null) waveSpawner = FindAnyObjectByType<WaveSpawner>();
        if (waveSpawner != null)
        {
            waveSpawner.OnWaveCompleted -= OnWaveCompleted;
            waveSpawner.OnWaveCompleted += OnWaveCompleted;
            IsUnlocked = waveSpawner.EffectiveCompletedWave >= 3;
        }

        if (panel != null) panel.manager = this;
        RefreshAll();
    }

    void OnDestroy()
    {
        if (waveSpawner != null) waveSpawner.OnWaveCompleted -= OnWaveCompleted;
    }

    void Update()
    {
        if (!Application.isPlaying)
            BuildEditorObjects();
        else
        {
            // Для обычной игры разблокировка происходит после прохождения 3-й волны.
            // currentWave >= 4 также позволяет удобно тестировать, сразу выставив 4/5/50 волну в Inspector.
            if (!IsUnlocked && waveSpawner != null &&
                waveSpawner.EffectiveCompletedWave >= 3)
            {
                IsUnlocked = true;
            }

            bool battle = GameStateManager.Instance != null && GameStateManager.Instance.currentState == GameStateManager.GameState.Battle;
            squad1.IsActiveInBattle = battle;
            squad2.IsActiveInBattle = battle;

            // Видимость управляющих квадратов зависит от состояния Menu/Battle, поэтому обновляем её
            // и при возврате из боя в меню, а не только в момент завершения волны.
            RefreshControlSlots();
        }
    }

    void OnWaveCompleted(int wave)
    {
        if (wave >= 3)
        {
            IsUnlocked = true;
            RefreshControlSlots();
        }
    }


    public void ApplyLoadedProgress()
    {
        if (waveSpawner == null) waveSpawner = FindAnyObjectByType<WaveSpawner>();
        IsUnlocked = waveSpawner != null && waveSpawner.EffectiveCompletedWave >= 3;
        RefreshAll();
    }

    public CityArcherSquad GetSquad(int index) => index == 0 ? squad1 : index == 1 ? squad2 : null;

    public void OpenSquad(int index)
    {
        if (!Application.isPlaying || !IsUnlocked) return;
        if (GameStateManager.Instance != null && GameStateManager.Instance.currentState != GameStateManager.GameState.Menu) return;

        // Если открыто меню героя, переключаемся на лучников без промежуточного закрытия пользователем.
        HeroPanelController.CloseAnyOpenPanel();

        if (panel != null) panel.Open(index);
    }

    public void BuyOrUpgradeSquad(int index)
    {
        if (!IsUnlocked) return;
        CityArcherSquad squad = GetSquad(index);
        if (squad == null || !squad.TryBuyOrUpgrade()) return;
        RefreshUnits();
    }

    void RefreshAll()
    {
        BuildEditorObjects();
        RefreshUnits();
        RefreshControlSlots();
    }

    void BuildEditorObjects()
    {
        if (castle == null) return;

        Vector2 center = (Vector2)castle.position + formationOffset;
        float left = center.x - formationSpacing.x * 2f;
        float bottom = center.y - formationSpacing.y * 1.5f;

        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                int index = row * 5 + col;
                string name = "CityArcherPosition" + (index + 1);
                Transform t = transform.Find(name);
                if (t == null)
                {
                    GameObject go = new GameObject(name);
                    t = go.transform;
                    t.SetParent(transform, false);
                }
                t.position = new Vector3(left + col * formationSpacing.x, bottom + row * formationSpacing.y, castle.position.z);
                unitPositions[index] = t;
                formationPreviewVisuals[index] = SetupFormationPreview(t);
            }
        }

        for (int i = 0; i < 2; i++)
        {
            string name = "CityArcherSquadSlot" + (i + 1);
            Transform t = transform.Find(name);
            if (t == null)
            {
                GameObject go = new GameObject(name);
                t = go.transform;
                t.SetParent(transform, false);
            }
            Vector2 slotOffset = i == 0 ? squad1ControlSlotOffset : squad2ControlSlotOffset;
            t.position = castle.position + new Vector3(slotOffset.x, slotOffset.y, 0f);
            controlSlotVisuals[i] = SetupControlSlot(t, i);
        }

        RefreshControlSlots();
    }


    SpriteRenderer SetupFormationPreview(Transform t)
    {
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();
        if (sr.sprite == null) sr.sprite = CreateOutlinedSquareSprite(formationPreviewAlpha);
        sr.sortingOrder = formationPreviewSortingOrder;
        sr.enabled = showFormationPreview && !Application.isPlaying;
        t.localScale = new Vector3(formationPreviewSize.x, formationPreviewSize.y, 1f);
        return sr;
    }

    SpriteRenderer SetupControlSlot(Transform t, int index)
    {
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();
        if (sr.sprite == null) sr.sprite = CreateOutlinedSquareSprite();
        sr.sortingOrder = slotSortingOrder;
        t.localScale = new Vector3(controlSlotSize.x, controlSlotSize.y, 1f);

        BoxCollider2D col = t.GetComponent<BoxCollider2D>();
        if (col == null) col = t.gameObject.AddComponent<BoxCollider2D>();

        CityArcherSlotClick click = t.GetComponent<CityArcherSlotClick>();
        if (click == null) click = t.gameObject.AddComponent<CityArcherSlotClick>();
        click.manager = this;
        // Visual slot order is opposite to squad data order in the current scene.
        // Swap only the click binding; do not move either square.
        click.squadIndex = 1 - index;
        return sr;
    }

    void RefreshControlSlots()
    {
        for (int i = 0; i < 2; i++)
        {
            if (controlSlotVisuals[i] == null) continue;
            bool show = !Application.isPlaying || (IsUnlocked && GameStateManager.Instance != null && GameStateManager.Instance.currentState == GameStateManager.GameState.Menu);
            controlSlotVisuals[i].gameObject.SetActive(show);
        }
    }

    void RefreshUnits()
    {
        if (!Application.isPlaying) return;
        RefreshSquadUnits(0, squad1, 0);
        RefreshSquadUnits(1, squad2, 10);
    }

    void RefreshSquadUnits(int squadIndex, CityArcherSquad squad, int startIndex)
    {
        for (int local = 0; local < 10; local++)
        {
            // Позиции заполняются СВЕРХУ ВНИЗ.
            // Squad 1 занимает два верхних ряда, Squad 2 — два нижних.
            int index = GetFormationIndexTopToBottom(squadIndex, local);
            bool shouldExist = local < squad.archerCount;

            if (shouldExist && spawnedUnits[index] == null && cityArcherPrefab != null && unitPositions[index] != null)
            {
                GameObject unit = Instantiate(cityArcherPrefab, unitPositions[index].position, Quaternion.identity);
                unit.name = "CityArcher_S" + (squadIndex + 1) + "_" + (local + 1);
                CityArcherUnit combat = unit.GetComponent<CityArcherUnit>();
                if (combat == null) combat = unit.AddComponent<CityArcherUnit>();
                combat.squad = squad;
                combat.projectilePrefab = projectilePrefab;
                if (combat.firePoint == null)
                {
                    Transform fp = unit.transform.Find("FirePoint");
                    if (fp != null) combat.firePoint = fp;
                }
                spawnedUnits[index] = unit;
            }
            else if (!shouldExist && spawnedUnits[index] != null)
            {
                Destroy(spawnedUnits[index]);
                spawnedUnits[index] = null;
            }

            if (spawnedUnits[index] != null && unitPositions[index] != null)
                spawnedUnits[index].transform.position = unitPositions[index].position;
        }
    }


    int GetFormationIndexTopToBottom(int squadIndex, int local)
    {
        int rowWithinSquad = local / 5;
        int col = local % 5;

        // В BuildEditorObjects row=0 — нижний ряд, row=3 — верхний.
        // Отряд 1: row 3 -> row 2. Отряд 2: row 1 -> row 0.
        int row = squadIndex == 0 ? 3 - rowWithinSquad : 1 - rowWithinSquad;
        return row * 5 + col;
    }

    Sprite CreateOutlinedSquareSprite(float alpha = -1f)
    {
        const int size = 64;
        const int border = 4;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "RuntimeCityArcherSlot";
        Color inside = new Color(0f, 0f, 0f, alpha >= 0f ? alpha : slotAlpha);
        Color outline = Color.black;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x < border || x >= size - border || y < border || y >= size - border;
                tex.SetPixel(x, y, edge ? outline : inside);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
