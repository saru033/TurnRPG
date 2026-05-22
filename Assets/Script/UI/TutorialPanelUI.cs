using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public enum TutorialType
{
    None,
    Item,
    Shelter,
    Battle
}

/// <summary>
/// 게임 가이드 및 튜토리얼 연출을 관리하는 UI 스크립트입니다.
/// </summary>
public class TutorialPanelUI : MonoBehaviour
{
    public static TutorialPanelUI Instance { get; private set; }

    [Header("UI Roots & Interaction")]
    [Tooltip("튜토리얼 패널의 최상위 루트 오브젝트")]
    public GameObject panelRoot;
    [Tooltip("점진적으로 어두워지는 암전용 배경 이미지")]
    public Image bgFadeImage;
    [Tooltip("화면 클릭 시 다음 대사로 넘어가도록 처리하는 버튼")]
    public Button nextButton;

    [Header("Text Displays (튜토리얼별 개별 텍스트 박스 사용)")]
    [Tooltip("아이템 가이드용 설명문 텍스트")]
    public TextMeshProUGUI itemDialogueText;
    [Tooltip("쉘터 가이드용 설명문 텍스트")]
    public TextMeshProUGUI shelterDialogueText;
    [Tooltip("전투 가이드용 설명문 텍스트")]
    public TextMeshProUGUI battleDialogueText;

    [Header("Item Tutorial Visuals (Image 및 복합 GameObject 제어)")]
    [Tooltip("아이템 튜토리얼 시 활성화할 비주얼 부모")]
    public GameObject itemVisualsRoot;
    [Tooltip("스킬 강화 아이템에 해당하는 Image")]
    public Image skillUpgradeImg;
    [Tooltip("장비 Reroll 아이템에 해당하는 Image")]
    public Image equipmentRerollImg;
    [Tooltip("장비 고급 Reroll 아이템에 해당하는 Image")]
    public Image highEquipmentRerollImg;
    
    [Tooltip("변경된 장비의 예시 복합 GameObject (위에서 아래로 동시 등장)")]
    public GameObject equipmentExampleObj;
    [Tooltip("변경된 스킬의 예시 복합 GameObject (위에서 아래로 동시 등장)")]
    public GameObject skillExampleObj;

    [Header("Shelter Tutorial Visuals")]
    [Tooltip("쉘터 튜토리얼 시 활성화할 비주얼 부모")]
    public GameObject shelterVisualsRoot;
    [Tooltip("쉬고 있는 캐릭터 이미지 (왼쪽에서 부드럽게 들어옴)")]
    public Image shelterRestingCharImg;
    [Tooltip("휴식처 안내 이미지 (오른쪽에서 부드럽게 들어옴)")]
    public Image shelterRestPlaceImg;

    [Header("Battle Tutorial Visuals")]
    [Tooltip("전투 튜토리얼 시 활성화할 비주얼 부모")]
    public GameObject battleVisualsRoot;
    [Tooltip("모래시계 정보창 예시 GameObject (우측에서 좌측으로 튀어나옴)")]
    public GameObject hourglassExampleObj;
    [Tooltip("아군 스킬 모음 예시 GameObject (아래에서 위로 튀어나옴)")]
    public GameObject skillGroupExampleObj;
    [Tooltip("전투 가방 아이템 예시 GameObject (아래에서 위로 튀어나옴)")]
    public GameObject bagExampleObj;

    [Header("Testing & Save Settings")]
    [Tooltip("활성화 시 플레이어별 최초 1회 제한을 무시하고 매번 튜토리얼을 엽니다 (테스트용)")]
    public bool forceShowForTesting = true;

    private TutorialType _currentType = TutorialType.None;
    private int _currentStepIndex = 0;
    private System.Action _onComplete;

    // 원래 앵커 위치 캐싱용 변수들
    private Vector2 _restingCharOriginalPos;
    private Vector2 _restPlaceOriginalPos;
    
    // 아이템 원래 좌표 캐싱
    private Vector2 _skillUpgradeOrigPos;
    private Vector2 _equipmentRerollOrigPos;
    private Vector2 _highEquipmentRerollOrigPos;
    private Vector2 _equipmentExampleOrigPos;
    private Vector2 _skillExampleOrigPos;

