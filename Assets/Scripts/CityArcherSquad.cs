using UnityEngine;

[System.Serializable]
public class CityArcherSquad
{
    public string squadName = "Городские лучники";

    [Header("Progress")]
    public bool purchased = false;
    [Min(0)] public int level = 0;
    [Range(0, 10)] public int archerCount = 0;

    [Header("Base stats of ONE archer")]
    public float baseDamage = 25f;
    [Tooltip("Полный интервал между выстрелами одного городского лучника.")]
    public float attackInterval = 0.8f;
    public float projectileSpeed = 8f;

    [Header("Critical hit of EACH archer")]
    [Range(0f, 1f)] public float critChance = 0.01f;
    [Min(1f)] public float critMultiplier = 2f;

    [Header("Upgrade - balance in Inspector")]
    public float damagePerLevelAfterTenArchers = 10f;
    [Tooltip("Цена первого лучника.")] public int firstArcherCost = 10;
    [Tooltip("На сколько растёт цена каждого следующего лучника до 10/10.")] public int archerCostStep = 10;
    [Tooltip("Цена первого улучшения после 10/10.")] public int postTenStartCost = 150;
    [Tooltip("Шаг цены после 10/10 до lateCostStartsAfterLevel.")] public int postTenCostStep = 50;
    [Tooltip("После этого уровня используется lateCostStep.")] public int lateCostStartsAfterLevel = 50;
    [Tooltip("Шаг цены после lateCostStartsAfterLevel.")] public int lateCostStep = 100;

    [System.NonSerialized] public bool IsActiveInBattle;

    public float DamagePerArcher
    {
        get
        {
            int damageLevels = Mathf.Max(0, level - 1);
            return baseDamage + damageLevels * damagePerLevelAfterTenArchers;
        }
    }

    public float AttackCooldown => Mathf.Max(0.01f, attackInterval);
    public float ProjectileSpeed => projectileSpeed;

    public int CurrentCost
    {
        get
        {
            if (!purchased)
                return Mathf.Max(0, firstArcherCost);

            if (level < 10)
                return Mathf.Max(0, firstArcherCost + level * archerCostStep);

            int threshold = Mathf.Max(11, lateCostStartsAfterLevel);
            if (level < threshold)
                return Mathf.Max(0, postTenStartCost + (level - 10) * postTenCostStep);

            int costAtThreshold = postTenStartCost + (threshold - 10) * postTenCostStep;
            return Mathf.Max(0, costAtThreshold + (level - threshold) * lateCostStep);
        }
    }

    public bool TryBuyOrUpgrade()
    {
        if (PlayerProgress.Instance == null || !PlayerProgress.Instance.SpendGold(CurrentCost))
            return false;

        if (!purchased)
        {
            purchased = true;
            level = 1;
            archerCount = 1;
            return true;
        }

        level++;
        if (archerCount < 10)
            archerCount++;

        return true;
    }
}
