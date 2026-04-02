using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using TurnRPG.SkillSystem;
using DG.Tweening;

/// <summary>
/// 캐릭터 프리팹 루트에 붙는 컴포넌트.
/// 일러스트, 체력바, 데미지 텍스트, 버프 패널을 관리.
///


public class CharacterView : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    public Image illustration;
    public Image hitbox;      // 유저가 따로 만든 히트박스 전용 이미지
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

        // 프리팹에서 설정한 최초 길이를 100% 기준으로 사용 (배경 이미지에 맞추지 않음)
        if (hpBar != null)
        {
            _maxBarWidth = hpBar.rectTransform.rect.width;
            // 찌그러짐 방지 (필드 타입 설정 유도)
            if (hpBar.type != Image.Type.Filled)
            {
                // 실시간으로 설정을 바꾸면 에디터에서 보이지 않을 수 있으므로 경고만 표시하거나
                // 필요 시 코드에서 강제 설정 (유저가 직접 조절한다고 했으므로 주석 처리 가능)
                // hpBar.type = Image.Type.Filled;
                // hpBar.fillMethod = Image.FillMethod.Horizontal;
            }
        }

        UpdateHp();

        if (damageText != null)
            damageText.gameObject.SetActive(false);

        if (buffPanel != null)
            buffPanel.SetActive(true); // GridLayout 정렬을 위해 활성화 유지

        // 히트박스 방식 적용 (기존 일러스트는 클릭 무시)
        if (hitbox != null)
        {
            hitbox.raycastTarget = true;
            if (illustration != null) illustration.raycastTarget = false;
        }
        else if (illustration != null)
        {
            // 히트박스가 할당되지 않았을 때만 일러스트를 히트박스로 사용
            illustration.raycastTarget = true;
        }

        BattleEventManager.OnStatusEffectChanged += HandleStatusEffectChanged;
        BattleEventManager.OnStatusEffectApplied += HandleStatusEffectApplied; // 신규 효과 부여 이벤트 구독
        BattleEventManager.OnDamageTaken += HandleDamageTaken;
        BattleEventManager.OnHealed += HandleHealed;
    }

    private void OnDestroy()
    {
        BattleEventManager.OnStatusEffectChanged -= HandleStatusEffectChanged;
        BattleEventManager.OnStatusEffectApplied -= HandleStatusEffectApplied;
        BattleEventManager.OnDamageTaken -= HandleDamageTaken;
        BattleEventManager.OnHealed -= HandleHealed;
    }

    private void HandleDamageTaken(BattleCharacter victim, BattleCharacter attacker, float damage, bool cannotBeCountered, bool isEvaded, bool isCritical)
    {
        if (victim != _character) return;

        UpdateHp();

        // 회피 여부에 상관없이 항상 데미지 연출 수행 (isEvaded 전달)
        ShowDamage(damage, isCritical, isEvaded);

        if (!isEvaded)
        {
            // [추가] 시전자가 있는 직접 타격인 경우 공용 타격 VFX 생성
            if (damage > 0 && attacker != null && BattleVFXManager.Instance != null)
            {
                // 히트박스가 있으면 히트박스 위치에, 없으면 일러스트나 루트 위치에 생성
                Transform spawnTarget = hitbox != null ? hitbox.transform : (illustration != null ? illustration.transform : this.transform);
                BattleVFXManager.Instance.SpawnVFX(VFXType.Hit, spawnTarget, _character.IsPlayer);
                Debug.Log($"[VFX] Hit VFX spawned on {_character.Name}");
            }
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

    private void HandleStatusEffectApplied(BattleCharacter target, StatusEffect effect)
    {
        if (target != _character || BattleVFXManager.Instance == null) return;

        // 히트박스가 있으면 히트박스 위치에 생성
        Transform spawnTarget = hitbox != null ? hitbox.transform : (illustration != null ? illustration.transform : this.transform);

        // 효과의 카테고리에 맞춰 공용 VFX 생성
        if (effect.Category == StatusEffectCategory.Buff)
        {
            BattleVFXManager.Instance.SpawnVFX(VFXType.Buff, spawnTarget);
            Debug.Log($"[VFX] Buff VFX spawned on {_character.Name} ({effect.Type})");
        }
        else if (effect.Category == StatusEffectCategory.Debuff)
        {
            BattleVFXManager.Instance.SpawnVFX(VFXType.Debuff, spawnTarget);
            Debug.Log($"[VFX] Debuff VFX spawned on {_character.Name} ({effect.Type})");
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

        // DOTween을 사용하여 부드럽게 게이지 변화 (0.3초 동안)
        hpBar.DOKill(); // 이전 트윈이 진행 중이면 멈춤
        hpBar.DOFillAmount(ratio, 0.3f).SetEase(Ease.OutCubic);

        // 만약 기존처럼 너비 조절이 필요하다면 아래 주석 해제 (단, 이미지가 찌그러짐)
        // var rt = hpBar.rectTransform;
        // rt.sizeDelta = new Vector2(_maxBarWidth * ratio, rt.sizeDelta.y);
    }

    // -------------------------------------------------------
    // 데미지 텍스트 표시
    // -------------------------------------------------------
    public void ShowDamage(float damage, bool isCrit, bool isEvaded = false)
    {
        if (damageText == null) return;

        damageText.gameObject.SetActive(true);
        string dmgStr = isCrit ? $"<b>{Mathf.RoundToInt(damage)}!</b>" : Mathf.RoundToInt(damage).ToString();
        damageText.text = isEvaded ? $"Miss! {dmgStr}" : dmgStr;

        if (isEvaded)
        {
            damageText.color = Color.gray;
        }
        else
        {
            damageText.color = isCrit ? Color.yellow : Color.white;
        }

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

    // -------------------------------------------------------
    // 시각 효과 - 피격 컬러 플래시 (애니메이션 이벤트 연동)
    // -------------------------------------------------------
    public void OnHitFlash()
    {
        if (illustration == null) return;

        // 이전 연출이 진행 중이라면 즉시 중단 및 초기화
        illustration.DOKill();
        illustration.color = Color.white;

        // Sequence를 사용하여 빨강 -> 하양 -> 원래색으로 번쩍이는 효과
        Sequence seq = DOTween.Sequence();
        seq.Append(illustration.DOColor(Color.red, 0.05f));
        seq.Append(illustration.DOColor(Color.white, 0.05f));
        seq.Append(illustration.DOColor(Color.white, 0.1f)); // 원래색(하양)으로 복구

        seq.Play();
    }


    // 외부에서 피격 위치를 가져오기 위한 함수
    public Transform RetHitbox(){
        Transform spawnTarget = hitbox != null ? hitbox.transform : (illustration != null ? illustration.transform : this.transform);
        return spawnTarget;
    }
}
