using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using TurnRPG.SkillSystem;

/// <summary>
/// 버프/디버프 아이콘에 부착되어 0.3초 롱프레스 시 상세 정보를 호출하는 트리거입니다.
/// </summary>
public class StatusEffectTooltipTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private StatusEffect _effect;
    private Coroutine _showRoutine;
    private bool _isPressed = false;

    public void Init(StatusEffect effect)
    {
        _effect = effect;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_effect == null || _effect.Data == null) return;

        _isPressed = true;
        StopPressRoutine();
        _showRoutine = StartCoroutine(ShowDelayRoutine());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        HideTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    private IEnumerator ShowDelayRoutine()
    {
        // 0.3초 대기
        yield return new WaitForSeconds(0.3f);

        if (_isPressed && _effect != null)
        {
            // BattleUI를 통해 상태 효과 툴팁 표시 요청
            if (BattleUI.Instance != null)
            {
                RectTransform rt = GetComponent<RectTransform>();
                Vector3 worldPos = transform.position;
                float height = rt.rect.height;

                // 아이콘 높이만큼 오프셋을 주어 바로 위에 표시
                BattleUI.Instance.ShowStatusTooltip(_effect, worldPos, height);
            }
        }
    }

    private void HideTooltip()
    {
        _isPressed = false;
        StopPressRoutine();
        if (BattleUI.Instance != null)
        {
            BattleUI.Instance.HideStatusTooltip();
        }
    }

    private void StopPressRoutine()
    {
        if (_showRoutine != null)
        {
            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }
    }

    private void OnDisable()
    {
        HideTooltip();
    }
}
