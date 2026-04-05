using System.Collections.Generic;
using UnityEngine;
using TurnRPG.SkillSystem;
using System.Linq;

/// <summary>
/// 전투에 참여하는 캐릭터 하나의 데이터.
/// 아군/적군 공통으로 사용.
/// </summary>
public class BattleCharacter
{
    // -------------------------------------------------------
    // 원본 데이터 참조
    // -------------------------------------------------------
    public CharacterData Data { get; private set; }
    public Animator Animator { get; private set; }
    public CharacterView View { get; set; } // [추가] 시각적 뷰 객체 참조
    public GameObject ViewObject { get; private set; } // 캐릭터 이펙트 부착용 등

    // -------------------------------------------------------
    // 기본 정보
    // -------------------------------------------------------
    public int ID;
    public string Name;
    public bool IsPlayer;   // true = 아군, false = 적

    // -------------------------------------------------------
    // 스탯 (전투 중 버프 등으로 변경 가능)
    // -------------------------------------------------------
    public float MaxHp;
    public float Attack;
    public float CurrentHp;
    public float Defense;        // 방어력
    public float Speed;          // 속도
    public float CritChance;     // 치명 확률 (0~1)
    public float CritDamage;     // 치명 피해 배율 (예: 1.5 = 150%)
    public float EvasionRate;    // 회피율 (0~1)
    public float AccuracyRate;   // 명중률 (0~1)

    // -------------------------------------------------------
    // 원본 스탯 (버프/디버프 계산의 기준점)
    // -------------------------------------------------------
    public float BaseMaxHp;
    public float BaseAttack;
    public float BaseDefense;
    public float BaseSpeed;
    public float BaseCritChance;
    public float BaseCritDamage;
    public float BaseEvasionRate;
    public float BaseAccuracyRate;

    // -------------------------------------------------------
    // 행동게이지 및 스킬 세팅
    // -------------------------------------------------------
    public float ActionGauge;    // 현재 게이지 (0 ~ 100)
    public Actiongaugesystem ActionGaugeSystem { get; set; } // [추가] 게이지 시스템 참조

    // 최대 3개의 스킬 슬롯 유지
    public List<SkillData> ActiveSkills = new List<SkillData>(3);
    public int[] SkillLevels = new int[3] { 1, 1, 1 }; // 디폴트 1레벨
    public int[] SkillCooldowns = new int[3]; // 현재 남은 쿨타임 턴수

    // 이번 턴에 내가 사용한 스킬(차감 방지를 위함)
    public int CastedSkillIndexThisTurn = -1;

    public List<StatusEffect> ActiveStatusEffects = new List<StatusEffect>();
    public HashSet<StatusEffect> AppliedBuffsThisTurn = new HashSet<StatusEffect>();

    // -------------------------------------------------------
    // 상시 패시브용 특수 스탯 (오라/면역)
    // -------------------------------------------------------
    public float PassiveDamageReduction = 0f; // 받는 피해 감소량 (0.2 = 20% 감소)
    public List<StatusEffectType> PermanentImmunities = new List<StatusEffectType>(); // 영구 면역 리스트

    // -------------------------------------------------------
    // 상태
    // -------------------------------------------------------
    public bool LastReceivedAttackEvaded; // [추가] 마지막으로 받은 공격의 회피 여부
    public bool IsAlive => CurrentHp > 0f;


    public bool isExtraTurnSelf = false;

    // -------------------------------------------------------
    // 생성자 (ScriptableObject 원본 데이터로 생성)
    // -------------------------------------------------------
    public BattleCharacter(CharacterData data)
    {
        Data = data;
        ID = data.ID;
        Name = data.CharacterName;
        IsPlayer = data.isPlayer;

        // 원본 및 현재 스탯 초기화
        BaseMaxHp = MaxHp = data.MaxHp;
        BaseAttack = Attack = data.Attack;
        CurrentHp = data.MaxHp;
        BaseDefense = Defense = data.Defense;
        BaseSpeed = Speed = data.Speed;
        BaseCritChance = CritChance = data.CritChance;
        BaseCritDamage = CritDamage = data.CritDamage;
        BaseEvasionRate = EvasionRate = data.Evasion;
        BaseAccuracyRate = AccuracyRate = data.Accuracy;

        ActionGauge = 0f;

        // 시작 스킬 장착 (데이터의 SlotIndex 기반으로 제 자리에 장착)
        if (data.StartingSkills != null)
        {
            foreach (var skillSO in data.StartingSkills)
            {
                if (skillSO != null)
                {
                    EquipSkill((int)skillSO.SlotIndex, skillSO, 1); // 1레벨 기본 장착
                }
            }
        }
    }

