using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WaveSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Обычный враг (Normal)")]
    public GameObject enemyPrefab;

    [Tooltip("Element 0 = Fast, Element 1 = Tank")]
    public GameObject[] additionalEnemyPrefabs;

    public GameObject bossPrefab;
    public Transform spawnPoint;

    [Header("Road / Spawn Corridor")]
    [Tooltip("Высота центра дороги. Обычно совпадает с центром замка по Y.")]
    public float roadCenterY = 0f;

    [Tooltip("Полная высота дороги. Поставь примерно равной высоте замка.")]
    public float roadHeight = 4f;

    [Tooltip("Насколько далеко за правым краем камеры рождаются враги.")]
    public float spawnOutsideScreen = 1f;

    [Header("Wave")]
    public int currentWave = 1;
    public int lastCompletedWave = 0;

    // Если для теста вручную поставить currentWave = 10, логично считать,
    // что волны 1-9 уже пройдены. Это делает быстрый тест через Inspector стабильным.
    public int EffectiveCompletedWave => Mathf.Max(lastCompletedWave, Mathf.Max(0, currentWave - 1));
    public float timeBetweenWaves = 2f;

    public event System.Action<int> OnWaveCompleted;

    [Header("Wave Duration")]
    public float firstWaveDuration = 2f;
    public float wave99Duration = 55f;
    public float wave100PlusDuration = 60f;

    [Header("Enemy Scaling Per Wave")]
    public float enemyHealthGrowth = 1.035f;
    public float enemyDamageGrowth = 1.025f;
    public float enemyGoldGrowth = 1.025f;
    public float enemyExperienceGrowth = 1.020f;

    [Header("Boss Rewards & Scaling")]
    public int firstBossGoldReward = 300;
    public int firstBossExperienceReward = 100;
    public float bossScalingPower = 5f;

    [Header("Boss Wave")]
    public int bossEveryWaves = 10;
    public float bossHealthMultiplier = 10f;
    public float bossDamageMultiplier = 2f;
    public float bossScaleMultiplier = 2.5f;

    [Header("UI")]
    public RectTransform waveBarFill;
    public TMP_Text waveText;

    private int enemiesAlive;
    private Enemy activeBoss;

    private float fullWaveBarWidth;
    private Vector2 fullWaveBarPosition;
    private Image waveBarImage;

    private readonly Color normalWaveColor = Color.white;
    private readonly Color bossWaveColor = Color.red;

    private GameObject FastPrefab
    {
        get
        {
            if (additionalEnemyPrefabs != null && additionalEnemyPrefabs.Length > 0)
                return additionalEnemyPrefabs[0];

            return null;
        }
    }

    private GameObject TankPrefab
    {
        get
        {
            if (additionalEnemyPrefabs != null && additionalEnemyPrefabs.Length > 1)
                return additionalEnemyPrefabs[1];

            return null;
        }
    }

    void Start()
    {
        currentWave = Mathf.Max(1, currentWave);
        if (lastCompletedWave < currentWave - 1)
            lastCompletedWave = currentWave - 1;

        if (waveBarFill != null)
        {
            fullWaveBarWidth = waveBarFill.sizeDelta.x;
            fullWaveBarPosition = waveBarFill.anchoredPosition;
            waveBarImage = waveBarFill.GetComponent<Image>();
            SetWaveBarColor(normalWaveColor);
            SetWaveBar(1f);
        }

        UpdateWaveText();
        // Волны больше НЕ стартуют автоматически.
        // Их запускает GameStateManager только после нажатия кнопки боя.
    }

    private bool battleStarted = false;

    public void BeginBattle()
    {
        if (battleStarted)
            return;

        battleStarted = true;
        if (battleStarted)
            StartCoroutine(RunWave());
    }

    public void StopBattle()
    {
        battleStarted = false;
        StopAllCoroutines();
        ClearBattleObjects();
        enemiesAlive = 0;
        activeBoss = null;
        SetWaveBarColor(normalWaveColor);
        SetWaveBar(1f);
    }

    public void RestartCurrentWave()
    {
        StopAllCoroutines();
        ClearBattleObjects();
        enemiesAlive = 0;
        activeBoss = null;
        battleStarted = true;
        UpdateWaveText();
        SetWaveBarColor(normalWaveColor);
        SetWaveBar(1f);
        StartCoroutine(RunWave());
    }

    void ClearBattleObjects()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies)
            if (enemy != null) Destroy(enemy.gameObject);

        Projectile[] projectiles = FindObjectsByType<Projectile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Projectile projectile in projectiles)
            if (projectile != null) Destroy(projectile.gameObject);
    }

    IEnumerator RunWave()
    {
        if (currentWave > 1)
            yield return new WaitForSeconds(timeBetweenWaves);

        float waveDuration = GetWaveDuration(currentWave);
        int enemiesToSpawn = GetEnemyCount(currentWave);

        float elapsed = 0f;
        int spawned = 0;
        float spawnStep = enemiesToSpawn > 0
            ? waveDuration / enemiesToSpawn
            : waveDuration;

        SetWaveBarColor(normalWaveColor);
        SetWaveBar(1f);

        // Обычная часть волны: враги появляются в течение заданного времени.
        while (elapsed < waveDuration)
        {
            while (spawned < enemiesToSpawn && elapsed >= spawnStep * spawned)
            {
                SpawnEnemyForWave(currentWave);
                spawned++;
            }

            elapsed += Time.deltaTime;
            SetWaveBar(1f - (elapsed / waveDuration));
            yield return null;
        }

        // На случай пропуска кадра гарантированно создаём весь состав волны.
        while (spawned < enemiesToSpawn)
        {
            SpawnEnemyForWave(currentWave);
            spawned++;
        }

        SetWaveBar(0f);

        // На каждой 10-й волне босс появляется сразу после окончания времени спавна,
        // даже если обычные враги ещё живы.
        if (IsBossWave(currentWave))
        {
            SpawnBoss();
            SpawnBossEscort(currentWave);
        }

        // Следующая волна начинается только после смерти всех оставшихся врагов и босса.
        while (enemiesAlive > 0)
            yield return null;

        lastCompletedWave = currentWave;

        // Последний враг умер: сразу возвращаем бой в исходное состояние.
        // Пауза timeBetweenWaves происходит уже ПОСЛЕ этого сброса, перед следующей волной.
        ResetCombatStateAfterWave();

        OnWaveCompleted?.Invoke(lastCompletedWave);

        // За завершение ЛЮБОЙ волны, включая боссовую, даём 1 изумруд.
        if (PlayerProgress.Instance != null)
            PlayerProgress.Instance.AddEmeralds(1);

        activeBoss = null;
        currentWave++;
        UpdateWaveText();

        if (battleStarted)
            StartCoroutine(RunWave());
    }

    void ResetCombatStateAfterWave()
    {
        // Замок и глобальная мана сразу полностью восстанавливаются.
        Castle castle = FindAnyObjectByType<Castle>();
        if (castle != null)
            castle.RestoreToFull();

        // Все герои: снять временную ульту/визуал, очистить цель и сделать ульту готовой.
        Hero[] heroes = FindObjectsByType<Hero>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Hero hero in heroes)
            if (hero != null) hero.ResetCombatStateAfterWave();

        // Городские лучники тоже возвращаются в Ready и сбрасывают текущую атаку.
        CityArcherUnit[] archers = FindObjectsByType<CityArcherUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (CityArcherUnit archer in archers)
            if (archer != null) archer.ResetCombatStateAfterWave();

        // После окончания волны не оставляем летящие/застрявшие стрелы на поле.
        Projectile[] projectiles = FindObjectsByType<Projectile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Projectile projectile in projectiles)
            if (projectile != null) Destroy(projectile.gameObject);
    }

    float GetWaveDuration(int wave)
    {
        // До 100-й волны длительность плавно растёт от 2 до 55 секунд.
        if (wave < 100)
        {
            float t = Mathf.InverseLerp(1f, 99f, wave);
            return Mathf.Lerp(firstWaveDuration, wave99Duration, t);
        }

        // С 100-й волны — минутные волны.
        return wave100PlusDuration;
    }

    int GetEnemyCount(int wave)
    {
        if (wave <= 1) return 5;
        if (wave == 2) return 7;
        if (wave == 3) return 9;
        if (wave == 4) return 11;
        if (wave == 5) return 14;

        if (wave == 6) return 16;
        if (wave == 7) return 18;
        if (wave == 8) return 20;
        if (wave == 9) return 24;
        if (wave == 10) return 27;

        // 11-30: +5 врагов за волну.
        if (wave <= 30)
            return 30 + (wave - 11) * 5;

        // 31-60: +6 врагов за волну.
        // 30-я = 125, поэтому 31-я = 131.
        if (wave <= 60)
            return 131 + (wave - 31) * 6;

        // 61-80: +7 врагов за волну.
        // 60-я = 305, поэтому 61-я = 312.
        if (wave <= 80)
            return 312 + (wave - 61) * 7;

        // 81-90: +8 врагов за волну.
        // 80-я = 445, поэтому 81-я = 453.
        if (wave <= 90)
            return 453 + (wave - 81) * 8;

        // 91-100: +9 врагов за волну.
        // 90-я = 525, поэтому 91-я = 534.
        if (wave <= 100)
            return 534 + (wave - 91) * 9;

        // После 100-й пока продолжаем +9. Позже заменим отдельным алгоритмом.
        return 615 + (wave - 100) * 9;
    }

    void GetSpawnChances(int wave, out float normalChance, out float fastChance, out float tankChance)
    {
        if (wave <= 5)
        {
            normalChance = 1f;
            fastChance = 0f;
            tankChance = 0f;
            return;
        }

        if (wave <= 10)
        {
            normalChance = 0.80f;
            fastChance = 0.20f;
            tankChance = 0f;
            return;
        }

        if (wave <= 20)
        {
            normalChance = 0.70f;
            fastChance = 0.25f;
            tankChance = 0.05f;
            return;
        }

        if (wave <= 30)
        {
            normalChance = 0.60f;
            fastChance = 0.30f;
            tankChance = 0.10f;
            return;
        }

        // 31-100 и пока после 100 — стабильный состав.
        normalChance = 0.50f;
        fastChance = 0.35f;
        tankChance = 0.15f;
    }

    bool IsBossWave(int wave)
    {
        return bossEveryWaves > 0 && wave % bossEveryWaves == 0;
    }

    void SpawnEnemyForWave(int wave)
    {
        GameObject prefab = GetWeightedEnemyPrefab(wave);
        SpawnEnemy(prefab);
    }

    GameObject GetWeightedEnemyPrefab(int wave)
    {
        GetSpawnChances(wave, out float normalChance, out float fastChance, out float tankChance);

        float roll = Random.value;

        if (roll < normalChance)
            return GetNormalPrefab();

        roll -= normalChance;

        if (roll < fastChance)
            return FastPrefab != null ? FastPrefab : GetNormalPrefab();

        return TankPrefab != null ? TankPrefab : GetNormalPrefab();
    }

    GameObject GetNormalPrefab()
    {
        if (enemyPrefab != null)
            return enemyPrefab;

        if (FastPrefab != null)
            return FastPrefab;

        return TankPrefab;
    }

    Vector3 GetEnemySpawnPosition()
    {
        float spawnX = spawnPoint != null ? spawnPoint.position.x : 8f;

        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float rightEdge = cam.transform.position.x +
                              cam.orthographicSize * cam.aspect;
            spawnX = rightEdge + spawnOutsideScreen;
        }

        float halfRoad = Mathf.Max(0f, roadHeight * 0.5f);
        float spawnY = Random.Range(
            roadCenterY - halfRoad,
            roadCenterY + halfRoad
        );

        float spawnZ = spawnPoint != null ? spawnPoint.position.z : 0f;
        return new Vector3(spawnX, spawnY, spawnZ);
    }

    void SpawnEnemy(GameObject prefab)
    {
        if (prefab == null)
            return;

        Vector3 spawnPosition = GetEnemySpawnPosition();

        GameObject enemyObject = Instantiate(
            prefab,
            spawnPosition,
            Quaternion.identity
        );

        Enemy enemy = enemyObject.GetComponent<Enemy>();

        if (enemy != null)
        {
            enemy.SetWaveSpawner(this);
            enemy.SetLaneTarget(spawnPosition.y);
            enemy.ApplyWaveScaling(
                currentWave,
                enemyHealthGrowth,
                enemyDamageGrowth,
                enemyGoldGrowth,
                enemyExperienceGrowth
            );
            enemiesAlive++;
        }
    }

    void SpawnBossEscort(int wave)
    {
        // 10-я: строго 4 Tank.
        if (wave == 10)
        {
            for (int i = 0; i < 4; i++)
                SpawnEnemy(TankPrefab != null ? TankPrefab : GetNormalPrefab());

            return;
        }

        // 20-я: 8 случайных врагов по шансам текущей волны.
        int escortCount = wave == 20 ? 8 : 10;

        // 30, 40, 50 ... 100: по 10 случайных врагов.
        for (int i = 0; i < escortCount; i++)
            SpawnEnemyForWave(wave);
    }

    void SpawnBoss()
    {
        GameObject prefab = bossPrefab != null ? bossPrefab : GetNormalPrefab();

        if (prefab == null)
            return;

        Vector3 spawnPosition = GetEnemySpawnPosition();

        GameObject bossObject = Instantiate(
            prefab,
            spawnPosition,
            Quaternion.identity
        );

        bossObject.transform.localScale *= bossScaleMultiplier;

        Enemy boss = bossObject.GetComponent<Enemy>();

        if (boss != null)
        {
            boss.SetWaveSpawner(this);
            boss.SetLaneTarget(spawnPosition.y);
            // Боссовые коэффициенты растут в 5 раз сильнее обычных.
            // Например 1.035 -> 1.175 (+17.5% вместо +3.5%).
            float bossHealthGrowth = 1f + (enemyHealthGrowth - 1f) * bossScalingPower;
            float bossDamageGrowth = 1f + (enemyDamageGrowth - 1f) * bossScalingPower;
            float bossGoldGrowth = 1f + (enemyGoldGrowth - 1f) * bossScalingPower;
            float bossExperienceGrowth = 1f + (enemyExperienceGrowth - 1f) * bossScalingPower;

            int bossIndex = Mathf.Max(0, (currentWave / bossEveryWaves) - 1);

            // Первый босс (10-я волна) имеет индекс 0 и не получает
            // дополнительного роста. Каждый следующий босс получает один
            // дополнительный шаг усиленных коэффициентов.
            boss.ApplyWaveScaling(
                10 + bossIndex,
                bossHealthGrowth,
                bossDamageGrowth,
                bossGoldGrowth,
                bossExperienceGrowth
            );

            boss.goldReward = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    firstBossGoldReward *
                    Mathf.Pow(bossGoldGrowth, bossIndex)
                )
            );

            boss.experienceReward = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    firstBossExperienceReward *
                    Mathf.Pow(bossExperienceGrowth, bossIndex)
                )
            );

            boss.SetAsBoss(bossHealthMultiplier, bossDamageMultiplier);
            activeBoss = boss;
            enemiesAlive++;

            // После окончания таймера та же полоса становится HP босса.
            SetWaveBarColor(bossWaveColor);
            SetWaveBar(1f);
        }
    }

    public void EnemyDied(Enemy enemy)
    {
        enemiesAlive = Mathf.Max(0, enemiesAlive - 1);

        if (enemy == activeBoss)
        {
            activeBoss = null;
            SetWaveBar(0f);
        }
    }

    public void UpdateBossHealth(float currentHealth, float maxHealth)
    {
        if (activeBoss == null || maxHealth <= 0f)
            return;

        SetWaveBar(currentHealth / maxHealth);
    }

    public void UpdateWaveText()
    {
        if (waveText != null)
            waveText.text = currentWave.ToString();
    }

    void SetWaveBarColor(Color color)
    {
        if (waveBarImage != null)
            waveBarImage.color = color;
    }

    void SetWaveBar(float normalized)
    {
        if (waveBarFill == null)
            return;

        normalized = Mathf.Clamp01(normalized);

        float newWidth = fullWaveBarWidth * normalized;
        Vector2 size = waveBarFill.sizeDelta;
        size.x = newWidth;
        waveBarFill.sizeDelta = size;

        Vector2 position = fullWaveBarPosition;
        position.x -= waveBarFill.pivot.x * (fullWaveBarWidth - newWidth);
        waveBarFill.anchoredPosition = position;
    }
}
