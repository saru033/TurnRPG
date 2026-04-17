using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 2D 범용 이펙트 컨트롤러 (UI Image 기반)
/// OneShot  : 한번만 재생 후 비활성화
/// Looping  : Stop() 호출 전까지 계속 재생
/// </summary>


public class EffectController : MonoBehaviour
{
    public enum EffectType { OneShot, Looping }

    [Header("# 이펙트 타입")]
    [SerializeField] private EffectType effectType = EffectType.OneShot;

    // ─────────────────────────────────────────
    [Header("# OneShot 설정")]
    [Tooltip("페이드 인 + 스케일 업 시간 (n초)")]
    [SerializeField] private float fadeInDuration = 0.3f;

    [Tooltip("유지 시간 (k초)")]
    [SerializeField] private float holdDuration = 0.5f;

    [Tooltip("페이드 아웃 시간 (t초)  ※ 사이즈 변화 없음")]
    [SerializeField] private float fadeOutDuration = 0.3f;

    // ─────────────────────────────────────────
    [Header("# Looping 설정")]
    [Tooltip("페이드 인 시간 (z초)")]
    [SerializeField] private float loopFadeInDuration = 0.3f;

    [Tooltip("종료 시 페이드 아웃 시간 (c초)")]
    [SerializeField] private float loopFadeOutDuration = 0.5f;

    [Tooltip("회전 속도 (도/초, 시계방향 = 음수)")]
    [SerializeField] private float rotationSpeed = -10f;

    // ─────────────────────────────────────────
    [Header("# 공통 설정")]
    [Tooltip("목표 알파값 (0~1)")]
    [SerializeField] private float targetAlpha = 1f;

    [Tooltip("페이드 인 Ease")]
    [SerializeField] private Ease fadeInEase = Ease.OutQuad;

    [Tooltip("페이드 아웃 Ease")]
    [SerializeField] private Ease fadeOutEase = Ease.InQuad;

    // ─────────────────────────────────────────
    private Image[] images;
    private Vector3 originalScale;
    private Sequence currentSequence;
    private Tween rotateTween;
    private bool isPlaying = false;

