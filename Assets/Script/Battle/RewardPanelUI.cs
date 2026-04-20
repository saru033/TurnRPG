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

    [Header("Texts")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI skillUpText;

    [Header("Item Icons")]
    public RectTransform itemSnapshotContainer;
    public GameObject itemIconPrefab; // 아이템 이미지를 보여줄 프리팹 (Image 컴포넌트 포함)

    [Header("Buttons")]
    public Button confirmButton;

    private System.Action _onConfirm;

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

        // 3. 배틀 아이템 표시
        if (itemGroup != null && itemSnapshotContainer != null)
        {
            itemGroup.SetActive(items != null && items.Count > 0);

            // 기존 아이콘 제거
            foreach (Transform child in itemSnapshotContainer)
                Destroy(child.gameObject);

            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null || itemIconPrefab == null) continue;

                    // [중요] 씬에 이미 배치된 인스턴스를 사용해야 하며, 
                    // itemSnapshotContainer가 프로젝트 창의 프리팹이 아닌지 확인이 필요합니다.
                    var go = Instantiate(itemIconPrefab);
                    if (go != null)
                    {
                        go.transform.SetParent(itemSnapshotContainer, false);
                        var img = go.GetComponent<Image>() ?? go.GetComponentInChildren<Image>();
                        if (img != null) img.sprite = item.SkillIcon;
                    }
                }
            }
        }
    }

    private void HandleConfirm()
    {
        gameObject.SetActive(false);
        BattleManager.Instance.OnRewardConfirmed();
    }
}
