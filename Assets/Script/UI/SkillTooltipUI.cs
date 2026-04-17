using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TurnRPG.SkillSystem;

/// <summary>
/// 스킬의 상세 정보를 표시하는 툴팁 패널을 관리합니다.
/// </summary>
public class SkillTooltipUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI skillname;
    public TextMeshProUGUI skilltype;
    public TextMeshProUGUI cooldown;
    public TextMeshProUGUI skillexp;
    public Image skillimg;

    private RectTransform _rect;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    /// <summary>
    /// 스킬 데이터를 바탕으로 툴팁 내용을 채우고 표시합니다.
    /// </summary>
    public void SetData(SkillData skill, int level)
    {
        if (skill == null) return;

        if (skillname != null) skillname.text = skill.SkillName;

        if (skilltype != null)
        {
            // 스킬 타입 (공격, 비공격, 패시브 등) 표시
            string typeStr = skill.Type switch
            {
                SkillType.Attack => "공격 스킬",
                SkillType.NonAttack => "공격이 아닌 스킬",
                SkillType.Passive => "패시브",
                SkillType.Item => "아이템",
                _ => "???",
            };
            skilltype.text = typeStr;
        }

        if (skillimg != null) skillimg.sprite = skill.SkillIcon;

        // 레벨 데이터 기반 쿨타임 및 설명
        var levelData = (skill.LevelDatas != null && skill.LevelDatas.Count >= level)
            ? skill.LevelDatas[level - 1] : null;

        if (levelData != null)
        {
            if (cooldown != null) cooldown.text = $"쿨타임 : {levelData.Cooldown}";
            if (skillexp != null) skillexp.text = levelData.Description;
        }
        else
        {
            if (cooldown != null) cooldown.text = "-";
            if (skillexp != null) skillexp.text = "설명 정보 없음";
        }

        // 아이템의 경우 쿨타임 표기x
        if (skill.Type == SkillType.Item)
        {
            if (cooldown != null) cooldown.text = "";
        }
    }

    /// <summary>
    /// 툴팁의 위치를 지정된 위치로 이동시킵니다.
    /// </summary>
    public void SetPosition(Vector2 anchoredPos)
    {
        if (_rect == null) _rect = GetComponent<RectTransform>();
        _rect.anchoredPosition = anchoredPos;
    }
}