    // ─────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        // 자기 자신 + 자식 오브젝트 모두 탐색
        images = GetComponentsInChildren<Image>(includeInactive: true);
        if (images.Length == 0)
        {
            Debug.LogError($"[EffectController] Image 컴포넌트가 하나도 없습니다: {gameObject.name}");
        }
    }

    private void OnEnable()
    {
        Play();
    }

    private void OnDisable()
    {
        KillAllTweens();
    }

    #endregion

    // ─────────────────────────────────────────
    #region Public API

    /// <summary>
    /// 이펙트 재생 (OnEnable에서 자동 호출됨)
    /// </summary>
    public void Play()
    {
        if (isPlaying) return;

        KillAllTweens();
        CalculateSize();  // 부모 높이 기준으로 크기 계산
        ResetState();
        isPlaying = true;

        if (effectType == EffectType.OneShot)
            PlayOneShot();
        else
            PlayLooping();
    }

    /// <summary>
    /// Looping 이펙트 종료 함수 (외부에서 호출)
    /// OneShot에서 호출해도 무시됩니다.
    /// </summary>
    public void Stop()
    {
        if (effectType != EffectType.Looping || !isPlaying) return;

        StopLooping();
    }

    #endregion

    // ─────────────────────────────────────────
    #region OneShot

    private void PlayOneShot()
    {
        // 시작 상태: 투명 + 스케일 0
        SetAlpha(0f);
        transform.localScale = Vector3.zero;

        currentSequence = DOTween.Sequence();

        // 1단계: 페이드 인 + 스케일 업 (n초)
        currentSequence
            .Append(transform.DOScale(originalScale, fadeInDuration).SetEase(fadeInEase));

        foreach (Image img in images)
        {
            Color fadeIn = new Color(img.color.r, img.color.g, img.color.b, targetAlpha);
            currentSequence.Join(img.DOColor(fadeIn, fadeInDuration).SetEase(fadeInEase));
        }

        // 2단계: 유지 (k초)
        currentSequence.AppendInterval(holdDuration);

        // 3단계: 페이드 아웃만 (t초, 사이즈 변화 없음)
        bool firstFadeOut = true;
        foreach (Image img in images)
        {
            Color fadeOut = new Color(img.color.r, img.color.g, img.color.b, 0f);
            if (firstFadeOut)
            {
                currentSequence.Append(img.DOColor(fadeOut, fadeOutDuration).SetEase(fadeOutEase));
                firstFadeOut = false;
            }
            else
            {
                currentSequence.Join(img.DOColor(fadeOut, fadeOutDuration).SetEase(fadeOutEase));
            }
        }

        // 완료 후 비활성화
        currentSequence.OnComplete(Deactivate);
    }

    #endregion

    // ─────────────────────────────────────────
    #region Looping

    private void PlayLooping()
    {
        // 시작 상태: 투명
        SetAlpha(0f);

        currentSequence = DOTween.Sequence();

        // 1단계: 페이드 인 (z초) - 첫 번째 Image로 Append, 나머지는 Join
        bool first = true;
        foreach (Image img in images)
        {
            Color fadeIn = new Color(img.color.r, img.color.g, img.color.b, targetAlpha);
            if (first)
            {
                currentSequence.Append(img.DOColor(fadeIn, loopFadeInDuration).SetEase(fadeInEase));
                first = false;
            }
            else
            {
                currentSequence.Join(img.DOColor(fadeIn, loopFadeInDuration).SetEase(fadeInEase));
            }
        }

        // 페이드 인 완료 후 회전 시작
        currentSequence.AppendCallback(StartRotation);
    }

    private void StartRotation()
    {
        // 시계방향 무한 회전 (rotationSpeed가 음수면 시계방향)
        rotateTween = transform
            .DORotate(new Vector3(0f, 0f, rotationSpeed), 1f, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental);
    }

    private void StopLooping()
    {
        rotateTween?.Kill();
        rotateTween = null;

        currentSequence = DOTween.Sequence();

        // 페이드 아웃 (c초)
        bool first = true;
        foreach (Image img in images)
        {
            Color fadeOut = new Color(img.color.r, img.color.g, img.color.b, 0f);
            if (first)
            {
                currentSequence.Append(img.DOColor(fadeOut, loopFadeOutDuration).SetEase(fadeOutEase));
                first = false;
            }
            else
            {
                currentSequence.Join(img.DOColor(fadeOut, loopFadeOutDuration).SetEase(fadeOutEase));
            }
        }

        currentSequence.OnComplete(Deactivate);
    }

    #endregion

    // ─────────────────────────────────────────
    #region Helpers

    private void CalculateSize()
    {
        if (transform.parent != null)
        {
            RectTransform parentRect = transform.parent.GetComponent<RectTransform>();
            RectTransform myRect = GetComponent<RectTransform>();

            if (parentRect != null && myRect != null)
            {
                float parentHeight = parentRect.rect.height;
                myRect.sizeDelta = new Vector2(parentHeight, parentHeight);
            }
            else
            {
                Debug.LogWarning($"[EffectController] 부모 또는 자신에 RectTransform이 없습니다: {gameObject.name}");
            }
        }

        // 크기 조정 후 기준 스케일 저장
        originalScale = transform.localScale;
    }

    private void ResetState()
    {
        transform.localPosition = Vector3.zero;
        transform.localScale = originalScale;
        transform.rotation = Quaternion.identity;
        SetAlpha(0f);
    }

    private void SetAlpha(float alpha)
    {
        foreach (Image img in images)
        {
            if (img == null) continue;
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }

    private void KillAllTweens()
    {
        currentSequence?.Kill();
        rotateTween?.Kill();
        currentSequence = null;
        rotateTween = null;
        isPlaying = false;
    }

    /// <summary>
    /// 이펙트 즉시 종료 및 파괴
    /// </summary>
    public void Deactivate()
    {
        isPlaying = false;
        //gameObject.SetActive(false);
        // TODO: 추후 Prefab Pool 사용 시 아래로 교체
        Destroy(gameObject);
    }

    #endregion
}