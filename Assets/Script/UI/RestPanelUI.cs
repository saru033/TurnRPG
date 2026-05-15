using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 맵에서 휴식(Rest) 노드 선택 시 등장하는 패널을 관리합니다.
/// 휴식하기: 쿨타임 초기화 및 체력 30% 회복
/// 훈련하기: 스킬 강화석 2개 획득 및 스킬 강화 UI 오픈
/// </summary>
public class RestPanelUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button restButton;
    public Button trainButton;

    [Header("References")]
    public GameObject SelectPanel;
    public GameObject nextBtn;
    public GameObject mapui;

    private Vector2 _origSelectPos;
    private Vector2 _origNextBtnPos;
    private bool _isInitialized = false;

    private void Awake()
    {
        if (restButton != null) restButton.onClick.AddListener(OnRestClicked);
        if (trainButton != null) trainButton.onClick.AddListener(OnTrainClicked);
    }

    private void InitIfNecessary()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        if (SelectPanel != null)
        {
            var rt = SelectPanel.GetComponent<RectTransform>();
            if (rt != null) _origSelectPos = rt.anchoredPosition;
        }

        if (nextBtn != null)
        {
            var rt = nextBtn.GetComponent<RectTransform>();
            if (rt != null) _origNextBtnPos = rt.anchoredPosition;
        }
    }

    private void OnEnable()
    {
        InitIfNecessary();

        if (SelectPanel != null)
        {
            SelectPanel.SetActive(true);
            var rt = SelectPanel.GetComponent<RectTransform>();
            if (rt != null)
            {
                // 위에서 아래로 내려오는 연출
                rt.anchoredPosition = new Vector2(_origSelectPos.x, _origSelectPos.y + 1000f);
                rt.DOAnchorPos(_origSelectPos, 0.4f).SetEase(Ease.OutCubic);
            }
        }

        if (nextBtn != null) nextBtn.SetActive(false);
    }


    private void OnRestClicked()
    {
        if (GameManager.Instance != null)
        {
            foreach (var charState in GameManager.Instance.party)
            {
                if (charState != null)
                {
                    // 1. 최대 체력의 30% 회복 (장비 보너스가 포함된 TotalMaxHp 기준)
                    float maxHp = charState.TotalMaxHp;
                    float healAmount = maxHp * 0.3f;
                    charState.currentHp += healAmount;
                    if (charState.currentHp > maxHp)
                    {
                        charState.currentHp = maxHp;
                    }

                    // 2. 스킬 쿨타임 초기화
                    for (int i = 0; i < 3; i++)
                    {
                        charState.skillCooldowns[i] = 0;
                    }
                }
            }
            Debug.Log("[RestPanel] 파티 전원 체력 30% 회복 및 쿨타임 초기화 완료.");
        }

        ClosePanel();
    }

    private void OnTrainClicked()
    {
        if (GameManager.Instance != null)
        {
            // 강화석 1개 지급
            GameManager.Instance.skillup += 1;
            if (LobbyTopUI.Instance != null) LobbyTopUI.Instance.Refresh();
            Debug.Log("[RestPanel] 스킬 강화석 1개 획득!");
        }

        // 스킬 강화창 열기
        if (BattleManager.Instance != null && BattleManager.Instance.battleUI != null && BattleManager.Instance.battleUI.skillUpgradeUI != null)
        {
            // 스킬 강화창이 닫힐 때 다시 맵으로 돌아오도록 콜백 지정
            BattleManager.Instance.battleUI.skillUpgradeUI.OnClose = ClosePanel;
            BattleManager.Instance.battleUI.skillUpgradeUI.Open();
        }
        else
        {
            Debug.LogError("[RestPanel] SkillUpgradeUI 래퍼런스를 찾을 수 없습니다.");
            ClosePanel();
        }
    }

    private void ClosePanel()
    {
        if (SelectPanel != null)
        {
            var rt = SelectPanel.GetComponent<RectTransform>();
            if (rt != null)
            {
                // 위로 올라가며 사라짐
                rt.DOAnchorPos(new Vector2(_origSelectPos.x, _origSelectPos.y + 1000f), 0.4f)
                  .SetEase(Ease.InCubic)
                  .OnComplete(() =>
                  {
                      SelectPanel.SetActive(false);
                      ShowNextBtn();
                  });
            }
            else
            {
                SelectPanel.SetActive(false);
                ShowNextBtn();
            }
        }
        else
        {
            ShowNextBtn();
        }
    }

    private void ShowNextBtn()
    {
        if (nextBtn != null)
        {
            nextBtn.SetActive(true);
            var rt = nextBtn.GetComponent<RectTransform>();
            if (rt != null)
            {
                // 아래에서 위로 등장
                rt.anchoredPosition = new Vector2(_origNextBtnPos.x, _origNextBtnPos.y - 1000f);
                rt.DOAnchorPos(_origNextBtnPos, 0.4f).SetEase(Ease.OutCubic);
            }
        }
    }

    public void openMap()
    {
        mapui.SetActive(true);
    }
}
