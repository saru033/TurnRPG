using UnityEngine;

namespace TurnRPG.Battle
{
    /// <summary>
    /// 캐릭터 프리팹의 Animator가 붙은 오브젝트에 추가하여
    /// 애니메이션 이벤트를 수신하고 BattleManager에 전달하는 컴포넌트입니다.
    /// </summary>
    public class AnimationEventReceiver : MonoBehaviour
    {
        /// <summary>
        /// 애니메이션 클립에서 추가한 Animation Event에 의해 호출됩니다.
        /// </summary>
        public void OnAnimationImpact()
        {
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.OnAnimationImpact();
            }
        }

        /// <summary>
        /// 피격 애니메이션(hit) 이벤트에서 호출됩니다.
        /// 캐릭터의 색상을 깜빡이게 합니다.
        /// </summary>
        public void OnHitFlash()
        {
            var view = GetComponentInParent<CharacterView>();
            if (view != null)
            {
                view.OnHitFlash();
            }
        }
    }
}
