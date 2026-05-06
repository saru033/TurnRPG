using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;

public class EquipmentRerollUI : MonoBehaviour
{
    [System.Serializable]
    public class CharSlot
    {
        public GameObject root;
        public Image portrait;
        public Button btnHead;
        public Button btnBody;
        public Button btnShoes;
    }

    [Header("Main Panels")]
    public GameObject rerollPanel;
    public GameObject offPanel;
    public Image offPanelIllustration;

    [Header("Character Slots")]
    public CharSlot[] characterSlots = new CharSlot[3];

    [Header("Comparison Display")]
    public EquipmentTooltipUI currentEquipDetail;
    public EquipmentTooltipUI previewEquipDetail;
    public Button btnConfirmReroll; // 프리뷰를 적용하는 버튼 (previewEquipDetail 자체에 붙여도 됨)

    [Header("Stat Comparison (Total Stats)")]
    public TextMeshProUGUI txtBeforeHp;
    public TextMeshProUGUI txtBeforeAtk, txtBeforeDef, txtBeforeSpd, txtBeforeCritRate, txtBeforeCritDmg;
    public TextMeshProUGUI txtAfterHp, txtAfterAtk, txtAfterDef, txtAfterSpd, txtAfterCritRate, txtAfterCritDmg;

    [Header("Reroll Settings")]
    public bool isHighTier; // [추가] 고급 리롤 여부

    public System.Action OnClose;

    private Vector2 _originalIllustrationPos;
    private Vector2 _originalRerollPanelPos;
    private bool _isInitialized = false;

    private PlayerCharacterState _selectedState;
    private EquipmentPart _selectedPart;
    private EquipmentState _previewState;

    private void Start()
    {
        InitIfNecessary();
    }

    private void InitIfNecessary()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        if (currentEquipDetail != null) currentEquipDetail.gameObject.SetActive(false);
        if (previewEquipDetail != null) previewEquipDetail.gameObject.SetActive(false);

        if (offPanelIllustration != null)
        {
            _originalIllustrationPos = offPanelIllustration.GetComponent<RectTransform>().anchoredPosition;
        }

        if (rerollPanel != null)
        {
            var rt = rerollPanel.GetComponent<RectTransform>();
            if (rt != null) _originalRerollPanelPos = rt.anchoredPosition;
        }

