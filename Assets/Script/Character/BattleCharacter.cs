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

    // -------------------------------------------------------
    // 행동게이지 및 스킬 세팅
    // -------------------------------------------------------
    public float ActionGauge;    // 현재 게이지 (0 ~ 100)

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
    public bool IsAlive => CurrentHp > 0f;

    // -------------------------------------------------------
    // 생성자 (ScriptableObject 원본 데이터로 생성)
    // -------------------------------------------------------
    public BattleCharacter(CharacterData data)
    {
        Data = data;
        ID = data.ID;
        Name = data.CharacterName;
        IsPlayer = data.isPlayer;
        MaxHp = data.MaxHp;
        Attack = data.Attack;
        CurrentHp = data.MaxHp;
        Defense = data.Defense;
        Speed = data.Speed;
        CritChance = data.CritChance;
        CritDamage = data.CritDamage;
        ActionGauge = 0f;

        // 시작 스킬 장착 (최대 3슬롯)
        if (data.StartingSkills != null)
        {
            for (int i = 0; i < data.StartingSkills.Count && i < 3; i++)
            {
                if (data.StartingSkills[i] != null)
                {
                    EquipSkill(i, data.StartingSkills[i], 1); // 1레벨 기본 장착
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

    // -------------------------------------------------------
    // 피해 및 회복 계산
    // -------------------------------------------------------
    public float TakeDamage(float rawDamage, BattleCharacter attacker = null, float penetration = 0f, bool alwaysHit = false, bool cannotBeCountered = false)
    {
        if (HasStatusEffect(StatusEffectType.Invincible))
        {
            Debug.Log($"{Name} 무적 상태! 데미지 0");
            return 0;
        }

        if (HasStatusEffect(StatusEffectType.SkillDmgNullify))
        {
            Debug.Log($"{Name} 스킬 데미지 1회 무효화!");
            // TODO: 버프 해제 로직 추가 필요
            return 0;
        }

        bool isEvaded = false;
        // 1. 회피 체크 (무조건 적중이 아닐 때)
        if (!alwaysHit && Random.value < EvasionRate)
        {
            isEvaded = true;
            Debug.Log($"{Name} 회피 성공! (피해 감소)");
            rawDamage *= 0.5f; // 회피 시 통상 데미지 50% 감소 및 빗나감 판정
            // 추가로 이벤트 쏴서 팝업창에 '빗나감' 처리 가능
        }

        // 2. 방어력 계산 (관통 적용)
        float targetDefense = Defense * (1f - Mathf.Clamp01(penetration));
        float reduction = targetDefense / (targetDefense + 200f);
        float actualDamage = rawDamage * (1f - reduction);

        // 상시 피해 감소 패시브 적용 (투기장 체르미아 효과 등)
        actualDamage *= (1f - Mathf.Clamp01(PassiveDamageReduction));

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

        // 반격불가 여부를 이벤트 매니저에게 같이 전달하여 반격이 터지지 않도록 함
        BattleEventManager.TriggerDamageTaken(this, attacker, actualDamage, cannotBeCountered, isEvaded);

        return actualDamage;
    }

    public void Heal(float amount)
    {
        if (CurrentHp <= 0 || HasStatusEffect(StatusEffectType.Unhealable)) return;

        CurrentHp = Mathf.Min(CurrentHp + amount, MaxHp);
        BattleEventManager.TriggerHealed(this, amount);
    }

    public (float damage, bool isCrit) CalcDamage(float baseDamage)
    {
        bool isCrit = Random.value < CritChance;
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
                TakeDamage(eff.DynamicValue);
                Debug.Log($"{Name} : {eff.Type} 피해로 {eff.DynamicValue} 데미지를 받음!");
            }
        }

        BattleEventManager.TriggerTurnStarted(this);
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
            if (eff.RemainingDuration <= 0)
            {
                eff.DestroyVFX();
                ActiveStatusEffects.RemoveAt(i);
                BattleEventManager.TriggerStatusEffectChanged(this, eff); // 해제됨 브로드캐스트
            }
        }

        BattleEventManager.TriggerTurnEnded(this);
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

        var existing = ActiveStatusEffects.FirstOrDefault(e => e.Type == data.EffectType);
        if (existing != null)
        {
            // 이미 있으면 지속시간 갱신 및 (기획에 따라) 수치 덮어씌우기 혹은 높은 쪽 유지 등
            existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, duration);
            if (dynamicValue > existing.DynamicValue) existing.DynamicValue = dynamicValue;
        }
        else
        {
            var newEff = new StatusEffect(data, duration, dynamicValue);

            // 시각 이펙트(VFX) 처리
            if (data.VFXPrefab != null && ViewObject != null)
            {
                GameObject vfx = Object.Instantiate(data.VFXPrefab, ViewObject.transform.position, Quaternion.identity);
                if (data.KeepVFXAttached)
                {
                    vfx.transform.SetParent(ViewObject.transform);
                    newEff.SpawnedVFX = vfx;
                }
                else
                {
                    Object.Destroy(vfx, 2f); // 임시 삭제
                }
            }

            ActiveStatusEffects.Add(newEff);
            AppliedBuffsThisTurn.Add(newEff); // 생존 보장 등록
            BattleEventManager.TriggerStatusEffectChanged(this, newEff);
        }
    }

    public void RemoveStatusEffects(StatusEffectCategory category, int count)
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
    }
}
