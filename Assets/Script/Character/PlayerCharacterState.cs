using System.Collections.Generic;
using UnityEngine;
using TurnRPG.SkillSystem;

/// <summary>
/// 게임 세션 동안 유지되는 아군 캐릭터의 가변 데이터 상태입니다.
/// 로비에서 강화하거나 스킬을 교체할 때 이 데이터가 수정되며, 전투 시 BattleCharacter로 복사됩니다.
/// </summary>
[System.Serializable]
public class PlayerCharacterState
{
    [Header("Base Data")]
    public CharacterData template;
    public string characterName; // [추가] 유저가 정한 커스텀 이름

    [Header("Current Baseline Stats (Lobby/Equipment applied)")]
    public float currentBaseMaxHp;
    public float currentBaseAttack;
    public float currentBaseDefense;
    public float currentBaseSpeed;
    public float currentBaseCritChance;
    public float currentBaseCritDamage;
    public float currentBaseEvasion;
    public float currentBaseAccuracy;
    public float currentBaseDualAttackChance; // [추가] 협공 확률 (기본 0.03 = 3%)

    [Header("Persistent Battle State")]
    public float currentHp; // 전투 간 유지되는 체력

    [Header("Growth System")]
    public int bonusPoints = 50; // 초기 보너스 포인트 (테스트용 10)
    public int spentHp, spentAtk, spentDef, spentSpeed, spentCritRate, spentCritDmg;

    [Header("Skills")]
    // 최대 3개의 스킬 슬롯 유지 (SkillData 레퍼런스 및 레벨)
    public List<SkillData> equippedSkills = new List<SkillData>(3);
    public int[] skillLevels = new int[3] { 1, 1, 1 };
    public int[] skillCooldowns = new int[3]; // [추가] 스테이지 간 유지되는 스킬 쿨타임

    [Header("Equipments")]
    public EquipmentState headGear;
    public EquipmentState bodyArmor;
    public EquipmentState shoes;

    public PlayerCharacterState(CharacterData data)
    {
        if (data == null) return;

        template = data;
        characterName = data.CharacterName;

        // 장비 초기화
        headGear = new EquipmentState(EquipmentPart.Head);
        bodyArmor = new EquipmentState(EquipmentPart.Body);
        shoes = new EquipmentState(EquipmentPart.Shoes);

        headGear.GenerateRandomStats();
        bodyArmor.GenerateRandomStats();
        shoes.GenerateRandomStats();

        // 초기화 시 CharacterData의 기본값을 복사
        currentBaseMaxHp = data.MaxHp;
        currentBaseAttack = data.Attack;
        currentBaseDefense = data.Defense;
        currentBaseSpeed = data.Speed;
        currentBaseCritChance = data.CritChance;
        currentBaseCritDamage = data.CritDamage;
        currentBaseEvasion = data.Evasion;
        currentBaseAccuracy = data.Accuracy;
        currentBaseDualAttackChance = data.DualAttackChance;

        // 현재 체력을 장비 보너스가 포함된 최종 MaxHp로 초기화 (풀피 시작)
        currentHp = TotalMaxHp;

        // 초기 스킬 로스터 설정
        equippedSkills.Clear();
        for (int i = 0; i < 3; i++) equippedSkills.Add(null);

        if (data.StartingSkills != null)
        {
            foreach (var slot in data.StartingSkills)
            {
                if (slot != null && slot.skillData != null)
                {
                    int idx = (int)slot.skillData.SlotIndex;
                    if (idx >= 0 && idx < 3)
                    {
                        equippedSkills[idx] = slot.skillData;
                        skillLevels[idx] = slot.level;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 모든 장비 부위에서 특정 타입의 스탯 보너스 합계를 가져옵니다.
    /// </summary>
    public float GetEquipmentBonus(StatType type)
    {
        float total = 0f;
        total += GetBonusFromPart(headGear, type);
        total += GetBonusFromPart(bodyArmor, type);
        total += GetBonusFromPart(shoes, type);
        return total;
    }

    public float TotalMaxHp
    {
        get
        {
            if (template == null) return 0f;
            float pntHpBonus = template.MaxHp * spentHp * 0.01f;
            float eqHpPer = GetEquipmentBonus(StatType.HP);
            float eqHpVal = template.MaxHp * eqHpPer * 0.01f;
            return template.MaxHp + pntHpBonus + eqHpVal;
        }
    }

    private float GetBonusFromPart(EquipmentState equip, StatType type)
    {
        if (equip == null) return 0f;
        float partTotal = 0f;
        foreach (var sub in equip.subStats)
        {
            if (sub.statType == type) partTotal += sub.value;
        }
        return partTotal;
    }
}
