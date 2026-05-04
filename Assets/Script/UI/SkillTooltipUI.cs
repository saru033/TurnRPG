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
        SetDataWithComparison(skill, level, null, -1);
    }

    /// <summary>
    /// 원본 스킬과 비교하여 달라진 부분만 노란색으로 표시하며 데이터를 채웁니다.
    /// </summary>
    public void SetDataWithComparison(SkillData skill, int level, SkillData originalSkill, int originalLevel)
    {
        if (skill == null) return;

        bool isComparison = originalSkill != null && originalLevel > 0;

        // 1. 이름
        string nameStr = skill.SkillName;
        if (isComparison && nameStr != originalSkill.SkillName)
            nameStr = $"<color=#FFFF00>{nameStr}</color>";
        if (skillname != null) skillname.text = nameStr;

        // 2. 스킬 타입
        string typeStr = GetSkillTypeString(skill);
        if (isComparison && typeStr != GetSkillTypeString(originalSkill))
            typeStr = $"<color=#FFFF00>{typeStr}</color>";
        if (skilltype != null) skilltype.text = typeStr;

        if (skillimg != null) skillimg.sprite = skill.SkillIcon;

        // 3. 레벨 데이터 (쿨타임, 설명)
        var levelData = (skill.LevelDatas != null && skill.LevelDatas.Count >= level)
            ? skill.LevelDatas[level - 1] : null;
        var origLevelData = (isComparison && originalSkill.LevelDatas != null && originalSkill.LevelDatas.Count >= originalLevel)
            ? originalSkill.LevelDatas[originalLevel - 1] : null;

        if (levelData != null)
        {
            // 쿨타임
            string cdVal = levelData.Cooldown.ToString();
            if (isComparison && origLevelData != null && levelData.Cooldown != origLevelData.Cooldown)
                cdVal = $"<color=#FFFF00>{cdVal}</color>";
            
            if (cooldown != null) 
            {
                if (skill.Type == SkillType.Item) cooldown.text = "";
                else cooldown.text = $"쿨타임 : {cdVal}";
            }

            // 설명
            string descStr = levelData.Description;
            if (isComparison && origLevelData != null && descStr != origLevelData.Description)
                descStr = GetHighlightedDescription(origLevelData.Description, descStr);
            
            if (skillexp != null) skillexp.text = descStr;
        }
        else
        {
            if (cooldown != null) cooldown.text = "-";
            if (skillexp != null) skillexp.text = "설명 정보 없음";
        }
    }

    private string GetSkillTypeString(SkillData skill)
    {
        return skill.Type switch
        {
            SkillType.Attack => "공격 스킬",
            SkillType.NonAttack => "공격이 아닌 스킬",
            SkillType.Passive => "패시브",
            SkillType.Item => "아이템",
            _ => "???",
        };
    }

    private string GetHighlightedDescription(string original, string upgraded)
    {
        if (string.IsNullOrEmpty(original)) return $"<color=#FFFF00>{upgraded}</color>";
        if (original == upgraded) return upgraded;

        string[] oldWords = original.Split(' ');
        string[] newWords = upgraded.Split(' ');

        int prefixLen = 0;
        int minLen = Mathf.Min(oldWords.Length, newWords.Length);

        // 1. 앞에서부터 일치하는 단어 개수 찾기
        while (prefixLen < minLen && oldWords[prefixLen] == newWords[prefixLen])
        {
            prefixLen++;
        }

        // 2. 뒤에서부터 일치하는 단어 개수 찾기
        int suffixLen = 0;
        int maxSuffixLen = minLen - prefixLen;
        while (suffixLen < maxSuffixLen && 
               oldWords[oldWords.Length - 1 - suffixLen] == newWords[newWords.Length - 1 - suffixLen])
        {
            suffixLen++;
        }

        // 3. 결과 조합
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // 공통 접두사
        for (int i = 0; i < prefixLen; i++)
        {
            sb.Append(newWords[i]);
            sb.Append(" ");
        }

        // 달라진 중간 부분 (강조)
        if (prefixLen < newWords.Length - suffixLen)
        {
            sb.Append("<color=#FFFF00>");
            for (int i = prefixLen; i < newWords.Length - suffixLen; i++)
            {
                sb.Append(newWords[i]);
                if (i < newWords.Length - suffixLen - 1) sb.Append(" ");
            }
            sb.Append("</color>");
            if (suffixLen > 0) sb.Append(" ");
        }

        // 공통 접미사
        for (int i = newWords.Length - suffixLen; i < newWords.Length; i++)
        {
            sb.Append(newWords[i]);
            if (i < newWords.Length - 1) sb.Append(" ");
        }

        return sb.ToString().Trim();
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
