using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

public class hand : MonoBehaviour
{
public RectTransform targetImage;
    public Canvas canvas;

    public float duration = 0.08f; // 부드러움 정도

    private Tween moveTween;

    void Awake()
    {
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        float size = canvasRect.rect.height;

        targetImage.sizeDelta = new Vector2(size, size);
    }



    void FixedUpdate()
    {
        // 새 Input System 마우스 위치
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // UI 좌표 변환
        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            mousePos,
            canvas.worldCamera,
            out pos
        );

        moveTween?.Kill();

        moveTween = targetImage.DOAnchorPos(pos, duration)
            .SetEase(Ease.OutQuad);
    }

    void OnEnable()
    {
        // 활성화 순간 위치 맞추기 (튐 방지)
        Vector2 mousePos = Mouse.current.position.ReadValue();

        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            mousePos,
            canvas.worldCamera,
            out pos
        );

        targetImage.anchoredPosition = pos;
    }

    void OnDisable()
    {
        moveTween?.Kill();
    }
}
