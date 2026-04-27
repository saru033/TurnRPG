using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using TurnRPG.SkillSystem;
using DG.Tweening;

public class SkillUpgradeUI : MonoBehaviour
{
    [System.Serializable]
    public class CharSlot
    {
        public GameObject root;
        public Image portrait;
        public Button[] skillButtons; // s1, s2, s3 순서
    }

    [Header("Main Panels")]
    public GameObject skillUpPanel;
    public GameObject offPanel;
    public Image offPanelIllustration; // [추가] 상세창 배경 캐릭터 일러스트

    [Header("Character Slots")]
    public CharSlot[] characterSlots = new CharSlot[3];

    [Header("Detail Display (Fixed Objects)")]
    // 프리팹 생성이 아닌 미리 배치된 오브젝트를 사용합니다.
    public SkillTooltipUI currentSkillDetail;
    public SkillTooltipUI singleUpgradeDetail;
    public SkillTooltipUI branchUpgradeDetailA;
    public SkillTooltipUI branchUpgradeDetailB;

    public System.Action OnClose; // UI가 완전히 닫힐 때 실행될 콜백

    private Vector2 _originalIllustrationPos; // [추가] 일러스트의 원본 위치 저장용

    private void Start()
    {
        // 초기에는 디테일 창들 비활성화
        HideAllDetails();

        // 일러스트 원본 위치 저장
        if (offPanelIllustration != null)
        {
            _originalIllustrationPos = offPanelIllustration.GetComponent<RectTransform>().anchoredPosition;
        }

        if (offPanel != null)
        {
            // 배경 클릭 시 상세창 닫기
            var btn = offPanel.GetComponent<Button>();
            if (btn == null) btn = offPanel.AddComponent<Button>();
            btn.onClick.AddListener(CloseDetail);
        }
    }

    private void HideAllDetails()
    {
        if (currentSkillDetail != null) currentSkillDetail.gameObject.SetActive(false);
        if (singleUpgradeDetail != null) singleUpgradeDetail.gameObject.SetActive(false);
        if (branchUpgradeDetailA != null) branchUpgradeDetailA.gameObject.SetActive(false);
        if (branchUpgradeDetailB != null) branchUpgradeDetailB.gameObject.SetActive(false);
    }

    /// <summary>
    /// 스킬 강화 UI를 엽니다.
    /// </summary>
    public void Open()
    {
        if (skillUpPanel == null) return;

        skillUpPanel.SetActive(true);
        if (offPanel != null) offPanel.SetActive(false);

        RefreshCharacterList();
    }

    /// <summary>
    /// 캐릭터 리스트 정보를 갱신합니다.
    /// </summary>
    public void RefreshCharacterList()
    {
        if (GameManager.Instance == null) return;

        var party = GameManager.Instance.party;
        for (int i = 0; i < 3; i++)
        {
            if (i >= party.Length || party[i] == null || party[i].template == null)
            {
                if (characterSlots[i].root != null) characterSlots[i].root.SetActive(false);
                continue;
            }

            var charState = party[i];
            var slot = characterSlots[i];

            if (slot.root != null) slot.root.SetActive(true);
            if (slot.portrait != null) slot.portrait.sprite = charState.template.illustration;

            // 스킬 버튼 설정
            for (int s = 0; s < 3; s++)
            {
                if (s >= charState.equippedSkills.Count || charState.equippedSkills[s] == null)
                {
                    if (slot.skillButtons[s] != null) slot.skillButtons[s].gameObject.SetActive(false);
                    continue;
                }

                var skill = charState.equippedSkills[s];
                var btn = slot.skillButtons[s];

                if (btn != null)
                {
                    btn.gameObject.SetActive(true);
                    var img = btn.GetComponent<Image>();
                    if (img != null) img.sprite = skill.SkillIcon;

                    // 클릭 이벤트
                    int charIdx = i;
                    int skillIdx = s;
                    btn.onClick.RemoveAllListeners();

                    // 강화석이 없거나 이미 최대 레벨인 경우 시각적 피드백(예: 어둡게)은 나중에 추가 가능
                    btn.onClick.AddListener(() => OnSkillSelected(charIdx, skillIdx));
                }
            }
        }
    }

    private void OnSkillSelected(int charIdx, int skillIdx)
    {
        if (GameManager.Instance == null || GameManager.Instance.skillup <= 0)
        {
            Debug.Log("강화석이 부족합니다.");
            return;
        }

        var charState = GameManager.Instance.party[charIdx];
        var skill = charState.equippedSkills[skillIdx];
        int currentLevel = charState.skillLevels[skillIdx];

        if (offPanel != null)
        {
            offPanel.SetActive(true);

            // [DOTween] 배경 페이드 인 (alpha: 0 -> 245/255)
            var bgImg = offPanel.GetComponent<Image>();
            if (bgImg != null)
            {
                Color targetColor = bgImg.color;
                bgImg.color = new Color(targetColor.r, targetColor.g, targetColor.b, 0);
                bgImg.DOFade(245f / 255f, 0.3f);
            }

            // [DOTween] 캐릭터 일러스트 아래에서 위로 이동
            if (offPanelIllustration != null)
            {
                offPanelIllustration.sprite = charState.template.illustration;
                var rt = offPanelIllustration.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(_originalIllustrationPos.x, _originalIllustrationPos.y - 300f);
                    rt.DOAnchorPos(_originalIllustrationPos, 0.3f).SetEase(Ease.OutCubic);
                }
            }
        }

