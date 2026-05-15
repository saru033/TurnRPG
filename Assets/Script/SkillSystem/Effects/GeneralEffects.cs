using UnityEngine;
using System.Linq;

namespace TurnRPG.SkillSystem.Effects
{
    // 효과의 실제 적용 대상을 정하기 위한 열거형
    public enum EffectTargetType
    {
        Target,             // 지정한 단일 대상 (디폴트)
        Caster,             // 스킬 시전자 자신
        AllAllies,          // 시전자 기준 아군 전체
        AllEnemies,         // 시전자 기준 적군 전체
        TriggerVictim,      // [특수용] 방아쇠(패시브 등)를 촉발시킨 사건의 피해자/당사자
        AllDeadAllies,      // 사망한 아군 전체 (부활 전용 광역기)
        RandomAlly,         // 생존한 무작위 아군 1명
        RandomEnemy,        // 생존한 무작위 적 1명
        LowestHpEnemy       // 생존한 적 중 현재 체력(%) 비율이 가장 낮은 적 1명
    }

    // EffectTargetType에 맞게 실제 타겟 리스트를 반환해주는 헬퍼
    public static class EffectTargetHelper
    {
        public static System.Collections.Generic.List<BattleCharacter> GetActualTargets(BattleCharacter caster, BattleCharacter explicitTarget, EffectTargetType type)
        {
            var list = new System.Collections.Generic.List<BattleCharacter>();

            switch (type)
            {
                case EffectTargetType.Target:
                    if (explicitTarget != null) list.Add(explicitTarget);
                    break;
                case EffectTargetType.Caster:
                    if (caster != null) list.Add(caster);
                    break;
                case EffectTargetType.AllAllies:
                    if (BattleManager.Instance != null && caster != null)
                        list.AddRange(BattleManager.Instance.allCharacters.Where(c => c.IsAlive && c.IsPlayer == caster.IsPlayer));
                    break;
                case EffectTargetType.AllEnemies:
                    if (BattleManager.Instance != null && caster != null)
                        list.AddRange(BattleManager.Instance.allCharacters.Where(c => c.IsAlive && c.IsPlayer != caster.IsPlayer));
                    break;
                case EffectTargetType.TriggerVictim:
                    if (BattleEventManager.LastVictim != null)
                        list.Add(BattleEventManager.LastVictim);
                    break;
                case EffectTargetType.RandomAlly:
                    if (BattleManager.Instance != null && caster != null)
                    {
                        var aliveAllies = BattleManager.Instance.allCharacters.Where(c => c.IsAlive && c.IsPlayer == caster.IsPlayer).ToList();
                        if (aliveAllies.Count > 0)
                        {
                            list.Add(aliveAllies[Random.Range(0, aliveAllies.Count)]);
                        }
                    }
                    break;
                case EffectTargetType.RandomEnemy:
                    if (BattleManager.Instance != null && caster != null)
                    {
                        var aliveEnemies = BattleManager.Instance.allCharacters.Where(c => c.IsAlive && c.IsPlayer != caster.IsPlayer).ToList();
                        if (aliveEnemies.Count > 0)
                        {
                            list.Add(aliveEnemies[Random.Range(0, aliveEnemies.Count)]);
                        }
                    }
                    break;
                case EffectTargetType.LowestHpEnemy:
                    if (BattleManager.Instance != null && caster != null)
                    {
                        var aliveEnemies = BattleManager.Instance.allCharacters.Where(c => c.IsAlive && c.IsPlayer != caster.IsPlayer).ToList();
                        if (aliveEnemies.Count > 0)
                        {
                            var lowestHpEnemy = aliveEnemies.OrderBy(c => c.CurrentHp / c.MaxHp).First();
                            list.Add(lowestHpEnemy);
                        }
                    }
                    break;
                case EffectTargetType.AllDeadAllies:
                    if (BattleManager.Instance != null && caster != null)
                    {
                        var deadAllies = BattleManager.Instance.allCharacters.Where(c => !c.IsAlive && c.IsPlayer == caster.IsPlayer).ToList();
                        list.AddRange(deadAllies);
                    }
                    break;
            }
            return list;
        }
    }

