using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TurnRPG.SkillSystem;
using System.Linq;

public class BattleManager : MonoBehaviour
{
    [Header("References")]
    public BattleUI battleUI;
    public CharacterPlacer characterPlacer;

    [Header("전투 참가 데이터 (테스트용)")]
    public List<CharacterData> initialCharacterDatas;

    // -------------------------------------------------------
    // 전투 상태
    // -------------------------------------------------------
    public enum BattleState { Idle, PlayerTurn, SelectTarget, EnemyTurn, WaitAction, Win, Lose }
    public BattleState State { get; private set; } = BattleState.Idle;

    public static BattleManager Instance { get; private set; }

    Actiongaugesystem gaugeSystem;
    public List<BattleCharacter> allCharacters = new();
    BattleCharacter currentActor;

    void Awake()
    {
        Instance = this;
    }

    // -------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------
    IEnumerator Start()
    {
        yield return null;   // UI Layout 계산 완료 대기
        InitBattle();
    }

    // -------------------------------------------------------
    // 초기화 (나중에 Stage/캐릭터 데이터에서 받아올 부분)
    // -------------------------------------------------------
    void InitBattle()
    {
        // ScriptableObject(CharacterData) 리스트를 기반으로 런타임 캐릭터 생성
        allCharacters = new List<BattleCharacter>();

        if (initialCharacterDatas != null && initialCharacterDatas.Count > 0)
        {
            foreach (var data in initialCharacterDatas)
            {
                if (data != null)
                    allCharacters.Add(new BattleCharacter(data));
            }
        }
        else
        {
            Debug.LogWarning("[BattleManager] initialCharacterDatas가 비어있습니다. 에디터에서 할당해주세요.");
        }

        gaugeSystem = new Actiongaugesystem();

        battleUI.Init(allCharacters);
        battleUI.SetSkillButtonsVisible(false);
        battleUI.SetSideImageVisible(false, currentActor);


        if (!object.ReferenceEquals(characterPlacer, null))
            characterPlacer.PlaceCharacters(allCharacters);

        State = BattleState.Idle;
        StartCoroutine(TurnLoop());
    }

    // -------------------------------------------------------
    // 턴 루프
    // -------------------------------------------------------
    IEnumerator TurnLoop()
    {
        while (!IsOver())
        {
            // 1. 다음 행동 캐릭터 결정
            gaugeSystem.Advance(allCharacters);
            currentActor = gaugeSystem.DequeueReady();

            if (object.ReferenceEquals(currentActor, null))
            {
                yield break;
            }

            // ⭐ 1.5 턴 시작 시스템 발동 (쿨타임 감소, 출혈 피해 등)
            currentActor.OnTurnStart();
            
            // 만약 출혈/화상 등 턴 시작 데미지로 사망했다면 턴 즉시 스킵
            if (!currentActor.IsAlive)
            {
                currentActor.OnTurnEnd();
                gaugeSystem.OnTurnEnd(currentActor);
                continue;
            }

            battleUI.UpdateGaugePositions(allCharacters);
            battleUI.HighlightActor(currentActor);


            // 짧은 연출 딜레이
            yield return new WaitForSeconds(0.3f);

            if (currentActor.IsPlayer)
            {
                // 2. 아군 턴 — 플레이어 입력 대기
                State = BattleState.PlayerTurn;
                battleUI.SetSkillButtonsVisible(true, currentActor);
                battleUI.SetSideImageVisible(true, currentActor);

                // OnSkillSelected() 및 타겟 지정 완료 시점까지 대기 (SelectTarget 상태도 포함해 대기)
                yield return new WaitUntil(() => State != BattleState.PlayerTurn && State != BattleState.SelectTarget);

                // 스킬 코루틴 등 애니메이션 대기
                yield return new WaitUntil(() => State == BattleState.Idle || State == BattleState.Win || State == BattleState.Lose);
            }
            else
            {
                // 3. 적 턴 — 자동 행동
                State = BattleState.EnemyTurn;
                battleUI.SetSkillButtonsVisible(false);
                battleUI.SetSideImageVisible(true, currentActor);

                yield return new WaitForSeconds(0.5f);   // 적 행동 연출 딜레이
                EnemyAct(currentActor);
                
                // 적 스킬 사용(코루틴) 대기
                yield return new WaitUntil(() => State == BattleState.Idle || State == BattleState.Win || State == BattleState.Lose);
            }

            // 승패 결판 났으면 턴 넘기지 않고 종료
            if (State == BattleState.Win || State == BattleState.Lose) break;

            // 4. 턴 종료 — 버프 지속시간 차감
            currentActor.OnTurnEnd();
            gaugeSystem.OnTurnEnd(currentActor);
            battleUI.UpdateGaugePositions(allCharacters);
            battleUI.SetSkillButtonsVisible(false);
            battleUI.SetSideImageVisible(false, currentActor);

            yield return new WaitForSeconds(0.2f);
        }

        OnBattleEnd();
    }

