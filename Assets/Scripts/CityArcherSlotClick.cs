using System.Collections;
using UnityEngine;

public class CityArcherSlotClick : MonoBehaviour
{
    public CityArcherManager manager;
    public int squadIndex;

    private Vector3 baseScale;
    private Coroutine clickAnimation;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    void OnEnable()
    {
        baseScale = transform.localScale;
    }

    void OnMouseDown()
    {
        if (BowMasterEvolutionController.IsEvolutionModalOpen) return;
        // Эти два квадрата должны оставаться кликабельными даже когда открыто
        // меню городских лучников, чтобы можно было быстро переключать отряды.
        if (manager == null)
            return;

        if (clickAnimation != null)
            StopCoroutine(clickAnimation);
        clickAnimation = StartCoroutine(ClickPulse());

        manager.OpenSquad(squadIndex);
    }

    IEnumerator ClickPulse()
    {
        Vector3 normal = baseScale;
        Vector3 pressed = normal * 0.86f;

        float t = 0f;
        const float downTime = 0.07f;
        while (t < downTime)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(normal, pressed, Mathf.Clamp01(t / downTime));
            yield return null;
        }

        t = 0f;
        const float upTime = 0.10f;
        while (t < upTime)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(pressed, normal, Mathf.Clamp01(t / upTime));
            yield return null;
        }

        transform.localScale = normal;
        clickAnimation = null;
    }
}
