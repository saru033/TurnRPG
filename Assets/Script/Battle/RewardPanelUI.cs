using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TurnRPG.SkillSystem;

/// <summary>
/// 전투 승리 후 획득한 보상(골드, 강화석, 아이템)을 보여주는 패널을 관리합니다.
/// </summary>
public class RewardPanelUI : MonoBehaviour
{
    [Header("UI Groups (수량이 0이면 비활성화됨)")]
    public GameObject goldGroup;
    public GameObject skillUpGroup;
    public GameObject itemGroup;
    public GameObject rewardBagGroup; // [추가] 보상용 가방 UI 부모 오브젝트

    [Header("Texts")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI skillUpText;
    public GameObject warningPanel; // [수정] 보관하지 않은 아이템 경고 패널 (GameObject)

    [Header("Item Icons")]
    public RectTransform itemSnapshotContainer;
    public GameObject[] bagSlots; // [수정] 고정된 3개의 가방 슬롯 오브젝트
    public GameObject itemIconPrefab;

    [Header("Buttons")]
    public Button confirmButton;

    private System.Action _onConfirm;
    private List<SkillData> _droppedItems = new List<SkillData>(); // [추가] 현재 보상 영역의 아이템들
    private bool _hasShownBagWarning = false; // [추가] 가방 공간 경고 체크용

    private void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(HandleConfirm);
    }

    /// <summary>
    /// 보상 패널을 설정하고 활성화합니다.
    /// </summary>
    public void Setup(int gold, int skillUp, List<SkillData> items, System.Action onConfirm)
    {
        _onConfirm = onConfirm;
        gameObject.SetActive(true);
        _hasShownBagWarning = false;
        if (warningPanel != null) warningPanel.SetActive(false); // [추가] 초기화

        // 보상 아이템 리스트 초기 할당 (미표시 버그 수정)
        _droppedItems = (items != null) ? new List<SkillData>(items) : new List<SkillData>();

        Debug.Log($"[RewardPanel] Setup 호출됨. 보상 아이템 개수: {_droppedItems.Count}");

        // 보상용 가방 전용 UI 활성화
        if (rewardBagGroup != null) rewardBagGroup.SetActive(true);

        // 1. 골드 표시
        if (goldGroup != null)
        {
            goldGroup.SetActive(gold > 0);
            if (goldText != null) goldText.text = gold.ToString();
        }

        // 2. 강화석 표시
        if (skillUpGroup != null)
        {
            skillUpGroup.SetActive(skillUp > 0);
            if (skillUpText != null) skillUpText.text = skillUp.ToString();
        }

        // 3. 배틀 아이템 및 가방 UI 갱신
        RefreshUI();
    }

    /// <summary>
    /// 보상 영역과 가방 영역의 UI를 모두 다시 그립니다.
    /// </summary>
    public void RefreshUI()
    {
        // 상황이 변했으므로 경고 패널은 우선 숨김
        if (warningPanel != null) warningPanel.SetActive(false);
        _hasShownBagWarning = false;

        RefreshDrops();
        RefreshBag();
    }

    private void RefreshDrops()
    {
        if (itemGroup == null || itemSnapshotContainer == null) return;

        // 보상템이 있거나 가방에 템이 있을 때 활성화 (유동적)
        bool hasAnyItem = _droppedItems.Count > 0 || (GameManager.Instance != null && GameManager.Instance.playerItems.Count > 0);
        itemGroup.SetActive(hasAnyItem);

        foreach (Transform child in itemSnapshotContainer)
            Destroy(child.gameObject);

        foreach (var item in _droppedItems)
        {
            if (item == null || itemIconPrefab == null) continue;

            // RectTransform 설정이 프리팹에 따라 달라질 수 있으므로 두 번째 인자로 부모 지정
            var go = Instantiate(itemIconPrefab, itemSnapshotContainer);
            go.transform.localScale = Vector3.one; // 스케일 깨짐 방지
            SetupIcon(go, item, true);
        }
    }

