using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TurnRPG.SkillSystem;

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


    [Header("Tooltip")]
    public Canvas tooltipCanvas; // [추가] 툴팁이 생성될 캔버스
    public SkillTooltipUI tooltipPrefab;
    private SkillTooltipUI _tooltipInstance;

    [Header("Status Tooltip")]
    public StatusEffectTooltipUI statusTooltipPrefab;
    private StatusEffectTooltipUI _statusTooltipInstance;

    [Header("Character Naming")]
    public CharacterNamingUI namingUI;

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

    private IEnumerator ResetTooltipFlagRoutine()
    {
        yield return new WaitForSeconds(0.1f);
        IsTooltipPerforming = false;
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
        List<PlayerCharacterState> needsNaming = new List<PlayerCharacterState>();

        for (int i = 0; i < 3; i++)
        {
            if (initialTemplates[i] != null)
            {
                party[i] = new PlayerCharacterState(initialTemplates[i]);
                Debug.Log($"[GameManager] {i + 1}번 슬롯 {initialTemplates[i].CharacterName} 초기화 완료");

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

    private IEnumerator NamingFlowRoutine(List<PlayerCharacterState> targets)
    {
        if (namingUI == null)
        {
            Debug.LogError("[GameManager] namingUI가 할당되지 않았습니다!");
            yield break;
        }

        foreach (var charState in targets)
        {
            bool isWaiting = true;
            namingUI.Open(charState, () => isWaiting = false);
            
            // 유저가 이름을 확정할 때까지 대기
            while (isWaiting)
            {
                yield return null;
            }
        }

        Debug.Log("[GameManager] 모든 캐릭터 명명 완료");
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
