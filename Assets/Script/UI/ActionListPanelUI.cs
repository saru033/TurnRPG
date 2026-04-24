using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening; // [추가] DOTween 사용

public class ActionListPanelUI : MonoBehaviour
{
    [Header("References")]
    public GameObject panelRoot;
    public RectTransform panelContent; // 실제로 애니메이션될 컨텐츠 부모
    public GameObject backgroundCloseButton; // [추가] 배경 끄기 버튼
    public Transform entryParent;
    public GameObject entryPrefab;

    [Header("Animation Settings")]
    public float animationDuration = 0.3f;
    private float _targetPosX; // 원래 위치 (화면 안)
    private float _hiddenPosX; // 숨겨진 위치 (화면 밖)

    private List<ActionListEntryUI> _entries = new List<ActionListEntryUI>();
    private bool _isOpening = false;
    private bool _posInitialized = false;

    private void Start()
    {
        // 1. 유저가 화면 밖에 배치한 현재 위치를 숨김 위치로 저장
        if (panelContent != null)
        {
            _hiddenPosX = panelContent.anchoredPosition.x;
            // 2. 패널 너비만큼 오른쪽으로 이동한 지점을 타겟 위치로 설정
            _targetPosX = _hiddenPosX + panelContent.rect.width;

            // 안전장치: 너비 계산이 안 될 경우 대비
            if (panelContent.rect.width <= 0) _targetPosX = _hiddenPosX + 500f;

            _posInitialized = true;
        }

        if (panelRoot != null) panelRoot.SetActive(false);
        if (backgroundCloseButton != null) backgroundCloseButton.SetActive(false);
    }

    public void Toggle()
    {
        if (panelRoot == null || panelContent == null) return;

        if (!_isOpening)
        {
            Open();
        }
        else
        {
            Close();
        }
    }

    public void Open()
    {
        if (BattleManager.Instance == null) return;

        // 행동 대기 중(PlayerTurn, SelectTarget)일 때만 열기 가능
        var state = BattleManager.Instance.State;
        if (state != BattleManager.BattleState.PlayerTurn && state != BattleManager.BattleState.SelectTarget)
        {
            return;
        }

        _isOpening = true;

        // 1. 패널 및 끄기 버튼 활성화
        panelRoot.SetActive(true);
        if (backgroundCloseButton != null) backgroundCloseButton.SetActive(true);

        RefreshList();

        // 2. 화면 안으로 이동
        panelContent.DOKill();
        panelContent.DOAnchorPosX(_targetPosX, animationDuration).SetEase(Ease.OutCubic);
    }

    public void Close()
    {
        _isOpening = false;

        // 1. 화면 밖으로 이동
        panelContent.DOKill();
        panelContent.DOAnchorPosX(_hiddenPosX, animationDuration).SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                // 2. 이동 완료 후 비활성화
                if (!_isOpening)
                {
                    panelRoot.SetActive(false);
                    if (backgroundCloseButton != null) backgroundCloseButton.SetActive(false);
                }
            });
    }

    public void OnBackgroundClicked()
    {
        if (_isOpening)
        {
            Close();
        }
    }

    public void RefreshList()
    {
        if (BattleManager.Instance == null || entryParent == null || entryPrefab == null) return;

        // 행동게이지 기준 오름차순 정렬 (0% -> 100%)
        // 이렇게 하면 VerticalLayoutGroup (Bottom 기준)에서 100%가 가장 아래에 오게 됨
        var sortedChars = BattleManager.Instance.allCharacters
            .OrderBy(c => c.ActionGauge)
            .ToList();

        // 엔트리 개수 조절
        while (_entries.Count < sortedChars.Count)
        {
            GameObject go = Instantiate(entryPrefab, entryParent);
            var entry = go.GetComponent<ActionListEntryUI>();
            if (entry != null) _entries.Add(entry);
        }

        // 데이터 갱신 및 활성화 여부 결정
        for (int i = 0; i < _entries.Count; i++)
        {
            if (i < sortedChars.Count)
            {
                _entries[i].gameObject.SetActive(true);
                _entries[i].UpdateUI(sortedChars[i]);
                // Sibling Index를 정렬 순서대로 설정
                _entries[i].transform.SetSiblingIndex(i);
            }
            else
            {
                _entries[i].gameObject.SetActive(false);
            }
        }
    }
}
