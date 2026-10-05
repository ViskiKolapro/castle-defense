using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroPanelController : MonoBehaviour
{
    public static bool IsModalOpen { get; private set; }
    private static HeroPanelController activePanel;
    [Header("Hero")]
    public Hero bowMaster;
    public Hero victoria;
    public Hero crossbowman;
    public HeroSlotManager heroSlotManager;

    [Header("Hero List")]
    [Tooltip("Правая панель со списком героев. Можно подключить позже.")]
    public GameObject heroListPanel;

    [Header("Texts")]
    public TMP_Text heroNameText;
    public TMP_Text heroLevelText;
    public TMP_Text heroDamageText;
    public TMP_Text upgradeCostText;

    [Header("Buttons")]
    public Button installButton;
    public TMP_Text installButtonText;
    public Button upgradeButton;
    public TMP_Text upgradeButtonText;
    [Tooltip("Показывается для героев, у которых уже есть дерево эволюций.")]
    public Button evolutionButton;

    private Hero selectedHero;
    private int selectedEmptySlot = 0;
    private Image panelBackground;
    private GameObject modalBlocker;
    private RectTransform panelRect;
    private Coroutine panelAnimation;

    void Awake()
    {
        // EvolutionPanel был создан дубликатом HeroPanel и мог сохранить этот компонент.
        // Только настоящий HeroPanel имеет право становиться activePanel и управлять меню героев.
        if (gameObject.name != "HeroPanel")
        {
            enabled = false;
            return;
        }

        activePanel = this;
        panelRect = transform as RectTransform;
        panelBackground = GetComponent<Image>();
        EnsureModalBlocker();
        FindAndHookEvolutionButton();
    }

    void Start()
    {
        panelBackground = GetComponent<Image>();
        FindAndHookEvolutionButton();

        if (installButton != null)
            installButton.onClick.AddListener(InstallOrRemoveSelectedHero);

        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(UpgradeSelectedHero);
            HoldToRepeatButton.Ensure(upgradeButton);
        }

        selectedHero = bowMaster;
        Refresh();

        // При запуске все модальные панели должны быть в чистом состоянии.
        ClosePanel();
        BowMasterEvolutionController evo = BowMasterEvolutionController.EnsureFor(this);
        if (evo != null) evo.ResetPanelsForPlay();
        VictoriaEvolutionController victoriaEvo = VictoriaEvolutionController.EnsureFor(this);
        if (victoriaEvo != null) victoriaEvo.ResetPanelsForPlay();
    }

    // Открытие по клику на установленного героя: статистика + список героев справа.
    public void OpenForHero(Hero hero)
    {
        bool switching = gameObject.activeSelf;
        CityArcherPanelController.CloseAnyOpenPanel();
        CastleUpgradePanelController.CloseAnyOpenPanel();
        selectedHero = hero != null ? hero : bowMaster;
        selectedEmptySlot = 0;

        gameObject.SetActive(true);
        SetPanelBackgroundVisible(true);
        SetHeroListVisible(true);
        SetStatsVisible(true);
        SetModalState(true);
        Refresh();
        PlayPanelPulse(switching);
    }

    // Совместимость со старым HeroClickOpenPanel.
    public void OpenPanel()
    {
        OpenForHero(bowMaster);
    }

    // Пустой слот: только список героев, без статистики Bow Master.
    public void OpenForEmptySlot(int slotIndex)
    {
        bool switching = gameObject.activeSelf;
        CityArcherPanelController.CloseAnyOpenPanel();
        CastleUpgradePanelController.CloseAnyOpenPanel();
        selectedEmptySlot = slotIndex;
        selectedHero = null;

        gameObject.SetActive(true);
        SetPanelBackgroundVisible(false);
        SetHeroListVisible(true);
        SetStatsVisible(false);
        SetModalState(true);
        PlayPanelPulse(switching);
    }

    // Эту функцию потом привяжем к кнопке Bow Master в правом списке.
    public void SelectBowMasterFromList()
    {
        SelectHeroFromList(bowMaster);
    }

    public void SelectVictoriaFromList()
    {
        SelectHeroFromList(victoria);
    }

    public void SelectCrossbowmanFromList()
    {
        SelectHeroFromList(crossbowman);
    }

    // Compatibility with the old TestHero button event.
    // It now selects Victoria, so an existing Unity OnClick will not break.
    public void SelectTestHeroFromList()
    {
        SelectHeroFromList(victoria);
    }

    public void SelectHeroFromList(Hero hero)
    {
        if (hero == null)
            return;

        selectedHero = hero;
        SetPanelBackgroundVisible(true);
        SetStatsVisible(true);
        Refresh();
    }

    public void ClosePanel()
    {
        selectedHero = null;
        selectedEmptySlot = 0;

        // HeroListPanel находится отдельно от HeroPanel в Canvas,
        // поэтому его нужно скрывать отдельно.
        SetHeroListVisible(false);
        SetModalState(false);
        gameObject.SetActive(false);
    }

    public void Refresh()
    {
        if (selectedHero == null)
            return;

        if (evolutionButton != null)
            evolutionButton.gameObject.SetActive(selectedHero == bowMaster || selectedHero == victoria);

        if (heroNameText != null)
            heroNameText.text = selectedHero.heroName;

        if (heroLevelText != null)
            heroLevelText.text = "Уровень: " + selectedHero.heroLevel;

        if (heroDamageText != null)
        {
            if (selectedHero == bowMaster && selectedHero.evolution1Purchased)
            {
                string speed = selectedHero.attackCooldown.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
                heroDamageText.text = "Урон: " + Mathf.RoundToInt(selectedHero.EffectiveDamage) +
                    "\nСкорость атаки: " + speed;
            }
            else
            {
                heroDamageText.text = "Урон: " + Mathf.RoundToInt(selectedHero.EffectiveDamage);
            }
        }

        if (upgradeCostText != null)
            upgradeCostText.text = selectedHero.IsMaxLevel
                ? "MAX"
                : selectedHero.CurrentUpgradeCost.ToString();

        if (!selectedHero.isPurchased)
        {
            if (installButtonText != null)
                installButtonText.text = "Купить: " + selectedHero.purchasePrice;

            if (installButton != null)
                installButton.interactable = PlayerProgress.Instance != null &&
                    PlayerProgress.Instance.gold >= selectedHero.purchasePrice;

            if (upgradeButton != null)
                upgradeButton.gameObject.SetActive(false);

            if (upgradeCostText != null)
                upgradeCostText.gameObject.SetActive(false);

            return;
        }

        if (upgradeButton != null)
            upgradeButton.gameObject.SetActive(true);

        if (upgradeCostText != null)
        {
            upgradeCostText.gameObject.SetActive(true);
            upgradeCostText.text = selectedHero.IsMaxLevel
                ? "MAX"
                : selectedHero.CurrentUpgradeCost.ToString();
        }

        if (installButtonText != null)
        {
            if (selectedEmptySlot > 0)
                installButtonText.text = "Установить";
            else if (selectedHero.isInstalled)
                installButtonText.text = "Снять";
            else
                installButtonText.text = "Выбери слот";
        }

        if (installButton != null)
        {
            installButton.interactable =
                (selectedEmptySlot > 0 && heroSlotManager != null &&
                 heroSlotManager.IsSlotUnlocked(selectedEmptySlot) &&
                 (!heroSlotManager.IsSlotOccupied(selectedEmptySlot) ||
                  selectedHero.installedSlot == selectedEmptySlot)) ||
                (selectedEmptySlot == 0 && selectedHero.isInstalled);
        }

        if (upgradeButtonText != null)
            upgradeButtonText.text = selectedHero.IsMaxLevel ? "MAX" : "Улучшить";

        if (upgradeButton != null)
            upgradeButton.interactable = !selectedHero.IsMaxLevel;
    }

    void UpgradeSelectedHero()
    {
        if (selectedHero == null)
            return;

        selectedHero.TryUpgrade();
        Refresh();
    }

    void InstallOrRemoveSelectedHero()
    {
        if (selectedHero == null)
            return;

        // Не куплен: эта же кнопка работает как "Купить".
        if (!selectedHero.isPurchased)
        {
            if (PlayerProgress.Instance == null)
                return;

            if (!PlayerProgress.Instance.SpendGold(selectedHero.purchasePrice))
                return;

            selectedHero.isPurchased = true;
            Refresh();
            return;
        }

        if (heroSlotManager == null)
            return;

        // Если меню открыто через пустой слот — "Установить" всегда означает
        // поставить/перенести выбранного героя именно в этот слот.
        if (selectedEmptySlot > 0)
        {
            if (heroSlotManager.InstallHero(selectedHero, selectedEmptySlot))
            {
                selectedEmptySlot = 0;
                Refresh();
            }
            return;
        }

        // Если открыли самого установленного героя на замке — можно снять.
        if (selectedHero.isInstalled)
        {
            // Запоминаем тот же слот: после снятия кнопка сразу становится "Установить"
            // и позволяет вернуть героя туда же без повторного клика по квадрату.
            int previousSlot = selectedHero.installedSlot;
            heroSlotManager.RemoveHero(selectedHero);
            selectedEmptySlot = previousSlot;
            Refresh();
        }
    }

    void SetStatsVisible(bool visible)
    {
        if (heroNameText != null) heroNameText.gameObject.SetActive(visible);
        if (heroLevelText != null) heroLevelText.gameObject.SetActive(visible);
        if (heroDamageText != null) heroDamageText.gameObject.SetActive(visible);
        if (upgradeCostText != null) upgradeCostText.gameObject.SetActive(visible);
        if (installButton != null) installButton.gameObject.SetActive(visible);
        if (upgradeButton != null) upgradeButton.gameObject.SetActive(visible);
        if (evolutionButton != null)
            evolutionButton.gameObject.SetActive(visible && (selectedHero == bowMaster || selectedHero == victoria));
    }

    void FindAndHookEvolutionButton()
    {
        if (evolutionButton == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "EvolutionButton")
                {
                    evolutionButton = t.GetComponent<Button>();
                    break;
                }
            }
        }

        if (evolutionButton == null) return;

        evolutionButton.onClick.RemoveListener(OpenEvolutionForSelectedHero);
        evolutionButton.onClick.AddListener(OpenEvolutionForSelectedHero);
    }

    void OpenEvolutionForSelectedHero()
    {
        if (selectedHero == bowMaster)
        {
            BowMasterEvolutionController controller = BowMasterEvolutionController.EnsureFor(this);
            if (controller != null) controller.OpenEvolutionTree();
            return;
        }

        if (selectedHero == victoria)
        {
            VictoriaEvolutionController controller = VictoriaEvolutionController.EnsureFor(this);
            if (controller != null) controller.OpenEvolutionTree();
        }
    }

    void SetPanelBackgroundVisible(bool visible)
    {
        if (panelBackground == null)
            panelBackground = GetComponent<Image>();

        if (panelBackground != null)
            panelBackground.enabled = visible;
    }

    void SetHeroListVisible(bool visible)
    {
        if (heroListPanel != null)
            heroListPanel.SetActive(visible);
    }
    public static void CloseAnyOpenPanel()
    {
        if (activePanel != null && activePanel.gameObject.activeSelf)
            activePanel.ClosePanel();
    }

    void OnDisable()
    {
        if (activePanel == this && gameObject != null)
            IsModalOpen = false;
        if (modalBlocker != null)
            modalBlocker.SetActive(false);
    }

    void EnsureModalBlocker()
    {
        if (modalBlocker != null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("HeroModalBlocker");
        if (existing != null)
        {
            modalBlocker = existing.gameObject;
        }
        else
        {
            modalBlocker = new GameObject("HeroModalBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = modalBlocker.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = modalBlocker.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.08f);
            image.raycastTarget = true;
        }

        // Блокировщик должен быть выше обычного UI, но ниже самой панели героя и списка героев.
        modalBlocker.transform.SetAsLastSibling();
        transform.SetAsLastSibling();
        if (heroListPanel != null)
            heroListPanel.transform.SetAsLastSibling();
        modalBlocker.SetActive(false);
    }

    void SetModalState(bool open)
    {
        IsModalOpen = open;
        EnsureModalBlocker();
        if (modalBlocker != null)
            modalBlocker.SetActive(open);
    }

    void PlayPanelPulse(bool switching)
    {
        if (!isActiveAndEnabled || panelRect == null) return;
        if (panelAnimation != null) StopCoroutine(panelAnimation);
        panelAnimation = StartCoroutine(PanelPulse(switching));
    }

    IEnumerator PanelPulse(bool switching)
    {
        Vector3 normal = Vector3.one;
        Vector3 start = switching ? new Vector3(0.97f, 0.97f, 1f) : new Vector3(0.93f, 0.93f, 1f);
        panelRect.localScale = start;
        float elapsed = 0f;
        float duration = switching ? 0.10f : 0.15f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            panelRect.localScale = Vector3.Lerp(start, normal, t);
            yield return null;
        }
        panelRect.localScale = normal;
        panelAnimation = null;
    }

}
