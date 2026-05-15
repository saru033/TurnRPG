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
    [Header("Resource Reward Slots (Dynamic)")]
    public GameObject[] resourceSlots; // 3개의 슬롯 (각 슬롯은 Button이며, 자식으로 Image와 Text를 가짐)

    [Header("Resource Icons")]
    public Sprite goldIcon;
    public Sprite skillUpIcon;
    public Sprite rerollIcon;
    public Sprite highRerollIcon;

    public GameObject rewardBagGroup; // [추가] 보상용 가방 UI 부모 오브젝트
    public GameObject itemGroup;      // [복구] 배틀 아이템 영역 그룹

    [Header("Texts/Panels")]
    public GameObject warningPanel; // 보관하지 않은 아이템 경고 패널
    public GameObject resourceWarningPanel; // [추가] 획득하지 않은 재화(돈/강화석) 경고 패널

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
    public void Setup(int gold, int skillUp, int rerollCount, int highRerollCount, List<SkillData> items, System.Action onConfirm)
    {
        _onConfirm = onConfirm;
        gameObject.SetActive(true);
        if (warningPanel != null) warningPanel.SetActive(false);
        if (resourceWarningPanel != null) resourceWarningPanel.SetActive(false);
        _hasShownBagWarning = false;

        _droppedItems = (items != null) ? new List<SkillData>(items) : new List<SkillData>();

        // 1. 재화형 보상 데이터 리스트 구성 (위에서부터 채우기 위함)
        var rewards = new List<ResourceRewardData>();
        if (gold > 0) rewards.Add(new ResourceRewardData { Icon = goldIcon, Amount = gold, Type = ResourceType.Gold });
        if (skillUp > 0) rewards.Add(new ResourceRewardData { Icon = skillUpIcon, Amount = skillUp, Type = ResourceType.SkillUp });
        if (rerollCount > 0) rewards.Add(new ResourceRewardData { Icon = rerollIcon, Amount = rerollCount, Type = ResourceType.Reroll });
        if (highRerollCount > 0) rewards.Add(new ResourceRewardData { Icon = highRerollIcon, Amount = highRerollCount, Type = ResourceType.HighReroll });

        // 2. 슬롯 배치 (최대 3개까지 지원)
        for (int i = 0; i < resourceSlots.Length; i++)
        {
            if (i < rewards.Count)
            {
                resourceSlots[i].SetActive(true);
                var data = rewards[i];

                // 자식 오브젝트에서 컴포넌트 자동 찾기
                var amountText = resourceSlots[i].GetComponentInChildren<TextMeshProUGUI>();
                var btn = resourceSlots[i].GetComponent<Button>();

                // [수정] 슬롯 자체의 Image(버튼 배경)가 아닌 자식의 Image(아이콘)를 찾음
                Image iconImage = null;
                var allImages = resourceSlots[i].GetComponentsInChildren<Image>();
                foreach (var img in allImages)
                {
                    if (img.gameObject != resourceSlots[i]) // 루트가 아닌 첫 번째 자식 이미지 선택
                    {
                        iconImage = img;
                        break;
                    }
                }

                if (iconImage != null) iconImage.sprite = data.Icon;
                if (amountText != null) amountText.text = data.Amount.ToString("N0");

                // 클릭 리스너 설정
                int slotIdx = i; // 클로저용
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnResourceClicked(slotIdx, data));
                }
            }
            else
            {
                resourceSlots[i].SetActive(false);
            }
        }

        if (rewardBagGroup != null) rewardBagGroup.SetActive(true);
        RefreshUI();
    }

    private enum ResourceType { Gold, SkillUp, Reroll, HighReroll }
    private struct ResourceRewardData
    {
        public Sprite Icon;
        public int Amount;
        public ResourceType Type;
    }

    private void OnResourceClicked(int slotIdx, ResourceRewardData data)
    {
        if (GameManager.Instance == null) return;

        switch (data.Type)
        {
            case ResourceType.Gold:
                GameManager.Instance.gold += data.Amount;
                break;
            case ResourceType.SkillUp:
                GameManager.Instance.skillup += data.Amount;
                if (BattleManager.Instance != null && BattleManager.Instance.battleUI != null && BattleManager.Instance.battleUI.skillUpgradeUI != null)
                {
                    BattleManager.Instance.battleUI.skillUpgradeUI.Open();
                }
                break;
            case ResourceType.Reroll:
                GameManager.Instance.rerollItemCount += data.Amount;
                if (BattleManager.Instance != null && BattleManager.Instance.battleUI != null && BattleManager.Instance.battleUI.rerollUI != null)
                {
                    BattleManager.Instance.battleUI.rerollUI.isHighTier = false; // 일반 리롤
                    BattleManager.Instance.battleUI.rerollUI.Open();
                }
                break;
            case ResourceType.HighReroll:
                GameManager.Instance.highRerollItemCount += data.Amount;
                if (BattleManager.Instance != null && BattleManager.Instance.battleUI != null && BattleManager.Instance.battleUI.highRerollUI != null)
                {
                    BattleManager.Instance.battleUI.highRerollUI.isHighTier = true; // 고급 리롤 설정 보장
                    BattleManager.Instance.battleUI.highRerollUI.Open();
                }
                break;
        }

        Debug.Log($"[RewardPanel] Resource Clicked: {data.Type}, New Count: {(data.Type == ResourceType.Reroll ? GameManager.Instance.rerollItemCount : GameManager.Instance.highRerollItemCount)}");

        if (LobbyTopUI.Instance != null) LobbyTopUI.Instance.Refresh();

        resourceSlots[slotIdx].SetActive(false);
        resourceWarningPanel.SetActive(false);
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
        // 0. 재화(골드, 강화석, 리롤템)를 획득하지 않은 상태면 경고 패널 표시 후 중단
        bool hasUncollectedResources = false;
        foreach (var slot in resourceSlots)
        {
            if (slot != null && slot.activeSelf)
            {
                hasUncollectedResources = true;
                break;
            }
        }
        if (hasUncollectedResources)
        {
            if (resourceWarningPanel != null) resourceWarningPanel.SetActive(true);
            return;
        }

        // 1. 이미 아이템 경고 패널이 떠있는 상태에서 다시 누르면 즉시 종료
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
        if (resourceWarningPanel != null) resourceWarningPanel.SetActive(false);
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
