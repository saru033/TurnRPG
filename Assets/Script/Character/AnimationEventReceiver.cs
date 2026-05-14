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

        private bool _isReadyVoicePlayed = false;

        public void ResetReadyVoice()
        {
            _isReadyVoicePlayed = false;
        }

        private void OnEnable()
        {
            ResetReadyVoice();
        }

        /// <summary>
        /// 애니메이션 특정 시점에 랜덤 보이스를 재생합니다.
        /// category: "Ready", "Attack", "Hit", "Skill" 등
        /// </summary>
        public void PlayRandomVoice(string category)
        {
            var view = GetComponentInParent<CharacterView>();
            if (view == null || view.character == null || view.character.Data == null) return;
            if (SoundManager.Instance == null) return;

            // [추가] "Ready" 포즈는 한 턴에 한 번만 대사가 나오도록 제한
            if (category == "Ready")
            {
                if (_isReadyVoicePlayed) return;
                _isReadyVoicePlayed = true;
            }

            CharacterData data = view.character.Data;
            AudioClip clip = null;

            switch (category)
            {
                case "Ready":
                    if (data.readyVoices.Count > 0)
                        clip = data.readyVoices[UnityEngine.Random.Range(0, data.readyVoices.Count)];
                    break;
                case "Attack":
                    if (data.attackVoices.Count > 0)
                        clip = data.attackVoices[UnityEngine.Random.Range(0, data.attackVoices.Count)];
                    break;
                case "Hit":
                    if (data.hitVoices.Count > 0)
                        clip = data.hitVoices[UnityEngine.Random.Range(0, data.hitVoices.Count)];
                    break;
                case "Skill":
                    if (data.skillVoices.Count > 0)
                        clip = data.skillVoices[UnityEngine.Random.Range(0, data.skillVoices.Count)];
                    break;
            }

            if (clip != null)
            {
                SoundManager.Instance.PlayVoice(clip);
            }
        }
        /// <summary>
        /// 애니메이션 특정 시점에 지정된 SFX를 재생합니다.
        /// sfxName: "hit", "attackPunch", "attackShoot" 등 (SoundManager의 SfxType 이름과 매칭)
        /// </summary>
        public void PlaySFX(string sfxName)
        {
            if (SoundManager.Instance == null) return;

            // 문자열을 Enum으로 변환하여 재생
            if (System.Enum.TryParse(sfxName, out SfxType type))
            {
                SoundManager.Instance.PlaySFX(type);
            }
            else
            {
                Debug.LogWarning($"[AnimationEventReceiver] {sfxName}에 해당하는 SfxType을 찾을 수 없습니다.");
            }
        }
    }
}
