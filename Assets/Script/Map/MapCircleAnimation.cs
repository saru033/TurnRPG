using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 맵 노드 클릭 시 생성되어 빨간 동그라미가 그려지는 연출을 담당하는 스크립트.
/// Image Type이 Filled, Method가 Radial 360인 컴포넌트가 필요합니다.
/// </summary>
public class MapCircleAnimation : MonoBehaviour
{
    [Tooltip("동그라미가 그려지는 시간(초)")]
    public float duration = 0.5f;

    private Image _image;

    void Awake()
    {
        _image = GetComponent<Image>();
        if (_image != null)
        {
            _image.fillAmount = 0f;
        }
    }

    void Start()
    {
        if (_image != null)
        {
            // 0에서 1까지 부드럽게 채워지는 연출
            _image.DOFillAmount(1f, duration).SetEase(Ease.Linear);
        }
    }
}
