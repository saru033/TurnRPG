using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TurnRPG.SkillSystem;
using System;

public class SkillExchangeUI : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject exchangePanel;
    public Image charIllustration;
    public SkillTooltipUI currentSkillTooltip;
    public SkillTooltipUI newSkillTooltip;

    [Header("References")]
    public SkillUpgradeUI skillUpgradeUI;

    private Vector2 _originalIllustrationPos;
    private bool _isInitialized = false;

    private void Awake()
    {
        if (exchangePanel == null) exchangePanel = gameObject;
    }

    /// <summary>
    /// 스킬 교체 확인 패널을 엽니다.
    /// </summary>
    /// <param name="newSkill">바꿀 새로운 스킬</param>
    /// <param name="price">가격 (0이면 무료)</param>
    /// <param name="onSuccess">교체 성공 시 실행할 콜백</param>
    public void Open(SkillData newSkill, int price, Action onSuccess)
    {
        if (exchangePanel == null) return;

        // 0. 초기 위치 저장
        if (!_isInitialized && charIllustration != null)
        {
            _originalIllustrationPos = charIllustration.rectTransform.anchoredPosition;
            _isInitialized = true;
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
            Debug.LogWarning($"[SkillExchange] {newSkill.SkillName}을(를) 장착할 수 있는 캐릭터가 파티에 없습니다.");
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
            
            charIllustration.rectTransform.DOKill();
            charIllustration.rectTransform.anchoredPosition = new Vector2(_originalIllustrationPos.x, _originalIllustrationPos.y - 300f);
            charIllustration.rectTransform.DOAnchorPos(_originalIllustrationPos, 0.4f).SetEase(Ease.OutCubic);
        }
        
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
            
            Button btn = newSkillTooltip.GetComponent<Button>();
            if (btn == null) btn = newSkillTooltip.gameObject.AddComponent<Button>();
            
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => ExecuteExchange(targetChar, newSkill, price, onSuccess));
        }

        exchangePanel.SetActive(true);
    }

    /// <summary>
    /// 패널을 닫습니다.
    /// </summary>
    public void Close()
    {
        if (exchangePanel == null) return;

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

    private void ExecuteExchange(PlayerCharacterState targetChar, SkillData newSkill, int price, Action onSuccess)
    {
        if (GameManager.Instance.gold < price)
        {
            Debug.Log("[SkillExchange] 소지금이 부족합니다!");
            return;
        }

        // 1. 재화 차감
        GameManager.Instance.gold -= price;

        // 2. 기존 스킬 강화석 환급 로직
        int slotIdx = (int)newSkill.SlotIndex;
        int oldLevel = targetChar.skillLevels[slotIdx];
        int refund = 0;

        if (oldLevel > 1)
        {
            int branch = newSkill.GetBranchLevel();
            if (oldLevel <= branch) refund = oldLevel - 1;
            else refund = branch;

            GameManager.Instance.skillup += refund;
            Debug.Log($"[SkillExchange] 기존 스킬 환급: 강화석 {refund}개 획득!");
        }

        // 3. 스킬 교체 및 레벨 초기화
        targetChar.equippedSkills[slotIdx] = newSkill;
        targetChar.skillLevels[slotIdx] = 1;

        // 4. 결과 반영
        if (LobbyTopUI.Instance != null) LobbyTopUI.Instance.Refresh();

        // [추가] 스킬 교체 보이스 및 SFX
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(SfxType.Newskill);

            if (targetChar.template != null && targetChar.template.skillChangeVoices.Count > 0)
            {
                var voice = targetChar.template.skillChangeVoices[UnityEngine.Random.Range(0, targetChar.template.skillChangeVoices.Count)];
                SoundManager.Instance.PlayVoice(voice);
            }
        }
        
        // 5. 즉시 강화창 열기
        if (refund > 0 && skillUpgradeUI != null)
        {
            skillUpgradeUI.Open();
        }

        // 6. 성공 콜백 및 패널 닫기
        onSuccess?.Invoke();
        Close();

        Debug.Log($"[SkillExchange] {targetChar.characterName}의 스킬 교체 완료!");
    }
}
