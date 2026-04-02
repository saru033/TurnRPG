using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;


/// <summary>
/// 행동게이지 바 위에서 캐릭터의 현재 게이지 위치를 나타내는 원형 아이콘.
/// GaugeBar의 자식으로 생성되며, BattleUI가 위치를 갱신한다.


public class GaugePortrait : MonoBehaviour
{
    [Header("References")]
    public Image portraitImage;
    public TextMeshProUGUI nameLabel;
    public Image highlightRing;

    [Header("Colors")]
    public Color playerColor = new Color(0.20f, 0.60f, 1.00f);
    public Color enemyColor = new Color(1.00f, 0.35f, 0.35f);
    public Color highlightColor = new Color(1.00f, 0.90f, 0.20f);

    // -------------------------------------------------------
    // 내부 상태 — BattleCharacter 참조를 직접 들고 있음
    // -------------------------------------------------------
    BattleCharacter _char;
    float _xOffset = 0f;
    bool _initialized = false;


    RectTransform _rt;
    public RectTransform RectTransform => _rt ??= GetComponent<RectTransform>();
    // -------------------------------------------------------
    // 초기화
    // -------------------------------------------------------
    public void SetXOffset(float x) => _xOffset = x;

    public void Init(BattleCharacter c)
    {
        _char = c;
        _initialized = true;

        // 앵커/피벗을 바 상단 기준으로 설정
        RectTransform.anchorMin = new Vector2(0.5f, 1f);
        RectTransform.anchorMax = new Vector2(0.5f, 1f);
        RectTransform.pivot = new Vector2(0.5f, 0.5f);

        if (nameLabel != null)
            nameLabel.text = c.Name.Length > 2 ? c.Name[..2] : c.Name;

        if (portraitImage != null)
            portraitImage.color = c.IsPlayer ? playerColor : enemyColor;

        SetHighlight(false);
    }

    // -------------------------------------------------------
    // 위치 갱신
    // -------------------------------------------------------
    public void UpdatePosition(RectTransform barRect, float time)
    {
        if (!_initialized)
        {
            return;
        }

        float gauge = _char.ActionGauge;
        float ratio = gauge / Actiongaugesystem.MaxGauge;
        float height = barRect.rect.height;


        if (height <= 0f) return;

        float posY = -ratio * height;
        var rt = GetComponent<RectTransform>();

        // 진행 중인 트윈 취소 후 새 트윈 시작
        rt.DOKill();
        rt.DOAnchorPosY(posY, time).SetEase(Ease.OutCubic);
    }

    // -------------------------------------------------------
    // 하이라이트
    // -------------------------------------------------------
    public void SetHighlight(bool on)
    {
        if (highlightRing != null)
        {
            highlightRing.enabled = on;
            if (on) highlightRing.color = highlightColor;
        }

        if (on) RectTransform.SetAsLastSibling();

        float targetScale = on ? 1.25f : 1.0f;
        transform.DOScale(Vector3.one * targetScale, 0.2f).SetEase(Ease.OutBack);
    }
}
