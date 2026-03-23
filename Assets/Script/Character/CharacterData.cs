using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "TurnRPG/Character Data", order = 0)]
public class CharacterData : ScriptableObject
{
    [Header("기본 정보")]
    public int ID;
    public string CharacterName;
    public bool isPlayer;

    [Header("기본 스탯")]
    public float MaxHp = 1000f;
    public float Attack = 100f;
    public float Defense = 50f;
    public float Speed = 100f;
    [Range(0f, 1f)] public float CritChance = 0.15f;
    public float CritDamage = 1.5f;

    [Header("비주얼 및 UI 자원")]
    [Tooltip("전신 일러스트 혹은 메인 이미지")]
    public Sprite illustration;
    
    [Tooltip("행동 게이지에 표시될 작은 아이콘 이미지")]
    public Sprite iconImage;
    
    [Tooltip("자신 턴일 때 나타나는 사이드 컷씬 이미지")]
    public Sprite sideImage;

[Tooltip("캐릭터 프리팹 (Animator 포함)")]
public GameObject characterPrefab;
}
