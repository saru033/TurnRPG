using System.Collections.Generic;
using UnityEngine;
using TurnRPG.SkillSystem;

public enum StageDifficulty { Normal, Elite }
public enum StageEra { Early, Late }

[CreateAssetMenu(fileName = "StageData", menuName = "TurnRPG/StageData")]
public class StageData : ScriptableObject
{
    [Header("Stage Info")]
    public int stageID;
    public Sprite backgroundSprite;
    public StageDifficulty difficulty = StageDifficulty.Normal;
    public StageEra era = StageEra.Early;
    
    [Header("Enemies")]
    public List<CharacterData> enemies = new List<CharacterData>();

    [Header("Rewards - Currency")]
    public int minGold = 100;
    public int maxGold = 200;
    public int minSkillUp = 1;
    public int maxSkillUp = 5;
    public int minReroll = 0;
    public int maxReroll = 2;
    public int minHighReroll = 0;
    public int maxHighReroll = 1;

    [Header("Rewards - Battle Items")]
    [Tooltip("전투 중 사용 가능한 아이템들 중 랜덤으로 지급될 목록입니다.")]
    public List<SkillData> potentialBattleItems = new List<SkillData>();
}
