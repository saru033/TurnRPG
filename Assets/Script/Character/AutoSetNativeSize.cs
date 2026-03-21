using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 애니메이터가 UI Image의 Sprite를 교체할 때마다 원래 이미지의 비율(Aspect Ratio)을 유지하면서,
/// 캐릭터 프리팹 루트(CharacterView) 높이의 특정 비율(기본 80%)에 맞춰 크기를 조절하는 컴포넌트입니다.
/// </summary>
[RequireComponent(typeof(Image))]
public class AutoSetNativeSize : MonoBehaviour
{
    [Tooltip("프리팹 전체 크기(높이) 대비 출력할 일러스트 높이 비율 (예: 0.8 = 80%)")]
    [Range(0.1f, 1.5f)]
    public float heightRatio = 0.8f;

    private Image _image;
    private Sprite _lastSprite;
    private RectTransform _rootRect;

    private void Awake()
    {
        _image = GetComponent<Image>();
        
        // 캐릭터 루트(CharacterView가 붙은 최상단 객체)의 RectTransform을 찾습니다.
        CharacterView view = GetComponentInParent<CharacterView>();
        if (view != null)
        {
            _rootRect = view.GetComponent<RectTransform>();
        }
        else
        {
            // 찾지 못했다면 바로 위 부모를 기준점으로 삼습니다.
            _rootRect = transform.parent as RectTransform;
        }

        if (_image != null)
        {
            _lastSprite = _image.sprite;
            UpdateSize();
        }
    }

    private void LateUpdate()
    {
        // 렌더링 직전(LateUpdate)에 스프라이트가 바뀌었는지 검사
        if (_image != null && _image.sprite != _lastSprite)
        {
            _lastSprite = _image.sprite;
            UpdateSize();
        }
    }

    private void UpdateSize()
    {
        if (_lastSprite == null || _rootRect == null) return;

        // 원본 이미지의 가로/세로 픽셀 사이즈
        float nativeWidth = _lastSprite.rect.width;
        float nativeHeight = _lastSprite.rect.height;

        if (nativeHeight <= 0) return;

        // 원본 종횡비(Aspect Ratio) 계산
        float aspect = nativeWidth / nativeHeight;

        // 기준(캐릭터 프리팹 루트)의 높이의 heightRatio(80%) 만큼으로 대상 높이를 설정
        float targetHeight = _rootRect.rect.height * heightRatio;
        
        // 원본 비율에 맞춰 계산된 목표 너비 할당
        float targetWidth = targetHeight * aspect;

        // Image 컴포넌트의 RectTransform 사이즈를 명시적으로 조절하여 찌그러짐 방지 & 크기 제한
        _image.rectTransform.sizeDelta = new Vector2(targetWidth, targetHeight);
    }
}
