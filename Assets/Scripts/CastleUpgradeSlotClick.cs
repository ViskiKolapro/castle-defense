using System.Collections;
using UnityEngine;

public class CastleUpgradeSlotClick : MonoBehaviour
{
    public Castle castle;
    private bool animating;

    void OnMouseDown()
    {
        if (BowMasterEvolutionController.IsEvolutionModalOpen) return;
        if (castle == null || !castle.CanOpenUpgradePanel) return;
        castle.OpenUpgradePanel();
        if (!animating) StartCoroutine(Pulse());
    }

    IEnumerator Pulse()
    {
        animating = true;
        Vector3 start = transform.localScale;
        transform.localScale = start * 0.88f;
        yield return new WaitForSecondsRealtime(0.06f);
        transform.localScale = start;
        animating = false;
    }
}