    private void RefreshBag()
    {
        if (bagSlots == null || bagSlots.Length == 0) return;

        int currentItemCount = (GameManager.Instance != null) ? GameManager.Instance.playerItems.Count : 0;

        for (int i = 0; i < bagSlots.Length; i++)
        {
            if (i < currentItemCount)
            {
                bagSlots[i].SetActive(true);
                var item = GameManager.Instance.playerItems[i];
                SetupIcon(bagSlots[i], item, false);
            }
            else
            {
                bagSlots[i].SetActive(false); // 아이템이 없으면 슬롯 비활성화
            }
        }
    }

    /// <summary>
    /// 아이콘 프리팹의 이미지, 툴팁, 클릭 이벤트를 설정합니다.
    /// </summary>
    private void SetupIcon(GameObject go, SkillData item, bool isFromDrops)
    {
        // 이미지 설정
        var img = go.GetComponent<Image>() ?? go.GetComponentInChildren<Image>();
        if (img != null) img.sprite = item.SkillIcon;

        // 툴팁 설정
        var trigger = go.GetComponent<SkillTooltipTrigger>();
        if (trigger != null) trigger.Init(item, 1);

        // 클릭 이벤트 설정 (넣기/빼기)
        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => HandleIconClick(item, isFromDrops, trigger));
        }
    }

    private void HandleIconClick(SkillData item, bool isFromDrops, SkillTooltipTrigger trigger)
    {
        // [중요] 롱프레스 중이었다면 이동하지 않음
        if (trigger != null && trigger.WasLongPressed)
        {
            Debug.Log("[RewardPanel] 롱프레스 감지: 아이템 이동을 취소합니다.");
            return;
        }

        if (isFromDrops)
            AddToBag(item);
        else
            RemoveFromBag(item);
    }

    private void AddToBag(SkillData item)
    {
        if (GameManager.Instance == null) return;

        // 가방은 최대 3개
        if (GameManager.Instance.playerItems.Count >= 3)
        {
            Debug.LogWarning("[RewardPanel] 가방이 가득 찼습니다! 아이템을 먼저 버려주세요.");
            return;
        }

        GameManager.Instance.playerItems.Add(item);
        _droppedItems.Remove(item);
        RefreshUI();
    }

    private void RemoveFromBag(SkillData item)
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.playerItems.Remove(item);
        _droppedItems.Add(item);
        RefreshUI();
    }

    private void HandleConfirm()
    {
        // 1. 이미 경고 패널이 떠있는 상태에서 다시 누르면 즉시 종료
        if (warningPanel != null && warningPanel.activeSelf)
        {
            ClosePanel();
            return;
        }

        // 2. 가방에 자리가 있고 보상 아이템이 남아있으면 경고 패널 표시
        if (!_hasShownBagWarning && _droppedItems.Count > 0 && GameManager.Instance != null && GameManager.Instance.playerItems.Count < 3)
        {
            if (warningPanel != null)
            {
                warningPanel.SetActive(true);
            }
            Debug.Log("[RewardPanel] 가져갈 수 있는 아이템이 남았습니다. 다시 한 번 누르면 종료합니다.");
            _hasShownBagWarning = true;
            return;
        }

        ClosePanel();
    }

    /// <summary>
    /// 외부 클릭 시 경고 패널을 숨기기 위해 호출하는 공용 메서드입니다.
    /// </summary>
    public void HideWarning()
    {
        if (warningPanel != null) warningPanel.SetActive(false);
        _hasShownBagWarning = false;
    }

    private void ClosePanel()
    {
        // 보상용 가방 UI 비활성화
        if (rewardBagGroup != null) rewardBagGroup.SetActive(false);
        if (warningPanel != null) warningPanel.SetActive(false);

        gameObject.SetActive(false);
        BattleManager.Instance.OnRewardConfirmed();
    }
}
