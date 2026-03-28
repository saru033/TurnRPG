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
                
                // 면역 체크 (디버프일 경우)
                if (EffectData.Category == StatusEffectCategory.Debuff && t.HasStatusEffect(StatusEffectType.Immunity))
                {
                    Debug.Log($"{t.Name}은(는) 면역 상태입니다! 디버프 무시");
                    continue;
                }

                // 시전자 기반 동적 수치 계산
                float dynamicVal = 0f;
                if (StatScaleType == 1) dynamicVal = caster.Attack * StatMultiplier;
                else if (StatScaleType == 2) dynamicVal = caster.MaxHp * StatMultiplier;

                t.ApplyStatusEffect(EffectData, Duration, dynamicVal);
                appliedAtLeastOne = true;
            }

            return appliedAtLeastOne;
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

                t.RemoveStatusEffects(TargetCategory, Count);
                
                if (TargetCategory == StatusEffectCategory.Buff)
                    Debug.Log($"{t.Name}의 강화효과가 {Count}개 해제되었습니다!");
                else
                    Debug.Log($"{t.Name}의 약화효과가 {Count}개 해제되었습니다!");

                cleansedAnyone = true;
            }
                
            return cleansedAnyone;
        }
    }
}
