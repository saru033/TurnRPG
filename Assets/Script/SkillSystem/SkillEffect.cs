using UnityEngine;

namespace TurnRPG.SkillSystem
{
    /// <summary>
    /// 모든 스킬 효과(데미지, 버프 부여, 해제 등)의 기본 형태
    /// 인스펙터에서 편하게 리스트업할 수 있도록 ScriptableObject는 아니지만 MonoBehaviour도 아닌 일반 직렬화 클래스(또는 컴포넌트)
    /// </summary>
    [System.Serializable]
    public abstract class SkillEffect
    {
        // [추가] 이 효과가 실제 무언가를 실행하는 행동인지, 단순 조건 체크(필터)인지 구분
        public virtual bool IsCondition => false;

        // Execute 메서드는 실제 효과가 작동하는 로직
        // true 반환시 성공, false 반환시 무효 처리
        public abstract bool Execute(BattleCharacter caster, BattleCharacter target);
    }
}
