using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Add to any UI Button: normal click still buys once; holding repeats purchases quickly.
public class HoldToRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public float holdDelay = 0.35f;
    public float repeatInterval = 0.08f;

    private Button button;
    private Coroutine repeatRoutine;

    void Awake() => button = GetComponent<Button>();

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button == null || !button.interactable) return;
        StopRepeat();
        repeatRoutine = StartCoroutine(Repeat());
    }

    public void OnPointerUp(PointerEventData eventData) => StopRepeat();
    public void OnPointerExit(PointerEventData eventData) => StopRepeat();
    void OnDisable() => StopRepeat();

    IEnumerator Repeat()
    {
        yield return new WaitForSecondsRealtime(holdDelay);
        while (button != null && button.interactable)
        {
            button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(repeatInterval);
        }
        repeatRoutine = null;
    }

    void StopRepeat()
    {
        if (repeatRoutine != null)
        {
            StopCoroutine(repeatRoutine);
            repeatRoutine = null;
        }
    }

    public static void Ensure(Button target)
    {
        if (target != null && target.GetComponent<HoldToRepeatButton>() == null)
            target.gameObject.AddComponent<HoldToRepeatButton>();
    }
}