    public void SetAnimator(Animator animator)
    {
        Animator = animator;
    }

    public void SetViewObject(GameObject obj)
    {
        ViewObject = obj;
    }

    /// <summary>
    /// 지정한 이름의 애니메이션 상태가 현재 재생 중인지 확인합니다. (0번 레이어 기준)
    /// </summary>
    public bool IsAnimationPlaying(string stateName)
    {
        if (Animator == null) return false;
        var info = Animator.GetCurrentAnimatorStateInfo(0);
        return info.IsName(stateName);
    }

    // -------------------------------------------------------
    // 피해 및 회복 계산
    // -------------------------------------------------------
    public float TakeDamage(float rawDamage, BattleCharacter attacker = null, float penetration = 0f, bool isEvaded = false, bool cannotBeCountered = false, bool isCritical = false, bool isBurn = false)
    {
        // 빗나감 상태 저장 (이후 스킬 체인의 디버프 적용 여부 판단용)
        LastReceivedAttackEvaded = isEvaded;

        if (HasStatusEffect(StatusEffectType.Invincible))
        {
            Debug.Log($"{Name} 무적 상태! 데미지 0");
            return 0;
        }

        if (HasStatusEffect(StatusEffectType.SkillDmgNullify) && !isBurn)
        {
            Debug.Log($"{Name} 스킬 데미지 1회 무효화!");
            RemoveStatusEffect(StatusEffectType.SkillDmgNullify);
            return 0;
        }

        // 1. 회피 체크 (외부에서 판정된 isEvaded 사용)
        if (isEvaded)
        {
            Debug.Log($"{Name} 회피 성공! (이미 외부에서 피해가 50% 감소됨)");
            // 추가로 이벤트 쏴서 팝업창에 '빗나감' 처리 가능
        }

        // 2. 방어력 계산 (관통 적용)
        float targetDefense = Defense * (1f - Mathf.Clamp01(penetration));
        float reduction = targetDefense / (targetDefense + 200f);
        float actualDamage = rawDamage * (1f - reduction);

        // 상시 피해 감소 패시브 적용 (투기장 체르미아 효과 등)
        actualDamage *= 1f - Mathf.Clamp01(PassiveDamageReduction);

        actualDamage = Mathf.Max(1f, actualDamage);   // 최소 1 피해

        // 보호막 차감 로직!
        var shield = ActiveStatusEffects.FirstOrDefault(e => e.Type == StatusEffectType.Shield);
        if (shield != null)
        {
            if (shield.DynamicValue >= actualDamage)
            {
                shield.DynamicValue -= actualDamage; // 데미지 전면 흡수
                Debug.Log($"{Name} : 보호막이 {actualDamage} 피해를 방어했습니다. (남은량: {shield.DynamicValue})");
                return 0; // 본체엔 0피해
            }
            else
            {
                actualDamage -= shield.DynamicValue; // 남은 데미지 관통
                Debug.Log($"{Name} : 보호막이 파괴되었습니다! ({shield.DynamicValue} 방어완료)");
                shield.DynamicValue = 0;

                // 쉴드 파괴 시 즉시 삭제 처리
                shield.DestroyVFX();
                ActiveStatusEffects.Remove(shield);
                BattleEventManager.TriggerStatusEffectChanged(this, shield); // 삭제 알림
            }
        }

        CurrentHp = Mathf.Max(0f, CurrentHp - actualDamage);

        // [추가] 수면 상태 해제 (데미지가 0보다 클 때)
        if (actualDamage > 0 && HasStatusEffect(StatusEffectType.Sleep))
        {
            RemoveStatusEffect(StatusEffectType.Sleep);
        }

        // [추가] 내가 현재 턴 캐릭터인 경우 사이드 UI HP 실시간 업데이트
        var bm = BattleManager.Instance;
        if (bm != null && bm.battleUI != null && bm.currentActor == this)
        {
            bm.battleUI.ImgSideHpUpdate(this);
        }

        // 피격 애니메이션 트리거 (데미지가 0보다 클 때만)
        if (actualDamage > 0 && Animator != null)
        {
            Animator.SetTrigger("hit");
        }

        // [추가] 은신(Stealth) 해제 로직: 화상/출혈이 아니고, 1 이상의 실제 피해를 입었을 때 해제
        if (!isBurn && actualDamage > 0 && HasStatusEffect(TurnRPG.SkillSystem.StatusEffectType.Stealth))
        {
            Debug.Log($"{Name} : 피해를 입어 은신이 해제되었습니다.");
            RemoveStatusEffect(TurnRPG.SkillSystem.StatusEffectType.Stealth);
        }

        // 반격불가 여부를 이벤트 매니저에게 같이 전달하여 반격이 터지지 않도록 함
        BattleEventManager.TriggerDamageTaken(this, attacker, actualDamage, cannotBeCountered, isEvaded, isCritical);



        // 출혈,화상으로 데미지를 입은게 아니고 , 기절/수면이 없고, 반격 버프가 있고 , 공격이 반격 불가가 아닌 경우, 공격자가 현재 턴인 경우
        if (!isBurn && !cannotBeCountered && !HasStatusEffect(StatusEffectType.Stun) && !HasStatusEffect(StatusEffectType.Sleep) && HasStatusEffect(StatusEffectType.CounterAttack) && attacker != null && attacker == BattleManager.Instance.currentActor)
        {
            //반격
            CounterAttack(attacker);
        }



        return actualDamage;
    }


