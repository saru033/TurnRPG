using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

public class hand : MonoBehaviour
{
    public RectTransform targetImage;
    public Canvas canvas;

    public float duration = 0.05f; // 부드러움 정도 (Update로 옮기며 약간 축소)

    private Tween moveTween;

    void Awake()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        
        // [주의] 아래 로직은 이미지를 화면 높이만큼 크게 만듭니다. 
        // 마우스 커서용이라면 부적절할 수 있어 일단 주석 처리하거나 검토가 필요합니다.
        /*
        if (canvas != null && targetImage != null)
        {
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            float size = canvasRect.rect.height;
            targetImage.sizeDelta = new Vector2(size, size);
        }
        */
    }

    void Update() // UI 추적은 FixedUpdate보다 Update가 더 적합합니다.
    {
        if (targetImage == null || canvas == null || Mouse.current == null) return;

        // 새 Input System 마우스 위치
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // UI 좌표 변환 (RenderMode에 따른 카메라 처리)
        Vector2 pos;
        Camera cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            mousePos,
            cam,
            out pos
        ))
        {
            // 매 프레임 Kill 후 재생하는 것보다 목적지를 갱신하는 것이 좋지만, 
            // DOTween 구조상 매 프레임 호출 시에는 즉시 반영되도록 최적화합니다.
            moveTween?.Kill();
            moveTween = targetImage.DOAnchorPos(pos, duration).SetEase(Ease.OutQuad);
        }
    }

    void OnEnable()
    {
        UpdatePositionImmediate();
    }

    void OnDisable()
    {
        moveTween?.Kill();
    }

    private void UpdatePositionImmediate()
    {
        if (targetImage == null || canvas == null || Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 pos;
        Camera cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            mousePos,
            cam,
            out pos
        ))
        {
            targetImage.anchoredPosition = pos;
        }
    }
}