    // 행동 게이지 증감을 처리하는 즉발형 일반 효과
    [System.Serializable]
    public class ActionGaugeEffect : SkillEffect
    {
        [Tooltip("효과를 적용할 대상")]
        public EffectTargetType TargetType = EffectTargetType.Target;

        [Tooltip("행동 게이지 변화량 (예: 20이면 20증가, -20이면 20감소)")]
        public float Amount;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);

            foreach (var t in actualTargets)
            {
                // [추가] 빗나감 체크 (행동 게이지 감소는 공격이 적중했을 때만 발생)
                if (Amount < 0 && t.LastReceivedAttackEvaded)
                {
                    Debug.Log($"{t.Name}의 행동 게이지 감소가 공격 빗나감으로 인해 무시되었습니다.");
                    continue;
                }

                if (t.ActionGaugeSystem != null)
                {
                    t.ActionGaugeSystem.ModifyGauge(t, Amount);
                }
                else
                {
                    t.ActionGauge = Mathf.Clamp(t.ActionGauge + Amount, 0f, 100f);
                }

                if (Amount > 0)
                    Debug.Log($"{t.Name}의 행동 게이지가 {Amount}만큼 증가했습니다.");
                else
                    Debug.Log($"{t.Name}의 행동 게이지가 {-Amount}만큼 감소했습니다.");
            }

