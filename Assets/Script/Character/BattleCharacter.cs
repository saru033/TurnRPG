using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투에 참여하는 캐릭터 하나의 데이터.
/// 아군/적군 공통으로 사용.
/// </summary>
/// 

public class BattleCharacter : MonoBehaviour
{
    // -------------------------------------------------------
    // 기본 정보
    // -------------------------------------------------------
    public string Name;
    public bool IsPlayer;   // true = 아군, false = 적

    // -------------------------------------------------------
    // 스탯
    // -------------------------------------------------------
    public float MaxHp;
    public float CurrentHp;
    public float Defense;        // 방어력
    public float Speed;          // 속도
    public float CritChance;     // 치명 확률 (0~1)
    public float CritDamage;     // 치명 피해 배율 (예: 1.5 = 150%)

    // -------------------------------------------------------
    // 행동게이지
    // -------------------------------------------------------
    public float ActionGauge;    // 현재 게이지 (0 ~ 100)

    // -------------------------------------------------------
    // 상태
    // -------------------------------------------------------
    public bool IsAlive => CurrentHp > 0f;

    // -------------------------------------------------------
    // 생성자
    // -------------------------------------------------------
    public BattleCharacter(string name, bool isPlayer,
                           float maxHp, float defense, float speed,
                           float critChance = 0.15f, float critDamage = 1.5f)
    {
        Name = name;
        IsPlayer = isPlayer;
        MaxHp = maxHp;
        CurrentHp = maxHp;
        Defense = defense;
        Speed = speed;
        CritChance = critChance;
        CritDamage = critDamage;
        ActionGauge = 0f;
    }

    // -------------------------------------------------------
    // 피해 계산
    // -------------------------------------------------------

    /// <summary>
    /// 피해를 받는다. 방어력 적용 후 체력 감소.
    /// 방어력 공식: 실제피해 = 공격력 * (1 - Defense / (Defense + 200))
    /// </summary>
    public float TakeDamage(float rawDamage)
    {
        float reduction = Defense / (Defense + 200f);
        float actualDamage = rawDamage * (1f - reduction);
        actualDamage = Mathf.Max(1f, actualDamage);   // 최소 1 피해
        CurrentHp = Mathf.Max(0f, CurrentHp - actualDamage);
        return actualDamage;
    }

    /// <summary>
    /// 치명타 여부 판정 후 최종 피해량 반환.
    /// </summary>
    public (float damage, bool isCrit) CalcDamage(float baseDamage)
    {
        bool isCrit = Random.value < CritChance;
        float finalDamage = isCrit ? baseDamage * CritDamage : baseDamage;
        return (finalDamage, isCrit);
    }
}
