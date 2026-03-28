using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using TurnRPG.SkillSystem;

/// <summary>
/// 캐릭터 프리팹 루트에 붙는 컴포넌트.
/// 일러스트, 체력바, 데미지 텍스트, 버프 패널을 관리.
///


public class CharacterView : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    public Image illustration;
    public Image hpBar;       // pivot (0, 0.5), anchor middle-left
    public Image hpBarBg;     // 체력바 배경 (최대 너비 참조용)
    public TextMeshProUGUI damageText;
    public GameObject buffPanel;
    [Tooltip("상태이상(버프/디버프) 아이콘 프리팹. StatusEffectIcon 스크립트가 붙어있어야 함")]
    public GameObject statusEffectPrefab;

    BattleCharacter _character;
    Dictionary<StatusEffect, GameObject> _buffIcons = new Dictionary<StatusEffect, GameObject>();
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
            buffPanel.SetActive(true); // GridLayout 정렬을 위해 활성화 유지
            
        // 타겟팅 처리를 위해 Raycaster 충돌 허용 (Image 레이캐스트활성화 필요)
        if (illustration != null) illustration.raycastTarget = true;
            
        BattleEventManager.OnStatusEffectChanged += HandleStatusEffectChanged;
        BattleEventManager.OnDamageTaken += HandleDamageTaken;
        BattleEventManager.OnHealed += HandleHealed;
    }

    private void OnDestroy()
    {
        BattleEventManager.OnStatusEffectChanged -= HandleStatusEffectChanged;
        BattleEventManager.OnDamageTaken -= HandleDamageTaken;
        BattleEventManager.OnHealed -= HandleHealed;
    }

    private void HandleDamageTaken(BattleCharacter victim, BattleCharacter attacker, float damage, bool cannotBe, bool isEvaded)
    {
        if (victim != _character) return;
        
        UpdateHp();

        // 회피 시 텍스트 팝업
        if (isEvaded)
        {
            if (damageText != null)
            {
                damageText.gameObject.SetActive(true);
                damageText.text = "Miss!";
                damageText.color = Color.gray;
            }
        }
        else
        {
            // 우선 일반 데미지 연출 수행
            ShowDamage(damage, false);
        }
    }

    private void HandleHealed(BattleCharacter victim, float amount)
    {
        if (victim != _character) return;
        
        UpdateHp();
        
        if (damageText != null)
        {
            damageText.gameObject.SetActive(true);
            damageText.text = $"+{Mathf.RoundToInt(amount)}";
            damageText.color = Color.green;
        }
    }

    private void HandleStatusEffectChanged(BattleCharacter target, StatusEffect effect)
    {
        if (target != _character) return;

        // 효과 소유 중 (부여 또는 시간 갱신)
        if (target.ActiveStatusEffects.Contains(effect))
        {
            if (_buffIcons.TryGetValue(effect, out var go))
            {
                var iconScript = go.GetComponent<StatusEffectIcon>();
                if (iconScript != null) iconScript.UpdateUI();
            }
            else if (statusEffectPrefab != null && buffPanel != null)
            {
                var newGo = Instantiate(statusEffectPrefab, buffPanel.transform);
                var iconScript = newGo.GetComponent<StatusEffectIcon>();
                if (iconScript != null) iconScript.Init(effect);
                _buffIcons[effect] = newGo;
            }
        }
        else // 만료 또는 해제됨
        {
            if (_buffIcons.TryGetValue(effect, out var go))
            {
                Destroy(go);
                _buffIcons.Remove(effect);
            }
        }
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

        StopCoroutine(nameof(FadeOutDamageText));
        StartCoroutine(nameof(FadeOutDamageText));
    }

    private System.Collections.IEnumerator FadeOutDamageText()
    {
        yield return new WaitForSeconds(1.5f);
        if (damageText != null) damageText.gameObject.SetActive(false);
    }

    // -------------------------------------------------------
    // UI 직접 지목 클릭 이벤트
    // -------------------------------------------------------
    public void OnPointerClick(PointerEventData eventData)
    {
        if (BattleManager.Instance != null && _character != null)
        {
            BattleManager.Instance.OnCharacterClicked(_character);
        }
    }
}
