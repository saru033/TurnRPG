using UnityEngine;
using System.Collections.Generic;

namespace TurnRPG.SkillSystem
{
    [System.Serializable]
    public class SkillLevelData
    {
        [TextArea(2, 4)]
        public string Description; // 레벨별 설명
        public int Cooldown;       // 레벨별 쿨타임 변경 가능
        public SkillTargetType TargetType; // 스킬 레벨별로 타겟군이 변할 수 있음
        
        [Tooltip("패시브 스킬일 경우, 해당 레벨에서 터질 방아쇠 (강화별로 트리거 추가 가능)")]
        public PassiveTriggerType PassiveTrigger = PassiveTriggerType.None;
        
        [Header("상시 적용 효과 (전투 시작 시 1회 적용)")]
        [SerializeReference] public List<SkillEffect> ConstantEffects = new List<SkillEffect>();

        [Header("액티브/반응형 효과 (조건 만족 또는 스킬 사용 시)")]
        [SerializeReference] public List<SkillEffect> Effects = new List<SkillEffect>(); // 인스펙터 직렬화용 Reference 속성
    }

    [CreateAssetMenu(fileName = "NewSkill", menuName = "TurnRPG/Skill System/Skill Data")]
    public class SkillData : ScriptableObject
    {
        [Header("기본 정보")]
        public string SkillID;
        public string SkillName;
        public Sprite SkillIcon;
        
        [Header("로직 정보")]
        public SkillType Type;

        public SkillSlotIndex SlotIndex; // 1스킬, 2스킬, 3스킬 구분
        [Tooltip("비트 플래그 설정 (Male, Female, Wolf, Enemy 둥)")]
        public CharacterType EquipRestriction = CharacterType.All;
        
        [Header("애니메이션 & 연출")]
        [Tooltip("캐릭터에게 보낼 애니메이션 트리거 이름 (1, 2, 3스킬 공통)")]
        public string RequiredAnimationTrigger; 
        
        [Tooltip("3스킬 전용 UI 컷신 클립 (단일 컷신 패널의 Animator Override Controller와 연동)")]
        public AnimationClip UltimateCutsceneClip;

        [Header("단계별 효과 (0번 인덱스가 1레벨)")]
        public List<SkillLevelData> LevelDatas;
    }
}
