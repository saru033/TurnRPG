using UnityEngine;

namespace TurnRPG.SkillSystem.Effects
{
    [System.Serializable]
    public class DamageEffect : SkillEffect
    {
        [Tooltip("어떤 대상(들)에게 데미지를 줄 것인지")]
        public EffectTargetType TargetType = EffectTargetType.Target;

        [Tooltip("공격력 대비 스킬 데미지 계수 (예: 1.5면 150%)")]
        public float DamageMultiplier = 1.0f;

        [Header("특성이 반영된 데미지 옵션")]
        [Tooltip("상대 방어력을 퍼센트만큼 관통 (0: 없음, 0.5: 50% 관통, 1: 100% 관통)")]
        [Range(0f, 1f)] public float Penetration = 0f;

        [Tooltip("반격 발생을 원천 차단 (반격불가 스킬)")]
        public bool CannotBeCountered = false;

        [Tooltip("명중/회피 판정을 무시하고 무조건 적중")]
        public bool AlwaysHit = false;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);
            bool hitAnyone = false;

            if (TargetType == EffectTargetType.AllEnemies && actualTargets.Count > 0)
            {
                BattleEventManager.TriggerAoEAttacked(caster, actualTargets);
            }

            foreach (var t in actualTargets)
            {
                if (t == null || !t.IsAlive) continue;

                // 추가: 각종 데미지 증가, 공격력 증가 버프 적용 및 체크
                float baseDamage = caster.Attack * DamageMultiplier;

                // 데미지 계산 및 치명타 판정 (개선된 BattleCharacter 메서드 호출 가정)
                var (finalDamage, isCrit) = caster.CalcDamage(baseDamage);

                // 확장된 파라미터로 데미지 적용
                t.TakeDamage(finalDamage, attacker: caster, penetration: Penetration, alwaysHit: AlwaysHit, cannotBeCountered: CannotBeCountered);

                hitAnyone = true;
            }
            return hitAnyone;
        }
    }

    [System.Serializable]
    public class HealEffect : SkillEffect
    {
        [Tooltip("회복시킬 대상 (기본: Target, 단체 회복: AllAllies)")]
        public EffectTargetType TargetType = EffectTargetType.Target;

        [Tooltip("최대 체력의 몇 %를 회복시킬지? (예: 0.15 = 15%)")]
        public float HealPercent = 0.15f;

        [Tooltip("자신/대상 시전자 체력 비례냐 대상 체력 비례냐")]
        public bool BasedOnTargetMaxHp = true;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);
            bool healedAnyone = false;

            foreach (var t in actualTargets)
            {
                if (t == null || !t.IsAlive) continue;

                // 회복 불가 디버프(Unhealable)가 걸려있는지 체크
                if (t.HasStatusEffect(StatusEffectType.Unhealable))
                {
                    Debug.Log($"{t.Name}은(는) 회복 불가 상태입니다!");
                    continue;
                }

                float healAmount = BasedOnTargetMaxHp ? t.MaxHp * HealPercent : caster.MaxHp * HealPercent;
                t.Heal(healAmount);
                healedAnyone = true;
            }

            return healedAnyone;
        }
    }

    [System.Serializable]
    public class ReviveEffect : SkillEffect
    {
        [Tooltip("누구를 부활시킬 것인가 (단일 죽은 대상 = Target, 전체 = AllDeadAllies)")]
        public EffectTargetType TargetType = EffectTargetType.AllDeadAllies;
        
        [Tooltip("부활 시 몇 %의 생명력으로 부활시킬지? (예: 0.25 = 25%)")]
        [Range(0f, 1f)] public float RevivalHpPercent = 0.25f;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);
            bool revivedAnyone = false;

            foreach (var t in actualTargets)
            {
                // 생존자(IsAlive == true)는 부활시킬 수 없으니 무시합니다.
                if (t != null && !t.IsAlive)
                {
                    float reviveHp = t.MaxHp * RevivalHpPercent;
                    
                    // 기존에 걸려있던 각종 해로운 상태이상이나 버프 모두 지우기
                    t.ActiveStatusEffects.Clear();
                    
                    // 행동 게이지 0 초기화 (부활 시 기본 국룰, 기획에 따라 변경 가능)
                    t.ActionGauge = 0f;

                    // 체력 상승 (자동으로 IsAlive가 true가 됨)
                    t.CurrentHp = reviveHp; 

                    Debug.Log($"[부활!] {t.Name}이(가) 체력 {reviveHp}으로 부활했습니다!");
                    revivedAnyone = true;
                }
            }

            return revivedAnyone;
        }
    }
}
