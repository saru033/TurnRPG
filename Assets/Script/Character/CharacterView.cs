using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using TurnRPG.SkillSystem;
using DG.Tweening;
using System.Linq; // [추가] LINQ 확장 메서드 사용

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
    public Image shieldBar;   // [추가] 보호막 바 (hpBar 뒤에 배치 권장)
    public TextMeshProUGUI damageText;
    public GameObject buffPanel;
    [Tooltip("상태이상(버프/디버프) 아이콘 프리팹. StatusEffectIcon 스크립트가 붙어있어야 함")]
    public GameObject statusEffectPrefab;

    BattleCharacter _character;
    Dictionary<StatusEffect, GameObject> _buffIcons = new Dictionary<StatusEffect, GameObject>();
    Dictionary<StatusEffect, GameObject> _vfxInstances = new Dictionary<StatusEffect, GameObject>(); // [추가] VFX 인스턴스 관리
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

        // [추가] 관리 중인 모든 VFX 정리
        foreach (var vfx in _vfxInstances.Values)
        {
            if (vfx != null) Destroy(vfx);
        }
        _vfxInstances.Clear();
    }

    private void HandleDamageTaken(BattleCharacter victim, BattleCharacter attacker, float damage, bool cannotBeCountered, bool isEvaded, bool isCritical, bool isItem)
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
            damageText.transform.SetAsLastSibling(); // 레이어 최상단으로 이동
            damageText.gameObject.SetActive(true);
            damageText.text = $"+{Mathf.RoundToInt(amount)}";
            damageText.color = Color.green;

            StopCoroutine(nameof(FadeOutDamageText));
            StartCoroutine(nameof(FadeOutDamageText));
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

            // [추가] 상시 VFX 처리
            if (effect.Data.VFXPrefab != null && !_vfxInstances.ContainsKey(effect))
            {
                Transform spawnTarget = RetHitbox();
                
                // 프리팹이 비활성화 상태이므로, 생성 시 부모를 즉시 지정하고 활성화
                GameObject vfxGo = Instantiate(effect.Data.VFXPrefab, spawnTarget);
                vfxGo.SetActive(true);
                
                // KeepVFXAttached가 false면 부모 관계 해제 (위치는 유지)
                if (!effect.Data.KeepVFXAttached)
                {
                    vfxGo.transform.SetParent(spawnTarget.parent); // 캐릭터가 아니라 전체 캐릭터 UI 루트 하위로 이동
                }

                _vfxInstances[effect] = vfxGo;
            }
        }
        else // 만료 또는 해제됨
        {
            if (_buffIcons.TryGetValue(effect, out var go))
            {
                Destroy(go);
                _buffIcons.Remove(effect);
            }

            // [추가] 연동된 VFX 제거 처리
            if (_vfxInstances.TryGetValue(effect, out var vfxGo))
            {
                if (vfxGo != null)
                {
                    var controller = vfxGo.GetComponent<EffectController>();
                    if (controller != null)
                    {
                        // Looping인 경우 Stop()으로 페이드아웃 유도, 아니면 즉시 제거
                        controller.Stop(); 
                    }
                    else
                    {
                        Destroy(vfxGo);
                    }
                }
                _vfxInstances.Remove(effect);
            }
        }

        // [추가] 상태 효과(특히 보호막) 변경 시 체력바 즉시 갱신
        UpdateHp();
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

        // 1. 현재 보호막 총 합량 계산
        float totalShield = _character.ActiveStatusEffects
            .Where(e => e.Data != null && e.Data.EffectType == StatusEffectType.Shield)
            .Sum(e => e.DynamicValue);

        // 2. 전체 길이의 기준점(Denom) 산출
        // 현재 HP + 보호막이 최대 체력을 넘어가면 그 합을 기준으로 비율 계산
        float denom = Mathf.Max(_character.MaxHp, _character.CurrentHp + totalShield);
        
        float hpRatio = _character.CurrentHp / denom;
        float totalRatio = (_character.CurrentHp + totalShield) / denom;

        // 3. 게이지 애니메이션 (DOTween)
        hpBar.DOKill();
        hpBar.DOFillAmount(hpRatio, 0.3f).SetEase(Ease.OutCubic);

        if (shieldBar != null)
        {
            shieldBar.DOKill();
            if (totalShield > 0)
            {
                shieldBar.gameObject.SetActive(true);
                shieldBar.DOFillAmount(totalRatio, 0.3f).SetEase(Ease.OutCubic);
            }
            else
            {
                // 보호막이 없으면 0으로 줄어든 뒤 비활성화
                shieldBar.DOFillAmount(0f, 0.2f).OnComplete(() => shieldBar.gameObject.SetActive(false));
            }
        }

        // 기존 너비 조절 방식은 사용자가 Fill 방식을 선호하므로 주석 유지
        // var rt = hpBar.rectTransform;
        // rt.sizeDelta = new Vector2(_maxBarWidth * ratio, rt.sizeDelta.y);
    }

    // -------------------------------------------------------
    // 데미지 텍스트 표시
    // -------------------------------------------------------
    public void ShowDamage(float damage, bool isCrit, bool isEvaded = false)
    {
        if (damageText == null) return;

        damageText.transform.SetAsLastSibling(); // 레이어 최상단으로 이동
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
        // [추가] 툴팁 확인 중(롱프레스)인 경우 클릭 선택 무시
        if (BattleUI.Instance != null && BattleUI.Instance.IsTooltipPerforming) return;

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
    public Transform RetHitbox()
    {
        Transform spawnTarget = hitbox != null ? hitbox.transform : (illustration != null ? illustration.transform : this.transform);
        return spawnTarget;
    }
}
