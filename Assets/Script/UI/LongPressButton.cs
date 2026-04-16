using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// 버튼을 꾹 누르고 있을 때 특정 이벤트를 반복해서 발생시키는 컴포넌트입니다.
/// </summary>
public class LongPressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Settings")]
    [Tooltip("연속 입력이 시작되기 전까지 누르고 있어야 하는 시간(초)")]
    public float initialDelay = 0.5f;

    [Tooltip("연속 입력 간격(초)")]
    public float interval = 0.1f;

    [Header("Events")]
    public UnityEvent onAction;

    private bool _isPressed = false;
    private Coroutine _pressRoutine;

    /// <summary>
    /// 포인터를 눌렀을 때 호출 (IPointerDownHandler)
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        
        // 처음 눌렀을 때 즉시 1회 실행
        onAction?.Invoke();

        // 연속 입력 루틴 시작
        StopPressRoutine();
        _pressRoutine = StartCoroutine(PressRoutine());
    }

    /// <summary>
    /// 포인터를 뗐을 때 호출 (IPointerUpHandler)
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        StopPressRoutine();
    }

    /// <summary>
    /// 포인터가 영역을 벗어났을 때 호출 (IPointerExitHandler)
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        _isPressed = false;
        StopPressRoutine();
    }

    private IEnumerator PressRoutine()
    {
        // 초기 대기 시간
        yield return new WaitForSeconds(initialDelay);

        // 눌려있는 동안 반복 실행
        while (_isPressed)
        {
            onAction?.Invoke();
            yield return new WaitForSeconds(interval);
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

    private void OnDisable()
    {
        _isPressed = false;
        StopPressRoutine();
    }
}
