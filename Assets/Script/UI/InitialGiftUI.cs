using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class InitialGiftUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject root;
    public RectTransform panel;
    public Button btnReroll;
    public Button btnGold;
    public Button btnSkillUp;
    public Button btnItem; // [추가] 무작위 아이템

    [Header("Result UI")]
    public GameObject resultPanel;
    public Image imgResult;
    public TextMeshProUGUI txtResult;
    public Button btnResultConfirm;

    [Header("Icons")]
    public Sprite sprReroll;
    public Sprite sprGold;
    public Sprite sprSkillUp;

    [Header("References")]
    public EquipmentRerollUI rerollUI;
    public SkillUpgradeUI skillUpgradeUI;

    private Vector2 _originalPos;
    private bool _isInitialized = false;

    private void Awake()
    {
        InitIfNecessary();
    }

    private void InitIfNecessary()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        if (root != null) root.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (panel != null) _originalPos = panel.anchoredPosition;

        if (btnReroll != null) btnReroll.onClick.AddListener(() => OnChoiceSelected(0));
        if (btnGold != null) btnGold.onClick.AddListener(() => OnChoiceSelected(1));
        if (btnSkillUp != null) btnSkillUp.onClick.AddListener(() => OnChoiceSelected(2));
        if (btnItem != null) btnItem.onClick.AddListener(() => OnChoiceSelected(3));

        if (btnResultConfirm != null) btnResultConfirm.onClick.AddListener(CloseAll);
    }

    public void Open()
    {
        InitIfNecessary();
        if (resultPanel != null) resultPanel.SetActive(false);
        root.SetActive(true);
        panel.gameObject.SetActive(true);

        // 아래에서 위로 올라오는 연출 (RestPanel 참고)
        panel.anchoredPosition = new Vector2(_originalPos.x, _originalPos.y - 1000f);
        panel.DOAnchorPos(_originalPos, 0.4f).SetEase(Ease.OutCubic);
    }

    private void OnChoiceSelected(int choice)
    {
        if (GameManager.Instance == null) return;

        string msg = "";
        Sprite icon = null;
        TurnRPG.SkillSystem.SkillData giftData = null; // [추가] 툴팁용 데이터
        System.Action followUp = null;

        switch (choice)
        {
            case 0: // 리롤 아이템
                GameManager.Instance.rerollItemCount += 3;
                msg = "  를 3개 획득했어요";
                icon = sprReroll;
                followUp = () => { if (rerollUI != null) rerollUI.Open(); };
                break;
            case 1: // 골드
                GameManager.Instance.gold += 300;
                msg = "  를 300 만큼 획득했어요";
                icon = sprGold;
                break;
            case 2: // 스킬 강화석
                GameManager.Instance.skillup += 2;
                msg = "  를 2개만큼 획득했어요";
                icon = sprSkillUp;
                followUp = () => { if (skillUpgradeUI != null) skillUpgradeUI.Open(); };
                break;
            case 3: // 무작위 아이템
                giftData = GiveRandomItem();
                if (giftData != null)
                {
                    msg = $"  를 획득했어요";
                    icon = giftData.SkillIcon;
                }
                break;
        }

        if (LobbyTopUI.Instance != null) LobbyTopUI.Instance.Refresh();

        ShowResult(msg, icon, giftData, followUp);
    }

    private void ShowResult(string msg, Sprite icon, TurnRPG.SkillSystem.SkillData data, System.Action followUp)
    {
        // 선택 패널 숨기기
        panel.DOAnchorPosY(_originalPos.y - 1000f, 0.3f).SetEase(Ease.InCubic).OnComplete(() =>
        {
            // [수정] panel을 비활성화하면 자식인 resultPanel도 꺼지므로 비활성화하지 않습니다.
            // 대신 화면 밖으로 치워둔 상태를 유지합니다.

            // 결과창 표시
            if (resultPanel != null)
            {
                if (txtResult != null) txtResult.text = msg;
                if (imgResult != null && icon != null)
                {
                    imgResult.sprite = icon;

                    // [추가] 아이템인 경우 꾹 눌러서 정보를 볼 수 있게 트리거 설정
                    var tooltip = imgResult.GetComponent<SkillTooltipTrigger>();
                    if (tooltip != null)
                    {
                        if (data != null)
                        {
                            tooltip.enabled = true;
                            tooltip.Init(data, 1);
                        }
                        else
                        {
                            tooltip.enabled = false;
                        }
                    }
                }

                resultPanel.SetActive(true);
                var cg = resultPanel.GetComponent<CanvasGroup>();
                if (cg == null) cg = resultPanel.AddComponent<CanvasGroup>();
                cg.alpha = 0;
                cg.DOFade(1f, 0.4f);

                // 후속 작업 (리롤창 열기 등) 예약
                btnResultConfirm.onClick.RemoveAllListeners();
                btnResultConfirm.onClick.AddListener(() =>
                {
                    CloseAll();
                    followUp?.Invoke();
                });
            }
            else
            {
                CloseAll();
                followUp?.Invoke();
            }
        });
    }


    private TurnRPG.SkillSystem.SkillData GiveRandomItem()
    {
        var master = GameManager.Instance.masterSkillList;
        var items = master.FindAll(s => s != null && s.Type == TurnRPG.SkillSystem.SkillType.Item);
        if (items.Count > 0)
        {
            var selected = items[Random.Range(0, items.Count)];
            GameManager.Instance.playerItems.Add(selected);
            return selected;
        }
        return null;
    }


    public void CloseAll()
    {
        if (resultPanel != null && resultPanel.activeSelf)
        {
            var cg = resultPanel.GetComponent<CanvasGroup>();
            if (cg != null) cg.DOFade(0f, 0.3f).OnComplete(() => resultPanel.SetActive(false));
            else resultPanel.SetActive(false);
        }

        root.SetActive(false);
    }
}
