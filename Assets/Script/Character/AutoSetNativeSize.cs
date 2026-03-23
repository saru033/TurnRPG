using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Image))]
public class AutoSetNativeSize : MonoBehaviour
{
    [Range(0.1f, 1.5f)]
    public float heightRatio = 0.8f;

    private Image _image;
    private Animator _animator;
    private RectTransform _rootRect;

    private int _lastAnimStateHash = -1;
    private Sprite _prevFrameSprite;

    private void Awake()
    {
        _image = GetComponent<Image>();
        if (_image != null) _image.preserveAspect = true;

        _animator = GetComponentInParent<Animator>();

        CharacterView view = GetComponentInParent<CharacterView>();
        _rootRect = view != null ? view.GetComponent<RectTransform>() : transform.parent as RectTransform;
    }

    private void LateUpdate()
    {
        if (_image == null || _image.sprite == null || _rootRect == null) return;

        if (_animator != null)
        {
            // Transition 도중을 제외한, 실제로 현재 진입한 애니메이션 상태 확인
            int currentState = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

            // 애니메이션 상태가 바뀌었을 때
            if (currentState != _lastAnimStateHash)
            {
                // 실행 타이밍으로 인해 이전 애니메이션의 마지막 스프라이트가 남아있는 것을 방지합니다.
                // 바로 직전 프레임의 스프라이트(_prevFrameSprite)와 달라지는 순간 (=새 애니메이션 첫 프레임)을 기다립니다!
                if (_image.sprite != _prevFrameSprite || _prevFrameSprite == null)
                {
                    _lastAnimStateHash = currentState;
                    ApplySizeBasedOnSprite(_image.sprite);
                }
            }
        }
        else if (_lastAnimStateHash == -1)
        {
            // Animator가 없을 경우를 대비해 맨 처음 한 번만 실행
            _lastAnimStateHash = 0;
            ApplySizeBasedOnSprite(_image.sprite);
        }

        // 매 업데이트의 마지막에 현재 렌더링된 스프라이트를 기억해 둡니다.
        _prevFrameSprite = _image.sprite;
    }

    private void ApplySizeBasedOnSprite(Sprite sprite)
    {
        if (sprite == null) return;

        int minY, maxY;
        GetVisibleYBounds(sprite, out minY, out maxY);

        if (minY > maxY) return; // 이미지에 그릴 내용이 전혀 없는 경우

        // 1. 투명 배경을 제외한 실제 그림의 높이 (visibleHeight)
        float visibleHeight = (maxY - minY + 1);

        // 2. 부모 객체의 현재 높이 (parentHeight) 가져오기
        float parentHeight = _rootRect.rect.height;
        
        // 3. 부모 객체 높이에서 heightRatio(80%) 비율만큼 차지할 '목표 높이' (targetHeight) 계산
        float targetHeight = parentHeight * heightRatio;

        // 4. 실제 그림(visibleHeight)을 목표 높이(targetHeight)로 맞추기 위해 곱해야 하는 '확대/축소 배율' (scaleRatio)
        float scaleRatio = targetHeight / visibleHeight;

        // 5. 원본 스프라이트 자체의 전체 높이에 도출된 확대 스케일을 적용
        float newHeight = sprite.rect.height * scaleRatio;
        
        // 6. 가로가 더 긴 이미지의 경우 잘리지 않도록 비례적으로 폭 설정
        float newWidth = newHeight * 2f; 

        _image.rectTransform.sizeDelta = new Vector2(newWidth, newHeight);

        // [추가됨] 발 끝을 부모의 맨 아랫단에 맞추는 posY 조정 로직
        // 원본 텍스처에서 하단 투명 여백(minY)이 전체 높이에서 차지하는 비율을 구합니다.
        float bottomPaddingRatio = (float)minY / sprite.rect.height;
        
        // 이 비율을 화면상 UI의 전체 높이(newHeight)에 곱하면, 화면에서 실제 비어있는 여백의 픽셀 크기가 나옵니다.
        float uiBottomPadding = newHeight * bottomPaddingRatio;

        // 계산을 쉽고 안전하게 하기 위해, Y축 앵커와 피벗을 가장 바닥(0)으로 고정합니다 (부모 기준 하단 정렬).
        _image.rectTransform.anchorMin = new Vector2(_image.rectTransform.anchorMin.x, 0f);
        _image.rectTransform.anchorMax = new Vector2(_image.rectTransform.anchorMax.x, 0f);
        _image.rectTransform.pivot = new Vector2(_image.rectTransform.pivot.x, 0f);

        // 계산된 하단 투명 여백만큼 컴포넌트를 아래로 끌어내리면(-), 실제 그림의 맨 밑바닥(발끝)이 부모의 Y=0 선에 완벽히 닿게 됩니다!
        _image.rectTransform.anchoredPosition = new Vector2(_image.rectTransform.anchoredPosition.x, -uiBottomPadding);

        // 사이즈가 바뀌자마자 1프레임 튀어보이지 않게 유니티 UI 컴포넌트들을 강제로 렌더 업데이트
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rootRect);

        // 정밀 디버깅 로그
        Debug.Log($"[{sprite.name}] 그림높이:{visibleHeight}px | 80%목표:{targetHeight}px | UI전체:{newHeight}px | 발끝내림: -{uiBottomPadding}px");
    }

    private void GetVisibleYBounds(Sprite sprite, out int minY, out int maxY)
    {
        Texture2D tex = sprite.texture;
        Rect rect = sprite.textureRect;

        int width = (int)rect.width;
        int height = (int)rect.height;

        Color[] pixels = tex.GetPixels(
            (int)rect.x,
            (int)rect.y,
            width,
            height
        );

        minY = height;
        maxY = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = pixels[y * width + x];

                // 알파값이 0.1보다 크면 그림 영역으로 취급 (기존 0.01은 압축 노이즈로 찌꺼기가 잡힐 수 있음)
                if (pixel.a > 0.1f)
                {
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (minY > maxY)
        {
            minY = 0;
            maxY = 0;
        }
    }
}