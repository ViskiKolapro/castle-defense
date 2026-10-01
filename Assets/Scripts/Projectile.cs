using System.Collections;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Flight")]
    public float speed = 8f;
    [Tooltip("Общий множитель скорости полёта стрел. 1.4 = на 40% быстрее.")]
    public float flightSpeedMultiplier = 1.68f;
    public float arcHeight = 0.35f;
    public float hitDistance = 0.15f;

    [Header("Rendering")]
    [Tooltip("Стрела должна рисоваться поверх героев.")]
    public int sortingOrder = 30;

    [Header("Missed arrow")]
    public float stuckLifetime = 2f;
    public float fadeDuration = 1f;

    private Enemy target;
    private float damage;
    private bool launched;
    private bool finished;
    private Vector3 lastKnownTargetPosition;
    private Vector3 previousPosition;
    private float travelled;
    private float initialDistance;
    private SpriteRenderer spriteRenderer;

    public void SetTarget(Enemy newTarget, float newDamage, float newSpeed)
    {
        target = newTarget;
        damage = newDamage;
        speed = newSpeed * Mathf.Max(0.01f, flightSpeedMultiplier);
        launched = true;
        previousPosition = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = sortingOrder;

        if (target != null)
        {
            lastKnownTargetPosition = target.transform.position;
            initialDistance = Mathf.Max(0.01f, Vector2.Distance(transform.position, lastKnownTargetPosition));
        }
    }

    void Update()
    {
        if (!launched || finished)
            return;

        bool targetAlive = target != null && !target.IsDead;
        if (targetAlive)
            lastKnownTargetPosition = target.transform.position;

        Vector3 before = transform.position;
        Vector3 flatNext = Vector3.MoveTowards(
            transform.position,
            lastKnownTargetPosition,
            speed * Time.deltaTime
        );

        travelled += Vector2.Distance(before, flatNext);
        float t = Mathf.Clamp01(travelled / initialDistance);
        float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;

        transform.position = new Vector3(flatNext.x, flatNext.y + arc * Time.deltaTime * 4f, flatNext.z);
        RotateAlongFlight();

        if (Vector2.Distance(transform.position, lastKnownTargetPosition) <= hitDistance)
        {
            if (targetAlive)
            {
                target.ReleaseIncomingDamage(damage);
                target.TakePreparedDamage(damage);
                finished = true;
                Destroy(gameObject);
            }
            else
            {
                StickInGround();
            }
        }
    }

    void RotateAlongFlight()
    {
        Vector3 direction = transform.position - previousPosition;
        previousPosition = transform.position;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void StickInGround()
    {
        if (finished)
            return;

        finished = true;
        StartCoroutine(FadeAndDestroy());
    }

    IEnumerator FadeAndDestroy()
    {
        float visibleTime = Mathf.Max(0f, stuckLifetime - fadeDuration);
        if (visibleTime > 0f)
            yield return new WaitForSeconds(visibleTime);

        if (spriteRenderer == null || fadeDuration <= 0f)
        {
            Destroy(gameObject);
            yield break;
        }

        Color startColor = spriteRenderer.color;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / fadeDuration);
            Color c = startColor;
            c.a = alpha;
            spriteRenderer.color = c;
            yield return null;
        }

        Destroy(gameObject);
    }
}
