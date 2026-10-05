using UnityEngine;
using UnityEngine.UI;

public class VictoriaEvolutionController : MonoBehaviour
{
    static VictoriaEvolutionController instance;
    HeroPanelController heroPanel;
    Hero victoria;

    GameObject tree, red1Info, red2Info, blue1Info, blue2Info, blocker;
    Button back, red1Node, red2Node, blue1Node, blue2Node;
    Button red1Close, red2Close, blue1Close, blue2Close, red1Buy, red2Buy, blue1Buy, blue2Buy;
    Image[] lines = new Image[6];
    readonly Color dark = new Color(0.23f,0.23f,0.23f,1f);
    readonly Color gold = new Color(1f,0.72f,0.08f,1f);

    public static VictoriaEvolutionController EnsureFor(HeroPanelController panel)
    {
        if (instance == null) instance = FindAnyObjectByType<VictoriaEvolutionController>(FindObjectsInactive.Include);
        if (instance == null)
        {
            var go = new GameObject("VictoriaEvolutionController");
            instance = go.AddComponent<VictoriaEvolutionController>();
        }
        instance.Setup(panel);
        return instance;
    }

    void Setup(HeroPanelController panel)
    {
        heroPanel = panel;
        victoria = panel != null ? panel.victoria : null;
        tree = Find("VictoriaEvolutionPanel");
        red1Info = Find("VictoriaRedElfEvolution1InfoPanel");
        red2Info = Find("VictoriaRedElfEvolution2InfoPanel");
        blue1Info = Find("VictoriaBlueElfEvolution1InfoPanel");
        blue2Info = Find("VictoriaBlueElfEvolution2InfoPanel");
        if (tree == null) return;

        // Info panels were created as children of VictoriaEvolutionPanel in the scene.
        // Disabling the tree would therefore also disable the info panel and leave only
        // the modal blocker active. Move them beside the tree once, preserving their
        // current world-space UI layout, exactly like the Bow Master evolution UI.
        DetachInfoPanel(red1Info);
        DetachInfoPanel(red2Info);
        DetachInfoPanel(blue1Info);
        DetachInfoPanel(blue2Info);

        back = FindButton(tree, "BackButton");
        red1Node = EnsureButton(tree, "VictoriaRedElfEvolution1Image");
        red2Node = EnsureButton(tree, "VictoriaRedElfEvolution2Image");
        blue1Node = EnsureButton(tree, "VictoriaBlueElfEvolution1Image");
        blue2Node = EnsureButton(tree, "VictoriaBlueElfEvolution2Image");
        red1Close = FindButton(red1Info, "VictoriaRedElfEvolution1CloseButton");
        red2Close = FindButton(red2Info, "VictoriaRedElfEvolution2CloseButton");
        blue1Close = FindButton(blue1Info, "VictoriaBlueElfEvolution1CloseButton");
        blue2Close = FindButton(blue2Info, "VictoriaBlueElfEvolution2CloseButton");
        red1Buy = FindButton(red1Info, "VictoriaRedElfEvolution1BuyButton");
        red2Buy = FindButton(red2Info, "VictoriaRedElfEvolution2BuyButton");
        blue1Buy = FindButton(blue1Info, "VictoriaBlueElfEvolution1BuyButton");
        blue2Buy = FindButton(blue2Info, "VictoriaBlueElfEvolution2BuyButton");
        for (int i=0;i<6;i++) { lines[i]=FindImage(tree,"EvolutionLine"+(i+1)); if(lines[i]!=null) lines[i].raycastTarget=false; }

        Hook(back, CloseTree); Hook(red1Node, ()=>OpenInfo(red1Info)); Hook(red2Node, OpenRed2); Hook(blue1Node, ()=>OpenInfo(blue1Info)); Hook(blue2Node, OpenBlue2);
        Hook(red1Close, ReturnToTree); Hook(red2Close, ReturnToTree); Hook(blue1Close, ReturnToTree); Hook(blue2Close, ReturnToTree);
        Hook(red1Buy, ToggleRed1); Hook(red2Buy, ToggleRed2); Hook(blue1Buy, ToggleBlue1); Hook(blue2Buy, ToggleBlue2);
        AddPressEffect(back); AddPressEffect(red1Node); AddPressEffect(red2Node); AddPressEffect(blue1Node); AddPressEffect(blue2Node);
        AddPressEffect(red1Close); AddPressEffect(red2Close); AddPressEffect(blue1Close); AddPressEffect(blue2Close);
        AddPressEffect(red1Buy); AddPressEffect(red2Buy); AddPressEffect(blue1Buy); AddPressEffect(blue2Buy);
        EnsureBlocker(); Refresh();
    }

