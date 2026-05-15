using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using TurnRPG.SkillSystem;

/// <summary>
/// 초기 파티 구성 완료 후, 각 캐릭터의 스탯과 스킬/장비를 확인시켜주는 프리뷰 UI입니다.
/// </summary>
public class InitialCharacterPreviewUI : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject previewPanel;
    public Image imgIllustration;
    public TMP_Text txtName;
    public TMP_Text txtIntro; // [추가] "철수(이)는 이런 장비로 시작해요" 안내 텍스트

    [Header("Stats UI")]
    public TMP_Text txtHp;
    public TMP_Text txtAtk;
    public TMP_Text txtDef;
    public TMP_Text txtSpeed;
    public TMP_Text txtCritRate;
    public TMP_Text txtCritDmg;

    [Header("Bonus Stats UI")]
    public TMP_Text txtHpBonus;
    public TMP_Text txtAtkBonus;
    public TMP_Text txtDefBonus;
    public TMP_Text txtSpeedBonus;
    public TMP_Text txtCritRateBonus;
    public TMP_Text txtCritDmgBonus;

    [Header("Equipment Slots")]
    public Image imgHead;
    public Image imgBody;
    public Image imgShoes;

    [Header("Skill Icons")]
    public Image[] skillIcons; // 3개

    [Header("Controls")]
    public Button btnConfirm;

    private Action _onConfirm;

    private void Awake()
    {
        if (previewPanel == null) previewPanel = gameObject;

        if (btnConfirm != null)
        {
            btnConfirm.onClick.AddListener(OnConfirmClick);
        }
    }

    public void Open(PlayerCharacterState state, Action onConfirm)
    {
        _onConfirm = onConfirm;

        if (SoundManager.Instance != null)
        {
            if (state != null && state.template != null && state.template.greeting.Count > 0)
            {
                // 여러 개일 경우 대비해 랜덤 재생
                var clip = state.template.greeting[UnityEngine.Random.Range(0, state.template.greeting.Count)];
                SoundManager.Instance.PlayVoice(clip);
            }
        }

        // 데이터 반영
        UpdateUI(state);

        // 연출 및 활성화 (슬라이드 방식)
        RectTransform rect = previewPanel.GetComponent<RectTransform>();
        Canvas canvas = previewPanel.GetComponentInParent<Canvas>();

        if (rect != null && canvas != null)
        {
            float canvasHeight = canvas.GetComponent<RectTransform>().rect.height;
            previewPanel.SetActive(true);

            // 1. 시작 위치: 캔버스 높이만큼 아래
            rect.anchoredPosition = new Vector2(0, -canvasHeight);

            // 2. 상승 애니메이션
            rect.DOAnchorPos(Vector2.zero, 0.5f).SetEase(Ease.OutCubic);
        }
        else
        {
            previewPanel.SetActive(true);
        }
    }

    private void UpdateUI(PlayerCharacterState state)
    {
        if (state == null || state.template == null) return;

        var data = state.template;
        if (imgIllustration != null) imgIllustration.sprite = data.illustration;
        if (txtName != null) txtName.text = state.characterName;

        // [추가] 안내 문구 설정
        if (txtIntro != null)
        {
            string particle = GetParticle(state.characterName);
            txtIntro.text = $"<color=#FFD700>{state.characterName}</color>{particle}\n처음에 이런 장비와\n스킬로 시작해요.\n꾹 눌러서 확인해봐요 ";
        }

        // 스탯 계산 (보너스 포인트는 0이므로 기본 + 장비)
        float eqHpPer = state.GetEquipmentBonus(StatType.HP);
        float eqAtkPer = state.GetEquipmentBonus(StatType.Attack);
        float eqDefPer = state.GetEquipmentBonus(StatType.Defense);
        float eqSpeed = state.GetEquipmentBonus(StatType.Speed);
        float eqCritRatePer = state.GetEquipmentBonus(StatType.CritChance);
        float eqCritDmgPer = state.GetEquipmentBonus(StatType.CritDamage);

        float eqHpVal = data.MaxHp * eqHpPer * 0.01f;
        float eqAtkVal = data.Attack * eqAtkPer * 0.01f;
        float eqDefVal = data.Defense * eqDefPer * 0.01f;

        float finalHp = data.MaxHp + eqHpVal;
        float finalAtk = data.Attack + eqAtkVal;
        float finalDef = data.Defense + eqDefVal;
        float finalSpeed = data.Speed + eqSpeed;
        float finalCritRate = data.CritChance + (eqCritRatePer * 0.01f);
        float finalCritDmg = data.CritDamage + (eqCritDmgPer * 0.01f);

        if (txtHp != null) txtHp.text = $"{finalHp:F0}";
        if (txtAtk != null) txtAtk.text = $"{finalAtk:F0}";
        if (txtDef != null) txtDef.text = $"{finalDef:F0}";
        if (txtSpeed != null) txtSpeed.text = $"{finalSpeed:F0}";
        if (txtCritRate != null) txtCritRate.text = $"{finalCritRate * 100f:F0}%";
        if (txtCritDmg != null) txtCritDmg.text = $"{finalCritDmg * 100f:F0}%";

        // 보너스 수치 개별 출력 (초록색)
        if (txtHpBonus != null) txtHpBonus.text = $"<color=#00FF00>+{(eqHpVal):F0}</color>";
        if (txtAtkBonus != null) txtAtkBonus.text = $"<color=#00FF00>+{(eqAtkVal):F0}</color>";
        if (txtDefBonus != null) txtDefBonus.text = $"<color=#00FF00>+{(eqDefVal):F0}</color>";
        if (txtSpeedBonus != null) txtSpeedBonus.text = $"<color=#00FF00>+{(eqSpeed):F0}</color>";
        if (txtCritRateBonus != null) txtCritRateBonus.text = $"<color=#00FF00>+{(eqCritRatePer):F0}%</color>";
        if (txtCritDmgBonus != null) txtCritDmgBonus.text = $"<color=#00FF00>+{(eqCritDmgPer):F0}%</color>";

        // 장비 툴팁 설정
        SetupEquipSlot(imgHead, state.headGear);
        SetupEquipSlot(imgBody, state.bodyArmor);
        SetupEquipSlot(imgShoes, state.shoes);

        // 스킬 아이콘 및 툴팁
        for (int i = 0; i < 3; i++)
        {
            if (i < skillIcons.Length && skillIcons[i] != null)
            {
                if (i < state.equippedSkills.Count && state.equippedSkills[i] != null)
                {
                    skillIcons[i].sprite = state.equippedSkills[i].SkillIcon;
                    skillIcons[i].enabled = true;

                    var trigger = skillIcons[i].GetComponent<SkillTooltipTrigger>();
                    if (trigger == null) trigger = skillIcons[i].gameObject.AddComponent<SkillTooltipTrigger>();
                    trigger.Init(state.equippedSkills[i], state.skillLevels[i]);
                }
                else
                {
                    skillIcons[i].enabled = false;
                }
            }
        }
    }

    private string GetParticle(string name)
    {
        if (string.IsNullOrEmpty(name)) return "은(는)";
        char lastChar = name[name.Length - 1];
        // 한글 범위 확인 (가 ~ 힣)
        if (lastChar < 0xAC00 || lastChar > 0xD7A3) return "은(는)";

        // 종성 유무 확인
        int batchimCode = (lastChar - 0xAC00) % 28;
        return batchimCode > 0 ? "이는" : "는";
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

    private void OnConfirmClick()
    {
        RectTransform rect = previewPanel.GetComponent<RectTransform>();
        Canvas canvas = previewPanel.GetComponentInParent<Canvas>();

        if (rect != null && canvas != null)
        {
            float canvasHeight = canvas.GetComponent<RectTransform>().rect.height;

            // 아래로 내려가며 퇴장
            rect.DOAnchorPos(new Vector2(0, -canvasHeight), 0.4f).SetEase(Ease.InCubic).OnComplete(() =>
            {
                previewPanel.SetActive(false);
                _onConfirm?.Invoke();
            });
        }
        else
        {
            previewPanel.SetActive(false);
            _onConfirm?.Invoke();
        }
    }
}