    public void CounterAttack(BattleCharacter target)
    {
        BattleManager.Instance.EnqueueExtraAction(BattleManager.Instance.CounterAttackRoutine(this, target));
    }


    public void Heal(float amount)
    {
        if (CurrentHp <= 0 || HasStatusEffect(StatusEffectType.Unhealable)) return;

        CurrentHp = Mathf.Min(CurrentHp + amount, MaxHp);
        BattleEventManager.TriggerHealed(this, amount);
    }

    public (float damage, bool isCrit) CalcDamage(float baseDamage, bool isCriResist = false)
    {
        bool isCrit;
        if (isCriResist)
            isCrit = Random.value < CritChance - 0.5f;

        else
            isCrit = Random.value < CritChance;


        float finalDamage = isCrit ? baseDamage * CritDamage : baseDamage;
        return (finalDamage, isCrit);
    }

    // -------------------------------------------------------
    // 스킬 관련 로직
    // -------------------------------------------------------
    public void EquipSkill(int slotIndex, SkillData skillSO, int initialLevel = 1)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;

        if (skillSO != null && (int)skillSO.SlotIndex != slotIndex)
        {
            Debug.LogWarning($"{Name}의 {slotIndex + 1}번 슬롯에 {skillSO.SlotIndex}용 스킬({skillSO.SkillName})을 장착하려 했습니다! 장착을 취소합니다.");
            return;
        }

        // 3칸보다 작으면 빈칸 채우기
        while (ActiveSkills.Count <= slotIndex) ActiveSkills.Add(null);

