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

                bool isEvaded = false;
                float finalDamage = 0f;
                bool isCrit = false;

                // [변경] 수면(Sleep) 체크: 수면 중이면 반드시 적중하고 반드시 치명타가 터짐
                bool isSleeping = t.HasStatusEffect(StatusEffectType.Sleep);

                // [변경] 시전자의 명중률(Accuracy)과 대상의 회피율(Evasion)을 함께 고려하여 적중 판정
                float hitChance = caster.AccuracyRate - t.EvasionRate;

                // 1. 적중 판정 (AlwaysHit이거나 수면 중인 경우 무시하고 100% 적중)
                if (!AlwaysHit && !isSleeping && Random.value > hitChance)
                {
                    isEvaded = true;
                    finalDamage = baseDamage * 0.5f; // 빗나감 시 50% 피해
                    isCrit = false;                  // [중요] 빗나감 발생 시 절대 치명타가 터질 수 없음
                }
                else
                {
                    // 2. 적중 시 치명타 및 데미지 계산
                    isEvaded = false;

                    if (isSleeping)
                    {
                        // 수면 중이면 확정 치명타 (캐스터의 치명 피해 배율 사용)
                        finalDamage = baseDamage * caster.CritDamage;
                        isCrit = true;
                    }
                    else
                    {
                        bool isCriResist = false;
                        if (t.HasStatusEffect(StatusEffectType.CritResist))
                            isCriResist = true;



                        var result = caster.CalcDamage(baseDamage, isCriResist);
                        finalDamage = result.damage;
                        isCrit = result.isCrit;
                    }
                }

                // 확장된 파라미터로 데미지 적용
                float actualDamage = t.TakeDamage(finalDamage, attacker: caster, penetration: Penetration, isEvaded: isEvaded, cannotBeCountered: CannotBeCountered, isCritical: isCrit);

                if (caster.HasStatusEffect(StatusEffectType.LifeSteal))
                {
                    caster.Heal(actualDamage * 0.25f);

                    if (BattleVFXManager.Instance != null)
                        BattleVFXManager.Instance.SpawnVFX(VFXType.Heal, caster.View.RetHitbox());
                }

                hitAnyone = true;
            }
            // [변경] 타겟피격 여부와 상관없이 항상 true 반환 (스킬 체인 유지)
            return true;
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

                // 시각 이펙트(VFX) 처리
                if (BattleVFXManager.Instance != null)
                    BattleVFXManager.Instance.SpawnVFX(VFXType.Heal, t.View.RetHitbox());


                healedAnyone = true;
            }

            // [변경] 항상 true 반환 (스킬 체인 유지)
            return true;
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

                    // 체력 상승 (자동으로 IsAlive가 true가 됨)
                    t.CurrentHp = reviveHp;

                    // 시각 이펙트(VFX) 처리
                    if (BattleVFXManager.Instance != null)
                        BattleVFXManager.Instance.SpawnVFX(VFXType.Heal, t.View.RetHitbox());

                    // [추가] 부활 알림 (UI 갱신 등을 위해 데미지 0짜리 이벤트를 활용하거나 별도 처리 가능)
                    // 여기서는 간단하게 데미지 이벤트를 0으로 쏘아 UI가 갱신되도록 유도
                    BattleEventManager.TriggerDamageTaken(t, caster, 0, true, false, false);

                    Debug.Log($"[부활!] {t.Name}이(가) 체력 {reviveHp}으로 부활했습니다!");
                    revivedAnyone = true;
                }
            }

            // [변경] 항상 true 반환 (스킬 체인 유지)
            return true;
        }
    }
}