    // -------------------------------------------------------
    // 아군 스킬 입력 (BattleUI 버튼에서 호출)
    // -------------------------------------------------------

    /// <summary>
    /// 스킬 버튼 클릭 시 BattleUI가 호출.
    /// skillIndex: 0 = 1스킬, 1 = 2스킬, 2 = 3스킬
    /// </summary>
    private int _selectedSkillIndex = -1;

    public void OnSkillSelected(int skillIndex)
    {
        // 타겟 선택 모드로 전환 (PlayerTurn -> SelectTarget)
        if (State != BattleState.PlayerTurn && State != BattleState.SelectTarget) return;

        var skillData = currentActor.ActiveSkills.Count > skillIndex ? currentActor.ActiveSkills[skillIndex] : null;
        if (skillData == null) return;
        
        if (currentActor.SkillCooldowns.Length > skillIndex && currentActor.SkillCooldowns[skillIndex] > 0)
        {
            Debug.LogWarning($"[Battle] {currentActor.Name}의 {skillIndex + 1}번 스킬은 쿨타임 중입니다.");
            return;
        }

        State = BattleState.SelectTarget;
        _selectedSkillIndex = skillIndex;

        battleUI.MoveSkillSelectIndicator(skillIndex);
        Debug.Log($"[Battle] {skillData.SkillName} 선택됨! 대상을 클릭해주세요.");
    }

    // UI에서 직접 호출하는 대신 CharacterView 클릭으로 실행
    public void OnCharacterClicked(BattleCharacter target)
    {
        if (State != BattleState.SelectTarget) return;
        if (_selectedSkillIndex == -1) return;

        var skillData = currentActor.ActiveSkills.Count > _selectedSkillIndex ? currentActor.ActiveSkills[_selectedSkillIndex] : null;
        if (skillData == null) return;

        int level = currentActor.SkillLevels[_selectedSkillIndex];
        var levelData = skillData.LevelDatas != null && skillData.LevelDatas.Count >= level ? skillData.LevelDatas[level - 1] : null;
        if (levelData == null) return;

        // 타겟 유효성 검증
        bool isValid = false;
        switch (levelData.TargetType)
        {
            case SkillTargetType.SingleEnemy:
            case SkillTargetType.RandomEnemy:
            case SkillTargetType.AllEnemies: // 전체 공격도 몬스터를 클릭해야 나가도록 유도
                isValid = (target.IsPlayer != currentActor.IsPlayer);
                break;
            case SkillTargetType.SingleAlly:
            case SkillTargetType.AllAllies:
                isValid = (target.IsPlayer == currentActor.IsPlayer);
                break;
            case SkillTargetType.Self:
                isValid = (target == currentActor);
                break;
            default:
                isValid = true;
                break;
        }

        if (!isValid)
        {
            Debug.Log("[Battle] 유효하지 않은 대상입니다.");
            return;
        }

        // 실행
        int executionIndex = _selectedSkillIndex;
        _selectedSkillIndex = -1; // 리셋
        battleUI.select_SkillRect.gameObject.SetActive(false); // 인디케이터 숨기기

        State = BattleState.WaitAction;
        StartCoroutine(ExecuteSkillRoutine(skillData, executionIndex, target));
    }

    // 기존의 OnSkillExecute는 더블클릭 대비용으로 혹시 모르니 남겨두지만 무용지물이 됨
    public void OnSkillExecute(int skillIndex)
    {
        // OnCharacterClicked 으로 일원화되어 사용하지 않음.
    }

