using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class EquipmentTooltipTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private EquipmentState _state;
    private Sprite _icon;
    private bool _isPressed = false;
    private Coroutine _pressRoutine;

    public bool WasLongPressed { get; private set; }

    public void Init(EquipmentState state, Sprite icon)
    {
        _state = state;
        _icon = icon;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        WasLongPressed = false;
        StopPressRoutine();
        _pressRoutine = StartCoroutine(ShowDelayRoutine());
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
        // 0.3초 대기 (SkillTooltipTrigger와 동일한 대기 시간)
        yield return new WaitForSeconds(0.3f);

        if (_isPressed && _state != null)
        {
            WasLongPressed = true;
            if (GameManager.Instance != null)
            {
                RectTransform rt = GetComponent<RectTransform>();
                Vector3 worldPos = transform.position;
                float height = rt.rect.height;

                GameManager.Instance.ShowEquipmentTooltip(_state, _icon, worldPos, height);
            }
        }
    }

    private void HideTooltip()
    {
        _isPressed = false;
        StopPressRoutine();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HideEquipmentTooltip();
        }
    }

    private void StopPressRoutine()
    {
        if (_pressRoutine != null)
        {
            StopCoroutine(_pressRoutine);
            _pressRoutine = null;
        }
    }
}
