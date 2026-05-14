using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TurnRPG.SkillSystem;
using DG.Tweening;

/// <summary>
/// 프로젝트 전체의 게임 데이터 및 상태를 관리하는 싱글톤 매니저입니다.
/// 아군 3명의 파티 데이터, 인벤토리, 성적 등을 관리하며 씬이 바뀌어도 파괴되지 않습니다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("아군 파티 관리 (최대 3명)")]
    // 이 배열이 로비와 전투 씬 사이의 데이터 허브 역할을 함
    public PlayerCharacterState[] party = new PlayerCharacterState[3];

    [Header("아군 아이템 관리")]
    public List<TurnRPG.SkillSystem.SkillData> playerItems = new List<TurnRPG.SkillSystem.SkillData>();

    [Header("초기 파티 템플릿 (최초 1회 설정용)")]
    public CharacterData[] initialTemplates = new CharacterData[3];


    [Header("재화")]
    public int gold = 0;
    public int skillup = 0;
    public int rerollItemCount = 50; // 장비 리롤 아이템 (테스트용 50)
    public int highRerollItemCount = 10; // 고급 장비 리롤 아이템 (테스트용 10)

    [Header("Master Skill List")]
    public List<TurnRPG.SkillSystem.SkillData> masterSkillList = new List<TurnRPG.SkillSystem.SkillData>();

    [Header("UI References")]
    public SettingsUI settingsUI; // [추가] ESC 메뉴 UI
    public InitialGiftUI initialGiftUI; // [추가] 시작 보상 선택 UI


    [Header("Tooltip")]
    public Canvas tooltipCanvas; // [추가] 툴팁이 생성될 캔버스
    public SkillTooltipUI tooltipPrefab;
    private SkillTooltipUI _tooltipInstance;

    [Header("Status Tooltip")]
    public StatusEffectTooltipUI statusTooltipPrefab;
    private StatusEffectTooltipUI _statusTooltipInstance;

    [Header("Character Naming & Preview")]
    public CharacterNamingUI namingUI;
    public InitialCharacterPreviewUI previewUI; // [추가] 초기 캐릭터 정보 확인 UI
    public EquipmentRerollUI rerollUI;

    [Header("Equipment Stat Ranges")]
    public float minHpPercent = 5f, maxHpPercent = 15f;
    public float minAtkPercent = 5f, maxAtkPercent = 15f;
    public float minDefPercent = 5f, maxDefPercent = 15f;
    public int minSpeed = 1, maxSpeed = 5;
    public float minCritChance = 3f, maxCritChance = 8f;
    public float minCritDamage = 10f, maxCritDamage = 20f;

    [Header("Equipment Tooltip")]
    public EquipmentTooltipUI equipTooltipPrefab;
    private EquipmentTooltipUI _equipTooltipInstance;

    /// <summary>
    /// 현재 툴팁인 상태인지(롱프레스 중인지) 여부
    /// </summary>
    public bool IsTooltipPerforming { get; private set; }

    // -------------------------------------------------------
    // 스킬 툴팁 제어 (전역)
    // -------------------------------------------------------

    public void ShowSkillTooltip(SkillData skill, int level, Vector3 worldPos, float yOffset)
    {
        if (tooltipPrefab == null) return;

        if (_tooltipInstance == null)
        {
            // 지정된 캔버스가 있으면 그 하위로, 없으면 GameManager 하위로 생성
            Transform parent = tooltipCanvas != null ? tooltipCanvas.transform : transform;
            _tooltipInstance = Instantiate(tooltipPrefab, parent);
        }

        _tooltipInstance.gameObject.SetActive(true);
        _tooltipInstance.transform.SetAsLastSibling();
        _tooltipInstance.SetData(skill, level);

        IsTooltipPerforming = true;

        float canvasScale = _tooltipInstance.transform.lossyScale.y;
        _tooltipInstance.transform.position = worldPos + new Vector3(0, yOffset * canvasScale, 0);
    }

    public void HideSkillTooltip()
    {
        if (_tooltipInstance != null)
        {
            _tooltipInstance.gameObject.SetActive(false);
        }
        StartCoroutine(ResetTooltipFlagRoutine());
    }

    // -------------------------------------------------------
    // 상태 효과 툴팁 제어 (전역)
    // -------------------------------------------------------

    public void ShowStatusTooltip(StatusEffect effect, Vector3 worldPos, float height)
    {
        if (statusTooltipPrefab == null) return;

        if (_statusTooltipInstance == null)
        {
            Transform parent = tooltipCanvas != null ? tooltipCanvas.transform : transform;
            _statusTooltipInstance = Instantiate(statusTooltipPrefab, parent);
        }

        _statusTooltipInstance.gameObject.SetActive(true);
        _statusTooltipInstance.transform.SetAsLastSibling();
        _statusTooltipInstance.SetData(effect);

        IsTooltipPerforming = true;

        _statusTooltipInstance.SetPosition(worldPos, height, _statusTooltipInstance.transform.lossyScale.y);
    }

    public void HideStatusTooltip()
    {
        if (_statusTooltipInstance != null)
        {
            _statusTooltipInstance.gameObject.SetActive(false);
        }
        StartCoroutine(ResetTooltipFlagRoutine());
    }

    // -------------------------------------------------------
    // 장비 툴팁 제어 (전역)
    // -------------------------------------------------------

    public void ShowEquipmentTooltip(EquipmentState state, Sprite icon, Vector3 worldPos, float height)
    {
        if (equipTooltipPrefab == null) return;

        if (_equipTooltipInstance == null)
        {
            Transform parent = tooltipCanvas != null ? tooltipCanvas.transform : transform;
            _equipTooltipInstance = Instantiate(equipTooltipPrefab, parent);
        }

        _equipTooltipInstance.gameObject.SetActive(true);
        _equipTooltipInstance.transform.SetAsLastSibling();
        _equipTooltipInstance.SetData(state, icon);

        IsTooltipPerforming = true;

        // 위치 설정 (다른 툴팁들과 동일한 로직)
        float canvasScale = _equipTooltipInstance.transform.lossyScale.y;
        _equipTooltipInstance.transform.position = worldPos + new Vector3(0, height * canvasScale, 0);
    }

    public void HideEquipmentTooltip()
    {
        if (_equipTooltipInstance != null)
        {
            _equipTooltipInstance.gameObject.SetActive(false);
        }
        StartCoroutine(ResetTooltipFlagRoutine());
    }

    // -------------------------------------------------------
    // 장비 리롤 (옵션 변경)
    // -------------------------------------------------------

    /// <summary>
    /// 특정 캐릭터의 특정 부위 장비 옵션을 랜덤하게 재설정합니다.
    /// minScale/maxScale을 조절하여 고급 리롤 기능을 구현할 수 있습니다.
    /// </summary>
    public void RerollEquipment(PlayerCharacterState state, EquipmentPart part, float minScale = 1.0f, float maxScale = 1.0f)
    {
        if (state == null) return;

        // 1. 기존 체력 비율 저장
        float oldMaxHp = state.TotalMaxHp;
        float hpRatio = oldMaxHp > 0 ? state.currentHp / oldMaxHp : 1f;

        EquipmentState targetEquip = null;
        switch (part)
        {
            case EquipmentPart.Head: targetEquip = state.headGear; break;
            case EquipmentPart.Body: targetEquip = state.bodyArmor; break;
            case EquipmentPart.Shoes: targetEquip = state.shoes; break;
        }

        if (targetEquip != null)
        {
            targetEquip.GenerateRandomStats(minScale, maxScale);

            // 2. 새로운 체력 비율에 맞춰 현재 체력 조정
            float newMaxHp = state.TotalMaxHp;
            state.currentHp = newMaxHp * hpRatio;

            // UI 갱신 (열려있다면)
            var opener = FindObjectOfType<CharacterStatePanelOpener>();
            if (opener != null && opener.characterStatePanel.activeSelf)
            {
                opener.UpdateUI();
            }

            Debug.Log($"[Equipment] {state.characterName}의 {part} 장비 옵션이 변경되었습니다.");
        }
    }

    private IEnumerator ResetTooltipFlagRoutine()
    {
        yield return new WaitForSeconds(0.1f);
        IsTooltipPerforming = false;
    }

    [Header("Transition UI")]
    [SerializeField] private UnityEngine.UI.Image screenFadeImage; // 암전용 블랙 이미지

    /// <summary>
    /// 게임 진행 상황을 초기화합니다. (화면 암전 연출 포함)
    /// </summary>
    public void ResetGameProgress()
    {
        StartCoroutine(ResetGameFlowRoutine());
    }

    private IEnumerator ResetGameFlowRoutine()
    {
        Debug.Log("[GameManager] 게임 진행 상황 초기화 시작 (암전 연출)");

        // 0. 화면 암전 (Fade In)
        if (screenFadeImage != null)
        {
            screenFadeImage.gameObject.SetActive(true);
            Color c = screenFadeImage.color;
            c.a = 0f;
            screenFadeImage.color = c;
            yield return screenFadeImage.DOFade(1f, 0.5f).WaitForCompletion();
        }

        // 1. 재화 및 아이템 초기화
        gold = 0;
        skillup = 0;
        rerollItemCount = 0;
        highRerollItemCount = 0;
        playerItems.Clear();

        // [추가] BGM을 로비 음악으로 변경
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(BgmType.MainLobby);
        }

        // [추가] 안전한 리셋을 위해 다른 매니저들의 코루틴 중단
        var battleMgr = GameObject.FindAnyObjectByType<BattleManager>(FindObjectsInactive.Include);
        if (battleMgr != null) battleMgr.StopAllCoroutines();

        // 2. UI 정리 ("GamePanel" 태그를 가진 모든 활성 패널 비활성화)
        GameObject[] gamePanels = GameObject.FindGameObjectsWithTag("GamePanel");
        foreach (GameObject panel in gamePanels)
        {
            panel.SetActive(false);
        }

        var mapUI = GameObject.FindAnyObjectByType<MapUI>(FindObjectsInactive.Include);
        if (mapUI != null)
        {
            mapUI.StopAllCoroutines();
            if (mapUI.BattlePanel != null) mapUI.BattlePanel.SetActive(false);
            if (mapUI.RestPanel != null) mapUI.RestPanel.SetActive(false);
            if (mapUI.shopPanel != null) mapUI.shopPanel.SetActive(false);
            if (mapUI.eventPanel != null) mapUI.eventPanel.SetActive(false);

            // 3. 맵 초기화 및 재생성
            mapUI.GenerateAndDraw();
            mapUI.gameObject.SetActive(false);
        }

        // 4. 캐릭터 파티 초기화
        InitializeParty();

        // 데이터 반영을 위해 한 프레임 대기
        yield return null;

        // 5. 암전 유지 (1초)
        yield return new WaitForSeconds(1.0f);

        // 6. 화면 밝아짐 (Fade Out)
        if (screenFadeImage != null)
        {
            yield return screenFadeImage.DOFade(0f, 0.5f).WaitForCompletion();
            screenFadeImage.gameObject.SetActive(false);
        }

        // 7. 상단 재화 UI 초기화 및 노출
        if (LobbyTopUI.Instance != null)
        {
            LobbyTopUI.Instance.Refresh();
            LobbyTopUI.Instance.ShowUI();
        }

        Debug.Log("[GameManager] 게임 진행 상황 초기화 완료");
    }

    private void Update()
    {
        // [수정] New Input System 방식의 ESC 메뉴 토글
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (settingsUI != null) settingsUI.Toggle();
        }

        // [추가] 전역 클릭 사운드 (마우스 좌클릭)
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(SfxType.Click);
            }
        }
    }

    private void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeParty();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 게임 시작 시 초기 템플릿을 기반으로 파티 데이터를 초기화합니다.
    /// </summary>
    public void InitializeParty()
    {
        if (LobbyTopUI.Instance != null) LobbyTopUI.Instance.Refresh();

        List<PlayerCharacterState> needsNaming = new List<PlayerCharacterState>();

        for (int i = 0; i < 3; i++)
        {
            if (initialTemplates[i] != null)
            {
                party[i] = new PlayerCharacterState(initialTemplates[i]);

                // [추가] 2, 3번 스킬 무작위 할당
                RandomizeSkills(party[i]);

                Debug.Log($"[GameManager] {i + 1}번 슬롯 {initialTemplates[i].CharacterName} 초기화 완료 (스킬 랜덤 배정됨)");

                // 이름이 비어있다면 명명 큐에 추가
                if (string.IsNullOrEmpty(party[i].characterName))
                {
                    needsNaming.Add(party[i]);
                }
            }
        }

        // 명명이 필요한 캐릭터가 있다면 연출 시작
        if (needsNaming.Count > 0)
        {
            StartCoroutine(NamingFlowRoutine(needsNaming));
        }
    }

    /// <summary>
    /// 캐릭터의 타입과 슬롯 조건에 맞는 스킬을 무작위로 할당합니다. (1번 스킬 제외)
    /// </summary>
    private void RandomizeSkills(PlayerCharacterState state)
    {
        if (state == null || state.template == null || masterSkillList == null || masterSkillList.Count == 0) return;

        var charType = state.template.charType;

        // 2번 스킬 (Index 1)
        var skill2Pool = masterSkillList.FindAll(s => s != null &&
                                                      s.Type != TurnRPG.SkillSystem.SkillType.Item &&
                                                      s.SlotIndex == SkillSlotIndex.Skill2 &&
                                                      (s.EquipRestriction & charType) != 0);
        if (skill2Pool.Count > 0)
        {
            state.equippedSkills[1] = skill2Pool[Random.Range(0, skill2Pool.Count)];
            state.skillLevels[1] = 1;
        }

        // 3번 스킬 (Index 2)
        var skill3Pool = masterSkillList.FindAll(s => s != null &&
                                                      s.Type != TurnRPG.SkillSystem.SkillType.Item &&
                                                      s.SlotIndex == SkillSlotIndex.Skill3 &&
                                                      (s.EquipRestriction & charType) != 0);
        if (skill3Pool.Count > 0)
        {
            state.equippedSkills[2] = skill3Pool[Random.Range(0, skill3Pool.Count)];
            state.skillLevels[2] = 1;
        }
    }

    private IEnumerator NamingFlowRoutine(List<PlayerCharacterState> targets)
    {
        if (namingUI == null)
        {
            Debug.LogError("[GameManager] namingUI가 할당되지 않았습니다!");
            yield break;
        }

        foreach (var charState in targets)
        {
            // 1. 이름 정해주기
            bool isWaitingName = true;
            namingUI.Open(charState, () => isWaitingName = false);

            while (isWaitingName) yield return null;

            // 2. [추가] 정해진 이름과 함께 랜덤으로 받은 스킬/장비 보여주기
            if (previewUI != null)
            {
                bool isWaitingPreview = true;
                previewUI.Open(charState, () => isWaitingPreview = false);

                while (isWaitingPreview) yield return null;
            }
        }

        Debug.Log("[GameManager] 모든 캐릭터 명명 완료");

        // [수정] 명명 완료 후 보상 선택 UI 오픈
        if (initialGiftUI != null)
        {
            initialGiftUI.Open();
        }
        else
        {
            // 폴백: UI가 없다면 기존처럼 리롤 아이템 지급
            rerollItemCount += 3;
            if (LobbyTopUI.Instance != null) LobbyTopUI.Instance.Refresh();
            if (rerollUI != null) rerollUI.Open();
        }
    }

    // -------------------------------------------------------
    // 로비에서 활용할 데이터 업데이트 메서드 예시
    // -------------------------------------------------------

    /// <summary>
    /// 특정 인덱스의 캐릭터 스탯을 강화합니다.
    /// </summary>
    public void AddMaxHp(int partyIndex, float amount)
    {
        if (partyIndex >= 0 && partyIndex < 3 && party[partyIndex] != null)
        {
            party[partyIndex].currentBaseMaxHp += amount;
            Debug.Log($"[GameManager] {party[partyIndex].template.CharacterName}의 BaseMaxHp가 {amount}만큼 증가함! (현재: {party[partyIndex].currentBaseMaxHp})");
        }
    }

    /// <summary>
    /// 특정 스킬의 레벨을 변경합니다.
    /// </summary>
    public void SetSkillLevel(int partyIndex, int skillSlot, int level)
    {
        if (partyIndex >= 0 && partyIndex < 3 && party[partyIndex] != null && skillSlot >= 0 && skillSlot < 3)
        {
            party[partyIndex].skillLevels[skillSlot] = level;
            Debug.Log($"[GameManager] {party[partyIndex].template.CharacterName}의 {skillSlot + 1}번 스킬 레벨을 {level}로 변경");
        }
    }
}
