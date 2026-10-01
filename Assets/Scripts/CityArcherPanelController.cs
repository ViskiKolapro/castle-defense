using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CityArcherPanelController : MonoBehaviour
{
    public static bool IsModalOpen { get; private set; }
    private static CityArcherPanelController activePanel;

    public CityArcherManager manager;
    public TMP_Text titleText;
    public TMP_Text levelText;
    public TMP_Text countText;
    public TMP_Text damageText;
    public TMP_Text attackSpeedText;
    public TMP_Text costText;
    public Button actionButton;
    public TMP_Text actionButtonText;

    private int selectedSquad = -1;
    private GameObject modalBlocker;
    private Button backButton;
    private RectTransform panelRect;
    private Coroutine openAnimation;

    void Awake()
    {
        activePanel = this;
        panelRect = transform as RectTransform;
        EnsureModalBlocker();
        FindAndHookBackButton();
        AutoWireUIByName();
    }

    void Start()
    {
        if (actionButton != null)
        {
            actionButton.onClick.RemoveListener(BuyOrUpgrade);
            actionButton.onClick.AddListener(BuyOrUpgrade);
            HoldToRepeatButton.Ensure(actionButton);
        }

        SetModalState(false);
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        IsModalOpen = false;
        if (modalBlocker != null)
            modalBlocker.SetActive(false);
    }

    public void Open(int squadIndex)
    {
        bool wasAlreadyOpen = gameObject.activeSelf;
        selectedSquad = squadIndex;
        gameObject.SetActive(true);
        HeroPanelController.CloseAnyOpenPanel();
        CastleUpgradePanelController.CloseAnyOpenPanel();
        EnsureModalBlocker();
        FindAndHookBackButton();
        AutoWireUIByName();
        SetModalState(true);
        Refresh();

        if (openAnimation != null)
            StopCoroutine(openAnimation);
        openAnimation = StartCoroutine(PanelPulse(wasAlreadyOpen));
    }

    public void ClosePanel()
    {
        selectedSquad = -1;
        SetModalState(false);
        gameObject.SetActive(false);
    }

    public void Refresh()
    {
        if (manager == null || selectedSquad < 0) return;
        CityArcherSquad squad = manager.GetSquad(selectedSquad);
        if (squad == null) return;

        if (titleText != null) titleText.text = "Городские лучники\nОтряд " + (selectedSquad + 1);
        if (levelText != null) levelText.text = squad.purchased ? "Уровень: " + squad.level : "Не куплен";
        if (countText != null) countText.text = "Лучники: " + squad.archerCount + "/10";
        if (damageText != null) damageText.text = "Урон одного: " + Mathf.RoundToInt(squad.DamagePerArcher);
        if (attackSpeedText != null)
        {
            attackSpeedText.text = "Скорость атаки: " + squad.AttackCooldown.ToString("0.##");
        }
        if (costText != null) costText.text = squad.CurrentCost.ToString();
        if (actionButtonText != null) actionButtonText.text = squad.purchased ? "Улучшить" : "Купить";
        if (actionButton != null)
            actionButton.interactable = manager.IsUnlocked && PlayerProgress.Instance != null && PlayerProgress.Instance.gold >= squad.CurrentCost;

        // Кнопка "Снять" пока только отображает будущую возможность:
        // нет лучников -> скрыта; есть лучники -> видна, но не нажимается.
        foreach (Button b in GetComponentsInChildren<Button>(true))
        {
            TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text.Trim().Equals("Снять", System.StringComparison.OrdinalIgnoreCase))
            {
                b.gameObject.SetActive(squad.archerCount > 0);
                b.interactable = false;
            }
        }
    }

    void BuyOrUpgrade()
    {
        if (manager == null || selectedSquad < 0) return;
        manager.BuyOrUpgradeSquad(selectedSquad);
        Refresh();
    }

    void FindAndHookBackButton()
    {
        if (backButton != null)
            return;

        Transform back = transform.Find("BackButton");
        if (back == null)
            return;

        backButton = back.GetComponent<Button>();
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(ClosePanel);
            backButton.onClick.AddListener(ClosePanel);
        }
    }

    void EnsureModalBlocker()
    {
        if (modalBlocker != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find("CityArcherModalBlocker");
        if (existing != null)
        {
            modalBlocker = existing.gameObject;
        }
        else
        {
            modalBlocker = new GameObject("CityArcherModalBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = modalBlocker.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = modalBlocker.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.12f);
            image.raycastTarget = true;
        }

        int panelIndex = transform.GetSiblingIndex();
        modalBlocker.transform.SetSiblingIndex(panelIndex);
        transform.SetAsLastSibling();
        modalBlocker.SetActive(false);
    }

    void SetModalState(bool open)
    {
        IsModalOpen = open;
        if (modalBlocker != null)
            modalBlocker.SetActive(open);
    }

    IEnumerator PanelPulse(bool switching)
    {
        if (panelRect == null)
            yield break;

        Vector3 normal = Vector3.one;
        Vector3 start = switching ? new Vector3(0.97f, 0.97f, 1f) : new Vector3(0.92f, 0.92f, 1f);
        panelRect.localScale = start;

        float elapsed = 0f;
        float duration = switching ? 0.10f : 0.16f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            panelRect.localScale = Vector3.LerpUnclamped(start, normal, t);
            yield return null;
        }

        panelRect.localScale = normal;
        openAnimation = null;
    }

    public static void CloseAnyOpenPanel()
    {
        if (activePanel != null && activePanel.gameObject.activeSelf)
            activePanel.ClosePanel();
    }

    void AutoWireUIByName()
    {
        TMP_Text FindText(string n)
        {
            Transform t = transform.Find(n);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        Button FindButton(string n)
        {
            Transform t = transform.Find(n);
            return t != null ? t.GetComponent<Button>() : null;
        }

        // Эти имена совпадают с твоей текущей CityArcherPanel.
        titleText = FindText("SquadNameText") ?? titleText;
        levelText = FindText("SquadLevelText") ?? levelText;
        countText = FindText("ArcherCountText") ?? countText;
        damageText = FindText("ArcherDamageText") ?? damageText;
        attackSpeedText = FindText("AttackSpeedText") ?? attackSpeedText;
        costText = FindText("UpgradeCostText") ?? costText;

        Button upgrade = FindButton("UpgradeButton");
        if (upgrade != null) actionButton = upgrade;
        if (actionButton != null)
        {
            TMP_Text childText = actionButton.GetComponentInChildren<TMP_Text>(true);
            if (childText != null) actionButtonText = childText;
            actionButton.onClick.RemoveListener(BuyOrUpgrade);
            actionButton.onClick.AddListener(BuyOrUpgrade);
            HoldToRepeatButton.Ensure(actionButton);
        }
    }

}
