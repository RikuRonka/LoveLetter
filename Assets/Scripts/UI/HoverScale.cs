using UnityEngine;
using UnityEngine.EventSystems;

public class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] float hoverScale = 1.08f;
    [SerializeField] float speed = 10f;

    Vector3 baseScale;
    Vector3 targetScale;
    bool hovering;

    void Awake()
    {
        baseScale = transform.localScale;
        targetScale = baseScale;
    }

    void OnEnable()
    {
        transform.localScale = baseScale;
        hovering = false;
        targetScale = baseScale;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * speed);
    }

    public void OnPointerEnter(PointerEventData _) { hovering = true; targetScale = baseScale * hoverScale; }
    public void OnPointerExit(PointerEventData _) { hovering = false; targetScale = baseScale; }
}
