using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using TurnRPG.SkillSystem;

/// <summary>
/// 스킬 버튼에 부착되어 0.3초 롱프레스 시 툴팁을 표시하고 떼면 숨깁니다.
/// </summary>
public class SkillTooltipTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private SkillData _skill;
    private int _level;
    private Coroutine _showRoutine;
    private bool _isPressed = false;
    public bool WasLongPressed { get; private set; } // [추가] 롱프레스 발생 여부 확인용

    public void Init(SkillData skill, int level)
    {
        _skill = skill;
        _level = level;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_skill == null) return;
        
        _isPressed = true;
        WasLongPressed = false; // [추가] 새로운 터치 시 리셋
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

        if (_isPressed)
        {
            WasLongPressed = true; // [추가] 툴팁이 떴으므로 롱프레스 성공
            // BattleUI를 통해 툴팁 표시 요청
            if (BattleUI.Instance != null)
            {
                RectTransform rt = GetComponent<RectTransform>();
                Vector3 worldPos = transform.position; // 월드 좌표 사용
                float height = rt.rect.height;

                // 버튼의 높이만큼 위로 오프셋 전달
                BattleUI.Instance.ShowSkillTooltip(_skill, _level, worldPos, height);
            }
        }
    }

    private void HideTooltip()
    {
        _isPressed = false;
        StopPressRoutine();
        if (BattleUI.Instance != null)
        {
            BattleUI.Instance.HideSkillTooltip();
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
