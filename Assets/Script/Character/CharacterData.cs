using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "TurnRPG/Character Data", order = 0)]
public class CharacterData : ScriptableObject
{
    [Header("기본 정보")]
    public int ID;
    public string CharacterName;
    public bool isPlayer;
    public TurnRPG.SkillSystem.CharacterType charType;

    [Header("기본 스탯")]
    public float MaxHp = 1000f;
    public float Attack = 100f;
    public float Defense = 50f;
    public float Speed = 100f;
    [Range(0f, 1f)] public float CritChance = 0.15f;
    public float CritDamage = 1.5f;
    public float Evasion = 0.0f;
    public float Accuracy = 1.0f;
    [Range(0f, 1f)] public float DualAttackChance = 0.03f;



    [Header("비주얼 및 UI 자원")]
    [Tooltip("전신 일러스트 혹은 메인 이미지")]
    public Sprite illustration;

    [Tooltip("행동 게이지에 표시될 작은 아이콘 이미지")]
    public Sprite iconImage;

    [Tooltip("자신 턴일 때 나타나는 사이드 컷씬 이미지")]
    public Sprite sideImage;

    [Tooltip("캐릭터 프리팹 (Animator 포함)")]
    public GameObject characterPrefab;

    [System.Serializable]
    public class SkillSlot
    {
        public TurnRPG.SkillSystem.SkillData skillData;
        public int level = 1;
    }

    [Header("시작 스킬 로스터")]
    [Tooltip("전투 시작 시 자동으로 장착될 스킬과 레벨 (최대 3개)")]
    public System.Collections.Generic.List<SkillSlot> StartingSkills;

    [Header("보이스 자원 (랜덤 3개 추천)")]
    public System.Collections.Generic.List<AudioClip> readyVoices; // 준비 (자기 턴)
    public System.Collections.Generic.List<AudioClip> attackVoices;
    public System.Collections.Generic.List<AudioClip> hitVoices;
    public System.Collections.Generic.List<AudioClip> skillVoices;  // 일반 스킬
    public System.Collections.Generic.List<AudioClip> winVoices;
    public System.Collections.Generic.List<AudioClip> skillUpgradeVoices;
    public System.Collections.Generic.List<AudioClip> skillChangeVoices; // 스킬 변경
    public System.Collections.Generic.List<AudioClip> call; // 선택 (호출) 시 대사
}
