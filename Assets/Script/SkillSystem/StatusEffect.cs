using UnityEngine;

namespace TurnRPG.SkillSystem
{
    /// <summary>
    /// 캐릭터에게 적용 중인 실제 상태이상 객체. (지속시간이나 Vfx 등 보관)
    /// </summary>
    public class StatusEffect
    {
        public StatusEffectData Data { get; private set; }
        public int RemainingDuration { get; set; }  // 남은 턴수 (일반 효과는 대부분 0턴으로 즉시 해제됨)
        public GameObject SpawnedVFX { get; set; }  // 캐릭터에 부착된 시각효과 인스턴스 (옵션)

        // 특정 상태이상이 소멸될 때 (이펙트 삭제 등)
        public StatusEffectType Type => Data.EffectType;
        public StatusEffectCategory Category => Data.Category;
        public float PrimaryValue => Data.PrimaryValue;

        // ✅ 방금 추가된 핵심! (보호막 체력, 출혈 데미지 등 시전 당시에 결정되는 가변수치)
        public float DynamicValue { get; set; } 


        // 버프/디버프 생성
        public StatusEffect(StatusEffectData data, int duration, float dynamicValue = 0f)
        {
            Data = data;
            RemainingDuration = duration;
            DynamicValue = dynamicValue;
        }

        // 특정 상태이상이 소멸될 때 (이펙트 삭제 등)
        public void DestroyVFX()
        {
            if (SpawnedVFX != null)
            {
                Object.Destroy(SpawnedVFX);
                SpawnedVFX = null;
            }
        }
    }
}
