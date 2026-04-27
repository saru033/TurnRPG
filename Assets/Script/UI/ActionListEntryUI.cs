using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TurnRPG.SkillSystem;
using System.Collections.Generic;

public class ActionListEntryUI : MonoBehaviour
{
    [Header("Basic Info")]
    public GaugePortrait portrait;
    public TextMeshProUGUI actionGaugeValue;

    [Header("Distinguish Number")]
    public GameObject distinguishNumObj;      // dintinguse_num 오브젝트
    public TextMeshProUGUI distinguishNumText; // 번호 TMP
    public Color allyNumColor = Color.blue;
    public Color enemyNumColor = Color.red;

    [Header("HP Bar")]
    public Image hpBarImage;
    public TextMeshProUGUI hpDetailText;
    private float _hpMaxWidth = -1f; // 내부적으로 계산된 최대폭

    [Header("Skills")]
    public GameObject skillPrefab; // skilltemp
    public Transform skillContainer;
    public float skillStartX = 40f;
    public float skillSpacing = 90f;

    [Header("Status Effects (Buffs/Debuffs)")]
    public GameObject buffPanel;      // 버프가 배치될 패널
    public GameObject buffIconPrefab; // bufficon 프리팹

    private List<GameObject> _spawnedSkills = new List<GameObject>();
    private List<GameObject> _spawnedBuffs = new List<GameObject>();

    private void Awake()
    {
        // 처음 생성 시의 Width를 최대 체력 시의 폭으로 자동 저장
        if (hpBarImage != null && _hpMaxWidth < 0)
        {
            _hpMaxWidth = hpBarImage.rectTransform.sizeDelta.x;
        }
    }

    public void UpdateUI(BattleCharacter character)
    {
        if (character == null) return;

        // 1. Portrait 설정
        if (portrait != null)
        {
            portrait.Init(character);

            // [추가] 자식 오브젝트 중 "Icon" 이미지를 찾아 캐릭터 아이콘으로 변경
            Transform iconTransform = portrait.transform.Find("Icon");
            if (iconTransform != null)
            {
                Image iconImg = iconTransform.GetComponent<Image>();
                if (iconImg != null) iconImg.sprite = character.Data.iconImage;
            }
            else if (portrait.portraitImage != null)
            {
                // "Icon"이 없고 portraitImage 필드가 있으면 직접 할당
                portrait.portraitImage.sprite = character.Data.iconImage;
            }

            // 외곽선 색상 추가 조정 (IsPlayer에 따라)
            if (portrait.highlightRing != null)
            {
                portrait.highlightRing.color = character.IsPlayer ? portrait.playerColor : portrait.enemyColor;
                portrait.highlightRing.gameObject.SetActive(true);
            }
        }

        // 2. 게이지 값
        if (actionGaugeValue != null)
        {
            actionGaugeValue.text = $"{(int)character.ActionGauge}%";
        }

        // [추가] 인식표 설정
        if (distinguishNumObj != null)
        {
            distinguishNumObj.SetActive(true);
            if (distinguishNumText != null)
            {
                distinguishNumText.text = character.DistinguishNum.ToString();
            }

            var img = distinguishNumObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = character.IsPlayer ? allyNumColor : enemyNumColor;
            }
        }

        // 3. HP 바 조절
        if (hpBarImage != null)
        {
            float hpRatio = character.MaxHp > 0 ? character.CurrentHp / character.MaxHp : 0;
            Vector2 size = hpBarImage.rectTransform.sizeDelta;
            size.x = _hpMaxWidth * hpRatio;
            hpBarImage.rectTransform.sizeDelta = size;
        }

        if (hpDetailText != null)
        {
            hpDetailText.text = $"{(int)character.CurrentHp} / {(int)character.MaxHp}";
        }

        // 4. 스킬 배치
        RefreshSkills(character);

        // 5. 버프 배치
        RefreshBuffs(character);
    }

    private void RefreshSkills(BattleCharacter character)
    {
        // 기존 스킬 제거
        foreach (var s in _spawnedSkills)
        {
            if (s != null) Destroy(s);
        }
        _spawnedSkills.Clear();

        if (skillPrefab == null || skillContainer == null) return;

        // 최대 3개까지만 표시 (1, 2, 3스킬)
        int skillCount = Mathf.Min(3, character.ActiveSkills.Count);
        for (int i = 0; i < skillCount; i++)
        {
            var skillData = character.ActiveSkills[i];
            if (skillData == null) continue;

            GameObject go = Instantiate(skillPrefab, skillContainer);
            _spawnedSkills.Add(go);

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(skillStartX + (i * skillSpacing), 0);
            }

            // 이미지 설정
            Image img = go.GetComponent<Image>();
            if (img == null) img = go.GetComponentInChildren<Image>(); // 구조에 따라 다를 수 있음
            if (img != null) img.sprite = skillData.SkillIcon;

            // 쿨타임 처리
            int cd = character.SkillCooldowns.Length > i ? character.SkillCooldowns[i] : 0;
            Transform coolTimeObj = go.transform.Find("CoolTime");
            if (coolTimeObj != null)
            {
                coolTimeObj.gameObject.SetActive(cd > 0);
                if (cd > 0)
                {
                    var cdText = coolTimeObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (cdText != null) cdText.text = cd.ToString();
                }
            }

            // 툴팁 트리거
            var trigger = go.GetComponent<SkillTooltipTrigger>();
            if (trigger == null) trigger = go.AddComponent<SkillTooltipTrigger>();
            
            int level = character.SkillLevels.Length > i ? character.SkillLevels[i] : 1;
            trigger.Init(skillData, level);
        }
    }

    private void RefreshBuffs(BattleCharacter character)
    {
        // 기존 버프 제거
        foreach (var b in _spawnedBuffs)
        {
            if (b != null) Destroy(b);
        }
        _spawnedBuffs.Clear();

        if (buffPanel == null || buffIconPrefab == null) return;

        foreach (var effect in character.ActiveStatusEffects)
        {
            if (effect == null) continue;

            GameObject go = Instantiate(buffIconPrefab, buffPanel.transform);
            _spawnedBuffs.Add(go);

            var iconScript = go.GetComponent<StatusEffectIcon>();
            if (iconScript != null)
            {
                iconScript.Init(effect);
            }
        }
    }
}
