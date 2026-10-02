using UnityEngine;
using UnityEngine.UI;

public class BowMasterEvolutionController : MonoBehaviour
{
    private HeroPanelController heroPanel;
    private Hero bowMaster;
    private GameObject evolutionPanel, infoPanel1, infoPanel2, evolutionModalBlocker;
    private Button treeBackButton, evolution1NodeButton, evolution2NodeButton;
    private Button info1CloseButton, buy1Button, info2CloseButton, buy2Button;
    private Image evolutionLine1, evolutionLine2, evolutionLine3, evolutionLine4;

    private static BowMasterEvolutionController instance;
    public static bool IsEvolutionModalOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        instance = null;
        IsEvolutionModalOpen = false;
    }

    void Awake()
    {
        instance = this;
        IsEvolutionModalOpen = false;
    }

    void OnDisable()
    {
        IsEvolutionModalOpen = false;
        if (evolutionModalBlocker != null) evolutionModalBlocker.SetActive(false);
    }

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
        infoPanel1 = FindSceneObject("EvolutionInfoPanel");
        infoPanel2 = FindSceneObject("EvolutionInfoPanel2");
        DetachInfoPanel(infoPanel1);
        DetachInfoPanel(infoPanel2);

        EnsureEvolutionBlocker();
        treeBackButton = FindButton("BackButton", evolutionPanel);
        evolution1NodeButton = EnsureButton("BowMasterEvolution1Image", evolutionPanel);
        evolution2NodeButton = EnsureFirstButton(evolutionPanel, "BowMasterEvolution2Image", "BowMasterPhysicalEvolution2Image");

        info1CloseButton = EnsureFirstButton(infoPanel1, "Evolution1InfoCloseButton", "EvolutionInfo1CloseButton", "EvolutionInfoCloseButton");
        buy1Button = FindButton("Evolution1BuyButton", infoPanel1);
        info2CloseButton = EnsureFirstButton(infoPanel2, "EvolutionInfo2CloseButton", "Evolution2InfoCloseButton", "EvolutionInfoCloseButton");
        buy2Button = FindFirstButton(infoPanel2, "Evolution2BuyButton", "Evolution1BuyButton");

        evolutionLine1 = FindImage("EvolutionLine1", evolutionPanel);
        evolutionLine2 = FindImage("EvolutionLine2", evolutionPanel);
        evolutionLine3 = FindImage("EvolutionLine3", evolutionPanel);
        evolutionLine4 = FindImage("EvolutionLine4", evolutionPanel);
        DisableLineRaycast(evolutionLine1); DisableLineRaycast(evolutionLine2);
        DisableLineRaycast(evolutionLine3); DisableLineRaycast(evolutionLine4);

        Hook(treeBackButton, BackToHero);
        Hook(evolution1NodeButton, OpenInfo1);
        Hook(evolution2NodeButton, OpenInfo2);
        Hook(info1CloseButton, CloseInfo);
        Hook(info2CloseButton, CloseInfo);
        Hook(buy1Button, BuyEvolution1);
        Hook(buy2Button, BuyEvolution2);

        AddPressEffect(treeBackButton); AddPressEffect(evolution1NodeButton); AddPressEffect(evolution2NodeButton);
        AddPressEffect(info1CloseButton); AddPressEffect(info2CloseButton); AddPressEffect(buy1Button); AddPressEffect(buy2Button);
        RefreshVisualState();
    }

    void DetachInfoPanel(GameObject panel)
    {
        if (panel == null || evolutionPanel == null || !panel.transform.IsChildOf(evolutionPanel.transform)) return;
        Canvas canvas = evolutionPanel.GetComponentInParent<Canvas>();
        if (canvas != null) panel.transform.SetParent(canvas.transform, true);
    }

    public void ResetPanelsForPlay()
    {
        // Scene objects may have been left active in Edit Mode. Never let that block world input on Play.
        if (infoPanel1 != null) infoPanel1.SetActive(false);
        if (infoPanel2 != null) infoPanel2.SetActive(false);
        if (evolutionPanel != null) evolutionPanel.SetActive(false);
        if (treeBackButton != null) treeBackButton.gameObject.SetActive(true);
        SetEvolutionModal(false);
    }

    public void OpenEvolutionTree()
    {
        Bind(heroPanel);
        if (heroPanel != null) heroPanel.ClosePanel();
        if (infoPanel1 != null) infoPanel1.SetActive(false);
        if (infoPanel2 != null) infoPanel2.SetActive(false);
        if (evolutionPanel != null) evolutionPanel.SetActive(true);
        if (treeBackButton != null) treeBackButton.gameObject.SetActive(true);
        SetEvolutionModal(true); BringCurrentPanelToFront(); RefreshVisualState();
    }

    void BackToHero()
    {
        if (infoPanel1 != null) infoPanel1.SetActive(false);
        if (infoPanel2 != null) infoPanel2.SetActive(false);
        if (evolutionPanel != null) evolutionPanel.SetActive(false);
        SetEvolutionModal(false);
        if (heroPanel != null && bowMaster != null) heroPanel.OpenForHero(bowMaster);
    }

    void OpenInfo1() { OpenInfo(infoPanel1); }
    void OpenInfo2()
    {
        // Physical Evolution II is a continuation of Evolution I.
        if (bowMaster == null || !bowMaster.evolution1Purchased) return;
        OpenInfo(infoPanel2);
    }
    void OpenInfo(GameObject panel)
    {
        if (treeBackButton != null) treeBackButton.gameObject.SetActive(false);
        if (evolutionPanel != null) evolutionPanel.SetActive(false);
        if (infoPanel1 != null) infoPanel1.SetActive(panel == infoPanel1);
        if (infoPanel2 != null) infoPanel2.SetActive(panel == infoPanel2);
        SetEvolutionModal(true); BringCurrentPanelToFront(); RefreshVisualState();
    }
    void CloseInfo()
    {
        if (infoPanel1 != null) infoPanel1.SetActive(false);
        if (infoPanel2 != null) infoPanel2.SetActive(false);
        if (evolutionPanel != null) evolutionPanel.SetActive(true);
        if (treeBackButton != null) treeBackButton.gameObject.SetActive(true);
        SetEvolutionModal(true); BringCurrentPanelToFront(); RefreshVisualState();
    }

    void BuyEvolution1()
    {
        if (bowMaster == null) return;
        if (!bowMaster.evolution1Purchased)
        {
            if (PlayerProgress.Instance == null || !PlayerProgress.Instance.SpendEmeralds(10)) return;
            bowMaster.ApplyEvolution1();
        }
        else
        {
            // Purchased forever: click installs it; clicking the active form removes it back to base.
            bowMaster.SetActiveEvolution(bowMaster.activeEvolution == 1 ? 0 : 1);
        }
        RefreshVisualState(); if (heroPanel != null) heroPanel.Refresh();
    }

    void BuyEvolution2()
    {
        if (bowMaster == null || !bowMaster.evolution1Purchased) return;
        if (!bowMaster.evolution2Purchased)
        {
            if (PlayerProgress.Instance == null || !PlayerProgress.Instance.SpendEmeralds(20)) return;
            bowMaster.ApplyEvolution2();
        }
        else
        {
            // Removing Physical II falls back to the already-owned Evolution I.
            bowMaster.SetActiveEvolution(bowMaster.activeEvolution == 2 ? 1 : 2);
        }
        RefreshVisualState(); if (heroPanel != null) heroPanel.Refresh();
    }

    void RefreshVisualState()
    {
        if (bowMaster == null) return;
        Color gold = new Color(1f, 0.78f, 0.12f, 1f), dark = new Color(0.25f, 0.25f, 0.25f, 1f);
        if (evolutionLine1 != null) evolutionLine1.color = bowMaster.evolution1Purchased ? gold : dark;
        Color physical = bowMaster.evolution2Purchased ? gold : dark;
        if (evolutionLine2 != null) evolutionLine2.color = physical;
        if (evolutionLine3 != null) evolutionLine3.color = physical;
        if (evolutionLine4 != null) evolutionLine4.color = physical;
        if (buy1Button != null) buy1Button.interactable = bowMaster.evolution1Purchased || (PlayerProgress.Instance != null && PlayerProgress.Instance.emeralds >= 10);
        if (buy2Button != null) buy2Button.interactable = bowMaster.evolution1Purchased && (bowMaster.evolution2Purchased || (PlayerProgress.Instance != null && PlayerProgress.Instance.emeralds >= 20));
    }

    void EnsureEvolutionBlocker()
    {
        Canvas canvas = evolutionPanel != null ? evolutionPanel.GetComponentInParent<Canvas>() : null;
        if (canvas == null && heroPanel != null) canvas = heroPanel.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Transform existing = canvas.transform.Find("EvolutionModalBlocker");
        if (existing != null) evolutionModalBlocker = existing.gameObject;
        else
        {
            evolutionModalBlocker = new GameObject("EvolutionModalBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform r = evolutionModalBlocker.GetComponent<RectTransform>(); r.SetParent(canvas.transform, false);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            Image img = evolutionModalBlocker.GetComponent<Image>(); img.color = new Color(0f,0f,0f,0f); img.raycastTarget = true;
        }
        evolutionModalBlocker.SetActive(false);
    }
    void SetEvolutionModal(bool open)
    {
        IsEvolutionModalOpen = open; EnsureEvolutionBlocker(); if (evolutionModalBlocker == null) return;
        evolutionModalBlocker.SetActive(open);
        if (open) { evolutionModalBlocker.transform.SetAsLastSibling(); BringCurrentPanelToFront(); }
    }
    void BringCurrentPanelToFront()
    {
        if (evolutionPanel != null && evolutionPanel.activeSelf) evolutionPanel.transform.SetAsLastSibling();
        if (infoPanel1 != null && infoPanel1.activeSelf) infoPanel1.transform.SetAsLastSibling();
        if (infoPanel2 != null && infoPanel2.activeSelf) infoPanel2.transform.SetAsLastSibling();
    }

    static void DisableLineRaycast(Image i) { if (i != null) i.raycastTarget = false; }
    static Image FindImage(string name, GameObject root) { GameObject g=FindChildOrScene(name,root); return g!=null?g.GetComponent<Image>():null; }
    static void Hook(Button b, UnityEngine.Events.UnityAction a) { if(b==null)return; b.onClick.RemoveListener(a); b.onClick.AddListener(a); }
    static void AddPressEffect(Button b) { if(b!=null && b.GetComponent<SimplePressScale>()==null)b.gameObject.AddComponent<SimplePressScale>(); }
    static Button EnsureFirstButton(GameObject root, params string[] names) { foreach(var n in names){var b=EnsureButton(n,root);if(b!=null)return b;}return null; }
    static Button FindFirstButton(GameObject root, params string[] names) { foreach(var n in names){var b=FindButton(n,root);if(b!=null)return b;}return null; }
    static Button EnsureButton(string name, GameObject root)
    {
        GameObject go=FindChildOrScene(name,root); if(go==null)return null; Button b=go.GetComponent<Button>(); if(b==null)b=go.AddComponent<Button>();
        Image img=go.GetComponent<Image>(); if(img!=null)img.raycastTarget=true; b.targetGraphic=img; return b;
    }
    static Button FindButton(string name, GameObject root) { GameObject go=FindChildOrScene(name,root); return go!=null?go.GetComponent<Button>():null; }
    static GameObject FindChildOrScene(string name, GameObject root)
    {
        if(root!=null)foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t.gameObject;
        return FindSceneObject(name);
    }
    static GameObject FindSceneObject(string name)
    {
        foreach(Transform t in Resources.FindObjectsOfTypeAll<Transform>())if(t.name==name&&t.gameObject.scene.IsValid())return t.gameObject; return null;
    }
}

public class SimplePressScale : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    private Vector3 normalScale; private Image image; private Color normalColor;
    void Awake(){normalScale=transform.localScale;image=GetComponent<Image>();if(image!=null)normalColor=image.color;}
    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e){transform.localScale=normalScale*0.93f;if(image!=null)image.color=new Color(1f,1f,1f,0.16f);}
    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e){Restore();} public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e){Restore();} void OnDisable(){Restore();}
    void Restore(){transform.localScale=normalScale;if(image!=null)image.color=normalColor;}
}
