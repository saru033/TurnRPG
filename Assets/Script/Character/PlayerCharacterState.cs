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

    [Header("Current Baseline Stats (Lobby/Equipment applied)")]
    public float currentBaseMaxHp;
    public float currentBaseAttack;
    public float currentBaseDefense;
    public float currentBaseSpeed;
    public float currentBaseCritChance;
    public float currentBaseCritDamage;
    public float currentBaseEvasion;
    public float currentBaseAccuracy;

    [Header("Persistent Battle State")]
    public float currentHp; // 전투 간 유지되는 체력

    [Header("Growth System")]
    public int bonusPoints = 50; // 초기 보너스 포인트 (테스트용 10)
    public int spentHp, spentAtk, spentDef, spentSpeed, spentCritRate, spentCritDmg;

    [Header("Skills")]
    // 최대 3개의 스킬 슬롯 유지 (SkillData 레퍼런스 및 레벨)
    public List<SkillData> equippedSkills = new List<SkillData>(3);
    public int[] skillLevels = new int[3] { 1, 1, 1 };

    public PlayerCharacterState(CharacterData data)
    {
        if (data == null) return;

        template = data;

        // 초기화 시 CharacterData의 기본값을 복사
        currentBaseMaxHp = data.MaxHp;
        currentBaseAttack = data.Attack;
        currentBaseDefense = data.Defense;
        currentBaseSpeed = data.Speed;
        currentBaseCritChance = data.CritChance;
        currentBaseCritDamage = data.CritDamage;
        currentBaseEvasion = data.Evasion;
        currentBaseAccuracy = data.Accuracy;

        // 현재 체력을 원본 데이터의 MaxHp로 초기화
        currentHp = data.MaxHp;

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
}
