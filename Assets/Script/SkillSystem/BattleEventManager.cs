using System;
using UnityEngine;
using TurnRPG.SkillSystem;

namespace TurnRPG.SkillSystem
{
    /// <summary>
    /// 전투 내 발생하는 이벤트를 브로드캐스팅하는 허브 (옵저버 패턴)
    /// </summary>
    public static class BattleEventManager
    {
        // C# Action 델리게이트를 이용한 이벤트들
        public static event Action<BattleCharacter> OnTurnStarted;
        public static event Action<BattleCharacter> OnTurnEnded;

        // 현재 발동 중인 패시브 트리거 컨텍스트 (이펙트 블록들이 판단 목적으로 사용)
        public static PassiveTriggerType CurrentExecutingTrigger = PassiveTriggerType.None;

        // 특정 사건들을 패시브가 알 수 있도록 마지막 타겟 임시 저장고
        public static BattleCharacter LastVictim;
        public static bool LastAttackWasEvaded;

        // victim, attacker, damage, cannotBeCountered, isEvaded, isCritical, isItem
        public static event Action<BattleCharacter, BattleCharacter, float, bool, bool, bool, bool> OnDamageTaken;

        // 협공 요청 이벤트 (협공할 아군, 타겟이 될 적군)
        public static event Action<BattleCharacter, BattleCharacter> OnDualAttackRequested;

        // 스킬 사용 이벤트
        public static event Action<BattleCharacter, SkillData> OnSkillUsed;

        // 누가 누군가에게 상태이상을 부여했거나 해제되었을 때
        public static event Action<BattleCharacter, StatusEffect> OnStatusEffectChanged; // 시각적 아이콘 업데이트용
        public static event Action<BattleCharacter, StatusEffect> OnStatusEffectApplied; // 신규 부여 시점 (VFX용)

        // 누가 체력을 회복했을 때
        public static event Action<BattleCharacter, float> OnHealed;

        // 광역 피격 이벤트 (광역 공격을 발동한 시전자, 맞은 아군들)
        public static event Action<BattleCharacter, System.Collections.Generic.List<BattleCharacter>> OnAoEAttacked;

        public static void TriggerTurnStarted(BattleCharacter turnOwner)
        {
            OnTurnStarted?.Invoke(turnOwner);
        }

        public static void TriggerTurnEnded(BattleCharacter turnOwner)
        {
            OnTurnEnded?.Invoke(turnOwner);
        }

        public static void TriggerDamageTaken(BattleCharacter victim, BattleCharacter attacker, float floatDamage, bool cannotBeCountered = false, bool isEvaded = false, bool isCritical = false, bool isItem = false)
        {
            LastVictim = victim;
            LastAttackWasEvaded = isEvaded;
            OnDamageTaken?.Invoke(victim, attacker, floatDamage, cannotBeCountered, isEvaded, isCritical, isItem);
        }

        public static void TriggerDualAttack(BattleCharacter helperAlly, BattleCharacter targetEnemy)
        {
            OnDualAttackRequested?.Invoke(helperAlly, targetEnemy);
        }

        public static void TriggerSkillUsed(BattleCharacter caster, SkillData skill)
        {
            OnSkillUsed?.Invoke(caster, skill);
        }

        public static void TriggerAoEAttacked(BattleCharacter attacker, System.Collections.Generic.List<BattleCharacter> victims)
        {
            OnAoEAttacked?.Invoke(attacker, victims);
        }

        public static void TriggerStatusEffectChanged(BattleCharacter character, StatusEffect effect)
        {
            OnStatusEffectChanged?.Invoke(character, effect);
        }

        public static void TriggerStatusEffectApplied(BattleCharacter character, StatusEffect effect)
        {
            OnStatusEffectApplied?.Invoke(character, effect);
        }

        public static void TriggerHealed(BattleCharacter target, float amount)
        {
            OnHealed?.Invoke(target, amount);
        }
    }
}
