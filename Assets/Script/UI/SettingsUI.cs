using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Runtime;

public class SettingsUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject root;
    public RectTransform menuPanel;
    public Button btnRestart;
    public Button btnClose;

    public GameObject confirmPanel;
    public Button btnConfirmYes;
    public Button btnConfirmNo;

    private Vector2 _originalPanelPos;
    private bool _isOpen = false;

    private void Awake()
    {
        if (root != null) root.SetActive(false);
        if (menuPanel != null) _originalPanelPos = menuPanel.anchoredPosition;

        if (btnRestart != null) btnRestart.onClick.AddListener(OpenConfirmPanel);
        if (btnClose != null) btnClose.onClick.AddListener(Close);
        if (btnConfirmYes != null) btnConfirmYes.onClick.AddListener(OnRestartClicked);
        if (btnConfirmNo != null) btnConfirmNo.onClick.AddListener(CloseConfirmPanel);
    }

    public void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;

        confirmPanel.SetActive(false);

        root.SetActive(true);
        // 위에서 아래로 내려오는 연출
        menuPanel.anchoredPosition = new Vector2(_originalPanelPos.x, _originalPanelPos.y + 1000f);
        menuPanel.DOAnchorPos(_originalPanelPos, 0.4f).SetEase(Ease.OutBack);
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;

        // 위로 올라가면서 사라지는 연출
        menuPanel.DOAnchorPosY(_originalPanelPos.y + 1000f, 0.3f).SetEase(Ease.InCubic).OnComplete(() =>
        {
            root.SetActive(false);
        });
    }


    public void OpenConfirmPanel()
    {
        confirmPanel.SetActive(true);
    }

    public void CloseConfirmPanel()
    {
        confirmPanel.SetActive(false);
    }


    private void OnRestartClicked()
    {
        Close();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetGameProgress();
        }
    }

    public void GameCloseBtn(){
        Application.Quit();
    }
}
