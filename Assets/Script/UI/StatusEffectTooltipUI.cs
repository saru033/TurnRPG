using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TurnRPG.SkillSystem;

/// <summary>
/// 버프/디버프 아이콘을 꾹 눌렀을 때 나타나는 상태 효과 상세 툴팁을 관리합니다.
/// </summary>
public class StatusEffectTooltipUI : MonoBehaviour
{
    [Header("UI References")]
    public Image statusIcon;          // 효과 아이콘
    public TextMeshProUGUI statusName;   // 효과 이름
    public TextMeshProUGUI statusDesc;   // 효과 설명
    public Image panelBg;             // 전체 배경 패널

    [Header("Buff Style")]
    public Color buffPanelColor = new Color(0.1f, 0.2f, 0.4f, 0.9f); // 푸른 색 계열
    public Color buffTextColor = Color.white;

    [Header("Debuff Style")]
    public Color debuffPanelColor = new Color(0.4f, 0.1f, 0.1f, 0.9f); // 붉은 색 계열
    public Color debuffTextColor = Color.white;

    private RectTransform _rect;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    /// <summary>
    /// 상태 효과 데이터를 기반으로 정보를 세팅하고 스타일을 변경합니다.
    /// </summary>
    public void SetData(StatusEffect effect)
    {
        if (effect == null || effect.Data == null) return;

        var data = effect.Data;

        // 1. 기본 텍스트 및 아이콘 세팅
        if (statusIcon != null) statusIcon.sprite = data.Icon;
        if (statusName != null) statusName.text = data.EffectName;
        if (statusDesc != null) statusDesc.text = data.Description;

        // 2. 카테고리에 따른 색상 변경
        bool isBuff = (data.Category == StatusEffectCategory.Buff);
        
        if (panelBg != null)
        {
            panelBg.color = isBuff ? buffPanelColor : debuffPanelColor;
        }

        Color targetTextColor = isBuff ? buffTextColor : debuffTextColor;
        if (statusName != null) statusName.color = targetTextColor;
        if (statusDesc != null) statusDesc.color = targetTextColor;
    }

    /// <summary>
    /// 툴팁의 위치를 지정된 월드 좌표로 이동시킵니다.
    /// </summary>
    public void SetPosition(Vector3 worldPos, float yOffset, float canvasScale)
    {
        if (_rect == null) _rect = GetComponent<RectTransform>();

        // 월드 좌표에서 y축 방향으로 살짝 띄우기 (해상도 스케일 보정)
        transform.position = worldPos + new Vector3(0, yOffset * canvasScale, 0);
    }
}
