using UnityEngine;

public class AspectRatioGuard : MonoBehaviour
{
public float targetAspect = 16f / 9f;

    void Start() => Apply();

    void Apply()
    {
        RectTransform rect = GetComponent<RectTransform>();

        float screenAspect = (float)Screen.width / Screen.height;

        if (screenAspect > targetAspect)
        {
            // 좌우 여백
            float width = Screen.height * targetAspect;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Screen.height);
        }
        else
        {
            // 상하 여백
            float height = Screen.width / targetAspect;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Screen.width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        rect.anchoredPosition = Vector2.zero; // 중앙 고정
    }
}
