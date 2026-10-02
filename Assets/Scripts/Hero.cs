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

    [Header("Upgrade 1-100")]
    public int heroLevel = 1;
    public int maxHeroLevel = 100;
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
            return firstUpgradeCost + (heroLevel - 1) * upgradeCostIncrease;
        }
    }

    public bool IsMaxLevel => heroLevel >= maxHeroLevel;

    // Ultimate modifiers are runtime-only. They never overwrite the permanent
    // damage/attackCooldown values shown in the hero card.
    public float EffectiveAttackCooldown => ultimateActive
        ? attackCooldown / (1f + Mathf.Max(0f, ultimateAttackSpeedBonus))
        : attackCooldown;

    public float EffectiveDamage => ultimateActive
        ? damage * (1f + Mathf.Max(0f, ultimateDamageBonus))
        : damage;

    void Awake()
    {
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
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
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
        shotDamage = target.PrepareIncomingDamage(shotDamage);
        target.ReserveIncomingDamage(shotDamage);

        GameObject arrow = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Projectile projectile = arrow.GetComponent<Projectile>();

        if (projectile != null)
            projectile.SetTarget(target, shotDamage, projectileSpeed);
        else
            target.ReleaseIncomingDamage(shotDamage);
    }

    float RollAttackDamage()
    {
        if (combatType == CombatUnitType.Siege)
            return EffectiveDamage;

        float baseDamage = EffectiveDamage;
        bool critical = Random.value < Mathf.Clamp01(critChance);
        return critical ? baseDamage * Mathf.Max(1f, critMultiplier) : baseDamage;
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
        // Cooldown only matters during Battle. It will be reset by WaveSpawner
        // at the beginning of every new wave.
        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.currentState == GameStateManager.GameState.Battle &&
            ultimateCooldownRemaining > 0f)
            ultimateCooldownRemaining = Mathf.Max(0f, ultimateCooldownRemaining - Time.deltaTime);

        if (evolution1Purchased && ultimateUnlocked && isInstalled)
        {
            EnsureUltimateBar();
            UpdateUltimateBar();
        }
        else if (ultimateBarRoot != null)
        {
            ultimateBarRoot.SetActive(false);
        }
    }

    void EnsureUltimateBar()
    {
        if (ultimateBarRoot != null && ultimateBarFill != null) return;

        // Uses the UI bar created manually on the Canvas:
        // HeroUltimateBarBackground -> HeroUltimateBarFill.
        Transform background = FindSceneTransform("HeroUltimateBarBackground");
        Transform fill = FindSceneTransform("HeroUltimateBarFill");

        // If the manually-created bar is missing/broken, build a small runtime bar
        // on the existing screen-space Canvas. This makes the ultimate indicator
        // reliable and does not touch the user's saved UI layout.
        if (background == null || fill == null || fill.GetComponent<Image>() == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject bgGO = new GameObject("HeroUltimateBarBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGO.transform.SetParent(canvas.transform, false);
            RectTransform bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.sizeDelta = new Vector2(70f, 7f);
            Image bgImage = bgGO.GetComponent<Image>();
            bgImage.color = new Color(0.10f, 0.10f, 0.10f, 0.85f);
            bgImage.raycastTarget = false;

            GameObject fillGO = new GameObject("HeroUltimateBarFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGO.transform.SetParent(bgGO.transform, false);
            RectTransform fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0f, 0f);
            fillRT.anchorMax = new Vector2(1f, 1f);
            fillRT.pivot = new Vector2(0f, 0.5f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            Image fillImage = fillGO.GetComponent<Image>();
            fillImage.color = new Color(0.15f, 1f, 0.20f, 1f);
            fillImage.raycastTarget = false;

            background = bgGO.transform;
            fill = fillGO.transform;
        }

        ultimateBarRoot = background.gameObject;
        ultimateBarRect = background as RectTransform;
        ultimateBarFill = fill.GetComponent<Image>();
        ultimateBarCanvas = background.GetComponentInParent<Canvas>();

        // Recalculate from a known offset every time the bar is bound. The bar
        // sits just above the installed hero and follows him if the slot changes.
        ultimateBarOffsetCaptured = false;
        CaptureUltimateBarOffset();
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

    void CaptureUltimateBarOffset()
    {
        if (ultimateBarRect == null || ultimateBarCanvas == null || ultimateBarRect.parent == null) return;

        RectTransform parentRect = ultimateBarRect.parent as RectTransform;
        if (parentRect == null) return;

        Camera uiCamera = ultimateBarCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : ultimateBarCanvas.worldCamera;
        Camera worldCamera = Camera.main;
        if (worldCamera == null) return;

        Vector2 screenPoint = worldCamera.WorldToScreenPoint(transform.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 heroLocalPoint))
        {
            // Fixed screen-space offset: directly above the hero. This avoids the
            // bar disappearing because of an old/manual anchored position.
            ultimateBarScreenOffset = new Vector2(0f, 45f);
            ultimateBarRect.anchoredPosition = heroLocalPoint + ultimateBarScreenOffset;
            ultimateBarOffsetCaptured = true;
        }
    }

    void FollowHeroWithUltimateBar()
    {
        if (ultimateBarRect == null || ultimateBarCanvas == null || ultimateBarRect.parent == null) return;
        if (!ultimateBarOffsetCaptured) CaptureUltimateBarOffset();
        if (!ultimateBarOffsetCaptured) return;

        RectTransform parentRect = ultimateBarRect.parent as RectTransform;
        Camera uiCamera = ultimateBarCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : ultimateBarCanvas.worldCamera;
        Camera worldCamera = Camera.main;
        if (parentRect == null || worldCamera == null) return;

        Vector2 screenPoint = worldCamera.WorldToScreenPoint(transform.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 heroLocalPoint))
            ultimateBarRect.anchoredPosition = heroLocalPoint + ultimateBarScreenOffset;
    }

    void UpdateUltimateBar()
    {
        if (ultimateBarRoot == null || ultimateBarFill == null) return;

        bool inBattle = GameStateManager.Instance != null &&
            (GameStateManager.Instance.currentState == GameStateManager.GameState.Battle ||
             GameStateManager.Instance.currentState == GameStateManager.GameState.Paused);

        bool show = inBattle && evolution1Purchased && ultimateUnlocked && isPurchased && isInstalled;
        ultimateBarRoot.SetActive(show);
        if (!show) return;

        FollowHeroWithUltimateBar();

        // Ready = full green bar. Pressing the ultimate resets it to zero,
        // then it fills from 0 to 1 during the complete 10 second cooldown.
        float progress = ultimateCooldown <= 0f
            ? 1f
            : 1f - Mathf.Clamp01(ultimateCooldownRemaining / ultimateCooldown);
        ultimateBarFill.fillAmount = progress;
        RectTransform fillRect = ultimateBarFill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(progress, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }


}
