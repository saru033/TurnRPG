using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TurnRPG.SkillSystem;
using DG.Tweening;

/// <summary>
/// 상점(Shop) UI 컨트롤러
/// 방문 시 랜덤한 아이템(강화석, 캐릭터 스킬, 배틀 아이템) 9개를 생성하여 보여줍니다.
/// </summary>
public class ShopPanelUI : MonoBehaviour
{
    [Header("UI References")]
    public Transform real_item_place; // 아이템 프리팹들이 생성될 Grid Layout 그룹 부모
    public GameObject item_pre;       // 상점 아이템 프리팹 (Image 컴포넌트 필수, "price" 텍스트 자식 필수)
    public SkillUpgradeUI skillUpgradeUI; // [추가] 스킬 강화 UI 참조

    [Header("Skill Upgrade Stone")]
    public Sprite skillUpgradeIcon;
    public int skillUpgradePrice = 100;

    [Header("Character Skills")]
    public List<SkillData> allSkillsPool; // 등장할 수 있는 캐릭터 스킬 리스트
    public int skillPrice = 300;

    [Header("Battle Items")]
    public List<SkillData> allItemsPool;  // 등장할 수 있는 배틀 아이템 리스트
    public int itemPrice = 150;

    [Header("Skill Exchange UI")]
    public GameObject exchangePanel;
    public Image charIllustration;
    public SkillTooltipUI currentSkillTooltip;
    public SkillTooltipUI newSkillTooltip;

    [Header("Inventory UI")]
    public GameObject inventoryPanel;
    public GameObject[] inventorySlots; // 3 slots

    [Header("Discard Confirmation UI")]
    public GameObject discardPanel;
    public Button discardYesBtn;
    public Button discardNoBtn;

    private List<GameObject> _spawnedItems = new List<GameObject>();
    private Vector2 _originalIllustrationPos;
    private bool _isExchangeInitialized = false;


    /// <summary>
    /// 상점을 열 때 랜덤하게 아이템 목록을 갱신하여 프리팹을 생성합니다.
    /// </summary>
    public void GenerateShopItems()
    {
        // 1. 기존 생성된 아이템 모두 지우기
        foreach (var obj in _spawnedItems)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedItems.Clear();

        if (item_pre == null || real_item_place == null)
        {
            Debug.LogError("[ShopPanelUI] 아이템 프리팹이나 부모 트랜스폼이 할당되지 않았습니다.");
            return;
        }

        // 2. 스킬 강화석 3개 (고정 등장)
        for (int i = 0; i < 3; i++)
        {
            CreateShopItem(skillUpgradeIcon, skillUpgradePrice, null);
        }

        // 3. 캐릭터 스킬 랜덤 3개 (보유 중인 스킬 제외)
        List<SkillData> pickedSkills = PickRandom(allSkillsPool, 3, true);
        foreach (var skill in pickedSkills)
        {
            CreateShopItem(skill.SkillIcon, skillPrice, skill);
        }

        // 4. 배틀 아이템 랜덤 3개
        List<SkillData> pickedItems = PickRandom(allItemsPool, 3);
        foreach (var item in pickedItems)
        {
            CreateShopItem(item.SkillIcon, itemPrice, item);
        }
        // 5. 인벤토리 UI 갱신
        RefreshInventoryUI();
    }

    /// <summary>
    /// 개별 상점 아이템 프리팹을 생성하고 데이터를 세팅합니다.
    /// </summary>
    private void CreateShopItem(Sprite icon, int price, SkillData data)
    {
        GameObject obj = Instantiate(item_pre, real_item_place);
        _spawnedItems.Add(obj);

        // 1. 이미지 할당 (프리팹 최상단이 Image라고 가정)
        Image img = obj.GetComponent<Image>();
        if (img != null && icon != null)
        {
            img.sprite = icon;
        }

        // 2. 가격 할당 ("price"라는 이름의 자식 오브젝트의 TMP 검색)
        Transform priceObj = obj.transform.Find("price");
        if (priceObj != null)
        {
            TextMeshProUGUI priceText = priceObj.GetComponent<TextMeshProUGUI>();
            if (priceText != null)
            {
                priceText.text = $"      = {price}";
            }
        }
        else
        {
            Debug.LogWarning("[ShopPanelUI] item_pre 프리팹에 'price'라는 이름의 자식이 없습니다!");
        }

        // 3. 툴팁 할당 (꾹 누르면 정보 표시)
        SkillTooltipTrigger tooltip = obj.GetComponent<SkillTooltipTrigger>();
        if (tooltip != null)
        {
            if (data != null)
            {
                tooltip.Init(data, 1); // 상점에서는 기본적으로 1레벨 기준의 정보를 보여줌
            }
            else
            {
                // 강화석 등 SkillData가 없는 경우에는 툴팁 작동 중지
                tooltip.enabled = false;
            }
        }

        // 4. 구매 버튼 이벤트 할당
        Button btn = obj.GetComponent<Button>();
        if (btn == null) btn = obj.AddComponent<Button>();

        btn.onClick.AddListener(() => 
        {
            if (tooltip != null && tooltip.WasLongPressed) return;
            OnPurchaseClicked(data, price, obj);
        });
    }