        ActiveSkills[slotIndex] = skillSO;
        SkillLevels[slotIndex] = initialLevel;
        SkillCooldowns[slotIndex] = 0;
    }

    // 턴이 시작될 때 화상 틱 발생, 보호 목록 초기화 등
    public void OnTurnStart()
    {
        if (!IsAlive) return;

        // 쿨타임 및 버프 감소는 OnTurnEnd() 로 이동. 턴 시작 시에는 이번 턴 판정 변수들을 리셋
        CastedSkillIndexThisTurn = -1;
        AppliedBuffsThisTurn.Clear();

        // 상태이상 피해 (출혈, 화상 등)
        foreach (var eff in ActiveStatusEffects.ToList()) // 데미지로 죽어서 리스트 변할 수 있으니 ToList 복사
        {
            if (eff.Type == StatusEffectType.Bleed || eff.Type == StatusEffectType.Burn)
            {
                TakeDamage(eff.DynamicValue, isCritical: false, isBurn: true);
                Debug.Log($"{Name} : {eff.Type} 피해로 {eff.DynamicValue} 데미지를 받음!");
            }
        }

        // 자동 회복
        if (HasStatusEffect(StatusEffectType.AutoHeal))
        {
            Heal(MaxHp * 0.15f);
            Debug.Log($"{Name} : 자동 회복 효과로 {MaxHp * 0.15f}만큼 회복!");
        }

        BattleEventManager.TriggerTurnStarted(this);
        UpdateControlAnimator(); // [추가] 턴 시작 시 기절/수면 체크
    }

    public void OnTurnEnd()
    {
        if (!IsAlive) return;

        // 쿨타임 감소 (이번 턴에 방금 쓴 스킬은 감소 제외!)
        for (int i = 0; i < SkillCooldowns.Length; i++)
        {
            if (i == CastedSkillIndexThisTurn) continue;
            if (SkillCooldowns[i] > 0) SkillCooldowns[i]--;
        }

        // 지속시간 감소 및 만료된 버프 해제
        for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
        {
            var eff = ActiveStatusEffects[i];

            // 이번 내 턴에 방금 부여된(스스로 버프를 건) 효과면 턴 유지!
            if (AppliedBuffsThisTurn.Contains(eff)) continue;

            eff.RemainingDuration--;
            BattleEventManager.TriggerStatusEffectChanged(this, eff); // [추가] 수치 변경 알림

            if (eff.RemainingDuration <= 0)
            {
                eff.DestroyVFX();
                ActiveStatusEffects.RemoveAt(i);
                // 삭제 시에도 알림 (중복 호출되어도 CharacterView에서 삭제 처리됨)
                BattleEventManager.TriggerStatusEffectChanged(this, eff);
            }
        }

        // 상태이상 변화가 있었으므로 스탯 재산정
        RefreshStats();

        BattleEventManager.TriggerTurnEnded(this);
        UpdateControlAnimator(); // [추가] 턴 종료 시 기절/수면 체크
    }

    // -------------------------------------------------------
    // 상태이상 관련 (버프/디버프)
    // -------------------------------------------------------
    public bool HasStatusEffect(StatusEffectType type)
    {
        return ActiveStatusEffects.Any(e => e.Type == type);
    }

    public void ApplyStatusEffect(StatusEffectData data, int duration, float dynamicValue = 0f)
    {
        if (data == null) return;

        // 영구 면역 패시브에 등록된 상태이상인지 체크
        if (PermanentImmunities.Contains(data.EffectType))
        {
            Debug.Log($"{Name}은(는) 상시 패시브 효과로 인해 [{data.EffectType}]에 면역되었습니다!");
            return;
        }

        // 동일 계열 상위/하위 버프 체크 및 교체 로직
        // 예: 공증(AtkUp50)이 있는데 공증대(AtkUp70)가 들어오면 기존 것을 지운다.
        HandleBuffUpgrade(data.EffectType);

        var existing = ActiveStatusEffects.FirstOrDefault(e => e.Type == data.EffectType);
        if (existing != null)
        {
            // 이미 있으면 지속시간 갱신 및 (기획에 따라) 수치 덮어씌우기 혹은 높은 쪽 유지 등
            existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, duration);
            if (dynamicValue > existing.DynamicValue) existing.DynamicValue = dynamicValue;

            // [추가] 갱신 시에도 알림을 주어 UI 숫자 반영
            BattleEventManager.TriggerStatusEffectChanged(this, existing);
        }
        else
        {
            var newEff = new StatusEffect(data, duration, dynamicValue);

            // 시각 이펙트(VFX) 처리
            if (data.VFXPrefab != null && ViewObject != null)
            {
                // StatusEffect 생성자에 VFX 생성을 맡기거나 여기서 수동 생성 후 할당
                GameObject vfx = Object.Instantiate(data.VFXPrefab, ViewObject.transform.position, Quaternion.identity, ViewObject.transform);
                newEff.SpawnedVFX = vfx;
            }

            ActiveStatusEffects.Add(newEff);
            AppliedBuffsThisTurn.Add(newEff); // 생존 보장 등록
            BattleEventManager.TriggerStatusEffectChanged(this, newEff);
            BattleEventManager.TriggerStatusEffectApplied(this, newEff); // 신규 부여 시점 전송
        }

        // 스탯 변동 버프일 수 있으므로 재산정
        RefreshStats();
        UpdateControlAnimator(); // [추가] 신규 효과 부여 시 기절/수면 체크
    }

    /// <summary>
    /// 동일 계열의 버프가 이미 있을 때, 새로 들어오는 버프의 종류에 따라 기존 버프를 제거하거나 무시하는 로직
    /// </summary>
    private void HandleBuffUpgrade(StatusEffectType newType)
    {
        // 공격력 증가 계열 예시
        if (newType == StatusEffectType.AtkUp50 || newType == StatusEffectType.AtkUp70)
        {
            // 아예 같은 타입은 ApplyStatusEffect 본문에서 지속시간 갱신으로 처리하므로,
            // 여기서는 '서로 다른' 공증/공증대 중첩을 막기 위해 다른 쪽을 찾아 제거함.
            var targetType = (newType == StatusEffectType.AtkUp50) ? StatusEffectType.AtkUp70 : StatusEffectType.AtkUp50;
            var other = ActiveStatusEffects.FirstOrDefault(e => e.Type == targetType);
            if (other != null)
            {
                // 강한 것 위주 유지
                if (newType == StatusEffectType.AtkUp50 && other.Type == StatusEffectType.AtkUp70) return;

                other.DestroyVFX();
                ActiveStatusEffects.Remove(other);
                BattleEventManager.TriggerStatusEffectChanged(this, other);
            }
        }

        // 방어력 증가 계열
        if (newType == StatusEffectType.DefUp50 || newType == StatusEffectType.DefUp70)
        {
            var targetType = (newType == StatusEffectType.DefUp50) ? StatusEffectType.DefUp70 : StatusEffectType.DefUp50;
            var other = ActiveStatusEffects.FirstOrDefault(e => e.Type == targetType);
            if (other != null)
            {
                if (newType == StatusEffectType.DefUp50 && other.Type == StatusEffectType.DefUp70) return;
                other.DestroyVFX();
                ActiveStatusEffects.Remove(other);
                BattleEventManager.TriggerStatusEffectChanged(this, other);
            }
        }
    }

    /// <summary>
    /// 현재 활성화된 모든 상태이상을 체크하여 실시간 스탯을 다시 계산합니다.
    /// </summary>
    public void RefreshStats()
    {
        // 1. 배율 및 합산값 초기화
        float atkMod = 0f;
        float defMod = 0f;
        float speedMod = 0f;
        float critChanceMod = 0f;
        float critDmgMod = 0f;
        float accuracyMod = 0f;
        float evasionMod = 0f;

        // 2. 모든 효과 순회하며 합산
        foreach (var eff in ActiveStatusEffects)
        {
            switch (eff.Type)
            {
                case StatusEffectType.AtkUp50:
                case StatusEffectType.AtkUp70:
                    atkMod += eff.PrimaryValue; break;
                case StatusEffectType.AtkDown50:
                    atkMod -= eff.PrimaryValue; break;

                case StatusEffectType.DefUp50:
                case StatusEffectType.DefUp70:
                    defMod += eff.PrimaryValue; break;
                case StatusEffectType.DefDown50:
                    defMod -= eff.PrimaryValue; break;

                case StatusEffectType.SpeedUp50: speedMod += eff.PrimaryValue; break;
                case StatusEffectType.SpeedDown50: speedMod -= eff.PrimaryValue; break;

                case StatusEffectType.CritChanceUp50: critChanceMod += eff.PrimaryValue; break;
                case StatusEffectType.CritDmgUp50: critDmgMod += eff.PrimaryValue; break;

                case StatusEffectType.Accuracy50: accuracyMod += eff.PrimaryValue; break;
                case StatusEffectType.AccuracyDown50: accuracyMod -= eff.PrimaryValue; break;

                case StatusEffectType.Evasion50: evasionMod += eff.PrimaryValue; break;
            }
        }

        // 3. 최종 스탯 적용 (합연산 방식)
        Attack = BaseAttack * (1f + atkMod);
        Defense = BaseDefense * (1f + defMod);
        Speed = BaseSpeed * (1f + speedMod);
        CritChance = BaseCritChance + critChanceMod;
        CritDamage = BaseCritDamage + critDmgMod;     // 치명 피해 배율 계산
        AccuracyRate = BaseAccuracyRate + accuracyMod;
        EvasionRate = BaseEvasionRate + evasionMod;

        // 속도가 변했으면 행동게이지 시스템에도 알려줘야 할 수 있음
        if (ActionGaugeSystem != null)
        {
            // Speed 값이 바뀌었으므로 게이지 증가량 등에 즉시 반영됨
        }

        Debug.Log($"{Name} 스탯 재계산 완료! (공: {Attack}, 방: {Defense}, 속: {Speed}, 치확 : {CritChance}, 치피: {CritDamage})");
    }

    public int RemoveStatusEffects(StatusEffectCategory category, int count)
    {
        int removed = 0;
        for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
        {
            var eff = ActiveStatusEffects[i];
            if (eff.Category == category)
            {
                eff.DestroyVFX();
                ActiveStatusEffects.RemoveAt(i);
                BattleEventManager.TriggerStatusEffectChanged(this, eff);

                removed++;

                if (removed >= count) break;
            }
        }

        // 해제 시에도 스탯 재산정
        if (removed > 0)
        {
            RefreshStats();
            UpdateControlAnimator(); // [추가] 효과 해제 시 기절/수면 체크
        }
        return removed;
    }

    /// <summary>
    /// [추가] 특정 타입의 상태이상을 즉시 제거합니다. (수면 해제 등)
    /// </summary>
    public void RemoveStatusEffect(StatusEffectType type)
    {
        var eff = ActiveStatusEffects.FirstOrDefault(e => e.Type == type);
        if (eff != null)
        {
            eff.DestroyVFX();
            ActiveStatusEffects.Remove(eff);
            BattleEventManager.TriggerStatusEffectChanged(this, eff);

            RefreshStats();
            UpdateControlAnimator();
        }
    }

    /// <summary>
    /// [추가] 기절/수면 상태에 따라 애니메이션 파라미터 isStuning을 제어합니다.
    /// </summary>
    private void UpdateControlAnimator()
    {
        if (Animator == null) return;

        // 기절(Stun) 혹은 수면(Sleep) 상태인 경우 isStuning = true
        bool isStuning = HasStatusEffect(StatusEffectType.Stun) || HasStatusEffect(StatusEffectType.Sleep);
        Animator.SetBool("isStuning", isStuning);
    }

    // -------------------------------------------------------
    // 타겟팅 관련 로직 (은신 등)
    // -------------------------------------------------------
    /// <summary>
    /// 공격자(attacker)가 이 캐릭터를 단일 타겟으로 삼을 수 있는지 확인합니다.
    /// (은신: 적군 중 은신하지 않은 자가 있다면 타겟팅 불가)
    /// </summary>
    public bool CanBeTargetedBy(BattleCharacter attacker, List<BattleCharacter> allCharacters)
    {
        // 아군끼리는 항상 타겟팅 가능 (버프/힐 용도)
        if (this.IsPlayer == attacker.IsPlayer) return true;

        // 은신 중이 아니라면 적군도 항상 타겟팅 가능
        if (!HasStatusEffect(TurnRPG.SkillSystem.StatusEffectType.Stealth)) return true;

        // 은신 중인 캐릭터인 경우:
        // 같은 팀원 중 '은신이 아니고(NOT Stealthed)' + '살아있는(Alive)' 캐릭터가 한 명이라도 있는지 체크
        bool anyOtherNonStealthAlive = allCharacters.Any(c =>
            c != this &&
            c.IsAlive &&
            c.IsPlayer == this.IsPlayer &&
            !c.HasStatusEffect(TurnRPG.SkillSystem.StatusEffectType.Stealth)
        );

        // 다른 타겟팅 가능한(은신 아닌) 적이 있다면, 나는 타겟이 될 수 없음
        // 만약 모든 적이 은신 중이라면(anyOtherNonStealthAlive == false) 선택 가능
        return !anyOtherNonStealthAlive;
    }
}
