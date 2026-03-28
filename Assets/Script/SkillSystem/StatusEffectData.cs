using UnityEngine;

namespace TurnRPG.SkillSystem
{
    [CreateAssetMenu(fileName = "NewStatusEffect", menuName = "TurnRPG/Skill System/Status Effect Data")]
    public class StatusEffectData : ScriptableObject
    {
        [Header("기본 정보")]
        public StatusEffectType EffectType;
        public StatusEffectCategory Category;
        public string EffectName;
        [TextArea(2, 4)]
        public string Description;

        [Header("비주얼 (UI & 연출)")]
        [Tooltip("전투 UI에 표시 될 아이콘")]
        public Sprite Icon;

        [Tooltip("캐릭터에게 효과가 걸릴 때 출력되거나 유지될 시각적 애니메이션 프리팹 (옵션, 없으면 아이콘만 띄움)")]
        public GameObject VFXPrefab;

        [Tooltip("이 시각 이펙트가 캐릭터 위치에 붙은 채로 유지되어야 하는지(예: 독, 보호막), 아니면 스폰 후 바로 끝나는지(예: 힐 반짝임)")]
        public bool KeepVFXAttached = false;
        
        [Header("효과 값 (옵션)")]
        [Tooltip("일반적인 버프/디버프의 경우 고유 값. 예) 공증 50%일 경우 0.5 또는 50 등으로 규약하여 사용")]
        public float PrimaryValue;
    }
}