    /// <summary>
    /// 실제 구매 로직
    /// </summary>
    private void OnPurchaseClicked(SkillData data, int price, GameObject itemObj)
    {
        if (GameManager.Instance == null) return;

        // 1. 소지금 확인
        if (GameManager.Instance.gold < price)
        {
            Debug.Log("[Shop] 소지 금이 부족합니다!");
            return;
        }

        bool purchaseSuccess = false;

        // 2. 아이템 종류별 처리
        if (data == null)
        {
            // [스킬 강화석]
            GameManager.Instance.gold -= price;
            GameManager.Instance.skillup++;

            // UI 반영
            if (LobbyTopUI.Instance != null) LobbyTopUI.Instance.Refresh();
            
            // 스킬 강화 패널 열기
            if (skillUpgradeUI != null)
            {
                skillUpgradeUI.Open();
            }

            Debug.Log($"[Shop] 스킬 강화석 구매 완료! 현재 보유량: {GameManager.Instance.skillup}");
            purchaseSuccess = true;
        }
        else if (data.Type != SkillType.Item)
        {
            // [캐릭터 스킬 교체]
            OpenSkillExchangePanel(data, price, itemObj);
        }
        else
        {
            // [배틀 아이템 구매]
            if (GameManager.Instance.playerItems.Count >= 3)
            {
                Debug.Log("[Shop] 가방이 가득 찼습니다!");
                return;
            }

            GameManager.Instance.gold -= price;
            GameManager.Instance.playerItems.Add(data);

            // UI 반영
            if (LobbyTopUI.Instance != null) LobbyTopUI.Instance.Refresh();
            RefreshInventoryUI();

            Debug.Log($"[Shop] 배틀 아이템 구매 완료: {data.SkillName}");
            purchaseSuccess = true;
        }

        // 3. 구매 성공 시 품절 처리
        if (purchaseSuccess)
        {
            SetItemSoldOut(itemObj);
        }
    }

    /// <summary>
    /// 스킬 교체 확인 패널을 엽니다.
    /// </summary>
    private void OpenSkillExchangePanel(SkillData newSkill, int price, GameObject itemObj)
    {
        if (exchangePanel == null) return;

        // 0. 초기 위치 저장 및 초기화
        if (!_isExchangeInitialized && charIllustration != null)
        {
            _originalIllustrationPos = charIllustration.rectTransform.anchoredPosition;
            _isExchangeInitialized = true;
        }

        // 1. 해당 스킬을 장착할 수 있는 캐릭터 찾기
        PlayerCharacterState targetChar = null;
        foreach (var charState in GameManager.Instance.party)
        {
            if (charState != null && (charState.template.charType & newSkill.EquipRestriction) != 0)
            {
                targetChar = charState;
                break;
            }
        }

        if (targetChar == null)
        {
            Debug.LogWarning($"[Shop] {newSkill.SkillName}을(를) 장착할 수 있는 캐릭터가 파티에 없습니다.");
            return;
        }

        // 2. 현재 장착 중인 동일 슬롯의 스킬 정보 가져오기
        int slotIdx = (int)newSkill.SlotIndex;
        SkillData currentSkill = targetChar.equippedSkills[slotIdx];
        int currentLevel = targetChar.skillLevels[slotIdx];

        // 3. UI 세팅 및 연출
        if (charIllustration != null)
        {
            charIllustration.sprite = targetChar.template.illustration;
            
            // [DOTween] 일러스트 아래에서 위로 슬라이드
            charIllustration.rectTransform.DOKill();
            charIllustration.rectTransform.anchoredPosition = new Vector2(_originalIllustrationPos.x, _originalIllustrationPos.y - 300f);
            charIllustration.rectTransform.DOAnchorPos(_originalIllustrationPos, 0.4f).SetEase(Ease.OutCubic);
        }
        
        // [DOTween] 패널 페이드 인
        CanvasGroup cg = exchangePanel.GetComponent<CanvasGroup>();
        if (cg == null) cg = exchangePanel.AddComponent<CanvasGroup>();
        cg.DOKill();
        cg.alpha = 0;
        cg.DOFade(1f, 0.4f);

        if (currentSkillTooltip != null)
        {
            currentSkillTooltip.gameObject.SetActive(currentSkill != null);
            if (currentSkill != null) currentSkillTooltip.SetData(currentSkill, currentLevel);
        }

        if (newSkillTooltip != null)
        {
            newSkillTooltip.SetData(newSkill, 1);
            
            // 버튼 이벤트 설정 (교체할 스킬 툴팁에 버튼이 있다고 가정)
            Button btn = newSkillTooltip.GetComponent<Button>();
            if (btn == null) btn = newSkillTooltip.gameObject.AddComponent<Button>();
            
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => ExecuteSkillExchange(targetChar, newSkill, price, itemObj));
        }

