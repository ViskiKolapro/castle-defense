using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2f;
    [Tooltip("Дополнительный зазор между врагом и правой стеной замка.")]
    public float castleStopGap = 0.05f;

    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;
    public Transform healthBarFill;
    [HideInInspector] public GameObject healthBarRoot;
    private readonly System.Collections.Generic.List<GameObject> healthBarVisualObjects = new System.Collections.Generic.List<GameObject>();

    [Header("Combat type")]
    public CombatUnitType combatType = CombatUnitType.Melee;

    [Header("Attack")]
    public float attackDamage = 10f;
    public float attackCooldown = 1f;
    [Range(0f, 1f)] public float critChance = 0.02f;
    [Min(1f)] public float critMultiplier = 2f;

    [Header("Defense")]
    [Range(0f, 1f)] public float dodgeChance = 0.01f;
    [Range(0f, 1f)] public float blockChance = 0.25f;
    [Range(0f, 1f)] public float blockDamageReduction = 0.50f;

    [Header("Reward")]
    public int goldReward = 10;
    public int experienceReward = 5;

    private Castle castle;
    private float attackTimer;
    private bool dead;
    private bool isBoss;
    private WaveSpawner waveSpawner;
    private bool hasLaneTarget;
    private float laneTargetY;

    // Урон снарядов, которые уже летят в этого врага.
    private float reservedIncomingDamage;
    private float bleedRemaining;
    private float bleedDamagePerSecond;
    private float bleedSlowFraction;

    void Start()
    {
        currentHealth = maxHealth;
        castle = FindAnyObjectByType<Castle>();

        CacheHealthBarVisuals();
        UpdateHealthBar();

        if (isBoss && waveSpawner != null)
            waveSpawner.UpdateBossHealth(currentHealth, maxHealth);
    }

    void Update()
    {
        if (dead)
            return;

        UpdateBleeding();

        if (castle == null)
            castle = FindAnyObjectByType<Castle>();

        if (castle == null)
            return;

        // Враг идёт по своей прямой линии внутри дороги.
        // Y фиксируется при спавне; позже взрывы/защитники смогут временно
        // смещать врага или менять его цель отдельной механикой.
        float targetY = hasLaneTarget ? laneTargetY : castle.transform.position.y;

        // Останавливаемся СНАРУЖИ у правой границы спрайта замка, а не у его центра.
        float castleRightX = castle.transform.position.x;
        SpriteRenderer castleRenderer = castle.GetComponent<SpriteRenderer>();
        if (castleRenderer != null && castleRenderer.sprite != null)
            castleRightX = castleRenderer.bounds.max.x;

        float enemyHalfWidth = 0f;
        SpriteRenderer enemyRenderer = GetComponent<SpriteRenderer>();
        if (enemyRenderer != null && enemyRenderer.sprite != null)
            enemyHalfWidth = enemyRenderer.bounds.extents.x;

        float stopX = castleRightX + enemyHalfWidth + Mathf.Max(0f, castleStopGap);

        if (transform.position.x > stopX)
        {
            // Движение только по X: каждый враг сохраняет свою линию по Y.
            float newX = Mathf.MoveTowards(transform.position.x, stopX, moveSpeed * (bleedRemaining > 0f ? 1f - bleedSlowFraction : 1f) * Time.deltaTime);
            transform.position = new Vector3(newX, targetY, transform.position.z);
        }
        else
        {
            // На случай большого шага кадра не даём врагу проскочить внутрь замка.
            transform.position = new Vector3(stopX, targetY, transform.position.z);
            AttackCastle();
        }
    }

    void AttackCastle()
    {
        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            float outgoingDamage = attackDamage;
            if (combatType != CombatUnitType.Siege && Random.value < Mathf.Clamp01(critChance))
                outgoingDamage *= Mathf.Max(1f, critMultiplier);

            castle.TakeDamage(outgoingDamage);
            attackTimer = attackCooldown;
        }
    }


    public bool IsDead => dead;
    public bool IsBoss => isBoss;

    public float ExpectedHealth => Mathf.Max(0f, currentHealth - reservedIncomingDamage);

    public bool IsExpectedDead => dead || ExpectedHealth <= 0f;

    public void ReserveIncomingDamage(float amount)
    {
        if (dead)
            return;

        reservedIncomingDamage += Mathf.Max(0f, amount);
    }

    public void ReleaseIncomingDamage(float amount)
    {
        reservedIncomingDamage = Mathf.Max(0f, reservedIncomingDamage - Mathf.Max(0f, amount));
    }

    public void TakeDamage(float damage)
    {
        TakePreparedDamage(PrepareIncomingDamage(damage));
    }

    public float TakePreparedDamage(float damage)
    {
        if (dead || damage <= 0f) return 0f;

        float before = currentHealth;
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        float actualDamage = Mathf.Max(0f, before - currentHealth);

        UpdateHealthBar();

        if (isBoss && waveSpawner != null)
            waveSpawner.UpdateBossHealth(currentHealth, maxHealth);

        if (currentHealth <= 0f)
            Die();
        return actualDamage;
    }

    public float PrepareIncomingDamage(float incomingDamage, bool ignoreDodge = false)
    {
        float result = Mathf.Max(0f, incomingDamage);

        // Siege has no innate dodge/block mechanics.
        if (combatType == CombatUnitType.Siege)
            return result;

        // Melee and Ranged can dodge.
        if (!ignoreDodge && Random.value < Mathf.Clamp01(dodgeChance))
            return 0f;

        // Block is exclusive to Melee.
        if (combatType == CombatUnitType.Melee && Random.value < Mathf.Clamp01(blockChance))
            result *= 1f - Mathf.Clamp01(blockDamageReduction);

        return result;
    }

    public void ApplyBleeding(float duration, float slowFraction, float maxHealthFractionPerProc)
    {
        if (dead || duration <= 0f) return;
        // Extending effect: every proc adds its duration; it does not create a second independent status.
        bleedRemaining += duration;
        bleedSlowFraction = Mathf.Max(bleedSlowFraction, Mathf.Clamp01(slowFraction));
        bleedDamagePerSecond = Mathf.Max(bleedDamagePerSecond, maxHealth * Mathf.Max(0f, maxHealthFractionPerProc) / duration);
    }

    void UpdateBleeding()
    {
        if (bleedRemaining <= 0f || dead) return;
        float dt = Mathf.Min(Time.deltaTime, bleedRemaining);
        bleedRemaining -= dt;
        if (bleedDamagePerSecond > 0f) TakePreparedDamage(bleedDamagePerSecond * dt);
        if (bleedRemaining <= 0f)
        {
            bleedRemaining = 0f;
            bleedDamagePerSecond = 0f;
            bleedSlowFraction = 0f;
        }
    }

    void CacheHealthBarVisuals()
    {
        healthBarVisualObjects.Clear();
        if (healthBarFill == null) return;

        void Add(GameObject go)
        {
            if (go != null && go != gameObject && !healthBarVisualObjects.Contains(go))
                healthBarVisualObjects.Add(go);
        }

        // Если Fill находится внутри отдельного контейнера HP-бара — скрываем контейнер целиком.
        Transform parent = healthBarFill.parent;
        if (parent != null && parent != transform)
        {
            healthBarRoot = parent.gameObject;
            Add(healthBarRoot);
            return;
        }

        // В текущих префабах Fill и серый Background могут быть соседями прямо у Enemy.
        // Тогда нельзя выключать родителя (это сам Enemy), поэтому находим все части HP-бара по имени.
        Add(healthBarFill.gameObject);
        foreach (Transform child in transform)
        {
            if (child == healthBarFill) continue;
            string n = child.name.ToLowerInvariant();
            if (n.Contains("health") || n.Contains("hpbar") || n.Contains("hp_bar") ||
                n.Contains("healthbar") || n.Contains("health_bar") || n.Contains("barbackground") ||
                n.Contains("bar_background"))
                Add(child.gameObject);
        }

        // Последний безопасный вариант: серый фон обычно является единственным соседним SpriteRenderer
        // маленького размера рядом с Fill. Берём только объекты с 'background'/'bg' в имени.
        foreach (Transform child in transform)
        {
            string n = child.name.ToLowerInvariant();
            if (n.Contains("background") || n == "bg" || n.Contains("hp bg") || n.Contains("health bg"))
                Add(child.gameObject);
        }
    }

    void SetHealthBarVisible(bool visible)
    {
        if (healthBarVisualObjects.Count == 0) CacheHealthBarVisuals();
        foreach (GameObject go in healthBarVisualObjects)
            if (go != null) go.SetActive(visible);
    }

    void UpdateHealthBar()
    {
        if (healthBarFill == null || maxHealth <= 0f) return;

        float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);
        bool shouldShow = currentHealth > 0f && currentHealth < maxHealth - 0.0001f;

        SetHealthBarVisible(shouldShow);

        // Даже когда объект скрыт, сохраняем правильный размер на момент следующего показа.
        healthBarFill.localScale = new Vector3(1.1f * healthPercent, 0.1f, 1f);
        healthBarFill.localPosition = new Vector3(-0.55f * (1f - healthPercent), 0.8f, -0.1f);
    }

    public void ApplyWaveScaling(
        int wave,
        float healthGrowth,
        float damageGrowth,
        float goldGrowth,
        float experienceGrowth
    )
    {
        int waveIndex = Mathf.Max(0, wave - 10);

        maxHealth *= Mathf.Pow(Mathf.Max(1f, healthGrowth), waveIndex);
        attackDamage *= Mathf.Pow(Mathf.Max(1f, damageGrowth), waveIndex);

        goldReward = Mathf.Max(
            1,
            Mathf.RoundToInt(
                goldReward * Mathf.Pow(Mathf.Max(1f, goldGrowth), waveIndex)
            )
        );

        experienceReward = Mathf.Max(
            1,
            Mathf.RoundToInt(
                experienceReward * Mathf.Pow(Mathf.Max(1f, experienceGrowth), waveIndex)
            )
        );

        currentHealth = maxHealth;
        UpdateHealthBar();
    }

    public void SetLaneTarget(float targetY)
    {
        laneTargetY = targetY;
        hasLaneTarget = true;
    }

    public void SetWaveSpawner(WaveSpawner spawner)
    {
        waveSpawner = spawner;
    }

    public void SetAsBoss(float healthMultiplier, float damageMultiplier)
    {
        isBoss = true;
        maxHealth *= Mathf.Max(1f, healthMultiplier);
        attackDamage *= Mathf.Max(1f, damageMultiplier);
        // Награда босса задаётся WaveSpawner отдельно.
        currentHealth = maxHealth;
        UpdateHealthBar();

        if (waveSpawner != null)
            waveSpawner.UpdateBossHealth(currentHealth, maxHealth);
    }

    void Die()
    {
        if (dead)
            return;

        dead = true;

        if (PlayerProgress.Instance != null)
        {
            PlayerProgress.Instance.AddGold(goldReward);
            PlayerProgress.Instance.AddExperience(experienceReward);
        }

        Debug.Log(
            (isBoss ? "Босс убит" : "Враг убит") +
            " | Золото: " + goldReward +
            " | Опыт: " + experienceReward
        );

        if (waveSpawner != null)
            waveSpawner.EnemyDied(this);

        Destroy(gameObject);
    }
}