        if (offPanel != null)
        {
            var btn = offPanel.GetComponent<Button>();
            if (btn == null) btn = offPanel.AddComponent<Button>();
            btn.onClick.AddListener(CloseDetail);
        }
    }

    public void Open()
    {
        InitIfNecessary();
        if (rerollPanel == null) return;

        int currentCount = isHighTier ? GameManager.Instance.highRerollItemCount : GameManager.Instance.rerollItemCount;
        Debug.Log($"[EquipmentReroll] Open called. isHighTier: {isHighTier}, currentCount: {currentCount}");

        rerollPanel.SetActive(true);
        if (offPanel != null) offPanel.SetActive(false);

        var rt = rerollPanel.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = new Vector2(_originalRerollPanelPos.x, _originalRerollPanelPos.y - 1500f);
            rt.DOAnchorPos(_originalRerollPanelPos, 0.4f).SetEase(Ease.OutCubic);
        }

        RefreshCharacterList();
    }

    public void RefreshCharacterList()
    {
        if (GameManager.Instance == null) return;

        var party = GameManager.Instance.party;
        for (int i = 0; i < 3; i++)
        {
            if (i >= party.Length || party[i] == null)
            {
                if (characterSlots[i].root != null) characterSlots[i].root.SetActive(false);
                continue;
            }

            var state = party[i];
            var slot = characterSlots[i];
            if (slot.root != null) slot.root.SetActive(true);
            if (slot.portrait != null) slot.portrait.sprite = state.template.illustration;

            // 버튼 리스너 및 툴팁 설정
            if (slot.btnHead != null) SetupPartButton(slot.btnHead, state, EquipmentPart.Head);
            if (slot.btnBody != null) SetupPartButton(slot.btnBody, state, EquipmentPart.Body);
            if (slot.btnShoes != null) SetupPartButton(slot.btnShoes, state, EquipmentPart.Shoes);
        }
    }

    private void SetupPartButton(Button btn, PlayerCharacterState state, EquipmentPart part)
    {
        if (btn == null) return;

        EquipmentState equip = null;
        switch (part)
        {
            case EquipmentPart.Head: equip = state.headGear; break;
            case EquipmentPart.Body: equip = state.bodyArmor; break;
            case EquipmentPart.Shoes: equip = state.shoes; break;
        }

        Image img = btn.GetComponent<Image>();
        Sprite icon = (img != null) ? img.sprite : null;

        // 1. 클릭 리스너 (리롤 시작)
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnPartSelected(state, part, icon));

        // 2. 툴팁 트리거 설정 (기존 장비 정보 확인용)
        var trigger = btn.GetComponent<EquipmentTooltipTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EquipmentTooltipTrigger>();
        trigger.Init(equip, icon);
    }

    private void OnPartSelected(PlayerCharacterState state, EquipmentPart part, Sprite icon)
    {
        if (GameManager.Instance == null) return;

        // [수정] 고급 여부에 따라 다른 재화 체크
        int currentCount = isHighTier ? GameManager.Instance.highRerollItemCount : GameManager.Instance.rerollItemCount;

        if (currentCount <= 0)
        {
            Debug.Log(isHighTier ? "고급 리롤 아이템이 부족합니다." : "리롤 아이템이 부족합니다.");
            return;
        }

        // [중요] 누르는 순간 아이템 소모
        if (isHighTier) GameManager.Instance.highRerollItemCount--;
        else GameManager.Instance.rerollItemCount--;
        if (LobbyTopUI.Instance != null) LobbyTopUI.Instance.Refresh();

        _selectedState = state;
        _selectedPart = part;

        // 1. 현재 장비 복사해서 프리뷰 생성
        EquipmentState currentEquip = null;
        switch (part)
        {
            case EquipmentPart.Head: currentEquip = state.headGear; break;
            case EquipmentPart.Body: currentEquip = state.bodyArmor; break;
            case EquipmentPart.Shoes: currentEquip = state.shoes; break;
        }

        if (currentEquip == null) return;

        _previewState = currentEquip.Clone();
        _previewState.GenerateRandomStats(isHighTier: isHighTier); // 프리뷰에 고급 리롤 여부 전달

        // 2. UI 연출
        if (offPanel != null)
        {
            offPanel.SetActive(true);
            var bgImg = offPanel.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = new Color(0, 0, 0, 0);
                bgImg.DOFade(245f / 255f, 0.3f);
            }

            if (offPanelIllustration != null)
            {
                offPanelIllustration.sprite = state.template.illustration;
                var rt = offPanelIllustration.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(_originalIllustrationPos.x, _originalIllustrationPos.y - 300f);
                    rt.DOAnchorPos(_originalIllustrationPos, 0.3f).SetEase(Ease.OutCubic);
                }
            }
        }

        // 3. 상세 비교 표시
        if (currentEquipDetail != null)
        {
            currentEquipDetail.gameObject.SetActive(true);
            currentEquipDetail.SetData(currentEquip, icon);
            AnimateDetailIn(currentEquipDetail.gameObject);
        }

        if (previewEquipDetail != null)
        {
            previewEquipDetail.gameObject.SetActive(true);
            previewEquipDetail.SetData(_previewState, icon);
            AnimateDetailIn(previewEquipDetail.gameObject);

            // 프리뷰 클릭 시 적용
            var btn = previewEquipDetail.GetComponent<Button>();
            if (btn == null) btn = previewEquipDetail.gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(ExecuteReroll);
        }

        // 4. 최종 스탯 비교 수치 갱신
        UpdateStatComparison();
    }

    private void UpdateStatComparison()
    {
        if (_selectedState == null || _previewState == null) return;

        StatType[] types = { StatType.HP, StatType.Attack, StatType.Defense, StatType.Speed, StatType.CritChance, StatType.CritDamage };
        TextMeshProUGUI[] beforeTxts = { txtBeforeHp, txtBeforeAtk, txtBeforeDef, txtBeforeSpd, txtBeforeCritRate, txtBeforeCritDmg };
        TextMeshProUGUI[] afterTxts = { txtAfterHp, txtAfterAtk, txtAfterDef, txtAfterSpd, txtAfterCritRate, txtAfterCritDmg };

        for (int i = 0; i < types.Length; i++)
        {
            float beforeVal = CalculateFinalStat(_selectedState, types[i], false);
            float afterVal = CalculateFinalStat(_selectedState, types[i], true);

            // 텍스트 출력
            FormatStatText(beforeTxts[i], types[i], beforeVal);
            FormatStatText(afterTxts[i], types[i], afterVal);

            // 색상 적용 (After 텍스트에만)
            if (afterTxts[i] != null)
            {
                if (afterVal > beforeVal) afterTxts[i].color = new Color(0.2f, 0.6f, 1f); // 파란색 (증가)
                else if (afterVal < beforeVal) afterTxts[i].color = new Color(1f, 0.3f, 0.3f); // 빨간색 (감소)
                else afterTxts[i].color = Color.white; // 동일
            }
        }
    }

    private float CalculateFinalStat(PlayerCharacterState state, StatType type, bool usePreview)
    {
        var data = state.template;
        float baseVal = 0f;
        float pointBonus = 0f;

        switch (type)
        {
            case StatType.HP:
                baseVal = data.MaxHp;
                pointBonus = data.MaxHp * state.spentHp * 0.01f;
                break;
            case StatType.Attack:
                baseVal = data.Attack;
                pointBonus = data.Attack * state.spentAtk * 0.01f;
                break;
            case StatType.Defense:
                baseVal = data.Defense;
                pointBonus = data.Defense * state.spentDef * 0.01f;
                break;
            case StatType.Speed:
                baseVal = data.Speed;
                pointBonus = state.spentSpeed;
                break;
            case StatType.CritChance:
                baseVal = data.CritChance; // 0.15 (15%)
                pointBonus = state.spentCritRate * 0.01f; // 1% -> 0.01
                break;
            case StatType.CritDamage:
                baseVal = data.CritDamage; // 1.5 (150%)
                pointBonus = state.spentCritDmg * 0.01f; // 1% -> 0.01
                break;
        }

        // 2. 장비 보너스 계산
        float equipBonusPer = 0f;
        if (usePreview)
        {
            equipBonusPer += GetBonusFromPart(state.headGear, type, EquipmentPart.Head == _selectedPart);
            equipBonusPer += GetBonusFromPart(state.bodyArmor, type, EquipmentPart.Body == _selectedPart);
            equipBonusPer += GetBonusFromPart(state.shoes, type, EquipmentPart.Shoes == _selectedPart);
        }
        else
        {
            equipBonusPer = state.GetEquipmentBonus(type);
        }

        // 3. 최종 합산
        if (type == StatType.HP || type == StatType.Attack || type == StatType.Defense)
        {
            // %, %, % 스탯 (기본값 + 포인트수치 + (기본값 * 장비% * 0.01))
            return baseVal + pointBonus + (baseVal * equipBonusPer * 0.01f);
        }
        else if (type == StatType.Speed)
        {
            // 속도 (고정 수치 합산)
            return baseVal + pointBonus + equipBonusPer;
        }
        else
        {
            // 치확, 치피 (0.01 단위로 변환 후 합산)
            return baseVal + pointBonus + (equipBonusPer * 0.01f);
        }
    }

    private void FormatStatText(TextMeshProUGUI txt, StatType type, float val)
    {
        if (txt == null) return;

        switch (type)
        {
            case StatType.HP:
            case StatType.Attack:
            case StatType.Defense:
            case StatType.Speed:
                // :F0는 반올림을 수행하므로 정보창(StatePanel)과 동일하게 작동합니다.
                txt.text = $"{val:F0}";
                break;
            case StatType.CritChance:
            case StatType.CritDamage:
                // 치확, 치피는 *100 후 반올림 표기
                txt.text = $"{val * 100f:F0}%";
                break;
        }
    }

    private float GetBonusFromPart(EquipmentState current, StatType type, bool isPreviewTarget)
    {
        EquipmentState target = isPreviewTarget ? _previewState : current;
        if (target == null) return 0f;

        float total = 0f;
        foreach (var sub in target.subStats)
        {
            if (sub.statType == type) total += sub.value;
        }
        return total;
    }

    private void ExecuteReroll()
    {
        if (GameManager.Instance == null) return;

        // 1. 적용 (기존 비율 유지 로직 포함)
        float oldMaxHp = _selectedState.TotalMaxHp;
        float hpRatio = oldMaxHp > 0 ? _selectedState.currentHp / oldMaxHp : 1f;

        switch (_selectedPart)
        {
            case EquipmentPart.Head: _selectedState.headGear = _previewState; break;
            case EquipmentPart.Body: _selectedState.bodyArmor = _previewState; break;
            case EquipmentPart.Shoes: _selectedState.shoes = _previewState; break;
        }

        float newMaxHp = _selectedState.TotalMaxHp;
        _selectedState.currentHp = newMaxHp * hpRatio;

        // 3. UI 갱신 및 닫기
        CloseDetail();
        RefreshCharacterList();

        Debug.Log($"[EquipmentReroll] {_selectedState.characterName}의 {_selectedPart} 리롤 완료.");
    }

    public void CloseDetail()
    {
        float duration = 0.3f;
        if (offPanel != null && offPanel.activeSelf)
        {
            var bgImg = offPanel.GetComponent<Image>();
            if (bgImg != null) bgImg.DOFade(0, duration).OnComplete(() => offPanel.SetActive(false));

            if (offPanelIllustration != null)
            {
                var rt = offPanelIllustration.GetComponent<RectTransform>();
                if (rt != null) rt.DOAnchorPos(new Vector2(_originalIllustrationPos.x, _originalIllustrationPos.y - 300f), duration).SetEase(Ease.InCubic);
            }
        }

        AnimateDetailOut(currentEquipDetail?.gameObject, duration);
        AnimateDetailOut(previewEquipDetail?.gameObject, duration);

        // [수정] 상세창이 닫힐 때 아이템이 없다면 전체 패널도 닫기
        // 단, 이미 CloseAll이 진행 중이거나 Open된 직후라면 방지
        if (GameManager.Instance != null)
        {
            int currentCount = isHighTier ? GameManager.Instance.highRerollItemCount : GameManager.Instance.rerollItemCount;
            if (currentCount <= 0 && rerollPanel != null && rerollPanel.activeSelf)
            {
                Debug.Log($"[EquipmentReroll] No items left ({currentCount}). Closing all.");
                DOVirtual.DelayedCall(duration, () =>
                {
                    // 딜레이 후에도 여전히 아이템이 0개인 경우에만 닫기
                    int checkCount = isHighTier ? GameManager.Instance.highRerollItemCount : GameManager.Instance.rerollItemCount;
                    if (checkCount <= 0) CloseAll();
                });
            }
        }
    }

    public void CloseAll()
    {
        // CloseDetail을 직접 호출하지 않고 내부 로직만 수행하여 루프 방지
        float duration = 0.3f;
        if (offPanel != null && offPanel.activeSelf)
        {
            var bgImg = offPanel.GetComponent<Image>();
            if (bgImg != null) bgImg.DOFade(0, duration).OnComplete(() => offPanel.SetActive(false));

            if (offPanelIllustration != null)
            {
                var rt = offPanelIllustration.GetComponent<RectTransform>();
                if (rt != null) rt.DOAnchorPos(new Vector2(_originalIllustrationPos.x, _originalIllustrationPos.y - 300f), duration).SetEase(Ease.InCubic);
            }
        }

        AnimateDetailOut(currentEquipDetail?.gameObject, duration);
        AnimateDetailOut(previewEquipDetail?.gameObject, duration);

        if (rerollPanel != null)
        {
            var rt = rerollPanel.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.DOAnchorPos(new Vector2(_originalRerollPanelPos.x, _originalRerollPanelPos.y - 1500f), 0.4f)
                  .SetEase(Ease.InCubic)
                  .OnComplete(() =>
                  {
                      rerollPanel.SetActive(false);
                      OnClose?.Invoke();
                      OnClose = null;
                  });
            }
            else
            {
                rerollPanel.SetActive(false);
                OnClose?.Invoke();
                OnClose = null;
            }
        }
    }

    private void AnimateDetailIn(GameObject go)
    {
        if (go == null) return;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0;
        cg.DOFade(1f, 0.3f);
    }

    private void AnimateDetailOut(GameObject go, float duration)
    {
        if (go == null || !go.activeSelf) return;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg != null) cg.DOFade(0, duration).OnComplete(() => go.SetActive(false));
        else go.SetActive(false);
    }
}
