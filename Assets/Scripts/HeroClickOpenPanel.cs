using System.Collections;
using UnityEngine;

public class HeroClickOpenPanel : MonoBehaviour
{
    public HeroPanelController heroPanel;
    private Hero hero;
    private Coroutine clickAnimation;
    private Vector3 baseScale;

    void Awake()
    {
        hero = GetComponent<Hero>();
        baseScale = transform.localScale;

        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();

            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
                col.size = sr.sprite.bounds.size;
        }
    }

    void OnMouseDown()
    {
        if (WorldInputGuard.IsBlocked()) return;
        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.currentState == GameStateManager.GameState.Battle)
        {
            if (hero != null && hero.TryActivateUltimate())
            {
                if (clickAnimation != null) StopCoroutine(clickAnimation);
                clickAnimation = StartCoroutine(ClickPulse());
            }
            return;
        }

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.currentState != GameStateManager.GameState.Menu)
            return;

        if (heroPanel != null && hero != null)
        {
            CityArcherPanelController.CloseAnyOpenPanel();
            if (clickAnimation != null) StopCoroutine(clickAnimation);
            clickAnimation = StartCoroutine(ClickPulse());
            heroPanel.OpenForHero(hero);
        }
    }

    IEnumerator ClickPulse()
    {
        Vector3 normal = baseScale;
        Vector3 pressed = normal * 0.90f;
        float t = 0f;
        while (t < 0.07f)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(normal, pressed, Mathf.Clamp01(t / 0.07f));
            yield return null;
        }
        t = 0f;
        while (t < 0.10f)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(pressed, normal, Mathf.Clamp01(t / 0.10f));
            yield return null;
        }
        transform.localScale = normal;
        clickAnimation = null;
    }
}
