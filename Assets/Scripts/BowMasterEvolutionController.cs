using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BowMasterEvolutionController : MonoBehaviour
{
    private HeroPanelController heroPanel;
    private Hero bowMaster;
    private GameObject evolutionPanel;
    private GameObject infoPanel;
    private Button treeBackButton;
    private Button evolutionNodeButton;
    private Button infoCloseButton;
    private Button buyButton;
    private Image evolutionLine1;
    private GameObject evolutionModalBlocker;

    private static BowMasterEvolutionController instance;
    public static bool IsEvolutionModalOpen { get; private set; }

    public static BowMasterEvolutionController EnsureFor(HeroPanelController panel)
    {
        if (instance == null)
        {
            instance = FindAnyObjectByType<BowMasterEvolutionController>(FindObjectsInactive.Include);
            if (instance == null)
            {
                GameObject host = new GameObject("BowMasterEvolutionController");
                Canvas canvas = panel != null ? panel.GetComponentInParent<Canvas>() : null;
                if (canvas != null) host.transform.SetParent(canvas.transform, false);
                instance = host.AddComponent<BowMasterEvolutionController>();
            }
        }
        instance.Bind(panel);
        return instance;
    }

    void Bind(HeroPanelController panel)
    {
        heroPanel = panel;
        bowMaster = panel != null ? panel.bowMaster : null;
        evolutionPanel = FindSceneObject("EvolutionPanel");
        infoPanel = FindSceneObject("EvolutionInfoPanel");

        if (infoPanel != null && evolutionPanel != null && infoPanel.transform.IsChildOf(evolutionPanel.transform))
        {
            Canvas canvas = evolutionPanel.GetComponentInParent<Canvas>();
            if (canvas != null) infoPanel.transform.SetParent(canvas.transform, true);
        }

        EnsureEvolutionBlocker();
        treeBackButton = FindButton("BackButton", evolutionPanel);
        evolutionNodeButton = EnsureButton("BowMasterEvolution1Image", evolutionPanel);
        infoCloseButton = FindButton("EvolutionInfoCloseButton", infoPanel);
        buyButton = FindButton("Evolution1BuyButton", infoPanel);

        GameObject line = FindChildOrScene("EvolutionLine1", evolutionPanel);
        evolutionLine1 = line != null ? line.GetComponent<Image>() : null;

        Hook(treeBackButton, BackToHero);
        Hook(evolutionNodeButton, OpenInfo);
        Hook(infoCloseButton, CloseInfo);
        Hook(buyButton, BuyEvolution1);

        AddPressEffect(treeBackButton);
        AddPressEffect(evolutionNodeButton);
        AddPressEffect(infoCloseButton);
        AddPressEffect(buyButton);
        RefreshVisualState();
    }

    public void OpenEvolutionTree()
    {
        Bind(heroPanel);
        if (heroPanel != null) heroPanel.ClosePanel();
        if (infoPanel != null) infoPanel.SetActive(false);
        if (evolutionPanel != null) evolutionPanel.SetActive(true);
        SetEvolutionModal(true);
        BringCurrentPanelToFront();
        RefreshVisualState();
    }

    void BackToHero()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
        if (evolutionPanel != null) evolutionPanel.SetActive(false);
        SetEvolutionModal(false);
        if (heroPanel != null && bowMaster != null) heroPanel.OpenForHero(bowMaster);
    }

    void OpenInfo()
    {
        if (evolutionPanel != null) evolutionPanel.SetActive(false);
        if (infoPanel != null) infoPanel.SetActive(true);
        SetEvolutionModal(true);
        BringCurrentPanelToFront();
    }

    void CloseInfo()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
        if (evolutionPanel != null) evolutionPanel.SetActive(true);
        SetEvolutionModal(true);
        BringCurrentPanelToFront();
        RefreshVisualState();
    }

    void BuyEvolution1()
    {
        if (bowMaster == null || bowMaster.evolution1Purchased) return;
        if (PlayerProgress.Instance == null || !PlayerProgress.Instance.SpendEmeralds(10)) return;
        bowMaster.ApplyEvolution1();
        RefreshVisualState();
        if (heroPanel != null) heroPanel.Refresh();
    }

    void EnsureEvolutionBlocker()
    {
        Canvas canvas = null;
        if (evolutionPanel != null) canvas = evolutionPanel.GetComponentInParent<Canvas>();
        if (canvas == null && heroPanel != null) canvas = heroPanel.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("EvolutionModalBlocker");
        if (existing != null) evolutionModalBlocker = existing.gameObject;
        else
        {
            evolutionModalBlocker = new GameObject("EvolutionModalBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform r = evolutionModalBlocker.GetComponent<RectTransform>();
            r.SetParent(canvas.transform, false);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            Image img = evolutionModalBlocker.GetComponent<Image>();
            // Невидимый полноэкранный перехватчик кликов. Ничего визуально не меняет.
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;
        }
        evolutionModalBlocker.SetActive(false);
    }

    void SetEvolutionModal(bool open)
    {
        IsEvolutionModalOpen = open;
        EnsureEvolutionBlocker();
        if (evolutionModalBlocker == null) return;
        evolutionModalBlocker.SetActive(open);
        if (open)
        {
            evolutionModalBlocker.transform.SetAsLastSibling();
            BringCurrentPanelToFront();
        }
    }

    void BringCurrentPanelToFront()
    {
        if (evolutionPanel != null && evolutionPanel.activeSelf) evolutionPanel.transform.SetAsLastSibling();
        if (infoPanel != null && infoPanel.activeSelf) infoPanel.transform.SetAsLastSibling();
    }

    void RefreshVisualState()
    {
        if (bowMaster == null) return;
        if (evolutionLine1 != null)
            evolutionLine1.color = bowMaster.evolution1Purchased ? new Color(1f, 0.78f, 0.12f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
        if (buyButton != null)
            buyButton.interactable = !bowMaster.evolution1Purchased && PlayerProgress.Instance != null && PlayerProgress.Instance.emeralds >= 10;
    }

    static void Hook(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        b.onClick.RemoveListener(action);
        b.onClick.AddListener(action);
    }

    static void AddPressEffect(Button b)
    {
        if (b == null) return;
        if (b.GetComponent<SimplePressScale>() == null) b.gameObject.AddComponent<SimplePressScale>();
    }

    static Button EnsureButton(string name, GameObject root)
    {
        GameObject go = FindChildOrScene(name, root);
        if (go == null) return null;
        Button b = go.GetComponent<Button>();
        if (b == null) b = go.AddComponent<Button>();
        Image img = go.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
        b.targetGraphic = img;
        return b;
    }

    static Button FindButton(string name, GameObject root)
    {
        GameObject go = FindChildOrScene(name, root);
        return go != null ? go.GetComponent<Button>() : null;
    }

    static GameObject FindChildOrScene(string name, GameObject root)
    {
        if (root != null)
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return FindSceneObject(name);
    }

    static GameObject FindSceneObject(string name)
    {
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
        return null;
    }
}

public class SimplePressScale : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    private Vector3 normalScale;
    private Image image;
    private Color normalColor;
    void Awake()
    {
        normalScale = transform.localScale;
        image = GetComponent<Image>();
        if (image != null) normalColor = image.color;
    }
    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
    {
        transform.localScale = normalScale * 0.93f;
        // Прозрачная overlay-кнопка теперь даёт видимый короткий flash.
        if (image != null) image.color = new Color(1f, 1f, 1f, 0.16f);
    }
    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { Restore(); }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { Restore(); }
    void OnDisable() { Restore(); }
    void Restore()
    {
        transform.localScale = normalScale;
        if (image != null) image.color = normalColor;
    }
}
