using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TurnRPG.SkillSystem;

/// <summary>
/// 상점(Shop) UI 컨트롤러
/// 방문 시 랜덤한 아이템(강화석, 캐릭터 스킬, 배틀 아이템) 9개를 생성하여 보여줍니다.
/// </summary>
public class ShopPanelUI : MonoBehaviour
{
    [Header("UI References")]
    public Transform real_item_place; // 아이템 프리팹들이 생성될 Grid Layout 그룹 부모
    public GameObject item_pre;       // 상점 아이템 프리팹 (Image 컴포넌트 필수, "price" 텍스트 자식 필수)

    [Header("Skill Upgrade Stone")]
    public Sprite skillUpgradeIcon;
    public int skillUpgradePrice = 100;

    [Header("Character Skills")]
    public List<SkillData> allSkillsPool; // 등장할 수 있는 캐릭터 스킬 리스트
    public int skillPrice = 300;

    [Header("Battle Items")]
    public List<SkillData> allItemsPool;  // 등장할 수 있는 배틀 아이템 리스트
    public int itemPrice = 150;

    private List<GameObject> _spawnedItems = new List<GameObject>();

    private void OnEnable()
    {
        GenerateShopItems();
    }

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
                priceText.text = $"{price}원";
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

        btn.onClick.AddListener(() => OnPurchaseClicked(data, price));
    }

    /// <summary>
    /// 실제 구매 로직 (차후 연동)
    /// </summary>
    private void OnPurchaseClicked(SkillData data, int price)
    {
        string itemName = data != null ? data.SkillName : "스킬 강화석";
        Debug.Log($"[ShopPanelUI] 구매 버튼 클릭됨! 아이템: {itemName}, 요구 골드: {price}원");

        // TODO: 유저의 골드가 충분한지 확인 후 재화 차감 및 인벤토리/스킬 획득 처리
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