        HideAllDetails();

        // 1. 현재 스킬 정보 표시
        if (currentSkillDetail != null)
        {
            AnimateDetailIn(currentSkillDetail.gameObject);
            currentSkillDetail.SetData(skill, currentLevel);
        }

        // 2. 강화 정보 표시
        if (!skill.IsMaxLevel(currentLevel))
        {
            int branchLevel = skill.GetBranchLevel();
            if (currentLevel == branchLevel)
            {
                SetupUpgradeDetail(branchUpgradeDetailA, skill, currentLevel + 1, charIdx, skillIdx);
                SetupUpgradeDetail(branchUpgradeDetailB, skill, currentLevel + 2, charIdx, skillIdx);
                AnimateDetailIn(branchUpgradeDetailA.gameObject);
                AnimateDetailIn(branchUpgradeDetailB.gameObject);
            }
            else
            {
                SetupUpgradeDetail(singleUpgradeDetail, skill, currentLevel + 1, charIdx, skillIdx);
                AnimateDetailIn(singleUpgradeDetail.gameObject);
            }
        }
    }

    /// <summary>
    /// 상세 정보창이 페이드 인 되도록 연출합니다.
    /// </summary>
    private void AnimateDetailIn(GameObject go)
    {
        if (go == null) return;
        go.SetActive(true);

        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();

        cg.alpha = 0;
        cg.DOFade(1f, 0.3f);
    }

    private void SetupUpgradeDetail(SkillTooltipUI detail, SkillData skill, int level, int charIdx, int skillIdx)
    {
        if (detail == null) return;

        detail.gameObject.SetActive(true);
        detail.SetData(skill, level);

        // 버튼 클릭 리스너 설정
        var btn = detail.GetComponent<Button>();
        if (btn == null) btn = detail.gameObject.AddComponent<Button>();

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => ExecuteUpgrade(charIdx, skillIdx, level));
    }

    private void ExecuteUpgrade(int charIdx, int skillIdx, int nextLevel)
    {
        if (GameManager.Instance == null || GameManager.Instance.skillup <= 0) return;

        // 1. 강화석 소모
        GameManager.Instance.skillup--;

        // 2. 강화 적용
        GameManager.Instance.SetSkillLevel(charIdx, skillIdx, nextLevel);

        // 3. UI 갱신 (연속 강화 가능하도록 상세창만 닫고 리스트 갱신)
        CloseDetail();
        RefreshCharacterList();

        // 만약 강화석을 다 썼다면 자동으로 닫기 (선택 사항)
        if (GameManager.Instance.skillup <= 0)
        {
            CloseAll();
        }

        Debug.Log($"[SkillUpgrade] {GameManager.Instance.party[charIdx].template.CharacterName}의 {skillIdx + 1}번 스킬 강화 완료 -> Lv.{nextLevel}");
    }

    public void CloseDetail()
    {
        float duration = 0.3f;

        // 1. 배경 및 일러스트 페이드 아웃 & 슬라이드 다운
        if (offPanel != null && offPanel.activeSelf)
        {
            var bgImg = offPanel.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.DOFade(0, duration).OnComplete(() => offPanel.SetActive(false));
            }

            if (offPanelIllustration != null)
            {
                var rt = offPanelIllustration.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.DOAnchorPos(new Vector2(_originalIllustrationPos.x, _originalIllustrationPos.y - 300f), duration).SetEase(Ease.InCubic);
                }
            }
        }

        // 2. 디테일 창들 페이드 아웃 후 숨기기
        AnimateDetailOut(currentSkillDetail?.gameObject, duration);
        AnimateDetailOut(singleUpgradeDetail?.gameObject, duration);
        AnimateDetailOut(branchUpgradeDetailA?.gameObject, duration);
        AnimateDetailOut(branchUpgradeDetailB?.gameObject, duration);
    }

    private void AnimateDetailOut(GameObject go, float duration)
    {
        if (go == null || !go.activeSelf) return;

        var cg = go.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.DOFade(0, duration).OnComplete(() => go.SetActive(false));
        }
        else
        {
            go.SetActive(false);
        }
    }

    public void CloseAll()
    {
        if (skillUpPanel != null) skillUpPanel.SetActive(false);
        CloseDetail(); // CloseDetail 내부에서 애니메이션 후 offPanel을 끔

        // [추가] 맵으로 복귀 등의 후속 처리를 위해 콜백 실행
        OnClose?.Invoke();
        OnClose = null;
    }
}
