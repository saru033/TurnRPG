using UnityEngine;

namespace TurnRPG.SkillSystem
{
    [System.Flags]
    public enum CharacterType
    {
        None = 0,
        Male = 1 << 0,
        Female = 1 << 1,
        Wolf = 1 << 2,
        Enemy = 1 << 3,
        PlayerAll = Male | Female | Wolf,
        All = ~0
    }

    public enum SkillType
    {
        Attack,         // 공격형 (데미지를 주로 입힘)
        NonAttack,      // 버프, 힐 등의 비공격형 액티브 스킬
        Passive         // 턴이나 특정 이벤트에 반응하는 패시브 스킬
    }

    [System.Flags]
    public enum PassiveTriggerType
    {
        None                 = 0,
        OnBasicAttackExecute = 1 << 0,  // 내 평타 공격 시 (추가타)
        OnAllyAttacked       = 1 << 1,  // 나를 제외한 아군 피격 시 (협공/커버)
        OnSelfAttacked       = 1 << 2,  // 내 피격 시 (반격)
        OnTurnStart          = 1 << 3,  // 턴 시작 시
        OnTurnEnd            = 1 << 4,  // (누군가의) 턴 종료 시
        OnAoEAttacked        = 1 << 5,  // 전체 광역 공격 피격 시
        OnSelfTurnEnd        = 1 << 6,  // 내 턴 종료 시
        OnEnemyNonAttackSkill= 1 << 7   // 적이 공격이 아닌 스킬(NonAttack) 사용 시
    }

    public enum SkillSlotIndex
    {
        Skill1 = 0,     // 1스킬 (평타 등)
        Skill2 = 1,     // 2스킬
        Skill3 = 2      // 3스킬 (궁극기 등)
    }

    public enum SkillTargetType
    {
        None,           // 타겟 없음 (패시브 등)
        Self,           // 자신
        SingleEnemy,    // 적 1명 지정
        AllEnemies,     // 적 전체
        SingleAlly,     // 아군 1명 지정
        AllAllies,      // 아군 전체
        RandomEnemy     // 랜덤 적 지정 (도발 고려 등)
    }

    public enum StatusEffectCategory
    {
        Buff,
        Debuff,
        General     // 일반 효과 (즉시 해제 등 지속시간이 없는 일회성 처리용인 경우도 있으나, 주로 버프/디버프 위주)
    }

    // 유저가 정의한 상태이상들 식별자
    public enum StatusEffectType
    {
        None,
        // Buffs
        AtkUp50, AtkUp70,
        DefUp50, DefUp70,
        SpeedUp50,
        CritChanceUp50,
        CritDmgUp50,
        Immunity,           // 면역
        Shield,             // 보호막
        AutoHeal,           // 자동 회복
        Evasion50,          // 회피
        Accuracy50,         // 명중 증가
        Stealth,            // 은신
        SkillDmgNullify,    // 스킬 데미지 1회 무효
        Invincible,         // 무적
        CounterAttack,      // 반격
        LifeSteal,          // 흡혈
        CritResist,         // 치명 저항

        // Debuffs
        AtkDown50,
        DefDown50,
        SpeedDown50,
        AccuracyDown50,
        Stun,               // 기절
        Sleep,              // 수면
        Unhealable,         // 회복 불가
        Unbuffable,         // 강화 불가
        Silence,            // 침묵
        Bleed,              // 출혈
        Burn                // 화상
    }
}