    public void ResetPanelsForPlay()
    {
        if(tree!=null) tree.SetActive(false); if(red1Info!=null) red1Info.SetActive(false); if(red2Info!=null) red2Info.SetActive(false); if(blue1Info!=null) blue1Info.SetActive(false); if(blue2Info!=null) blue2Info.SetActive(false);
        SetBlock(false);
    }

    public void OpenEvolutionTree()
    {
        Setup(heroPanel); if(tree==null) return;
        HeroPanelController.CloseAnyOpenPanel();
        HideInfos(); tree.SetActive(true); SetBlock(true); tree.transform.SetAsLastSibling(); Refresh();
    }

    void CloseTree(){ if(tree!=null) tree.SetActive(false); HideInfos(); SetBlock(false); }
    void ReturnToTree(){ HideInfos(); if(tree!=null){tree.SetActive(true); tree.transform.SetAsLastSibling();} SetBlock(true); Refresh(); }
    void OpenInfo(GameObject info){ if(info==null)return; if(tree!=null)tree.SetActive(false); HideInfos(); info.SetActive(true); SetBlock(true); info.transform.SetAsLastSibling(); Refresh(); }
    void OpenRed2(){ if(victoria==null || !victoria.victoriaRedEvolution1Purchased) return; OpenInfo(red2Info); }
    void OpenBlue2(){ if(victoria==null || !victoria.victoriaBlueEvolution1Purchased) return; OpenInfo(blue2Info); }
    void HideInfos(){ if(red1Info!=null)red1Info.SetActive(false); if(red2Info!=null)red2Info.SetActive(false); if(blue1Info!=null)blue1Info.SetActive(false); if(blue2Info!=null)blue2Info.SetActive(false); }

    void ToggleRed1()
    {
        if(victoria==null)return;
        if(!victoria.victoriaRedEvolution1Purchased){ if(!Spend(10))return; victoria.victoriaRedEvolution1Purchased=true; victoria.SetVictoriaActiveEvolution(1); }
        else victoria.SetVictoriaActiveEvolution(victoria.victoriaActiveEvolution==1 ? 0 : 1);
        SaveEvolutionState(); Refresh();
    }
    void ToggleRed2()
    {
        if(victoria==null || !victoria.victoriaRedEvolution1Purchased)return;
        if(!victoria.victoriaRedEvolution2Purchased){ if(!Spend(20))return; victoria.victoriaRedEvolution2Purchased=true; victoria.SetVictoriaActiveEvolution(2); }
        else victoria.SetVictoriaActiveEvolution(2);
        SaveEvolutionState(); Refresh();
    }
    void ToggleBlue1()
    {
        if(victoria==null)return;
        if(!victoria.victoriaBlueEvolution1Purchased){ if(!Spend(10))return; victoria.victoriaBlueEvolution1Purchased=true; victoria.SetVictoriaActiveEvolution(3); }
        else victoria.SetVictoriaActiveEvolution(victoria.victoriaActiveEvolution==3 ? 0 : 3);
        SaveEvolutionState(); Refresh();
    }
    void ToggleBlue2()
    {
        if(victoria==null || !victoria.victoriaBlueEvolution1Purchased)return;
        if(!victoria.victoriaBlueEvolution2Purchased){ if(!Spend(20))return; victoria.victoriaBlueEvolution2Purchased=true; victoria.SetVictoriaActiveEvolution(4); }
        else victoria.SetVictoriaActiveEvolution(4);
        SaveEvolutionState(); Refresh();
    }
    void SaveEvolutionState(){ var s=FindAnyObjectByType<SaveSystem>(); if(s!=null) s.Save(); }
    bool Spend(int amount){ return PlayerProgress.Instance!=null && PlayerProgress.Instance.SpendEmeralds(amount); }

