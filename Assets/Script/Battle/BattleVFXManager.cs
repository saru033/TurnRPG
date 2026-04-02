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
        public void SpawnVFX(VFXType type, Transform target, bool flipX = false)
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
            }

            if (prefab != null)
            {
                // 부모를 지정하여 UI 계층 구조(Canvas)에 포함되도록 함
                GameObject vfx = Instantiate(prefab, target);
                vfx.transform.localPosition = Vector3.zero;
                vfx.transform.localRotation = Quaternion.identity;

                // 좌우 반전 적용
                // 아군 전용 프리팹(hitAllyVFXPrefab)을 직접 쓰는 경우 이미 반전되어 있을 수 있으므로
                // 기본 프리팹을 공유해서 쓸 때만 코드로 반전을 수행합니다.
                if (flipX && prefab == hitVFXPrefab)
                {
                    vfx.transform.localScale = new Vector3(-1, 1, 1);
                }
                else
                {
                    vfx.transform.localScale = Vector3.one;
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
        RemoveDebuff
    }
}
