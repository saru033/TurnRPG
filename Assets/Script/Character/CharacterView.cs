using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 캐릭터 프리팹 루트에 붙는 컴포넌트.
/// 일러스트, 체력바, 데미지 텍스트, 버프 패널을 관리.
///


public class CharacterView : MonoBehaviour
{
    [Header("References")]
    public Image illustration;
    public Image hpBar;       // pivot (0, 0.5), anchor middle-left
    public Image hpBarBg;     // 체력바 배경 (최대 너비 참조용)
    public TextMeshProUGUI damageText;
    public GameObject buffPanel;

    BattleCharacter _character;
    float _maxBarWidth;   // 체력 100%일 때 너비

    // -------------------------------------------------------
    // 초기화
    // -------------------------------------------------------
    public void Init(BattleCharacter character)
    {
        _character = character;

        // 배경 이미지의 너비를 최대 너비로 사용
        if (hpBarBg != null)
            _maxBarWidth = hpBarBg.rectTransform.rect.width;
        else if (hpBar != null)
            _maxBarWidth = hpBar.rectTransform.rect.width;

        UpdateHp();

        if (damageText != null)
            damageText.gameObject.SetActive(false);

        if (buffPanel != null)
            buffPanel.SetActive(false);
    }

    // -------------------------------------------------------
    // 체력 갱신
    // -------------------------------------------------------
    public void UpdateHp()
    {
        if (object.ReferenceEquals(_character, null)) return;
        if (hpBar == null) return;

        float ratio = Mathf.Clamp01(_character.CurrentHp / _character.MaxHp);
        var rt = hpBar.rectTransform;
        rt.sizeDelta = new Vector2(_maxBarWidth * ratio, rt.sizeDelta.y);
    }

    // -------------------------------------------------------
    // 데미지 텍스트 표시
    // -------------------------------------------------------
    public void ShowDamage(float damage, bool isCrit)
    {
        if (damageText == null) return;

        damageText.gameObject.SetActive(true);
        damageText.text = isCrit
            ? $"<b>{Mathf.RoundToInt(damage)}!</b>"
            : Mathf.RoundToInt(damage).ToString();
        damageText.color = isCrit ? Color.yellow : Color.white;

        // TODO: DOTween으로 위로 올라가며 사라지는 애니메이션
    }
}
