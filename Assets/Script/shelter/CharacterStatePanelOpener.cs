using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 캐릭터 상세 정보 패널을 열고, 스탯 강화 로직을 처리하는 클래스입니다.
/// </summary>
public class CharacterStatePanelOpener : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject characterStatePanel;
    public Image imgIllustration; // 캐릭터 전신 일러스트 표시용
    public TMP_Text txtName;

    [Header("Stats UI (TextMeshPro)")]
    public TMP_Text txtHp;
    public TMP_Text txtAtk;
    public TMP_Text txtDef;
    public TMP_Text txtSpeed;
    public TMP_Text txtCritRate;
    public TMP_Text txtCritDmg;

    [Header("Bonus Stats UI (TextMeshPro)")]
    public TMP_Text txtHpBonus;
    public TMP_Text txtAtkBonus;
    public TMP_Text txtDefBonus;
    public TMP_Text txtSpeedBonus;
    public TMP_Text txtCritRateBonus;
    public TMP_Text txtCritDmgBonus;

    public TMP_Text txtBonusPoints;

    [Header("Other Panels")]
    public GameObject equiPanel; // 장비 패널
    public GameObject skillPanel; // 스킬 패널

    [Header("Equipment Slots")]
    public Image imgHead;
    public Image imgBody;
    public Image imgShoes;

    private int _currentIdx = -1;

    /// <summary>
    /// 지정된 인덱스의 캐릭터 정보로 패널을 엽니다. (아래에서 위로 올라오는 애니메이션)
    /// </summary>
    public void OpenPanel(int characterIndex)
    {
        if (GameManager.Instance == null) return;
        if (characterIndex < 0 || characterIndex >= GameManager.Instance.party.Length) return;

        _currentIdx = characterIndex;

        // [추가] 캐릭터 선택(호출) 보이스 출력
        if (SoundManager.Instance != null)
        {
            var charState = GameManager.Instance.party[_currentIdx];
            if (charState != null && charState.template != null && charState.template.call.Count > 0)
            {
                // 여러 개일 경우 대비해 랜덤 재생
                var clip = charState.template.call[UnityEngine.Random.Range(0, charState.template.call.Count)];
                SoundManager.Instance.PlayVoice(clip);
            }
        }



        // UI 갱신
        UpdateUI();

        // [애니메이션 개선: 캔버스 높이 기반]
        RectTransform rect = characterStatePanel.GetComponent<RectTransform>();
        Canvas canvas = characterStatePanel.GetComponentInParent<Canvas>();

        if (rect != null && canvas != null)
        {
            float canvasHeight = canvas.GetComponent<RectTransform>().rect.height;
            characterStatePanel.SetActive(true);

            // 시작 위치: 캔버스 높이만큼 아래
            rect.anchoredPosition = new Vector2(0, -canvasHeight);
            // 중앙(0)으로 부드럽게 상승
            rect.DOAnchorPos(Vector2.zero, 0.5f).SetEase(Ease.OutCubic);
        }
        else
        {
            characterStatePanel.SetActive(true);
        }
    }

    /// <summary>
    /// 패널을 닫습니다. (아래로 내려가는 애니메이션 후 비활성화)
    /// </summary>
    public void ClosePanel()
    {
        RectTransform rect = characterStatePanel.GetComponent<RectTransform>();
        Canvas canvas = characterStatePanel.GetComponentInParent<Canvas>();

        if (rect != null && canvas != null)
        {
            float canvasHeight = canvas.GetComponent<RectTransform>().rect.height;
            // 캔버스 높이만큼 아래로 퇴장
            rect.DOAnchorPos(new Vector2(0, -canvasHeight), 0.4f).SetEase(Ease.InCubic).OnComplete(() =>
            {
                characterStatePanel.SetActive(false);
                _currentIdx = -1;
            });
        }
        else
        {
            characterStatePanel.SetActive(false);
            _currentIdx = -1;
        }
    }

    /// <summary>
    /// 현재 선택된 캐릭터의 데이터를 기반으로 UI 텍스트 및 이미지를 갱신합니다.
    /// </summary>
    public void UpdateUI()
    {
        if (_currentIdx == -1 || GameManager.Instance == null) return;

        var state = GameManager.Instance.party[_currentIdx];
        if (state == null || state.template == null) return;

        var data = state.template;

        // 1. 일러스트 갱신
        if (imgIllustration != null) imgIllustration.sprite = data.illustration;

        // 1.1 이름 갱신
        if (txtName != null) txtName.text = state.characterName;

        // 2. 스탯 텍스트 갱신 (기본 + 보너스포인트 + 장비 보너스)
        float eqHpPer = state.GetEquipmentBonus(StatType.HP);
        float eqAtkPer = state.GetEquipmentBonus(StatType.Attack);
        float eqDefPer = state.GetEquipmentBonus(StatType.Defense);
        float eqSpeed = state.GetEquipmentBonus(StatType.Speed);
        float eqCritRatePer = state.GetEquipmentBonus(StatType.CritChance);
        float eqCritDmgPer = state.GetEquipmentBonus(StatType.CritDamage);

        float pntHpBonus = data.MaxHp * state.spentHp * 0.01f;
        float pntAtkBonus = data.Attack * state.spentAtk * 0.01f;
        float pntDefBonus = data.Defense * state.spentDef * 0.01f;
        float eqHpVal = data.MaxHp * eqHpPer * 0.01f;
        float eqAtkVal = data.Attack * eqAtkPer * 0.01f;
        float eqDefVal = data.Defense * eqDefPer * 0.01f;

        float finalHp = data.MaxHp + pntHpBonus + eqHpVal;
        float finalAtk = data.Attack + pntAtkBonus + eqAtkVal;
        float finalDef = data.Defense + pntDefBonus + eqDefVal;
        float finalSpeed = data.Speed + state.spentSpeed + eqSpeed;
        float finalCritRate = data.CritChance + (state.spentCritRate * 0.01f) + (eqCritRatePer * 0.01f);
        float finalCritDmg = data.CritDamage + (state.spentCritDmg * 0.01f) + (eqCritDmgPer * 0.01f);

        // 최종 수치 (왼쪽)
        if (txtHp != null) txtHp.text = $"{finalHp:F0}";
        if (txtAtk != null) txtAtk.text = $"{finalAtk:F0}";
        if (txtDef != null) txtDef.text = $"{finalDef:F0}";
        if (txtSpeed != null) txtSpeed.text = $"{finalSpeed:F0}";
        if (txtCritRate != null) txtCritRate.text = $"{finalCritRate * 100f:F0}%";
        if (txtCritDmg != null) txtCritDmg.text = $"{finalCritDmg * 100f:F0}%";

        // 보너스 수치 (오른쪽 + 텍스트) - 초록색 강조
        if (txtHpBonus != null) txtHpBonus.text = $"<color=#00FF00>+{(pntHpBonus + eqHpVal):F0}</color>";
        if (txtAtkBonus != null) txtAtkBonus.text = $"<color=#00FF00>+{(pntAtkBonus + eqAtkVal):F0}</color>";
        if (txtDefBonus != null) txtDefBonus.text = $"<color=#00FF00>+{(pntDefBonus + eqDefVal):F0}</color>";
        if (txtSpeedBonus != null) txtSpeedBonus.text = $"<color=#00FF00>+{(state.spentSpeed + eqSpeed):F0}</color>";
        if (txtCritRateBonus != null) txtCritRateBonus.text = $"<color=#00FF00>+{(state.spentCritRate + eqCritRatePer):F0}%</color>";
        if (txtCritDmgBonus != null) txtCritDmgBonus.text = $"<color=#00FF00>+{(state.spentCritDmg + eqCritDmgPer):F0}%</color>";

        // 3. 보너스 포인트 갱신
        if (txtBonusPoints != null) txtBonusPoints.text = $"Points: {state.bonusPoints}";

        // 3.1 장비 툴팁 갱신 (이미지에 설정된 Sprite를 그대로 사용)
        SetupEquipSlot(imgHead, state.headGear);
        SetupEquipSlot(imgBody, state.bodyArmor);
        SetupEquipSlot(imgShoes, state.shoes);

        // 4. 스킬 아이콘 갱신 (skill1, skill2, skill3 자식 오브젝트 찾기)
        if (skillPanel != null)
        {
            for (int i = 0; i < 3; i++)
            {
                string skillObjectName = $"skill{i + 1}";
                Transform skillSlotTransform = skillPanel.transform.Find(skillObjectName);

                if (skillSlotTransform != null)
                {
                    Image iconImage = skillSlotTransform.GetComponent<Image>();
                    // 만약 자식 오브젝트 자체에 Image가 없고 그 자식으로 Icon이 있다면 대비하여 체크
                    if (iconImage == null) iconImage = skillSlotTransform.GetComponentInChildren<Image>();

                    if (iconImage != null)
                    {
                        if (i < state.equippedSkills.Count && state.equippedSkills[i] != null)
                        {
                            iconImage.sprite = state.equippedSkills[i].SkillIcon;
                            iconImage.enabled = true; // 아이콘 활성화

                            // [추가] 툴팁 트리거 초기화
                            var trigger = skillSlotTransform.GetComponent<SkillTooltipTrigger>();
                            if (trigger == null) trigger = skillSlotTransform.GetComponentInChildren<SkillTooltipTrigger>();

                            if (trigger != null)
                            {
                                trigger.Init(state.equippedSkills[i], state.skillLevels[i]);
                                trigger.enabled = true;
                            }
                        }
                        else
                        {
                            iconImage.enabled = false; // 스킬이 없으면 이미지 비활성화

                            // 스킬이 없으면 트리거 비활성화
                            var trigger = skillSlotTransform.GetComponent<SkillTooltipTrigger>();
                            if (trigger == null) trigger = skillSlotTransform.GetComponentInChildren<SkillTooltipTrigger>();
                            if (trigger != null) trigger.enabled = false;
                        }
                    }
                }
            }
        }

        Debug.Log($"[UI] {data.CharacterName} 정보 갱신 완료");
    }

    // -------------------------------------------------------
    // 스탯 증감 메서드 (버튼에서 호출)
    // -------------------------------------------------------

    public void ChangeHp(bool isPlus)
    {
        if (_currentIdx == -1 || GameManager.Instance == null) return;
        var state = GameManager.Instance.party[_currentIdx];

        // 1. 변경 전 비율 저장 (장비 포함 최종 최대 체력 기준)
        float oldMaxHp = state.TotalMaxHp;
        float hpRatio = oldMaxHp > 0 ? state.currentHp / oldMaxHp : 1f;

        // 2. 포인트 변경 시도
        if (ProcessStatChange(ref state.spentHp, isPlus, true))
        {
            // 3. 변경 후 새로운 최대 체력에 맞춰 비율 적용
            float newMaxHp = state.TotalMaxHp;
            state.currentHp = newMaxHp * hpRatio;

            UpdateUI();
        }
    }

    public void ChangeAtk(bool isPlus)
    {
        if (ProcessStatChange(ref GameManager.Instance.party[_currentIdx].spentAtk, isPlus, false))
            UpdateUI();
    }

    public void ChangeDef(bool isPlus)
    {
        if (ProcessStatChange(ref GameManager.Instance.party[_currentIdx].spentDef, isPlus, false))
            UpdateUI();
    }

    public void ChangeSpeed(bool isPlus)
    {
        if (ProcessStatChange(ref GameManager.Instance.party[_currentIdx].spentSpeed, isPlus, false))
            UpdateUI();
    }

    public void ChangeCritRate(bool isPlus)
    {
        if (ProcessStatChange(ref GameManager.Instance.party[_currentIdx].spentCritRate, isPlus, false))
            UpdateUI();
    }

    public void ChangeCritDmg(bool isPlus)
    {
        if (ProcessStatChange(ref GameManager.Instance.party[_currentIdx].spentCritDmg, isPlus, false))
            UpdateUI();
    }

    /// <summary>
    /// 모든 투자된 스탯을 초기화하고 포인트를 환급합니다.
    /// </summary>
    public void ResetStats()
    {
        if (_currentIdx == -1 || GameManager.Instance == null) return;
        var state = GameManager.Instance.party[_currentIdx];
        var data = state.template;

        // 1. 투자한 총 포인트 계산
        int totalSpent = state.spentHp + state.spentAtk + state.spentDef +
                         state.spentSpeed + state.spentCritRate + state.spentCritDmg;

        if (totalSpent <= 0) return; // 투자한 포인트가 없으면 무시

        // 2. 포인트 환급
        state.bonusPoints += totalSpent;

        // 3. 체력 비율 저장 (장비 포함 최종 최대 체력 기준)
        float oldMaxHp = state.TotalMaxHp;
        float hpRatio = oldMaxHp > 0 ? state.currentHp / oldMaxHp : 1f;

        // 4. 모든 투자 포인트 초기화
        state.spentHp = 0;
        state.spentAtk = 0;
        state.spentDef = 0;
        state.spentSpeed = 0;
        state.spentCritRate = 0;
        state.spentCritDmg = 0;

        // 5. 초기화된 상태에서의 새로운 최대 체력 계산 및 비율 적용
        float newMaxHp = state.TotalMaxHp;
        state.currentHp = newMaxHp * hpRatio;

        // 6. UI 갱신
        UpdateUI();

        // [추가] 스탯 초기화 사운드
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SfxType.stateChange);

        Debug.Log($"[UI] {state.characterName} 스탯 초기화 완료. {totalSpent} 포인트 환급됨.");
    }

    /// <summary>
    /// 공통 스탯 변경 로직. 포인트가 충분하거나 환급 가능한지 체크합니다.
    /// </summary>
    private bool ProcessStatChange(ref int spentTarget, bool isPlus, bool isHp)
    {
        if (_currentIdx == -1 || GameManager.Instance == null) return false;
        var state = GameManager.Instance.party[_currentIdx];

        if (isPlus)
        {
            if (state.bonusPoints > 0)
            {
                state.bonusPoints--;
                spentTarget++;

                // [추가] 스탯 변경 사운드
                if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SfxType.stateChange);

                return true;
            }
        }
        else
        {
            if (spentTarget > 0)
            {
                state.bonusPoints++;
                spentTarget--;

                // [추가] 스탯 변경 사운드
                if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SfxType.stateChange);

                return true;
            }
        }

        return false;
    }

    private void SetupEquipSlot(Image img, EquipmentState state)
    {
        if (img == null || img.sprite == null) return;

        // [중요] Raycast Target이 꺼져 있으면 툴팁이 동작하지 않음
        img.raycastTarget = true;

        var trigger = img.GetComponent<EquipmentTooltipTrigger>();
        if (trigger == null) trigger = img.gameObject.AddComponent<EquipmentTooltipTrigger>();
        trigger.Init(state, img.sprite);
    }

    public void OpenRerollPanel()
    {
        // 리롤 아이템이 1개 이상일 때만 열리게
        if (GameManager.Instance != null && GameManager.Instance.rerollUI != null && GameManager.Instance.rerollItemCount > 0)
        {
            GameManager.Instance.rerollUI.Open();
        }
    }
}
