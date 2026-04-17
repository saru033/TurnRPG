using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 로비(Shelter) 상단에 상시 표시되는 재화(Gold, SkillUp 아이템)를 관리하는 UI 스크립트입니다.
/// </summary>
public class LobbyTopUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI txtGold;
    public TextMeshProUGUI txtSkillUp;

    private RectTransform _rect;
    private Vector2 _originalPos;
    private bool _isInitialized = false;

    // 데이터가 변경되었을 때 외부에서 호출할 수 있도록 싱글톤처럼 접근 가능하게 만듦 (편의용)
    public static LobbyTopUI Instance { get; private set; }
    
    private void Awake()
    {
        Instance = this;
        InitIfNecessary();
    }

    private void InitIfNecessary()
    {
        if (_isInitialized) return;
        _rect = GetComponent<RectTransform>();
        _originalPos = _rect.anchoredPosition;
        _isInitialized = true;
    }

    private void Start()
    {
        // 게임 시작 시 즉시 수치 반영
        Refresh();
    }

    /// <summary>
    /// GameManager의 현재 재화 데이터를 읽어와 UI를 최신화합니다.
    /// </summary>
    public void Refresh()
    {
        if (GameManager.Instance == null) return;

        if (txtGold != null)
        {
            txtGold.text = ": " + GameManager.Instance.gold.ToString("N0"); // 1,000 단위 콤마
        }

        if (txtSkillUp != null)
        {
            txtSkillUp.text = ": " + GameManager.Instance.skillup.ToString("N0");
        }
    }

    /// <summary>
    /// UI를 화면 안쪽(원래 위치)으로 이동시키며 활성화합니다.
    /// </summary>
    public void ShowUI(float duration = 0.5f)
    {
        InitIfNecessary();
        gameObject.SetActive(true);
        
        // 우측 밖에서 안으로 들어오는 연출
        _rect.anchoredPosition = new Vector2(_originalPos.x + _rect.rect.width, _originalPos.y);
        _rect.DOAnchorPos(_originalPos, duration).SetEase(Ease.OutCubic);
    }

    /// <summary>
    /// UI를 우측 밖으로 이동시킨 뒤 비활성화합니다.
    /// </summary>
    public void HideUI(float duration = 0.5f)
    {
        InitIfNecessary();
        
        // 우측으로 이동 후 비활성화
        _rect.DOAnchorPosX(_originalPos.x + _rect.rect.width, duration).SetEase(Ease.InCubic).OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }
}