    // 전투 원래 좌표 캐싱
    private Vector2 _hourglassOrigPos;
    private Vector2 _skillGroupOrigPos;
    private Vector2 _bagOrigPos;
    
    private bool _hasCachedPositions = false;

    // 아이템 설명 튜토리얼 대사 시퀀스
    private readonly string[] _itemTexts = new string[]
    {
        "획득한 강화 아이템을 사용해\n캐릭터를 강화할 수 있어요.", // index 0 (대기)
        "이 아이템은 캐릭터의 스킬을 강화해\n추가적인 효과를 부여할 수 있어요.", // index 1 (스킬 강화석) -> 위에서 아래로
        "이 아이템은 장비의 옵션을 랜덤하게\n재설정 할 수 있어요.", // index 2 (장비 리롤) -> 위에서 아래로
        "이 아이템은 장비의 옵션을 최고치 수준으로\n랜덤하게 재설정 할 수 있어요.", // index 3 (장비 고급 리롤) -> 위에서 아래로
        "어떤 캐릭터의 장비 혹은 스킬에\n사용하고 싶은지 선택해요.", // index 4 (다섯번째) -> 3종 위로 올려서 퇴장
        "변경된 장비 혹은 스킬을 눌러야\n최종적으로 적용돼요.", // index 5 (여섯번째) -> 장비/스킬 예시 GameObject 2개 동시에 위에서 아래로 등장
        "다른 부분을 누르면 사용이 취소돼요.", // index 6
        "이건 장비 옵션 변경 , 스킬 강화\n심지어 스킬 변경까지 동일해요.", // index 7
        "단! 장비의 옵션을 바꾸는 아이템은 \n새로운 장비 옵션을 보는 순간 \n사용이 확정되니 주의하세요!" // index 8
    };

    // 쉘터 튜토리얼 대사 시퀀스
    private readonly string[] _shelterTexts = new string[]
    {
        "우린 지금 안전한 쉘터에 있어요.",
        "그런데 쉘터에 남은 물자가 별로 없어요.\n슬슬 떠날 준비를 해야 할 것 같아요.",
        "캐릭터들은 지금 박스에 앉아 쉬고 있는 상태에요.\n이 캐릭터들을 눌러 추가적인 스탯을 올려줄 수 있어요.",
        "스탯은 여행 중에 휴식 장소에 가면 다시 올릴 수 있으니\n자유롭게 스탯을 올려봐요"
    };

