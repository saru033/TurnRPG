using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatType
{
    HP,           // 체력 (%)
    Attack,       // 공격력 (%)
    Defense,      // 방어력 (%)
    Speed,        // 속도 (Flat int)
    CritChance,   // 치확 (Flat %)
    CritDamage    // 치피 (Flat %)
}

public enum EquipmentPart
{
    Head,
    Body,
    Shoes
}

[Serializable]
public struct EquipmentSubStat
{
    public StatType statType;
    public float value;

    public EquipmentSubStat(StatType type, float val)
    {
        this.statType = type;
        this.value = val;
    }

    public string GetStatString()
    {
        string name = "";
        switch (statType)
        {
            case StatType.HP: name = "체력"; return $"{name} +{(int)value}%";
            case StatType.Attack: name = "공격력"; return $"{name} +{(int)value}%";
            case StatType.Defense: name = "방어력"; return $"{name} +{(int)value}%";
            case StatType.Speed: name = "속도"; return $"{name} +{(int)value}";
            case StatType.CritChance: name = "치명타 확률"; return $"{name} +{(int)value}%";
            case StatType.CritDamage: name = "치명타 피해"; return $"{name} +{(int)value}%";
            default: return "";
        }
    }
}

[Serializable]
public class EquipmentState
{
    public EquipmentPart part;
    public List<EquipmentSubStat> subStats = new List<EquipmentSubStat>();

    public EquipmentState(EquipmentPart part)
    {
        this.part = part;
    }

    /// <summary>
    /// 기존 장비 상태를 복사하여 새로운 인스턴스를 생성합니다. (리롤 프리뷰용)
    /// </summary>
    public EquipmentState(EquipmentState other)
    {
        if (other == null) return;
        this.part = other.part;
        this.subStats = new List<EquipmentSubStat>(other.subStats);
    }

    public EquipmentState Clone()
    {
        return new EquipmentState(this);
    }

    /// <summary>
    /// GameManager에 설정된 범위를 기반으로 4개의 중복되지 않는 랜덤 스탯을 생성합니다.
    /// minScale/maxScale을 조절하여 상위 수치가 붙는 '고급 리롤' 등을 구현할 수 있습니다.
    /// </summary>
    public void GenerateRandomStats(float minScale = 1.0f, float maxScale = 1.0f, bool isHighTier = false)
    {
        subStats.Clear();
        
        // 1. 가능한 모든 스탯 타입 리스트 생성
        List<StatType> availableTypes = new List<StatType>((StatType[])Enum.GetValues(typeof(StatType)));
        
        // 2. 랜덤하게 4개 선택
        for (int i = 0; i < 4; i++)
        {
            if (availableTypes.Count == 0) break;

            int randomIndex = UnityEngine.Random.Range(0, availableTypes.Count);
            StatType selectedType = availableTypes[randomIndex];
            availableTypes.RemoveAt(randomIndex);

            float randomValue = GetRandomValueForType(selectedType, minScale, maxScale, isHighTier);
            subStats.Add(new EquipmentSubStat(selectedType, randomValue));
        }
    }

    private float GetRandomValueForType(StatType type, float minScale, float maxScale, bool isHighTier)
    {
        if (GameManager.Instance == null) return 0f;

        float min = 0, max = 0;
        switch (type)
        {
            case StatType.HP: 
                min = GameManager.Instance.minHpPercent; 
                max = GameManager.Instance.maxHpPercent; 
                break;
            case StatType.Attack: 
                min = GameManager.Instance.minAtkPercent; 
                max = GameManager.Instance.maxAtkPercent; 
                break;
            case StatType.Defense: 
                min = GameManager.Instance.minDefPercent; 
                max = GameManager.Instance.maxDefPercent; 
                break;
            case StatType.Speed: 
                min = GameManager.Instance.minSpeed; 
                max = GameManager.Instance.maxSpeed; 
                if (isHighTier) min = max * 0.9f; // 고급 리롤 시 90% 이상 보장
                return (int)UnityEngine.Random.Range(min * minScale, (max * maxScale) + 1);
            case StatType.CritChance: 
                min = GameManager.Instance.minCritChance; 
                max = GameManager.Instance.maxCritChance; 
                break;
            case StatType.CritDamage: 
                min = GameManager.Instance.minCritDamage; 
                max = GameManager.Instance.maxCritDamage; 
                break;
        }

        if (isHighTier) min = max * 0.9f; // 고급 리롤 시 90% 이상 보장

        return (int)UnityEngine.Random.Range(min * minScale, max * maxScale);
    }
}
