using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CastleUpgradePanelController : MonoBehaviour
{
    public static bool IsModalOpen { get; private set; }
    private static CastleUpgradePanelController activePanel;

    public Castle castle;
    public TMP_Text titleText;
    public TMP_Text levelText;
    public TMP_Text costText;
    public Button upgradeButton;
    public TMP_Text upgradeButtonText;

    private Button backButton;
    private GameObject modalBlocker;
    private RectTransform panelRect;
    private Coroutine pulse;

    void Awake()
    {
        activePanel = this;
        panelRect = transform as RectTransform;
        AutoWire();
        EnsureBlocker();
    }

    void Start()
    {
        HookButtons();
        SetModal(false);
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        IsModalOpen = false;
        if (modalBlocker != null) modalBlocker.SetActive(false);
    }

    public void Open(Castle source)
    {
        bool switching = gameObject.activeSelf;
        castle = source;
        HeroPanelController.CloseAnyOpenPanel();
        CityArcherPanelController.CloseAnyOpenPanel();
        gameObject.SetActive(true);
        AutoWire();
        HookButtons();
        EnsureBlocker();
        SetModal(true);
        Refresh();
        PlayPulse(switching);
    }

    public void ClosePanel()
    {
        SetModal(false);
        gameObject.SetActive(false);
    }

    public void Refresh()
    {
        if (castle == null) return;
        if (titleText != null) titleText.text = "Замок";
        if (levelText != null) levelText.text = "Уровень: " + castle.castleLevel;
        if (costText != null) costText.text = castle.UpgradeCost.ToString();
        if (upgradeButton != null)
            upgradeButton.interactable = PlayerProgress.Instance != null && PlayerProgress.Instance.gold >= castle.UpgradeCost;
        if (upgradeButtonText != null) upgradeButtonText.text = "Улучшить";
    }

    void Upgrade()
    {
        if (castle == null) return;
        castle.TryUpgradeCastle();
        Refresh();
    }

    void AutoWire()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text t in texts)
        {
            if (t.name == "CastleNameText" || t.name == "SquadNameText") titleText = t;
            else if (t.name == "CastleLevelText" || t.name == "SquadLevelText") levelText = t;
            else if (t.name == "UpgradeCostText") costText = t;
        }
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button b in buttons)
        {
            if (b.name == "UpgradeButton") upgradeButton = b;
            else if (b.name == "BackButton") backButton = b;
        }
        if (upgradeButton != null)
            upgradeButtonText = upgradeButton.GetComponentInChildren<TMP_Text>(true);
    }

    void HookButtons()
    {
        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(Upgrade);
            upgradeButton.onClick.AddListener(Upgrade);
            HoldToRepeatButton.Ensure(upgradeButton);
        }
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(ClosePanel);
            backButton.onClick.AddListener(ClosePanel);
        }
    }

    void EnsureBlocker()
    {
        if (modalBlocker != null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Transform existing = canvas.transform.Find("CastleUpgradeModalBlocker");
        if (existing != null) modalBlocker = existing.gameObject;
        else
        {
            modalBlocker = new GameObject("CastleUpgradeModalBlocker", typeof(RectTransform), typeof(Image));
            modalBlocker.transform.SetParent(canvas.transform, false);
            RectTransform r = modalBlocker.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            Image img = modalBlocker.GetComponent<Image>();
            img.color = new Color(0,0,0,0.12f); img.raycastTarget = true;
        }
        modalBlocker.transform.SetSiblingIndex(Mathf.Max(0, transform.GetSiblingIndex()));
        transform.SetAsLastSibling();
        modalBlocker.SetActive(false);
    }

    void SetModal(bool open)
    {
        IsModalOpen = open;
        if (modalBlocker != null) modalBlocker.SetActive(open);
        if (open) transform.SetAsLastSibling();
    }

    void PlayPulse(bool switching)
    {
        if (panelRect == null) return;
        if (pulse != null) StopCoroutine(pulse);
        pulse = StartCoroutine(Pulse(switching));
    }

    IEnumerator Pulse(bool switching)
    {
        Vector3 baseScale = Vector3.one;
        panelRect.localScale = baseScale * (switching ? 0.97f : 0.94f);
        float time = 0f, duration = 0.10f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            panelRect.localScale = Vector3.Lerp(panelRect.localScale, baseScale, time / duration);
            yield return null;
        }
        panelRect.localScale = baseScale;
        pulse = null;
    }

    public static void CloseAnyOpenPanel()
    {
        if (activePanel != null && activePanel.gameObject.activeSelf)
            activePanel.ClosePanel();
    }
}