    // 전투 튜토리얼 대사 시퀀스
    private readonly string[] _battleTexts = new string[]
    {
        "쉘터를 떠나자 마자 적과 조우했어요.", // index 0 (대기)
        "화면 좌측에 표시되는 게이지는 행동 게이지로\n캐릭터의 속도에 따라 100%에 도달하는\n순서대로 턴이 돌아와요.", // index 1 (대기)
        "그 위에 모래시계 모양 버튼을 누르면 정확한 정보와\n 적 스킬의 스킬 등을 볼 수 있어요.\n처음 보는 적이라면 한 번 씩 확인해 봐요.", // index 2 -> 모래시계 등장 (우측에서 좌측으로)
        "아군 캐릭터의 턴이 오면 우측 하단의 스킬 중에서\n원하는 스킬을 골라, 대상 적(또는 아군)을\n클릭하여 스킬을 발동할 수 있어요.", // index 3 -> 모래시계 퇴장(우측으로), 스킬 모음 등장 (아래에서 위로)
        "스킬 옆에 가방이 있어요.\n모험을 하면서 얻은 아이템을 보관하고 있어요.", // index 4 -> 스킬 퇴장(아래로), 가방 등장 (아래에서 위로)
        "스킬과 마찬가지로 사용할 수 있지만\n턴을 소모하지 않고 사용할 수 있어요.", // index 5 -> 가방 유지
        "모든 적을 처치하면 승리!\n모든 아군이 처치 당하면 패배!", // index 6 -> 가방 퇴장(아래로)
        "자, 이제 첫 전투를 시작해봐요!" // index 7 (대기)
    };

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 초기 시작 시 꺼둠
        if (panelRoot != null) panelRoot.SetActive(false);

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnNextButtonClicked);
        }

        CachePositionsIfNecessary();
    }

    private void CachePositionsIfNecessary()
    {
        if (_hasCachedPositions) return;

        // 쉘터 좌표 캐싱
        if (shelterRestingCharImg != null) _restingCharOriginalPos = shelterRestingCharImg.rectTransform.anchoredPosition;
        if (shelterRestPlaceImg != null) _restPlaceOriginalPos = shelterRestPlaceImg.rectTransform.anchoredPosition;

        // 아이템 3종 좌표 캐싱
        if (skillUpgradeImg != null) _skillUpgradeOrigPos = skillUpgradeImg.rectTransform.anchoredPosition;
        if (equipmentRerollImg != null) _equipmentRerollOrigPos = equipmentRerollImg.rectTransform.anchoredPosition;
        if (highEquipmentRerollImg != null) _highEquipmentRerollOrigPos = highEquipmentRerollImg.rectTransform.anchoredPosition;

        // 예시 GameObject 좌표 캐싱 (RectTransform)
        if (equipmentExampleObj != null)
        {
            var rt = equipmentExampleObj.GetComponent<RectTransform>();
            if (rt != null) _equipmentExampleOrigPos = rt.anchoredPosition;
        }
        if (skillExampleObj != null)
        {
            var rt = skillExampleObj.GetComponent<RectTransform>();
            if (rt != null) _skillExampleOrigPos = rt.anchoredPosition;
        }

        // 전투 예시 GameObject 좌표 캐싱 (RectTransform)
        if (hourglassExampleObj != null)
        {
            var rt = hourglassExampleObj.GetComponent<RectTransform>();
            if (rt != null) _hourglassOrigPos = rt.anchoredPosition;
        }
        if (skillGroupExampleObj != null)
        {
            var rt = skillGroupExampleObj.GetComponent<RectTransform>();
            if (rt != null) _skillGroupOrigPos = rt.anchoredPosition;
        }
        if (bagExampleObj != null)
        {
            var rt = bagExampleObj.GetComponent<RectTransform>();
            if (rt != null) _bagOrigPos = rt.anchoredPosition;
        }

        _hasCachedPositions = true;
    }

    /// <summary>
    /// 지정된 튜토리얼을 시작합니다.
    /// </summary>
    public void StartTutorial(TutorialType type, System.Action onComplete = null)
    {
        if (type == TutorialType.None) return;

        // 이미 완료했는지 체크 (테스트 모드가 아닐 때만)
        if (!forceShowForTesting && HasSeenTutorial(type))
        {
            onComplete?.Invoke();
            return;
        }

        _currentType = type;
        _currentStepIndex = 0;
        _onComplete = onComplete;

        CachePositionsIfNecessary();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);

            // CanvasGroup을 활용해 전체 패널 부드럽게 페이드 인 등장
            var cg = panelRoot.GetComponent<CanvasGroup>();
            if (cg == null) cg = panelRoot.AddComponent<CanvasGroup>();
            cg.DOKill();
            cg.alpha = 0f;
            cg.DOFade(1f, 0.35f).SetUpdate(true);
        }

        // 배경 점진적 암전 연출
        if (bgFadeImage != null)
        {
            bgFadeImage.DOKill();
            Color c = bgFadeImage.color;
            c.a = 0f;
            bgFadeImage.color = c;
            bgFadeImage.DOFade(0.78f, 0.45f).SetUpdate(true); // Timescale 무관하게 연출
        }

        ResetVisualStates();
        ShowStep(_currentStepIndex);
    }

    private void ResetVisualStates()
    {
        // 1. 비주얼 루트 온/오프
        if (itemVisualsRoot != null) itemVisualsRoot.SetActive(_currentType == TutorialType.Item);
        if (shelterVisualsRoot != null) shelterVisualsRoot.SetActive(_currentType == TutorialType.Shelter);
        if (battleVisualsRoot != null) battleVisualsRoot.SetActive(_currentType == TutorialType.Battle);

        // 2. 텍스트 박스 전체 초기화 및 해당 타입만 활성화
        if (itemDialogueText != null) { itemDialogueText.gameObject.SetActive(_currentType == TutorialType.Item); itemDialogueText.text = ""; }
        if (shelterDialogueText != null) { shelterDialogueText.gameObject.SetActive(_currentType == TutorialType.Shelter); shelterDialogueText.text = ""; }
        if (battleDialogueText != null) { battleDialogueText.gameObject.SetActive(_currentType == TutorialType.Battle); battleDialogueText.text = ""; }

        // 3. 아이템 이미지 크기 0, 알파 0 초기화 및 오프셋 위치(위쪽 300px) 세팅
        if (skillUpgradeImg != null)
        {
            skillUpgradeImg.DOKill();
            skillUpgradeImg.rectTransform.DOKill();
            SetImageAlpha(skillUpgradeImg, 0f);
            skillUpgradeImg.rectTransform.anchoredPosition = _skillUpgradeOrigPos + new Vector2(0f, 300f);
        }
        if (equipmentRerollImg != null)
        {
            equipmentRerollImg.DOKill();
            equipmentRerollImg.rectTransform.DOKill();
            SetImageAlpha(equipmentRerollImg, 0f);
            equipmentRerollImg.rectTransform.anchoredPosition = _equipmentRerollOrigPos + new Vector2(0f, 300f);
        }
        if (highEquipmentRerollImg != null)
        {
            highEquipmentRerollImg.DOKill();
            highEquipmentRerollImg.rectTransform.DOKill();
            SetImageAlpha(highEquipmentRerollImg, 0f);
            highEquipmentRerollImg.rectTransform.anchoredPosition = _highEquipmentRerollOrigPos + new Vector2(0f, 300f);
        }

        // 3.5. 변경 예시 복합 GameObject 초기화 및 오프셋 위치(위쪽 300px) 세팅
        if (equipmentExampleObj != null)
        {
            equipmentExampleObj.SetActive(false);
            var rt = equipmentExampleObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.DOKill();
                rt.anchoredPosition = _equipmentExampleOrigPos + new Vector2(0f, 300f);
            }
            var cg = equipmentExampleObj.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
        }
        if (skillExampleObj != null)
        {
            skillExampleObj.SetActive(false);
            var rt = skillExampleObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.DOKill();
                rt.anchoredPosition = _skillExampleOrigPos + new Vector2(0f, 300f);
            }
            var cg = skillExampleObj.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
        }

        // 3.8. 전투 예시 GameObject 초기화 및 오프셋 위치 세팅
        if (hourglassExampleObj != null)
        {
            hourglassExampleObj.SetActive(false);
            var rt = hourglassExampleObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.DOKill();
                rt.anchoredPosition = _hourglassOrigPos + new Vector2(300f, 0f); // 우측 대기
            }
            var cg = hourglassExampleObj.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
        }
        if (skillGroupExampleObj != null)
        {
            skillGroupExampleObj.SetActive(false);
            var rt = skillGroupExampleObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.DOKill();
                rt.anchoredPosition = _skillGroupOrigPos - new Vector2(0f, 300f); // 아래 대기
            }
            var cg = skillGroupExampleObj.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
        }
        if (bagExampleObj != null)
        {
            bagExampleObj.SetActive(false);
            var rt = bagExampleObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.DOKill();
                rt.anchoredPosition = _bagOrigPos - new Vector2(0f, 300f); // 아래 대기
            }
            var cg = bagExampleObj.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
        }

        // 4. 쉘터 특수 이미지 초기화 및 화면 밖 배치 (좌우)
        if (shelterRestingCharImg != null)
        {
            shelterRestingCharImg.DOKill();
            shelterRestingCharImg.rectTransform.DOKill();
            SetImageAlpha(shelterRestingCharImg, 0f);
            shelterRestingCharImg.rectTransform.anchoredPosition = _restingCharOriginalPos - new Vector2(300f, 0f);
        }
        if (shelterRestPlaceImg != null)
        {
            shelterRestPlaceImg.DOKill();
            shelterRestPlaceImg.rectTransform.DOKill();
            SetImageAlpha(shelterRestPlaceImg, 0f);
            shelterRestPlaceImg.rectTransform.anchoredPosition = _restPlaceOriginalPos + new Vector2(300f, 0f);
        }
    }

    private void SetImageAlpha(Image img, float alpha)
    {
        if (img != null)
        {
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }

    private void ShowStep(int index)
    {
        string[] texts = _currentType switch
        {
            TutorialType.Item => _itemTexts,
            TutorialType.Shelter => _shelterTexts,
            TutorialType.Battle => _battleTexts,
            _ => new string[0]
        };

        if (index < 0 || index >= texts.Length)
        {
            CloseTutorial();
            return;
        }

        // 현재 유형에 맞는 개별 텍스트 컴포넌트 갱신
        TextMeshProUGUI activeText = _currentType switch
        {
            TutorialType.Item => itemDialogueText,
            TutorialType.Shelter => shelterDialogueText,
            TutorialType.Battle => battleDialogueText,
            _ => null
        };

        if (activeText != null)
        {
            activeText.text = texts[index];
            activeText.alpha = 0f;
            activeText.transform.localScale = Vector3.one * 0.93f;

            activeText.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
            DOTween.To(() => activeText.alpha, x => activeText.alpha = x, 1f, 0.2f).SetUpdate(true);
        }

        // --- 아이템 튜토리얼 이미지 단계별 정교한 DOTween 등장/퇴장 연출 ---
        if (_currentType == TutorialType.Item)
        {
            // index == 1 : 스킬 강화석 위에서 아래로 등장
            if (index == 1 && skillUpgradeImg != null)
            {
                AnimateImageInFromTop(skillUpgradeImg, _skillUpgradeOrigPos);
            }
            // index == 2 : 이전 스킬 강화석은 위로 밀어 퇴장하고, 장비 리롤 위에서 아래로 등장
            else if (index == 2)
            {
                if (skillUpgradeImg != null) AnimateImageOutToTop(skillUpgradeImg, _skillUpgradeOrigPos);
                if (equipmentRerollImg != null) AnimateImageInFromTop(equipmentRerollImg, _equipmentRerollOrigPos);
            }
            // index == 3 : 이전 장비 리롤은 위로 밀어 퇴장하고, 고급 리롤 위에서 아래로 등장
            else if (index == 3)
            {
                if (equipmentRerollImg != null) AnimateImageOutToTop(equipmentRerollImg, _equipmentRerollOrigPos);
                if (highEquipmentRerollImg != null) AnimateImageInFromTop(highEquipmentRerollImg, _highEquipmentRerollOrigPos);
            }
            // index == 4 (다섯번째) : 이전 마지막 고급 리롤 이미지를 위로 올려서 퇴장 & 비활성화
            else if (index == 4)
            {
                if (highEquipmentRerollImg != null) AnimateImageOutToTop(highEquipmentRerollImg, _highEquipmentRerollOrigPos);
            }
            // index == 5 (여섯번째) : 장비 예시 GameObject & 스킬 예시 GameObject 동시에 위에서 아래로 등장
            else if (index == 5)
            {
                if (equipmentExampleObj != null) AnimateObjectInFromTop(equipmentExampleObj, _equipmentExampleOrigPos);
                if (skillExampleObj != null) AnimateObjectInFromTop(skillExampleObj, _skillExampleOrigPos);
            }
        }
        // --- 쉘터 튜토리얼 특수 이미지 부드러운 좌우 슬라이드 등장 연출 ---
        else if (_currentType == TutorialType.Shelter)
        {
            // index == 2 : 앉아있는 캐릭터 이미지 등장 (왼쪽에서 들어옴)
            if (index == 2 && shelterRestingCharImg != null)
            {
                shelterRestingCharImg.DOKill();
                shelterRestingCharImg.rectTransform.DOKill();

                // 투명도 0 -> 1 페이드인
                shelterRestingCharImg.DOFade(1f, 0.4f).SetUpdate(true);
                // 왼쪽(-300px) 위치에서 원래 오리지널 포지션으로 슬라이딩
                shelterRestingCharImg.rectTransform.DOAnchorPos(_restingCharOriginalPos, 0.5f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
            // index == 3 : 휴식 장소 이미지 등장 (오른쪽에서 들어옴)
            else if (index == 3 && shelterRestPlaceImg != null)
            {
                shelterRestPlaceImg.DOKill();
                shelterRestPlaceImg.rectTransform.DOKill();

                // 투명도 0 -> 1 페이드인
                shelterRestPlaceImg.DOFade(1f, 0.4f).SetUpdate(true);
                // 오른쪽(+300px) 위치에서 원래 오리지널 포지션으로 슬라이딩
                shelterRestPlaceImg.rectTransform.DOAnchorPos(_restPlaceOriginalPos, 0.5f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }
        // --- 전투 튜토리얼 이미지 단계별 정교한 DOTween 등장/퇴장 연출 ---
        else if (_currentType == TutorialType.Battle)
        {
            // index == 2 : 모래시계 모양 버튼 설명 -> 모래시계 이미지 우측에서 좌측으로 등장
            if (index == 2 && hourglassExampleObj != null)
            {
                AnimateObjectInFromRight(hourglassExampleObj, _hourglassOrigPos);
            }
            // index == 3 : 아군 스킬 모음 설명 -> 이전 모래시계는 우측 퇴장, 스킬 모음 아래에서 위로 등장
            else if (index == 3)
            {
                if (hourglassExampleObj != null) AnimateObjectOutToRight(hourglassExampleObj, _hourglassOrigPos);
                if (skillGroupExampleObj != null) AnimateObjectInFromBottom(skillGroupExampleObj, _skillGroupOrigPos);
            }
            // index == 4 : 전투 가방 아이템 설명 -> 이전 스킬 모음은 아래로 퇴장, 가방 아래에서 위로 등장
            else if (index == 4)
            {
                if (skillGroupExampleObj != null) AnimateObjectOutToBottom(skillGroupExampleObj, _skillGroupOrigPos);
                if (bagExampleObj != null) AnimateObjectInFromBottom(bagExampleObj, _bagOrigPos);
            }
            // index == 6 : 모든 적 처치 언급 -> 이전 가방 이미지 아래로 퇴장
            else if (index == 6)
            {
                if (bagExampleObj != null) AnimateObjectOutToBottom(bagExampleObj, _bagOrigPos);
            }
        }
    }

    /// <summary>
    /// 이미지를 위쪽 오프셋(+300px)에서 원래 지정 좌표로 페이드인하며 내려오게 합니다.
    /// </summary>
    private void AnimateImageInFromTop(Image img, Vector2 targetPos)
    {
        img.DOKill();
        img.rectTransform.DOKill();

        SetImageAlpha(img, 0f);
        img.rectTransform.anchoredPosition = targetPos + new Vector2(0f, 300f);

        img.DOFade(1f, 0.4f).SetUpdate(true);
        img.rectTransform.DOAnchorPos(targetPos, 0.5f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    /// <summary>
    /// 이미지를 위쪽 방향으로 오프셋(+300px)만큼 슥 밀어내며 투명화시킵니다.
    /// </summary>
    private void AnimateImageOutToTop(Image img, Vector2 startPos)
    {
        if (img == null) return;
        img.DOKill();
        img.rectTransform.DOKill();

        img.DOFade(0f, 0.4f).SetUpdate(true);
        img.rectTransform.DOAnchorPos(startPos + new Vector2(0f, 300f), 0.4f).SetEase(Ease.InCubic).SetUpdate(true);
    }

    /// <summary>
    /// 복합 GameObject(장비/스킬 예시)를 위쪽 오프셋(+300px)에서 원래 좌표로 CanvasGroup 페이드를 사용하여 동시에 하강시킵니다.
    /// </summary>
    private void AnimateObjectInFromTop(GameObject go, Vector2 targetPos)
    {
        if (go == null) return;
        go.SetActive(true);

        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.DOKill();
        rt.anchoredPosition = targetPos + new Vector2(0f, 300f);

        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.DOKill();
        cg.alpha = 0f;

        cg.DOFade(1f, 0.4f).SetUpdate(true);
        rt.DOAnchorPos(targetPos, 0.5f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    // ==========================================
    // 전투 튜토리얼용 부드러운 방향성 연출 기능들
    // ==========================================

    /// <summary>
    /// 오브젝트를 우측 오프셋(+300px)에서 좌측(원래 좌표)으로 페이드인하며 이동시킵니다.
    /// </summary>
    private void AnimateObjectInFromRight(GameObject go, Vector2 targetPos)
    {
        if (go == null) return;
        go.SetActive(true);

        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.DOKill();
        rt.anchoredPosition = targetPos + new Vector2(300f, 0f);

        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.DOKill();
        cg.alpha = 0f;

        cg.DOFade(1f, 0.4f).SetUpdate(true);
        rt.DOAnchorPos(targetPos, 0.5f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    /// <summary>
    /// 오브젝트를 우측 오프셋(+300px) 방향으로 밀어내며 페이드아웃시킵니다.
    /// </summary>
    private void AnimateObjectOutToRight(GameObject go, Vector2 startPos)
    {
        if (go == null) return;
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.DOKill();
        var cg = go.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.DOKill();
            cg.DOFade(0f, 0.4f).SetUpdate(true);
        }

        rt.DOAnchorPos(startPos + new Vector2(300f, 0f), 0.4f).SetEase(Ease.InCubic).SetUpdate(true).OnComplete(() =>
        {
            go.SetActive(false);
        });
    }

    /// <summary>
    /// 오브젝트를 아래쪽 오프셋(-300px)에서 위쪽(원래 좌표)으로 페이드인하며 이동시킵니다.
    /// </summary>
    private void AnimateObjectInFromBottom(GameObject go, Vector2 targetPos)
    {
        if (go == null) return;
        go.SetActive(true);

        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.DOKill();
        rt.anchoredPosition = targetPos - new Vector2(0f, 300f);

        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.DOKill();
        cg.alpha = 0f;

        cg.DOFade(1f, 0.4f).SetUpdate(true);
        rt.DOAnchorPos(targetPos, 0.5f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    /// <summary>
    /// 오브젝트를 아래쪽 오프셋(-300px) 방향으로 하강시키며 페이드아웃시킵니다.
    /// </summary>
    private void AnimateObjectOutToBottom(GameObject go, Vector2 startPos)
    {
        if (go == null) return;
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.DOKill();
        var cg = go.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.DOKill();
            cg.DOFade(0f, 0.4f).SetUpdate(true);
        }

        rt.DOAnchorPos(startPos - new Vector2(0f, 300f), 0.4f).SetEase(Ease.InCubic).SetUpdate(true).OnComplete(() =>
        {
            go.SetActive(false);
        });
    }

    private void OnNextButtonClicked()
    {
        _currentStepIndex++;
        string[] texts = _currentType switch
        {
            TutorialType.Item => _itemTexts,
            TutorialType.Shelter => _shelterTexts,
            TutorialType.Battle => _battleTexts,
            _ => new string[0]
        };

        if (_currentStepIndex < texts.Length)
        {
            ShowStep(_currentStepIndex);
        }
        else
        {
            CloseTutorial();
        }
    }

    private void CloseTutorial()
    {
        if (!forceShowForTesting)
        {
            SetTutorialSeen(_currentType);
        }

        // CanvasGroup을 활용해 전체 패널(텍스트, 이미지, 암전 등) 부드럽게 페이드 아웃 퇴장
        if (panelRoot != null)
        {
            var cg = panelRoot.GetComponent<CanvasGroup>();
            if (cg == null) cg = panelRoot.AddComponent<CanvasGroup>();
            cg.DOKill();
            cg.DOFade(0f, 0.35f).SetUpdate(true).OnComplete(() =>
            {
                panelRoot.SetActive(false);
                _currentType = TutorialType.None;

                var callback = _onComplete;
                _onComplete = null;
                callback?.Invoke();
            });
        }
        else
        {
            _currentType = TutorialType.None;
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }
    }

    private bool HasSeenTutorial(TutorialType type)
    {
        return PlayerPrefs.GetInt("TutorialSeen_" + type.ToString(), 0) == 1;
    }

    private void SetTutorialSeen(TutorialType type)
    {
        PlayerPrefs.SetInt("TutorialSeen_" + type.ToString(), 1);
        PlayerPrefs.Save();
    }
}