    IEnumerator ExecuteSkillRoutine(SkillData skillData, int skillIndex, BattleCharacter manualTarget = null)
    {
        Debug.Log($"[Battle] {currentActor.Name} → {skillData.SkillName} 사용 시작");
        
        // 이 스킬을 이번 턴에 썼다고 마킹 (쿨타임 이중차감 방지)
        currentActor.CastedSkillIndexThisTurn = skillIndex;

        int level = currentActor.SkillLevels.Length > skillIndex ? currentActor.SkillLevels[skillIndex] : 1;
        var levelData = skillData.LevelDatas != null && skillData.LevelDatas.Count >= level ? skillData.LevelDatas[level - 1] : null;
        
        // --- 1. 타겟 지정 (수동 타겟팅 우선 로직) ---
        BattleCharacter selectedTarget = manualTarget;
        
        // 수동 지정이 안 되었을 경우만 랜덤 fallback (적 AI용)
        if (selectedTarget == null && levelData != null)
        {
            switch (levelData.TargetType)
            {
                case SkillTargetType.SingleEnemy:
                case SkillTargetType.AllEnemies:
                case SkillTargetType.RandomEnemy:
                    var aliveEnemies = allCharacters.Where(c => c.IsAlive && c.IsPlayer != currentActor.IsPlayer).ToList();
                    if (aliveEnemies.Count > 0)
                        selectedTarget = aliveEnemies[Random.Range(0, aliveEnemies.Count)];
                    break;
                case SkillTargetType.SingleAlly:
                case SkillTargetType.AllAllies:
                    var aliveAllies = allCharacters.Where(c => c.IsAlive && c.IsPlayer == currentActor.IsPlayer).ToList();
                    if (aliveAllies.Count > 0)
                        selectedTarget = aliveAllies[Random.Range(0, aliveAllies.Count)];
                    break;
                case SkillTargetType.Self:
                    selectedTarget = currentActor;
                    break;
                default:
                    selectedTarget = null;
                    break;
            }
        }

        // --- 2. 애니메이션 및 컷씬 재생 대기 ---
        if (!string.IsNullOrEmpty(skillData.RequiredAnimationTrigger) && currentActor.Animator != null)
        {
            currentActor.Animator.SetTrigger(skillData.RequiredAnimationTrigger);
            
            // 현재 모션이 끝날 때까지 딜레이 대기 (최신 엔진 기능 활용)
            yield return new WaitForSeconds(0.1f); // 전환 지연시간
            var stateInfo = currentActor.Animator.GetCurrentAnimatorStateInfo(0);
            yield return new WaitForSeconds(stateInfo.length);
        }

        // 컷씬 애니메이터 연동
        if (skillData.UltimateCutsceneClip != null && battleUI.ultimateCutsceneRoot != null)
        {
            battleUI.ultimateCutsceneRoot.SetActive(true);
            var anim = battleUI.ultimateCutsceneRoot.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.Play(skillData.UltimateCutsceneClip.name);
                yield return new WaitForSeconds(skillData.UltimateCutsceneClip.length);
            }
            battleUI.ultimateCutsceneRoot.SetActive(false);
        }
        else if (string.IsNullOrEmpty(skillData.RequiredAnimationTrigger))
        {
            // 컷씬도 애니메이션도 없을 경우만 기본 대기
            yield return new WaitForSeconds(0.4f);
        }

        // --- 3. 이펙트(피해/버프) 체인 실행 ---
        if (levelData != null)
        {
            currentActor.SkillCooldowns[skillIndex] = levelData.Cooldown;
            
            if (levelData.Effects != null)
            {
                foreach (var eff in levelData.Effects)
                {
                    if (eff != null)
                    {
                        if (!eff.Execute(currentActor, selectedTarget)) break; 
                    }
                }
            }
        }

        BattleEventManager.TriggerSkillUsed(currentActor, skillData);

        // 전투가 아예 끝났는지 검사
        if (IsOver()) 
        {
            OnBattleEnd();
        }
        else 
        {
            State = BattleState.Idle;   // 턴 루프에게 완전히 끝났음을 알림
        }
    }

    // -------------------------------------------------------
    // 적 자동 행동
    // -------------------------------------------------------
    void EnemyAct(BattleCharacter enemy)
    {
        int selectedIndex = 0;
        SkillData selectedSkill = null;
        
        // 간단한 AI: 강한 스킬(인덱스 2 -> 1 -> 0) 우선순위 검사
        for (int i = enemy.ActiveSkills.Count - 1; i >= 0; i--)
        {
            if (enemy.ActiveSkills[i] != null && enemy.SkillCooldowns[i] <= 0)
            {
                selectedIndex = i;
                selectedSkill = enemy.ActiveSkills[i];
                break;
            }
        }

        if (selectedSkill == null)
        {
            Debug.LogWarning($"[Battle] {enemy.Name}가 사용할 수 있는 스킬이 하나도 없습니다! (턴 강제 휴식)");
            State = BattleState.Idle; // 바로 턴 넘기기
            return;
        }

        // 스킬 사용 로직 코루틴(아군과 동일)으로 진입
        State = BattleState.WaitAction;
        StartCoroutine(ExecuteSkillRoutine(selectedSkill, selectedIndex));
    }

    // -------------------------------------------------------
    // 승패 판정
    // -------------------------------------------------------
    bool IsOver()
    {
        bool playerDead = allCharacters.TrueForAll(c => !c.IsPlayer || !c.IsAlive);
        bool enemyDead = allCharacters.TrueForAll(c => c.IsPlayer || !c.IsAlive);
        return playerDead || enemyDead;
    }

    void OnBattleEnd()
    {
        bool playerDead = allCharacters.TrueForAll(c => !c.IsPlayer || !c.IsAlive);
        State = playerDead ? BattleState.Lose : BattleState.Win;
        battleUI.SetSkillButtonsVisible(false);
    }

}
