using System.Collections;
using UnityEngine;

public class EmptyHeroSlotClick : MonoBehaviour
{
    public HeroSlotManager slotManager;
    public HeroPanelController heroPanel;
    public int slotIndex;
    private Coroutine clickAnimation;
    private Vector3 baseScale;

    void Awake() { baseScale = transform.localScale; }
    void OnEnable() { baseScale = transform.localScale; }

    void OnMouseDown()
    {
        if (BowMasterEvolutionController.IsEvolutionModalOpen) return;
        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.currentState == GameStateManager.GameState.Battle)
        {
            if (slotManager == null) return;
            Hero installedHero = slotManager.GetInstalledHeroInSlot(slotIndex);
            if (installedHero != null && installedHero.TryActivateUltimate())
            {
                if (clickAnimation != null) StopCoroutine(clickAnimation);
                clickAnimation = StartCoroutine(ClickPulse());
            }
            return;
        }

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.currentState != GameStateManager.GameState.Menu)
            return;

        if (slotManager == null || heroPanel == null)
            return;

        if (!slotManager.CanInstallToSlot(slotIndex))
            return;

        // Пустой слот открывает только список героев.
        // Если было открыто меню лучников, сразу переключаемся на героев.
        CityArcherPanelController.CloseAnyOpenPanel();
        if (clickAnimation != null) StopCoroutine(clickAnimation);
        clickAnimation = StartCoroutine(ClickPulse());
        heroPanel.OpenForEmptySlot(slotIndex);
    }

    IEnumerator ClickPulse()
    {
        Vector3 normal = baseScale;
        Vector3 pressed = normal * 0.86f;
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