    void Refresh()
    {
        if(victoria==null)return;
        // Gold means ACTIVE path, not merely purchased.
        for(int i=0;i<lines.Length;i++) if(lines[i]!=null) lines[i].color=dark;
        int a=victoria.victoriaActiveEvolution;
        // Exact branch mapping. Blue uses only its own central branch: Line5 -> Blue I, Line6 -> Blue II.
        if(a==1 || a==2) { Paint(0); Paint(1); Paint(2); }
        if(a==2) Paint(3);
        if(a==3 || a==4) Paint(4);
        if(a==4) Paint(5);
        if(red1Buy!=null) red1Buy.interactable=victoria.victoriaRedEvolution1Purchased || CanAfford(10);
        if(red2Buy!=null) red2Buy.interactable=victoria.victoriaRedEvolution1Purchased && (victoria.victoriaRedEvolution2Purchased || CanAfford(20));
        if(blue1Buy!=null) blue1Buy.interactable=victoria.victoriaBlueEvolution1Purchased || CanAfford(10);
        if(blue2Buy!=null) blue2Buy.interactable=victoria.victoriaBlueEvolution1Purchased && (victoria.victoriaBlueEvolution2Purchased || CanAfford(20));
    }
    void Paint(int i){ if(i>=0&&i<lines.Length&&lines[i]!=null) lines[i].color=gold; }
    bool CanAfford(int n){ return PlayerProgress.Instance!=null && PlayerProgress.Instance.emeralds>=n; }

    void DetachInfoPanel(GameObject info)
    {
        if (info == null || tree == null) return;
        Transform treeParent = tree.transform.parent;
        if (treeParent == null || info.transform.parent != tree.transform) return;
        info.transform.SetParent(treeParent, true);
    }

    void EnsureBlocker()
    {
        if(tree==null)return; Canvas c=tree.GetComponentInParent<Canvas>(); if(c==null)return;
        Transform e=c.transform.Find("VictoriaEvolutionModalBlocker");
        if(e!=null) blocker=e.gameObject; else { blocker=new GameObject("VictoriaEvolutionModalBlocker",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)); var r=blocker.GetComponent<RectTransform>(); r.SetParent(c.transform,false); r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero; var im=blocker.GetComponent<Image>();im.color=Color.clear;im.raycastTarget=true; }
        blocker.SetActive(false);
    }
    void SetBlock(bool on){ EnsureBlocker(); if(blocker==null)return; blocker.SetActive(on); if(on){blocker.transform.SetAsLastSibling(); if(tree!=null&&tree.activeSelf)tree.transform.SetAsLastSibling(); if(red1Info!=null&&red1Info.activeSelf)red1Info.transform.SetAsLastSibling(); if(red2Info!=null&&red2Info.activeSelf)red2Info.transform.SetAsLastSibling(); if(blue1Info!=null&&blue1Info.activeSelf)blue1Info.transform.SetAsLastSibling(); if(blue2Info!=null&&blue2Info.activeSelf)blue2Info.transform.SetAsLastSibling();} }

    static GameObject Find(string n){ foreach(var t in Resources.FindObjectsOfTypeAll<Transform>()) if(t!=null&&t.name==n&&t.gameObject.scene.IsValid()) return t.gameObject; return null; }
    static Button FindButton(GameObject root,string n){ if(root==null)return null; foreach(var t in root.GetComponentsInChildren<Transform>(true)) if(t.name==n)return t.GetComponent<Button>(); return null; }
    static Button EnsureButton(GameObject root,string n){ var b=FindButton(root,n); if(b!=null)return b; if(root==null)return null; foreach(var t in root.GetComponentsInChildren<Transform>(true)) if(t.name==n)return t.gameObject.AddComponent<Button>(); return null; }
    static Image FindImage(GameObject root,string n){ if(root==null)return null; foreach(var t in root.GetComponentsInChildren<Transform>(true)) if(t.name==n)return t.GetComponent<Image>(); return null; }
    static void Hook(Button b,UnityEngine.Events.UnityAction a){ if(b==null)return; b.onClick.RemoveAllListeners(); b.onClick.AddListener(a); }
    static void AddPressEffect(Button b){ if(b!=null && b.GetComponent<SimplePressScale>()==null) b.gameObject.AddComponent<SimplePressScale>(); }
}
