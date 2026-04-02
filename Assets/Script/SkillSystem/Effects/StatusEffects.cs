using UnityEngine;

namespace TurnRPG.SkillSystem.Effects
{
    [System.Serializable]
    public class ApplyStatusEffect : SkillEffect
    {
        [Tooltip("디버프/버프를 걸 대상 (기본: Target, 시전자 등 선택 가능)")]
        public EffectTargetType TargetType = EffectTargetType.Target;

        public StatusEffectData EffectData;
        public int Duration = 2; // 몇 턴 지속?

        // 발동 확률 (1 = 100%)
        [Range(0f, 1f)] public float ApplyChance = 1.0f;

        [Header("동적 수치 (보호막, 화상, 출혈 등)")]
        [Tooltip("비례할 스탯. 0=고정/없음, 1=시전자 공, 2=시전자 최대체력")]
        public int StatScaleType = 0;
        [Tooltip("비례할 계수 (예: 공비례 출혈 데미지 30% = 0.3)")]
        public float StatMultiplier = 0f;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);
            bool appliedAtLeastOne = false;

            foreach (var t in actualTargets)
            {
                if (t == null || !t.IsAlive || EffectData == null) continue;

                if (Random.value > ApplyChance) continue;

                // [추가] 빗나감 체크 (번조: 공격이 빗나갔다면 디버프는 절대 걸리지 않음)
                if (EffectData.Category == StatusEffectCategory.Debuff && t.LastReceivedAttackEvaded)
                {
                    Debug.Log($"{t.Name}에게 시도한 [{EffectData.EffectName}] 효과가 공격 빗나감으로 인해 무시되었습니다.");
                    continue;
                }

                // 면역 체크 (디버프일 경우)
                if (EffectData.Category == StatusEffectCategory.Debuff && t.HasStatusEffect(StatusEffectType.Immunity))
                {
                    Debug.Log($"{t.Name}은(는) 면역 상태입니다! 디버프 무시");
                    continue;
                }

                // 강화불가 체크 (강화효과일 경우)
                if (EffectData.Category == StatusEffectCategory.Buff && t.HasStatusEffect(StatusEffectType.Unbuffable))
                {
                    Debug.Log($"{t.Name}은(는) 강화불가 상태입니다! 강화효과 무시");
                    continue;
                }

                // 시전자 기반 동적 수치 계산
                float dynamicVal = 0f;
                if (StatScaleType == 1) dynamicVal = caster.Attack * StatMultiplier;
                else if (StatScaleType == 2) dynamicVal = caster.MaxHp * StatMultiplier;

                t.ApplyStatusEffect(EffectData, Duration, dynamicVal);
                appliedAtLeastOne = true;
            }
            
            // [변경] 저항/차단 여부와 상관없이 '시도' 자체는 완료되었으므로 true 반환 (스킬 체인 유지)
            return true;
        }
    }

    [System.Serializable]
    public class CleanseEffect : SkillEffect
    {
        [Tooltip("효과를 적용할 대상 (광역 해제: AllAllies / AllEnemies 등)")]
        public EffectTargetType TargetType = EffectTargetType.Target;

        [Tooltip("해제할 대상 (Debuff = 약화 해제, Buff = 강화 해제)")]
        public StatusEffectCategory TargetCategory = StatusEffectCategory.Debuff;

        [Tooltip("해제할 개수 (예: 1이면 가장 덜 남은 거나 가장 나중 것 1개 해제)")]
        public int Count = 1;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);
            bool cleansedAnyone = false;

            foreach (var t in actualTargets)
            {
                if (t == null || !t.IsAlive) continue;

                // [추가] 빗나감 체크 (강화효과 해제는 공격이 적중했을 때만 발생)
                if (TargetCategory == StatusEffectCategory.Buff && t.LastReceivedAttackEvaded)
                {
                    Debug.Log($"{t.Name}의 강화효과 해제가 공격 빗나감으로 인해 무시되었습니다.");
                    continue;
                }

                int num = t.RemoveStatusEffects(TargetCategory, Count);

                if (TargetCategory == StatusEffectCategory.Buff)
                {
                    if (BattleVFXManager.Instance != null && num > 0)
                        BattleVFXManager.Instance.SpawnVFX(VFXType.RemoveBuff, t.View.RetHitbox());


                    Debug.Log($"{t.Name}의 강화효과가 {num}개 해제되었습니다!");
                }
                else
                {
                    if (BattleVFXManager.Instance != null && num > 0)
                        BattleVFXManager.Instance.SpawnVFX(VFXType.RemoveDebuff, t.View.RetHitbox());
                    Debug.Log($"{t.Name}의 약화효과가 {num}개 해제되었습니다!");
                }

                cleansedAnyone = true;
            }

            // [변경] 항상 true 반환 (스킬 체인 유지)
            return true;
        }
    }
}
