using UnityEngine;
using UnityEngine.UI;

public class PlayableHighlight : MonoBehaviour
{
    [SerializeField] Image glowImage;     // a child Image used as glow
    [SerializeField] Color glowColor = new(0.2f, 1f, 0.4f, 0.9f);
    [SerializeField] float pulseSpeed = 2f;
    bool active;

    void Reset()
    {
        // try to find a child named "Glow"
        var t = transform.Find("Glow");
        if (t) glowImage = t.GetComponent<Image>();
    }

    void Update()
    {
        if (glowImage == null) return;
        if (!active) { glowImage.enabled = false; return; }

        glowImage.enabled = true;
        var c = glowColor;
        c.a *= 0.6f + 0.4f * Mathf.PingPong(Time.unscaledTime * pulseSpeed, 1f);
        glowImage.color = c;
    }

    public void SetPlayable(bool isPlayable) => active = isPlayable;
}