        exchangePanel.SetActive(true);
    }

    /// <summary>
    /// 스킬 교체 패널을 닫습니다. (배경 클릭 등)
    /// </summary>
    public void CloseExchangePanel()
    {
        if (exchangePanel == null) return;

        // [DOTween] 퇴장 연출
        CanvasGroup cg = exchangePanel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.DOKill();
            cg.DOFade(0f, 0.3f);
        }

        if (charIllustration != null)
        {
            charIllustration.rectTransform.DOKill();
            charIllustration.rectTransform.DOAnchorPos(new Vector2(_originalIllustrationPos.x, _originalIllustrationPos.y - 300f), 0.3f)
                .SetEase(Ease.InCubic)
                .OnComplete(() => exchangePanel.SetActive(false));
        }
        else
        {
            exchangePanel.SetActive(false);
        }
    }

    /// <summary>
    /// 실제 스킬 교체를 실행합니다.
    /// </summary>
    private void ExecuteSkillExchange(PlayerCharacterState targetChar, SkillData newSkill, int price, GameObject itemObj)
    {
        if (GameManager.Instance.gold < price)
        {
            Debug.Log("[Shop] 소지금이 부족합니다!");
            return;
        }

        // 1. 재화 차감
        GameManager.Instance.gold -= price;

        // 2. 기존 스킬 강화석 환급 로직 (분기점 기준)
        int slotIdx = (int)newSkill.SlotIndex;
        int oldLevel = targetChar.skillLevels[slotIdx];
        int refund = 0;

        if (oldLevel > 1)
        {
            int branch = newSkill.GetBranchLevel();
            // 분기점(branch) 레벨 이하일 때는 레벨-1만큼, 분기점 초과(강화 경로 선택 후)일 때는 분기점만큼 환급
            if (oldLevel <= branch)
            {
                refund = oldLevel - 1;
            }
            else
            {
                refund = branch;
            }

            GameManager.Instance.skillup += refund;
            Debug.Log($"[Shop] 기존 스킬 환급: 강화석 {refund}개 획득! (레벨:{oldLevel}, 분기:{branch})");
        }

        // 3. 스킬 교체 및 레벨 초기화
        targetChar.equippedSkills[slotIdx] = newSkill;
        targetChar.skillLevels[slotIdx] = 1;

        // 4. 결과 반영
        if (LobbyTopUI.Instance != null) LobbyTopUI.Instance.Refresh();
        
        // 5. 즉시 강화창 열기 (환급받은 강화석이 있을 때만)
        if (refund > 0 && skillUpgradeUI != null)
        {
            skillUpgradeUI.Open();
        }

        // 6. UI 정리
        SetItemSoldOut(itemObj);
        CloseExchangePanel();

        Debug.Log($"[Shop] {targetChar.template.CharacterName}의 {slotIdx + 1}번 스킬을 {newSkill.SkillName}(으)로 교체 완료!");
    }


    /// <summary>
    /// 인벤토리(가방) UI를 최신화합니다.
    /// </summary>
    public void RefreshInventoryUI()
    {
        if (inventorySlots == null || GameManager.Instance == null) return;

        var items = GameManager.Instance.playerItems;
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (i < items.Count)
            {
                inventorySlots[i].SetActive(true);
                SkillData item = items[i];
                
                // 1. 아이콘 설정
                Image img = inventorySlots[i].GetComponent<Image>();
                if (img == null) img = inventorySlots[i].GetComponentInChildren<Image>();
                if (img != null) img.sprite = item.SkillIcon;

                // 2. 툴팁 설정
                SkillTooltipTrigger trigger = inventorySlots[i].GetComponent<SkillTooltipTrigger>();
                if (trigger == null) trigger = inventorySlots[i].GetComponentInChildren<SkillTooltipTrigger>();
                if (trigger != null) trigger.Init(item, 1);

                // 3. 판매 버튼 설정 (클릭 시 50원에 판매)
                Button btn = inventorySlots[i].GetComponent<Button>();
                if (btn == null) btn = inventorySlots[i].GetComponentInChildren<Button>();
                
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => 
                    {
                        if (trigger != null && trigger.WasLongPressed) return;
                        OpenDiscardPanel(item);
                    });
                }
            }
            else
            {
                // 아이템이 없는 슬롯은 비활성화 (혹은 빈 슬롯 연출 가능)
                inventorySlots[i].SetActive(false);
            }
        }
    }

    /// <summary>
    /// 아이템을 버릴지 확인하는 패널을 엽니다.
    /// </summary>
    private void OpenDiscardPanel(SkillData item)
    {
        if (discardPanel == null)
        {
            Debug.LogWarning("[Shop] discardPanel이 할당되지 않았습니다!");
            return;
        }

        Debug.Log($"[Shop] 버리기 확인창 활성화: {item.SkillName}");

        discardYesBtn.onClick.RemoveAllListeners();
        discardYesBtn.onClick.AddListener(() => ExecuteDiscard(item));

        discardNoBtn.onClick.RemoveAllListeners();
        discardNoBtn.onClick.AddListener(() => discardPanel.SetActive(false));

        discardPanel.SetActive(true);
    }

    /// <summary>
    /// 아이템을 실제로 버립니다. (재화 환급 없음)
    /// </summary>
    private void ExecuteDiscard(SkillData item)
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.playerItems.Remove(item);
        RefreshInventoryUI();
        
        if (discardPanel != null) discardPanel.SetActive(false);

        Debug.Log($"[Shop] 아이템 버리기 완료: {item.SkillName}");
    }

    /// <summary>
    /// 아이템을 품절 상태로 표시하고 추가 구매를 방지합니다.
    /// </summary>
    private void SetItemSoldOut(GameObject itemObj)
    {
        if (itemObj == null) return;

        // 1. 가격 텍스트를 "품절!"로 변경
        Transform priceObj = itemObj.transform.Find("price");
        if (priceObj != null)
        {
            TextMeshProUGUI priceText = priceObj.GetComponent<TextMeshProUGUI>();
            if (priceText != null)
            {
                priceText.text = "      품절!";
            }
        }

        // 2. 버튼 비활성화 (추가 구매 방지)
        Button btn = itemObj.GetComponent<Button>();
        if (btn != null)
        {
            btn.interactable = false;
        }

        // 3. 시각적 피드백 (이미지 어둡게 처리)
        Image img = itemObj.GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        }
    }

    /// <summary>
    /// 주어진 풀에서 중복되지 않게 무작위로 일정 개수를 뽑습니다.
    /// filterOwnedSkills가 true일 경우, 현재 파티가 이미 장착 중인 스킬은 리스트에서 제외합니다.
    /// </summary>
    private List<SkillData> PickRandom(List<SkillData> pool, int count, bool filterOwnedSkills = false)
    {
        if (pool == null || pool.Count == 0) return new List<SkillData>();

        // 원본 리스트를 복사해서 뽑힌 요소는 지우는 방식(중복 방지)
        List<SkillData> copy = new List<SkillData>(pool);

        // [추가] 이미 보유한 스킬 필터링
        if (filterOwnedSkills && GameManager.Instance != null)
        {
            HashSet<string> ownedSkillIDs = new HashSet<string>();
            foreach (var charState in GameManager.Instance.party)
            {
                if (charState != null && charState.equippedSkills != null)
                {
                    foreach (var skill in charState.equippedSkills)
                    {
                        if (skill != null && !string.IsNullOrEmpty(skill.SkillID))
                        {
                            ownedSkillIDs.Add(skill.SkillID);
                        }
                    }
                }
            }

            copy.RemoveAll(s => s != null && ownedSkillIDs.Contains(s.SkillID));
        }

        List<SkillData> result = new List<SkillData>();

        for (int i = 0; i < count; i++)
        {
            if (copy.Count == 0) break; // 풀이 모자라면 있는 것까지만 뽑음
            int rand = Random.Range(0, copy.Count);
            result.Add(copy[rand]);
            copy.RemoveAt(rand);
        }

        return result;
    }
}
