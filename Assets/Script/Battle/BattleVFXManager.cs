using UnityEngine;

namespace TurnRPG.SkillSystem
{
    /// <summary>
    /// 전투 중 공용으로 발생하는 VFX(타격, 버프, 디버프 등)를 생성하는 매니저입니다.
    /// 모든 VFX 프리팹에는 스스로를 파괴하는 스크립트가 포함되어 있다고 가정합니다.
    /// </summary>
    public class BattleVFXManager : MonoBehaviour
    {
        private static BattleVFXManager _instance;
        public static BattleVFXManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<BattleVFXManager>();
                }
                return _instance;
            }
        }

        [Header("공용 VFX 프리팹")]
        public GameObject hitVFXPrefab;
        public GameObject hitAllyVFXPrefab; // [추가] 아군용 타격 VFX (미설정 시 기본 prefab 반전 사용)
        public GameObject buffVFXPrefab;
        public GameObject debuffVFXPrefab;
        public GameObject extraTurnEffect;
        public GameObject healVFXPrefab;
        public GameObject shieldVFXPrefab;
        public GameObject removeBuffVFXPrefab;
        public GameObject removeDebuffVFXPrefab;
        public GameObject extraMoveVFXPrefab;
        public GameObject deathVFXPrefab;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        /// <summary>
        /// 지정된 타입의 VFX를 대상 트랜스폼 위치에 생성합니다.
        /// </summary>
        public void SpawnVFX(VFXType type, Transform target, bool flipX = false, bool detach = false)
        {
            if (target == null) return;

            GameObject prefab = null;
            switch (type)
            {
                case VFXType.Hit:
                    // 아군 피격(flipX)이고 전용 프리팹이 있으면 그것을 사용
                    if (flipX && hitAllyVFXPrefab != null)
                        prefab = hitAllyVFXPrefab;
                    else
                        prefab = hitVFXPrefab;
                    break;
                case VFXType.Buff:
                    prefab = buffVFXPrefab;
                    break;
                case VFXType.Debuff:
                    prefab = debuffVFXPrefab;
                    break;
                case VFXType.ExtraTurn:
                    prefab = extraTurnEffect;
                    break;
                case VFXType.Heal:
                    prefab = healVFXPrefab;
                    break;
                case VFXType.Shield:
                    prefab = shieldVFXPrefab;
                    break;
                case VFXType.RemoveBuff:
                    prefab = removeBuffVFXPrefab;
                    break;
                case VFXType.RemoveDebuff:
                    prefab = removeDebuffVFXPrefab;
                    break;
                case VFXType.extraMove:
                    prefab = extraMoveVFXPrefab;
                    break;
                case VFXType.Death:
                    prefab = deathVFXPrefab;
                    break;
            }

            if (prefab != null)
            {
                // 부모를 지정하여 UI 계층 구조(Canvas)에 포함되도록 함
                GameObject vfx = Instantiate(prefab, target);
                vfx.transform.localPosition = Vector3.zero;
                vfx.transform.localRotation = Quaternion.identity;

                // 좌우 반전 적용
                if (flipX && prefab == hitVFXPrefab)
                {
                    vfx.transform.localScale = new Vector3(-1, 1, 1);
                }
                else
                {
                    vfx.transform.localScale = Vector3.one;
                }

                // [추가] 생성 직후 부모로부터 분리하여 캐릭터 비활성화와 무관하게 동작하게 함
                if (detach)
                {
                    // 히트박스의 부모는 캐릭터 본체이므로, 
                    // 본체의 부모(전투 패널)를 찾아 그쪽으로 옮겨야 캐릭터 비활성화의 영향을 받지 않습니다.
                    var characterRoot = target.GetComponentInParent<CharacterView>();
                    if (characterRoot != null)
                    {
                        vfx.transform.SetParent(characterRoot.transform.parent, true);
                    }
                    else if (target.parent != null)
                    {
                        // 최후의 수단으로 현재 타겟의 한 단계 위 부모로 이동
                        vfx.transform.SetParent(target.parent.parent, true);
                    }
                }
            }
        }
    }

    public enum VFXType
    {
        Hit,
        Buff,
        Debuff,
        ExtraTurn,
        Heal,
        Shield,
        RemoveBuff,
        RemoveDebuff,
        extraMove,
        Death
    }
}
