using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
using System;
using TurnRPG.SkillSystem;

public enum EventResultType
{
    None,
    Gold,
    HP,
    SkillUpgradeStone,
    AddItem,           // 아이템 획득
    RandomSkill,       // 무료 스킬 교체
}

[Serializable]
public struct EventChoice
{
    public string buttonText;
    public EventResultType resultType;
    public float value; // 골드 양, 회복 비율 등
}

[Serializable]
public struct RandomEventData
{
    public string eventTitle;
    [TextArea(3, 5)]
    public string eventDescription;
    public EventChoice choiceA;
    public EventChoice choiceB;
}

public class RandomEventPanelUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject rootPanel;       // 배경을 포함한 전체 패널
    public RectTransform contentPanel; // 실제 내용이 들어있는 패널 (DOTween 연출용)
    public TMP_Text txtDescription;

    [Header("Buttons")]
    public Button btnChoiceA;
    public TMP_Text txtChoiceA;
    public Button btnChoiceB;
    public TMP_Text txtChoiceB;

    public GameObject nextBtn;
    public GameObject mapui;
    private Vector2 _origNextBtnPos;

    [Header("External UI")]
    public SkillExchangeUI skillExchangeUI; // 스킬 교체 UI
    public SkillUpgradeUI skillUpgradeUI;   // 스킬 강화 UI

    [Header("Result Notification")]
    public GameObject resultPanel;
    public TMP_Text txtResult;
    public TMP_Text txtResultLong;
    public GameObject infoImg;
    public Sprite goldImg;
    public Sprite skillupImg;


    [Header("Event List")]
    public List<RandomEventData> eventPool;
    public List<SkillData> itemPool;  // 배틀 아이템 풀
    public List<SkillData> skillPool; // 캐릭터 스킬 풀

    private CanvasGroup _canvasGroup;
    private Vector2 _originalContentPos;
    private bool _isInitialized = false;

    private void InitIfNecessary()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _canvasGroup = rootPanel.GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = rootPanel.AddComponent<CanvasGroup>();

        // 초기 위치 미리 저장
        if (contentPanel != null)
        {
            _originalContentPos = contentPanel.anchoredPosition;
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
        // 패널이 활성화될 때마다 자동으로 랜덤 이벤트 세팅 및 연출 실행
        PrepareAndStartEvent();
    }

    /// <summary>
    /// 외부에서 이벤트를 강제로 시작하고 싶을 때 사용
    /// </summary>
    public void OpenRandomEvent()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        else PrepareAndStartEvent();
    }

    private void PrepareAndStartEvent()
    {
        //결과 창과 , 버튼 켜져 있다면 끄기
        if (resultPanel != null) resultPanel.SetActive(false);
        if (nextBtn != null) nextBtn.SetActive(false);

        if (eventPool == null || eventPool.Count == 0)
        {
            Debug.LogWarning("[Event] 이벤트 풀이 비어있습니다.");
            return;
        }

        // 1. 랜덤하게 하나 선택 및 배치
        int randomIndex = UnityEngine.Random.Range(0, eventPool.Count);
        SetupEvent(eventPool[randomIndex]);

        /*
                // 2. [DOTween] 등장 연출 시작
                rootPanel.SetActive(true);
                _canvasGroup.DOKill();
                _canvasGroup.alpha = 0;
                _canvasGroup.DOFade(1f, 0.4f);
        */
        if (contentPanel != null)
        {
            contentPanel.DOKill();
            contentPanel.gameObject.SetActive(true);
            // 위에서 아래로 내려오는 연출 (+200f)
            contentPanel.anchoredPosition = new Vector2(_originalContentPos.x, _originalContentPos.y + 200f);
            contentPanel.DOAnchorPos(_originalContentPos, 0.4f).SetEase(Ease.OutCubic);
        }
    }

    private void SetupEvent(RandomEventData data)
    {
        txtDescription.text = data.eventDescription;

        // 선택지 A 설정
        txtChoiceA.text = data.choiceA.buttonText;
        btnChoiceA.onClick.RemoveAllListeners();
        btnChoiceA.onClick.AddListener(() => ExecuteResult(data.choiceA));

        // 선택지 B 설정
        txtChoiceB.text = data.choiceB.buttonText;
        btnChoiceB.onClick.RemoveAllListeners();
        btnChoiceB.onClick.AddListener(() => ExecuteResult(data.choiceB));
    }

    private void ExecuteResult(EventChoice choice)
    {
        string resultMsg = "";
        SkillData data = null;
        bool itemBlock = false;
        switch (choice.resultType)
        {
            case EventResultType.Gold:
                GameManager.Instance.gold += (int)choice.value;
                resultMsg = $"를 {choice.value}만큼 획득 했어요.";
                break;

            case EventResultType.HP:
                foreach (var charState in GameManager.Instance.party)
                {
                    if (charState != null)
                    {
                        float healAmount = charState.template.MaxHp * choice.value;
                        charState.currentHp = Mathf.Min(charState.currentHp + healAmount, charState.template.MaxHp);
                    }
                }
                resultMsg = $"파티 전원의 체력이 {choice.value * 100}% 회복 되었어요.";
                break;

            case EventResultType.SkillUpgradeStone:
                GameManager.Instance.skillup += (int)choice.value;
                skillUpgradeUI.Open();
                resultMsg = $"를 {choice.value}개 얻어서\n바로 스킬을 강화했어요.";
                break;

            case EventResultType.AddItem:
                if (GameManager.Instance.playerItems.Count >= 3)
                {
                    resultMsg = "가방이 꽉 차서 아이템을 챙기지 못했어요.";
                    itemBlock = true;

                }
                else if (itemPool != null && itemPool.Count > 0)
                {
                    SkillData randomItem = itemPool[UnityEngine.Random.Range(0, itemPool.Count)];
                    GameManager.Instance.playerItems.Add(randomItem);
                    resultMsg = $"를 얻었어요.";
                    data = randomItem;
                }
                break;

            case EventResultType.RandomSkill:
                if (skillExchangeUI != null && skillPool != null && skillPool.Count > 0)
                {
                    // 현재 파티가 이미 가진 스킬 제외
                    List<SkillData> equipped = new List<SkillData>();
                    foreach (var p in GameManager.Instance.party)
                    {
                        if (p != null) equipped.AddRange(p.equippedSkills);
                    }

                    List<SkillData> available = new List<SkillData>();
                    foreach (var s in skillPool)
                    {
                        if (!equipped.Contains(s)) available.Add(s);
                    }

                    if (available.Count > 0)
                    {
                        SkillData targetSkill = available[UnityEngine.Random.Range(0, available.Count)];
                        skillExchangeUI.Open(targetSkill, 0, null);
                        resultMsg = "를 배울 기회를 얻었어요.";
                        data = targetSkill;
                    }
                    else
                    {
                        resultMsg = "이미 모든 스킬을 마스터 했어요.";
                    }
                }
                break;

            case EventResultType.None:
                resultMsg = "아무 일도 일어나지 않았어요.";
                break;
        }

        // 결과창 표시 시퀀스 시작
        TransitionToResult(resultMsg, choice, data, itemBlock);

        // 결과 반영 UI 갱신 (상단바 등)
        if (LobbyTopUI.Instance != null) LobbyTopUI.Instance.Refresh();
    }

    private void TransitionToResult(string msg, EventChoice choice, SkillData data = null, bool itemBlock = false)
    {
        // 1. 기존 내용물 위로 올리며 페이드 아웃
        if (contentPanel != null)
        {
            contentPanel.DOAnchorPos(new Vector2(_originalContentPos.x, _originalContentPos.y + 200f), 0.3f)
                .SetEase(Ease.InCubic)
                .OnComplete(() =>
                {
                    // 위치 초기화 (다음에 다시 켤 때를 위해)
                    contentPanel.gameObject.SetActive(false);
                    contentPanel.anchoredPosition = _originalContentPos;

                    // 2. 결과창 표시
                    ShowResult(msg, choice, data, itemBlock);
                });
        }
        else
        {
            ShowResult(msg, choice, data, itemBlock);
        }
    }

    private void ShowResult(string msg, EventChoice choice, SkillData data = null, bool isItemBlocked = false)
    {
        txtResultLong.gameObject.SetActive(false);
        txtResult.gameObject.SetActive(false);

        if (infoImg == null) return;

        SkillTooltipTrigger tooltip = infoImg.GetComponent<SkillTooltipTrigger>();
        if (tooltip != null)
        {
            if (data != null)
            {
                tooltip.enabled = true;
                tooltip.Init(data, 1);
            }
            else
            {
                // 강화석 등 SkillData가 없는 경우에는 툴팁 작동 중지
                tooltip.enabled = false;
            }
        }

        if (choice.resultType == EventResultType.None || choice.resultType == EventResultType.HP)
        {
            infoImg.SetActive(false);
            txtResultLong.gameObject.SetActive(true);
        }
        else
        {
            Image mainImg = infoImg.GetComponent<Image>();
            if (mainImg != null)
            {
                if (choice.resultType == EventResultType.Gold)
                {
                    mainImg.sprite = goldImg;
                    txtResult.gameObject.SetActive(true);
                }
                else if (choice.resultType == EventResultType.SkillUpgradeStone)
                {
                    mainImg.sprite = skillupImg;
                    txtResult.gameObject.SetActive(true);
                }
                else if (data != null)
                {
                    mainImg.sprite = data.SkillIcon;
                    txtResult.gameObject.SetActive(true);
                }
            }

            infoImg.SetActive(true);
        }

        if (isItemBlocked)
        {
            infoImg.SetActive(false);
            txtResultLong.gameObject.SetActive(true);
            txtResult.gameObject.SetActive(false);
        }

        if (resultPanel != null)
        {
            txtResultLong.text = msg;
            txtResult.text = msg;

            // 결과창 페이드 인 연출
            CanvasGroup resCg = resultPanel.GetComponent<CanvasGroup>();
            if (resCg == null) resCg = resultPanel.AddComponent<CanvasGroup>();

            resultPanel.SetActive(true);
            resCg.DOKill();
            resCg.alpha = 0;
            resCg.DOFade(1f, 0.4f);
        }
        else
        {
            Debug.Log($"[Event Result] {msg}");
        }
    }

    /// <summary>
    /// 단순히 패널을 닫을 때 (결과창 확인 버튼 등)
    /// </summary>
    public void CloseAll()
    {
        if (resultPanel != null && resultPanel.activeSelf)
        {
            CanvasGroup resCg = resultPanel.GetComponent<CanvasGroup>();
            if (resCg != null)
            {
                resCg.DOFade(0f, 0.3f).OnComplete(() => resultPanel.SetActive(false));
            }
            else resultPanel.SetActive(false);
        }

        if (rootPanel.activeSelf)
        {
            _canvasGroup.DOFade(0f, 0.3f);
        }
    }



    public void ShowNextBtn()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
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
