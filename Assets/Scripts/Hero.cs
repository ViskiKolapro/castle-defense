using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class Hero : MonoBehaviour
{
    [Header("Hero")]
    public string heroName = "Bow Master";

    [Header("Combat - balance in Inspector")]
    public CombatUnitType combatType = CombatUnitType.Ranged;
    public float damage = 25f;
    [Tooltip("Полный интервал между атаками в секундах.")]
    public float attackCooldown = 1.5f;
    [Tooltip("Скорость снаряда именно этого героя.")]
    public float projectileSpeed = 8f;

    [Header("Critical hit")]
    [Range(0f, 1f)] public float critChance = 0.01f;
    [Min(1f)] public float critMultiplier = 2f;

    // Technical evolution state. Hidden from the normal Hero Inspector;
    // BowMasterEvolutionController owns these values.
    [HideInInspector] public bool evolution1Purchased = false;
    [HideInInspector] public bool evolution2Purchased = false;
    [HideInInspector] public int activeEvolution = 0; // 0=base, 1=Evolution I, 2=Physical Evolution II
    [HideInInspector] public float bossDamageMultiplier = 1f;
    [HideInInspector] public bool ultimateUnlocked = false;
    [HideInInspector] public float ultimateAttackSpeedBonus = 1.5f;
    [HideInInspector] public float ultimateDamageBonus = 1.5f;
    [Header("Evolution I Visuals (Bow Master)")]
    [Tooltip("Ready sprite used after Evolution I is purchased.")]
    public Sprite evolution1ReadySprite;
    [Tooltip("Aim/draw sprite used after Evolution I is purchased.")]
    public Sprite evolution1AimSprite;
    [Tooltip("Release sprite used after Evolution I is purchased.")]
    public Sprite evolution1ReleaseSprite;

    [Header("Evolution II Physical Visuals (Bow Master)")]
    public Sprite evolution2ReadySprite;
    public Sprite evolution2AimSprite;
    [Tooltip("Golden Physical Evolution II arrow prefab. Assign after creating its glow/trail version.")]
    public GameObject evolution2ProjectilePrefab;

    [Header("Victoria Evolution Visuals")]
    public Sprite victoriaRedEvolution1ReadySprite;
    public Sprite victoriaRedEvolution1AimSprite;
    public Sprite victoriaRedEvolution2ReadySprite;
    public Sprite victoriaRedEvolution2AimSprite;
    public Sprite victoriaBlueEvolution1ReadySprite;
    public Sprite victoriaBlueEvolution1AimSprite;
    public Sprite victoriaBlueEvolution2ReadySprite;
    public Sprite victoriaBlueEvolution2AimSprite;
    [HideInInspector] public bool victoriaRedEvolution1Purchased;
    [HideInInspector] public bool victoriaRedEvolution2Purchased;
    [HideInInspector] public bool victoriaBlueEvolution1Purchased;
    [HideInInspector] public bool victoriaBlueEvolution2Purchased;
    [HideInInspector] public int victoriaActiveEvolution; // 0=base, 1=red I, 2=red II, 3=blue I, 4=blue II
    private GameObject baseProjectilePrefab;
    private Sprite baseReadySprite, baseAimSprite, baseReleaseSprite;

    // Bow Master Evolution I ultimate runtime state.
    [HideInInspector] public float ultimateDuration = 3f;
    [HideInInspector] public float ultimateCooldown = 10f;
    private bool ultimateActive;
    private Coroutine ultimateRoutine;
    private float ultimateCooldownRemaining;
    private Coroutine ultimateVisualRoutine;
    private Color ultimateOriginalColor = Color.white;
    private Vector3 ultimateOriginalScale;
    private bool ultimateVisualCaptured;
    private float ultimateBaseDamage;
    private float ultimateBaseAttackCooldown;
    private GameObject ultimateBarRoot;
    private RectTransform ultimateBarRect;
    private Image ultimateBarFill;
    private Canvas ultimateBarCanvas;
    private Vector2 ultimateBarScreenOffset;
    private bool ultimateBarOffsetCaptured;

    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Attack animation (optional)")]
    [Tooltip("Обычное состояние героя.")]
    public Sprite readySprite;
    [Tooltip("Натянутый лук со стрелой.")]
    public Sprite aimSprite;
    [Tooltip("Состояние сразу после выстрела, уже без стрелы.")]
    public Sprite releaseSprite;
    [Tooltip("Сколько длится подготовка/натяжение перед появлением Projectile.")]
    public float drawTime = 0.30f;
    [Tooltip("Сколько показывать состояние после выстрела.")]
    public float releaseTime = 0.10f;

    [Header("Upgrade 1-200")]
    public int heroLevel = 1;
    public int maxHeroLevel = 200;
    public float damagePerLevel = 5f;
    public int firstUpgradeCost = 20;
    public int upgradeCostIncrease = 10;

    [Header("State")]
    public int purchasePrice = 100;
    public bool isPurchased = true;
    public bool isInstalled = true;
    [Tooltip("0 = не установлен, 1-12 = номер слота на замке")]
    public int installedSlot = 1;

    private float attackTimer;
    private Enemy currentTarget;
    private bool hasShotAtCurrentTarget;
    private bool isAttacking;
    private SpriteRenderer spriteRenderer;

    public int CurrentUpgradeCost
    {
        get
        {
            if (heroLevel >= maxHeroLevel) return 0;

            // Levels 1-100 keep the original progression.
            // From level 100 onward, every next upgrade costs +100 gold
            // compared with the previous upgrade price.
            if (heroLevel < 100)
                return firstUpgradeCost + (heroLevel - 1) * upgradeCostIncrease;

            int costToReachLevel100 = firstUpgradeCost + 98 * upgradeCostIncrease;
            return costToReachLevel100 + (heroLevel - 99) * 100;
        }
    }

    public bool IsMaxLevel => heroLevel >= maxHeroLevel;

    // Ultimate modifiers are runtime-only. They never overwrite the permanent
    // damage/attackCooldown values shown in the hero card.
    private bool IsVictoria => heroName == "Victoria";
    private float VictoriaAttackSpeedMultiplier => IsVictoria && victoriaActiveEvolution != 0 ? 2.5f : 1f; // +150% attack speed
    // Additive damage-buff layer. Percent bonuses in this layer are summed first,
    // then applied to the hero's normal level-scaled base damage.
    // Victoria Evolution I/II/Blue I currently each provide +100% base damage.
    public float AdditiveDamageBonusPercent => IsVictoria && victoriaActiveEvolution != 0 ? 1f : 0f;

    public float EffectiveAttackCooldown
    {
        get
        {
            float value = attackCooldown / VictoriaAttackSpeedMultiplier;
            if (ultimateActive) value /= (1f + Mathf.Max(0f, ultimateAttackSpeedBonus));
            return Mathf.Max(0.01f, value);
        }
    }

    public float EffectiveDamage
    {
        get
        {
            float value = damage * (1f + Mathf.Max(0f, AdditiveDamageBonusPercent));
            // Runtime/upper-layer multipliers are applied after the additive buff layer.
            if (ultimateActive) value *= 1f + Mathf.Max(0f, ultimateDamageBonus);
            return value;
        }
    }

    public float EffectiveCritChance => Mathf.Clamp01(critChance + (IsVictoria && victoriaActiveEvolution == 2 ? 0.05f : 0f));
    public bool VictoriaNeverMisses => IsVictoria && (victoriaActiveEvolution == 3 || victoriaActiveEvolution == 4);

    void Awake()
    {
        maxHeroLevel = 200;
        if (baseProjectilePrefab == null) baseProjectilePrefab = projectilePrefab;
        if (baseReadySprite == null) baseReadySprite = readySprite;
        if (baseAimSprite == null) baseAimSprite = aimSprite;
        if (baseReleaseSprite == null) baseReleaseSprite = releaseSprite;
        ApplyReadySpriteInEditor();
    }

    void OnEnable()
    {
        ApplyReadySpriteInEditor();
    }

    void OnValidate()
    {
        maxHeroLevel = 200;
        ApplyReadySpriteInEditor();
    }

    void ApplyReadySpriteInEditor()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // Герой существует в сцене как данные, но визуально показывается только когда
        // он куплен И установлен в слот. Это действует и в редакторе, и в Play Mode.
        bool shouldBeVisible = isPurchased && isInstalled;
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = shouldBeVisible;
            if (readySprite != null && (!Application.isPlaying || !isAttacking))
                spriteRenderer.sprite = readySprite;
        }

        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (Collider2D col in colliders)
            if (col != null) col.enabled = Application.isPlaying && shouldBeVisible;
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            ApplyReadySpriteInEditor();
            return;
        }

        // Таймер идёт даже во время Draw/Release. Иначе время анимации ошибочно
        // прибавлялось к cooldown и герой стрелял медленнее заданного значения.
        attackTimer -= Time.deltaTime;
        UpdateUltimateRuntime();

        if (!isPurchased || !isInstalled || isAttacking)
            return;
        if (attackTimer > 0f)
            return;

        if (currentTarget == null || currentTarget.IsDead)
            SelectRandomTarget(false);

        if (currentTarget != null && hasShotAtCurrentTarget && currentTarget.IsExpectedDead)
            SelectRandomTarget(true);

        if (currentTarget == null)
            return;

        StartCoroutine(AttackRoutine(currentTarget));
        // attackCooldown — это ПОЛНЫЙ интервал между выстрелами.
        // Например 0.1 = примерно 10 выстрелов в секунду.
        attackTimer = Mathf.Max(0.01f, EffectiveAttackCooldown);
    }

    IEnumerator AttackRoutine(Enemy target)
    {
        isAttacking = true;

        // Если спрайты не назначены, герой продолжит работать как раньше — без анимации.
        if (spriteRenderer != null && aimSprite != null)
            spriteRenderer.sprite = aimSprite;

        // Визуальная анимация не должна замедлять реальную скорострельность.
        // Если cooldown меньше заданных Draw/Release, автоматически ужимаем кадры в его пределы.
        float totalCadence = Mathf.Max(0.01f, EffectiveAttackCooldown);
        float effectiveDrawTime = Mathf.Min(Mathf.Max(0f, drawTime), totalCadence * 0.60f);
        float remainingForRelease = Mathf.Max(0f, totalCadence - effectiveDrawTime);
        float effectiveReleaseTime = Mathf.Min(Mathf.Max(0f, releaseTime), remainingForRelease);

        if (effectiveDrawTime > 0f)
            yield return new WaitForSeconds(effectiveDrawTime);

        // За время натяжения цель могла умереть. Тогда ищем новую живую цель.
        if (target == null || target.IsDead || (hasShotAtCurrentTarget && target.IsExpectedDead))
        {
            SelectRandomTarget(true);
            target = currentTarget;
        }

        if (target != null && !target.IsDead)
        {
            Shoot(target);
            currentTarget = target;
            hasShotAtCurrentTarget = true;
        }

        if (spriteRenderer != null && releaseSprite != null)
            spriteRenderer.sprite = releaseSprite;

        if (effectiveReleaseTime > 0f)
            yield return new WaitForSeconds(effectiveReleaseTime);

        if (spriteRenderer != null && readySprite != null)
            spriteRenderer.sprite = readySprite;

        isAttacking = false;
    }

    void SelectRandomTarget(bool avoidExpectedDead)
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<Enemy> candidates = new List<Enemy>();

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            if (avoidExpectedDead && enemy.IsExpectedDead) continue;
            candidates.Add(enemy);
        }

        if (candidates.Count == 0)
        {
            currentTarget = null;
            hasShotAtCurrentTarget = false;
            return;
        }

        currentTarget = candidates[Random.Range(0, candidates.Count)];
        hasShotAtCurrentTarget = false;
    }

    void Shoot(Enemy target)
    {
        if (projectilePrefab == null || firePoint == null || target == null)
            return;

        float shotDamage = RollAttackDamage();
        if (target.IsBoss)
            shotDamage *= Mathf.Max(1f, bossDamageMultiplier);
        // Dodge/block are rolled NOW, before the arrow is launched. The exact
        // final damage is what gets reserved, so targeting knows whether this
        // projectile will really finish the enemy. It is not rolled again on hit.
        shotDamage = target.PrepareIncomingDamage(shotDamage, VictoriaNeverMisses);
        target.ReserveIncomingDamage(shotDamage);

        GameObject arrow = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Projectile projectile = arrow.GetComponent<Projectile>();

        if (projectile != null)
            projectile.SetTarget(target, shotDamage, projectileSpeed, this);
        else
            target.ReleaseIncomingDamage(shotDamage);
    }

    float RollAttackDamage()
    {
        if (combatType == CombatUnitType.Siege)
            return EffectiveDamage;

        float baseDamage = EffectiveDamage;
        bool critical = Random.value < EffectiveCritChance;
        return critical ? baseDamage * Mathf.Max(1f, critMultiplier) : baseDamage;
    }

    public void OnProjectileHit(Enemy target, float actualDamage)
    {
        if (!IsVictoria || victoriaActiveEvolution == 0 || target == null || actualDamage <= 0f) return;

        float hpVamp = victoriaActiveEvolution == 1 ? 0.08f : victoriaActiveEvolution == 2 ? 0.20f : victoriaActiveEvolution == 3 ? 0.05f : victoriaActiveEvolution == 4 ? 0.08f : 0f;
        float manaVamp = victoriaActiveEvolution == 3 ? 0.05f : victoriaActiveEvolution == 4 ? 0.08f : 0f;
        Castle castle = FindAnyObjectByType<Castle>();
        if (castle != null)
        {
            if (hpVamp > 0f) castle.AddHealth(actualDamage * hpVamp);
            if (manaVamp > 0f) castle.AddMana(actualDamage * manaVamp);
        }

        if (victoriaActiveEvolution == 1 || victoriaActiveEvolution == 2)
        {
            float chance = victoriaActiveEvolution == 2 ? 0.50f : 0.10f;
            if (Random.value < chance) target.ApplyBleeding(3f, 0.25f, 0.04f);
        }

        if (victoriaActiveEvolution == 4)
        {
            // Blue II: after the normal hit, deal an immediate extra 1% of target Max HP.
            // The bonus ignores dodge/block because the projectile already connected.
            if (!target.IsDead) target.TakePreparedDamage(target.maxHealth * 0.01f);

            // If Victoria's hit (normal or the extra 1%) killed the target, convert its FULL Max HP
            // into castle resources: 50% HP + 50% mana, capped by Castle.AddHealth/AddMana.
            if (target.IsDead && castle != null)
            {
                float halfMaxHp = target.maxHealth * 0.50f;
                castle.AddHealth(halfMaxHp);
                castle.AddMana(halfMaxHp);
            }
        }
    }

    public bool TryUpgrade()
    {
        if (!isPurchased || IsMaxLevel || PlayerProgress.Instance == null)
            return false;

        int cost = CurrentUpgradeCost;
        if (!PlayerProgress.Instance.SpendGold(cost))
            return false;

        heroLevel++;
        damage += damagePerLevel;
        return true;
    }

    public void SetInstalled(bool installed)
    {
        isInstalled = installed;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        bool shouldBeVisible = isPurchased && installed;
        if (spriteRenderer != null)
            spriteRenderer.enabled = shouldBeVisible;

        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (Collider2D col in colliders)
            if (col != null) col.enabled = Application.isPlaying && shouldBeVisible;

        if (!installed)
        {
            StopAllCoroutines();
            ultimateRoutine = null;
            ultimateVisualRoutine = null;
            ultimateActive = false;
            RestoreUltimateVisual();
            isAttacking = false;
            if (spriteRenderer != null && readySprite != null)
                spriteRenderer.sprite = readySprite;
        }
    }
    public void ApplyEvolution1Visuals()
    {
        if (evolution1ReadySprite != null) readySprite = evolution1ReadySprite;
        if (evolution1AimSprite != null) aimSprite = evolution1AimSprite;
        if (evolution1ReleaseSprite != null) releaseSprite = evolution1ReleaseSprite;
        if (baseProjectilePrefab != null) projectilePrefab = baseProjectilePrefab;
        RefreshCurrentSprite();
    }

    public void ApplyEvolution2Visuals()
    {
        if (evolution2ReadySprite != null) readySprite = evolution2ReadySprite;
        if (evolution2AimSprite != null) aimSprite = evolution2AimSprite;
        // Physical Evolution II intentionally uses only two poses: Ready and Aim.
        // The Aim pose is also used for the tiny post-shot phase.
        if (evolution2AimSprite != null) releaseSprite = evolution2AimSprite;
        if (evolution2ProjectilePrefab != null) projectilePrefab = evolution2ProjectilePrefab;
        else if (baseProjectilePrefab != null) projectilePrefab = baseProjectilePrefab;
        RefreshCurrentSprite();
    }

    void RefreshCurrentSprite()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && readySprite != null && !isAttacking) spriteRenderer.sprite = readySprite;
    }

    public void ApplyEvolution1()
    {
        evolution1Purchased = true;
        SetActiveEvolution(1);
    }

    public void ApplyEvolution2()
    {
        if (!evolution1Purchased) evolution1Purchased = true;
        evolution2Purchased = true;
        SetActiveEvolution(2);
    }

    public void SetActiveEvolution(int evolution)
    {
        // The legacy evolution1/evolution2 fields belong to Bow Master only.
        // Other heroes keep their own base combat values and use their own evolution state.
        if (!string.Equals(heroName, "Bow Master", System.StringComparison.OrdinalIgnoreCase))
        {
            activeEvolution = 0;
            return;
        }

        if (evolution == 2 && !evolution2Purchased) evolution = evolution1Purchased ? 1 : 0;
        if (evolution == 1 && !evolution1Purchased) evolution = 0;
        activeEvolution = Mathf.Clamp(evolution, 0, 2);

        if (activeEvolution == 2)
        {
            attackCooldown = 0.25f;
            bossDamageMultiplier = 6f;
            ultimateUnlocked = true;
            ultimateAttackSpeedBonus = 3f;
            ultimateDamageBonus = 3f;
            ApplyEvolution2Visuals();
        }
        else if (activeEvolution == 1)
        {
            attackCooldown = 0.5f;
            bossDamageMultiplier = 3f;
            ultimateUnlocked = true;
            ultimateAttackSpeedBonus = 1.5f;
            ultimateDamageBonus = 1.5f;
            ApplyEvolution1Visuals();
        }
        else
        {
            attackCooldown = 1f;
            bossDamageMultiplier = 1f;
            ultimateUnlocked = false;
            ultimateAttackSpeedBonus = 1.5f;
            ultimateDamageBonus = 1.5f;
            if (baseReadySprite != null) readySprite = baseReadySprite;
            if (baseAimSprite != null) aimSprite = baseAimSprite;
            if (baseReleaseSprite != null) releaseSprite = baseReleaseSprite;
            if (baseProjectilePrefab != null) projectilePrefab = baseProjectilePrefab;
            RefreshCurrentSprite();
        }
        EnsureUltimateBar();
        UpdateUltimateBar();
    }

    public void SetVictoriaActiveEvolution(int evolution)
    {
        // Victoria state must never overwrite Bow Master's already-loaded evolution visuals.
        if (!IsVictoria)
        {
            victoriaActiveEvolution = 0;
            return;
        }

        if (evolution == 2 && !victoriaRedEvolution2Purchased) evolution = victoriaRedEvolution1Purchased ? 1 : 0;
        if (evolution == 1 && !victoriaRedEvolution1Purchased) evolution = 0;
        if (evolution == 3 && !victoriaBlueEvolution1Purchased) evolution = 0;
        if (evolution == 4 && !victoriaBlueEvolution2Purchased) evolution = victoriaBlueEvolution1Purchased ? 3 : 0;
        victoriaActiveEvolution = Mathf.Clamp(evolution, 0, 4);

        Sprite r = baseReadySprite, a = baseAimSprite, rel = baseReleaseSprite;
        if (victoriaActiveEvolution == 1) { r = victoriaRedEvolution1ReadySprite; a = victoriaRedEvolution1AimSprite; rel = a; }
        else if (victoriaActiveEvolution == 2) { r = victoriaRedEvolution2ReadySprite; a = victoriaRedEvolution2AimSprite; rel = a; }
        else if (victoriaActiveEvolution == 3) { r = victoriaBlueEvolution1ReadySprite; a = victoriaBlueEvolution1AimSprite; rel = a; }
        else if (victoriaActiveEvolution == 4) { r = victoriaBlueEvolution2ReadySprite; a = victoriaBlueEvolution2AimSprite; rel = a; }

        if (r != null) readySprite = r;
        if (a != null) aimSprite = a;
        if (rel != null) releaseSprite = rel;
        if (baseProjectilePrefab != null) projectilePrefab = baseProjectilePrefab;
        RefreshCurrentSprite();
    }

    public bool TryActivateUltimate()
    {
        if (!Application.isPlaying || !isPurchased || !isInstalled ||
            !evolution1Purchased || !ultimateUnlocked || ultimateActive || ultimateCooldownRemaining > 0f)
            return false;

        ultimateRoutine = StartCoroutine(UltimateRoutine());
        return true;
    }

    IEnumerator UltimateRoutine()
    {
        ultimateActive = true;
        ultimateCooldownRemaining = Mathf.Max(0.01f, ultimateCooldown);
        StartUltimateVisual();
        UpdateUltimateBar();

        yield return new WaitForSeconds(Mathf.Max(0.01f, ultimateDuration));

        ultimateActive = false;
        ultimateRoutine = null;
        StopUltimateVisual();
        UpdateUltimateBar();
    }

    void StartUltimateVisual()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        if (!ultimateVisualCaptured)
        {
            ultimateOriginalColor = spriteRenderer.color;
            ultimateOriginalScale = transform.localScale;
            ultimateVisualCaptured = true;
        }

        if (ultimateVisualRoutine != null) StopCoroutine(ultimateVisualRoutine);
        ultimateVisualRoutine = StartCoroutine(UltimateVisualRoutine());
    }

    IEnumerator UltimateVisualRoutine()
    {
        // Visible gold pulse for the whole ultimate. It works with Evolution I and II
        // because it is applied to the current SpriteRenderer rather than a specific sprite.
        while (ultimateActive)
        {
            float pulse = (Mathf.Sin(Time.time * 14f) + 1f) * 0.5f;
            spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.35f, 1f), 0.35f + pulse * 0.45f);
            transform.localScale = ultimateOriginalScale * Mathf.Lerp(1.02f, 1.07f, pulse);
            yield return null;
        }
        RestoreUltimateVisual();
        ultimateVisualRoutine = null;
    }

    void StopUltimateVisual()
    {
        if (ultimateVisualRoutine != null)
        {
            StopCoroutine(ultimateVisualRoutine);
            ultimateVisualRoutine = null;
        }
        RestoreUltimateVisual();
    }

    void RestoreUltimateVisual()
    {
        if (!ultimateVisualCaptured) return;
        if (spriteRenderer != null) spriteRenderer.color = ultimateOriginalColor;
        transform.localScale = ultimateOriginalScale;
    }

    // Called immediately when the LAST enemy of the wave dies.
    // This is the single end-of-wave reset point; the next wave starts only after the inter-wave pause.
    public void ResetCombatStateAfterWave()
    {
        if (!Application.isPlaying) return;

        StopAllCoroutines();
        ultimateRoutine = null;
        ultimateVisualRoutine = null;
        ultimateActive = false;
        ultimateCooldownRemaining = 0f;

        currentTarget = null;
        hasShotAtCurrentTarget = false;
        isAttacking = false;
        attackTimer = 0f;

        RestoreUltimateVisual();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && readySprite != null)
            spriteRenderer.sprite = readySprite;

        UpdateUltimateBar();
    }

    // Called by WaveSpawner when a NEW wave actually starts. Every wave begins
    // with the ultimate fully ready, regardless of when it was used last wave.
    public void ResetUltimateForNewWave()
    {
        if (!Application.isPlaying) return;
        if (ultimateRoutine != null)
        {
            StopCoroutine(ultimateRoutine);
            ultimateRoutine = null;
        }
        ultimateActive = false;
        StopUltimateVisual();
        ultimateCooldownRemaining = 0f;
        UpdateUltimateBar();
    }

    void UpdateUltimateRuntime()
    {
        // The shared Bow Master ultimate bar must be controlled ONLY by Bow Master.
        // Victoria/Crossbowman also run Hero.Update(), and previously they could find
        // the same UI object and immediately hide it again, causing a one-frame flash.
        if (!IsBowMaster()) return;

        // Cooldown only matters during Battle. It will be reset by WaveSpawner
        // at the beginning of every new wave.
        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.currentState == GameStateManager.GameState.Battle &&
            ultimateCooldownRemaining > 0f)
            ultimateCooldownRemaining = Mathf.Max(0f, ultimateCooldownRemaining - Time.deltaTime);

        EnsureUltimateBar();
        UpdateUltimateBar();
    }

    bool IsBowMaster()
    {
        return string.Equals(heroName?.Trim(), "Bow Master", System.StringComparison.OrdinalIgnoreCase);
    }

    void EnsureUltimateBar()
    {
        if (!IsBowMaster()) return;
        if (ultimateBarRoot != null && ultimateBarFill != null) return;

        // Use the manually-created UI. Support both the old and new names so
        // renaming the objects can never break the ultimate bar again.
        Transform background = FindSceneTransform("BowMasterUltimateBarBG");
        if (background == null)
            background = FindSceneTransform("HeroUltimateBarBackground");
        if (background == null) return;

        Transform fill = null;
        foreach (Transform child in background.GetComponentsInChildren<Transform>(true))
        {
            if (child == null) continue;
            if (child.name == "BowMasterUltimateBarFill" || child.name == "HeroUltimateBarFill")
            {
                fill = child;
                break;
            }
        }
        if (fill == null) return;

        Image fillImage = fill.GetComponent<Image>();
        if (fillImage == null) return;

        ultimateBarRoot = background.gameObject;
        ultimateBarRect = background as RectTransform;
        ultimateBarFill = fillImage;
        ultimateBarCanvas = background.GetComponentInParent<Canvas>(true);

        // Keep the position/size exactly as configured in the Unity Inspector.
        // Code controls only visibility and cooldown fill.
        ultimateBarFill.type = Image.Type.Filled;
        ultimateBarFill.fillMethod = Image.FillMethod.Horizontal;
        ultimateBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        ultimateBarFill.fillClockwise = true;
        ultimateBarFill.raycastTarget = false;

        Image bgImage = background.GetComponent<Image>();
        if (bgImage != null) bgImage.raycastTarget = false;
    }

    Transform FindSceneTransform(string objectName)
    {
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t != null && t.name == objectName && t.gameObject.scene.IsValid())
                return t;
        }
        return null;
    }

    void UpdateUltimateBar()
    {
        if (!IsBowMaster()) return;
        EnsureUltimateBar();
        if (ultimateBarRoot == null || ultimateBarFill == null) return;

        bool inBattle = GameStateManager.Instance != null &&
            (GameStateManager.Instance.currentState == GameStateManager.GameState.Battle ||
             GameStateManager.Instance.currentState == GameStateManager.GameState.Paused);

        bool show = inBattle && evolution1Purchased && ultimateUnlocked && isPurchased && isInstalled;
        if (ultimateBarRoot.activeSelf != show)
            ultimateBarRoot.SetActive(show);
        if (!show) return;

        // Ready = 100%. On activation cooldownRemaining becomes 10 sec, so the
        // bar immediately drops to 0 and then fills back to 1 during cooldown.
        float progress = ultimateCooldown <= 0f
            ? 1f
            : 1f - Mathf.Clamp01(ultimateCooldownRemaining / ultimateCooldown);
        ultimateBarFill.fillAmount = progress;
    }

}