            return true;
        }
    }

    // 쿨타임 증감을 처리하는 즉발형 일반 효과
    [System.Serializable]
    public class CooldownEffect : SkillEffect
    {
        [Tooltip("효과를 적용할 대상")]
        public EffectTargetType TargetType = EffectTargetType.Target;

        [Tooltip("쿨타임 변화 턴 수 (양수=증가(디버프성), 음수=감소(버프성))")]
        public int TurnAmount;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);

            foreach (var t in actualTargets)
            {
                // [추가] 빗나감 체크 (쿨타임 증가는 공격이 적중했을 때만 발생)
                if (TurnAmount > 0 && t.LastReceivedAttackEvaded)
                {
                    Debug.Log($"{t.Name}의 쿨타임 증가가 공격 빗나감으로 인해 무시되었습니다.");
                    continue;
                }

                // 모든 장착 스킬 쿨타임 변경 (궁극기는 제외할지 여부는 별도 처리 가능)
                for (int i = 0; i < t.SkillCooldowns.Length; i++)
                {
                    int currentCooldown = t.SkillCooldowns[i];
                    int newCooldown = currentCooldown + TurnAmount;

                    // [추가] 쿨타임 증가 시, 기존 스킬의 최대 쿨타임보다 높게 증가하지 않게 제한
                    if (TurnAmount > 0 && t.ActiveSkills.Count > i && t.ActiveSkills[i] != null)
                    {
                        int level = t.SkillLevels[i];
                        if (t.ActiveSkills[i].LevelDatas.Count >= level)
                        {
                            int maxCD = t.ActiveSkills[i].LevelDatas[level - 1].Cooldown;
                            newCooldown = Mathf.Min(newCooldown, maxCD);
                        }
                    }

                    t.SkillCooldowns[i] = Mathf.Max(0, newCooldown);
                }
                Debug.Log($"{t.Name}의 쿨타임이 {TurnAmount}턴 변경되었습니다.");
            }
            return true;
        }
    }

    // 추가 턴을 부여하는 일반 효과
    [System.Serializable]
    public class ExtraTurnEffect : SkillEffect
    {
        [Tooltip("추가 턴을 획득할 대상 (보통 시전자(Caster) 자신)")]
        public EffectTargetType TargetType = EffectTargetType.Target;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);
            foreach (var t in actualTargets)
            {
                // 추가 턴: 행동 게이지를 100으로 만들고, 턴 큐의 맨 앞으로 강제 삽입!
                if (t.ActionGaugeSystem != null)
                {
                    t.ActionGaugeSystem.InsertFront(t);

                    // [추가] 추가 턴 획득 알림 표시
                    if (t.View != null) t.View.ShowPassiveNotice("추가 턴");

                    // 시각 이펙트(VFX) 처리
                    if (BattleVFXManager.Instance != null)
                        BattleVFXManager.Instance.SpawnVFX(VFXType.ExtraTurn, t.View.RetHitbox());

                    // 자신에 턴에 자신에게 추가턴 부여시 TunrEnd 함수로 행동게이지 0으로 가있는 버그 해소용 장치
                    if (caster == t)
                    {
                        t.isExtraTurnSelf = true;
                    }

                    Debug.Log($"{t.Name}이(가) 추가 턴을 획득하여 큐 맨 앞에 배치되었습니다!");
                }
                else
                {
                    t.ActionGauge = 100f; // 보험용
                }
            }
            return true;
        }
    }

    // -------------------------------------------------------
    // 죽은 아군을 부활시키는 일반 효과
    // -------------------------------------------------------

    public enum DualAttackTargetType
    {
        RandomAlly,             // 다른 무작위 아군 1명
        HighestAttackAlly       // 가장 공격력이 높은 다른 아군 1명
    }

    // -------------------------------------------------------
    // 아군 1명을 호출하여 1스킬로 타겟을 때리게 하는 협공 효과
    // -------------------------------------------------------
    [System.Serializable]
    public class DualAttackEffect : SkillEffect
    {
        [Tooltip("누구와 협공할 것인지 선정 방식")]
        public DualAttackTargetType HelperSelection = DualAttackTargetType.HighestAttackAlly;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            if (target == null || !target.IsAlive) return false;


            //자기턴이 아니면 스킵
            if (caster != BattleManager.Instance.currentActor) return false;

            // 1. 배틀 매니저를 통해 전장에 있는 캐릭터 리스트를 즉시 받아옵니다.
            var allChars = BattleManager.Instance != null
                            ? BattleManager.Instance.allCharacters
                            : new System.Collections.Generic.List<BattleCharacter>();

            // '나를 제외한', '생존해있는', '같은 편', '행동 불가(기절/수면)가 아닌' 아군을 싹 긁어모읍니다.
            var eligibleAllies = allChars.Where(c =>
                c.IsAlive &&
                c.IsPlayer == caster.IsPlayer &&
                c != caster &&
                !c.HasStatusEffect(StatusEffectType.Stun) &&
                !c.HasStatusEffect(StatusEffectType.Sleep)
            ).ToList();

            if (eligibleAllies.Count == 0)
            {
                Debug.Log("같이 협공할 아군이 전장에 없습니다!");
                return false;
            }

            BattleCharacter helper = null;

            if (HelperSelection == DualAttackTargetType.RandomAlly)
            {
                helper = eligibleAllies[Random.Range(0, eligibleAllies.Count)];
            }
            else if (HelperSelection == DualAttackTargetType.HighestAttackAlly)
            {
                helper = eligibleAllies.OrderByDescending(c => c.Attack).First();
            }

            if (helper != null)
            {
                Debug.Log($"[협공] {caster.Name}이(가) {helper.Name}에게 {target.Name}을(를) 협공하도록 요청했습니다!");
                // BattleManager의 우선순위 연출 큐(Front)에 1스킬 반격 루틴 예약
                if (BattleManager.Instance != null)
                {
                    BattleManager.Instance.isProcessingDualAttack = true; // 협공 발생 마킹
                    var routine = BattleManager.Instance.CombinationAttackRoutine(helper, target);
                    BattleManager.Instance.EnqueueExtraActionFront(routine);
                }

                return true;
            }

            return false;
        }
    }

    // -------------------------------------------------------
    // [패시브 전용] 트리거가 충족되면 지정된 대상에게 내 1스킬로 추가타/반격을 때리는 효과
    // -------------------------------------------------------
    [System.Serializable]
    public class CastBasicSkillEffect : SkillEffect
    {
        [Tooltip("누구에게 1스킬을 사용할지 (보통 Target = 조건 발생 대상)")]
        public EffectTargetType TargetType = EffectTargetType.Target;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);
            foreach (var t in actualTargets)
            {
                if (t == null || !t.IsAlive) continue;

                Debug.Log($"[패시브 발동!] {caster.Name}이(가) {t.Name}에게 1스킬로 반격/추가타를 날립니다!");

                // BattleManager의 우선순위 연출 큐(Front)에 1스킬 반격 루틴 예약
                if (BattleManager.Instance != null)
                {
                    var routine = BattleManager.Instance.CounterAttackRoutine(caster, t);
                    BattleManager.Instance.EnqueueExtraActionFront(routine);
                }
            }
            return actualTargets.Count > 0;
        }
    }

    // =======================================================
    // 투트랙 시스템 특수 부품 모음 (상시능력 / 흐름제어 필터)
    // =======================================================

    [System.Serializable]
    public class PassiveStatBoostEffect : SkillEffect
    {
        [Tooltip("효과를 적용할 대상 (예: Caster, AllAllies 등)")]
        public EffectTargetType TargetType = EffectTargetType.Caster;

        [Tooltip("방어력 등 어떤 기본 스탯을 영구 증가시킬지 (예: Def, Atk, Hp, Spd, CritChance, CritDmg, Evasion)")]
        public string TargetStat = "Def";
        [Tooltip("증가 비율 (예: 0.2 = 20% 증가)")]
        public float PercentAmount = 0.2f;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);
            bool executed = false;

            foreach (var t in actualTargets)
            {
                if (t == null) continue;

                string stat = TargetStat.ToLower();
                if (stat.Contains("def")) t.BaseDefense *= (1f + PercentAmount);
                else if (stat.Contains("atk")) t.BaseAttack *= (1f + PercentAmount);
                else if (stat.Contains("hp")) t.BaseMaxHp *= (1f + PercentAmount);
                else if (stat.Contains("spd")) t.BaseSpeed *= (1f + PercentAmount);
                else if (stat.Contains("critchance")) t.BaseCritChance += PercentAmount;
                else if (stat.Contains("critdmg") || stat.Contains("critdamage")) t.BaseCritDamage += PercentAmount;
                else if (stat.Contains("evasion")) t.BaseEvasionRate += PercentAmount;
                else if (stat.Contains("dual")) t.BaseDualAttackChance += PercentAmount;

                executed = true;
            }

            return executed;
        }
    }

    [System.Serializable]
    public class PassiveDamageReduceEffect : SkillEffect
    {
        [Tooltip("효과를 적용할 대상 (예: Caster, AllAllies 등)")]
        public EffectTargetType TargetType = EffectTargetType.Caster;

        [Tooltip("받는 피해량을 영구적으로 X% 감소 (예: 0.2 = 20% 감소)")]
        [Range(0f, 1f)] public float ReduceAmount = 0.2f;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);
            bool executed = false;

            foreach (var t in actualTargets)
            {
                if (t == null) continue;
                t.PassiveDamageReduction += ReduceAmount;
                executed = true;
            }

            return executed;
        }
    }

    [System.Serializable]
    public class PassiveImmunityEffect : SkillEffect
    {
        [Tooltip("효과를 적용할 대상 (예: Caster, AllAllies 등)")]
        public EffectTargetType TargetType = EffectTargetType.Caster;

        [Tooltip("전투 내내 무시할 상태이상 타입")]
        public StatusEffectType ImmuneType;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);
            bool executed = false;

            foreach (var t in actualTargets)
            {
                if (t == null) continue;

                if (!t.PermanentImmunities.Contains(ImmuneType))
                {
                    t.PermanentImmunities.Add(ImmuneType);
                    Debug.Log($"{t.Name}은(는) 이제부터 [{ImmuneType}]에 절대 걸리지 않습니다.");
                }
                executed = true;
            }

            return executed;
        }
    }

    [System.Serializable]
    public class CheckCurrentHpConditionEffect : SkillEffect
    {
        [Tooltip("누구의 체력을 검사할 것인가 (예: Target = 적 대상, Caster = 나 자신)")]
        public EffectTargetType TargetType = EffectTargetType.Caster; // 기존 하위호환을 위해 기본값 본인

        [Tooltip("기준 체력 퍼센트 (예: 0.5 = 50%)")]
        [Range(0f, 1f)] public float ThresholdPercent = 0.3f;

        [Tooltip("이하면 참인가? (체크 해제 시 이상이면 참)")]
        public bool IsBelow = true;

        public override bool IsCondition => true;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);

            foreach (var t in actualTargets)
            {
                if (t == null || !t.IsAlive) continue;

                float currentPct = t.CurrentHp / t.MaxHp;
                bool conditionMet = IsBelow ? (currentPct <= ThresholdPercent) : (currentPct >= ThresholdPercent);

                if (conditionMet)
                {
                    Debug.Log($"[필터 패스] {t.Name}의 체력이 {(currentPct * 100):F1}%로 기준치({(ThresholdPercent * 100):F1}%) {(IsBelow ? "이하" : "이상")} 조건을 만족했습니다.");
                    return true; // 한 명이라도 만족하면 체인 통과!
                }
            }

            Debug.Log($"[필터 정지] 체력 조건을 만족하는 대상이 없습니다.");
            return false; // 모두 불만족 시 정지
        }
    }

    [System.Serializable]
    public class ResetSkillCooldownEffect : SkillEffect
    {
        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            // 자신의 모든 활성 스킬 쿨타임 초기화
            for (int i = 0; i < caster.SkillCooldowns.Length; i++)
            {
                caster.SkillCooldowns[i] = 0;
            }
            Debug.Log($"[패시브 쿨타임 초기화 발동] {caster.Name}의 모든 스킬 쿨타임이 0이 되었습니다!");
            return true;
        }
    }

    [System.Serializable]
    public class CheckSkillCooldownEffect : SkillEffect
    {
        [Tooltip("검사할 본인의 스킬 슬롯 (0: 1스킬, 1: 2스킬, 2: 3스킬)")]
        public int SkillSlotIndex = 1;

        public override bool IsCondition => true;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            if (SkillSlotIndex < 0 || SkillSlotIndex >= caster.SkillCooldowns.Length) return false;

            // 쿨타임이 0초과(즉, 돌고있으면) 통과 실패 -> 하위 이펙트들 실행 체인 정지!
            if (caster.SkillCooldowns[SkillSlotIndex] > 0) return false;

            return true; // 쿨타임 0이므로 하위 이펙트 블록 실행 허용
        }
    }

    [System.Serializable]
    public class SetSkillCooldownEffect : SkillEffect
    {
        [Tooltip("쿨타임을 강제로 돌릴 본인의 스킬 슬롯 (0: 1스킬, 1: 2스킬, 2: 3스킬)")]
        public int SkillSlotIndex = 1;

        [Tooltip("적용할 쿨타임 턴 수")]
        public int CooldownTurns = 5;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            if (SkillSlotIndex >= 0 && SkillSlotIndex < caster.SkillCooldowns.Length)
            {
                caster.SkillCooldowns[SkillSlotIndex] = CooldownTurns;
                Debug.Log($"[{caster.Name}]의 조커 카드 발동! {SkillSlotIndex + 1}번째 스킬에 {CooldownTurns}턴의 쿨타임이 강제 갱신되었습니다!");
            }
            return true;
        }
    }

    [System.Serializable]
    public class CheckAllyIncapacitatedConditionEffect : SkillEffect
    {
        public override bool IsCondition => true;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            // 시전자와 같은 편인 모든 캐릭터 리스트 확보
            var allies = BattleManager.Instance.allCharacters.Where(c => c.IsAlive && c.IsPlayer == caster.IsPlayer);

            foreach (var ally in allies)
            {
                // 기절(Stun) 또는 수면(Sleep) 상태가 하나라도 있는지 체크
                if (ally.HasStatusEffect(StatusEffectType.Stun) || ally.HasStatusEffect(StatusEffectType.Sleep))
                {
                    Debug.Log($"[필터 패스] 아군 {ally.Name}이(가) 행동불가 상태입니다. 스킬 체인을 계속 진행합니다.");
                    return true;
                }
            }

            Debug.Log($"[필터 정지] 행동불가 상태인 아군이 없습니다.");
            return false; // 조건 불만족. 하위 이펙트 실행 정지.
        }
    }

    [System.Serializable]
    public class CheckStatusEffectCountConditionEffect : SkillEffect
    {
        [Tooltip("검사할 대상 (예: Target = 적 대상, Caster = 나 자신)")]
        public EffectTargetType TargetType = EffectTargetType.Target;

        [Tooltip("검사할 상태이상 종류 (Debuff = 약화 효과, Buff = 강화 효과)")]
        public StatusEffectCategory Category = StatusEffectCategory.Debuff;

        [Tooltip("기준 개수")]
        public int ThresholdCount = 5;

        [Tooltip("이하면 참인가? (체크 해제 시 이상이면 참)")]
        public bool IsBelow = false;

        public override bool IsCondition => true;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            var actualTargets = EffectTargetHelper.GetActualTargets(caster, target, TargetType);

            foreach (var t in actualTargets)
            {
                if (t == null || !t.IsAlive) continue;

                // 해당 카테고리의 상태이상 개수를 셉니다.
                int count = t.ActiveStatusEffects.Count(e => e.Category == Category);

                bool conditionMet = IsBelow ? (count <= ThresholdCount) : (count >= ThresholdCount);

                if (conditionMet)
                {
                    // 지정된 타겟(들) 중 하나라도 조건을 만족하면 무조건 체인을 이어갑니다.
                    Debug.Log($"[필터 패스] {t.Name}의 {Category} 개수가 {count}개로 조건을 만족했습니다!");
                    return true;
                }
            }

            // 모든 타겟이 조건을 만족하지 못하면 체인을 끊습니다.
            Debug.Log($"[필터 정지] 조건을 만족하는 대상이 없습니다 ({Category} 개수 기준 {ThresholdCount}개 {(IsBelow ? "이하" : "이상")})");
            return false;
        }
    }

    [System.Serializable]
    public class CheckEvadedConditionEffect : SkillEffect
    {
        [Tooltip("true일 경우 피격 시 회피에 성공했어야만 하위 이펙트를 실행합니다.")]
        public bool RequireEvaded = true;

        public override bool IsCondition => true;

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            if (RequireEvaded)
            {
                if (!BattleEventManager.LastAttackWasEvaded)
                {
                    Debug.Log($"[필터 정지] 방금 피격에서 회피하지 못했으므로 반격 등의 이펙트를 취소합니다.");
                    return false;
                }
                Debug.Log($"[필터 패스] 방금 피격에서 회피했습니다! 반격 체인을 이어갑니다!");
            }
            return true;
        }
    }

    [System.Serializable]
    public class TriggerBranchEffect : SkillEffect
    {
        [Tooltip("오직 이 트리거일 때만 하위 이펙트 리스트를 실행합니다.")]
        public PassiveTriggerType RequiredTrigger = PassiveTriggerType.OnTurnEnd;

        [Tooltip("이 조건이 맞을 때만 실행될 이펙트 서브 체인")]
        [SerializeReference] public System.Collections.Generic.List<SkillEffect> BranchEffects = new System.Collections.Generic.List<SkillEffect>();

        public override bool Execute(BattleCharacter caster, BattleCharacter target)
        {
            // 이 브랜치가 현재 트리거와 일치하지 않으면 무시하고 (True 반환하여) 메인 체인을 계속 이어가게 함
            if (!BattleEventManager.CurrentExecutingTrigger.HasFlag(RequiredTrigger)) return true;

            // 트리거가 일치하면 하위 체인을 순서대로 실행
            foreach (var eff in BranchEffects)
            {
                if (eff == null) continue;
                if (!eff.Execute(caster, target))
                {
                    // 하위 체인의 어떤 필터가 False를 반환하면 서브 체인만 중단함
                    Debug.Log($"[서브 브랜치 중단] {eff.GetType().Name} 조건 불만족으로 서브 체인 실행을 중지합니다.");
                    break;
                }
            }

            // 서브 체인이 중지되었더라도, 메인 체인(이 블록 밑의 다른 블록)은 계속 실행되어야 하므로 무조건 True 반환
            return true;
        }
    }
}
