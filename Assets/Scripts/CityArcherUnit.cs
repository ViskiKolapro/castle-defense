using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class CityArcherUnit : MonoBehaviour
{
    [Header("Runtime")]
    public CityArcherSquad squad;
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Attack animation")]
    [Tooltip("Обычное спокойное состояние лучника.")]
    public Sprite readySprite;
    [Tooltip("Лучник с натянутым луком и заряженной стрелой.")]
    public Sprite aimSprite;
    [Tooltip("Сколько держать натянутый лук перед появлением отдельной стрелы.")]
    public float drawTime = 0.25f;
    [Tooltip("Небольшая пауза после выстрела перед возвратом в Ready.")]
    public float releaseTime = 0.08f;

    private float attackTimer;
    private bool isAttacking;
    private SpriteRenderer spriteRenderer;

    void Awake() => ApplyReadySprite();
    void OnEnable() => ApplyReadySprite();
    void OnValidate() => ApplyReadySprite();

    void ApplyReadySprite()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null && readySprite != null && (!Application.isPlaying || !isAttacking))
            spriteRenderer.sprite = readySprite;
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            ApplyReadySprite();
            return;
        }

        // Таймер должен идти и во время анимации. Иначе draw/release
        // прибавлялись сверху к AttackCooldown и фактическая стрельба была медленнее.
        attackTimer -= Time.deltaTime;

        if (squad == null || !squad.IsActiveInBattle || isAttacking)
            return;
        if (attackTimer > 0f)
            return;

        Enemy target = FindRandomTarget();
        if (target == null)
            return;

        StartCoroutine(AttackRoutine(target));
        // AttackCooldown — полный интервал между выстрелами одного лучника.
        attackTimer = Mathf.Max(0.01f, squad.AttackCooldown);
    }

    IEnumerator AttackRoutine(Enemy target)
    {
        isAttacking = true;

        if (spriteRenderer != null && aimSprite != null)
            spriteRenderer.sprite = aimSprite;

        // Анимация укладывается внутрь cooldown и не снижает фактическую скорострельность.
        float totalCadence = Mathf.Max(0.01f, squad != null ? squad.AttackCooldown : 1f);
        float effectiveDrawTime = Mathf.Min(Mathf.Max(0f, drawTime), totalCadence * 0.60f);
        float remainingForRelease = Mathf.Max(0f, totalCadence - effectiveDrawTime);
        float effectiveReleaseTime = Mathf.Min(Mathf.Max(0f, releaseTime), remainingForRelease);

        if (effectiveDrawTime > 0f)
            yield return new WaitForSeconds(effectiveDrawTime);

        if (target == null || target.IsDead || target.IsExpectedDead)
            target = FindRandomTarget();

        if (target != null && !target.IsDead)
            Shoot(target);

        if (effectiveReleaseTime > 0f)
            yield return new WaitForSeconds(effectiveReleaseTime);

        if (spriteRenderer != null && readySprite != null)
            spriteRenderer.sprite = readySprite;

        isAttacking = false;
    }

    Enemy FindRandomTarget()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        List<Enemy> alive = new List<Enemy>();

        foreach (Enemy enemy in enemies)
            if (enemy != null && !enemy.IsDead && !enemy.IsExpectedDead)
                alive.Add(enemy);

        if (alive.Count == 0)
        {
            foreach (Enemy enemy in enemies)
                if (enemy != null && !enemy.IsDead)
                    alive.Add(enemy);
        }

        return alive.Count == 0 ? null : alive[Random.Range(0, alive.Count)];
    }

    void Shoot(Enemy target)
    {
        if (target == null || projectilePrefab == null)
            return;

        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
        float damage = squad.DamagePerArcher;
        if (Random.value < Mathf.Clamp01(squad.critChance))
            damage *= Mathf.Max(1f, squad.critMultiplier);

        target.ReserveIncomingDamage(damage);

        GameObject arrow = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        Projectile projectile = arrow.GetComponent<Projectile>();
        if (projectile != null)
            projectile.SetTarget(target, damage, squad.ProjectileSpeed);
        else
            target.ReleaseIncomingDamage(damage);
    }
}
