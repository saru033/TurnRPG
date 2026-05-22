using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TurnRPG.SkillSystem;
using TurnRPG.SkillSystem.Effects;
using System.Linq;
using UnityEngine.Rendering;
using System;

public class BattleManager : MonoBehaviour
{
    [Header("References")]
    public BattleUI battleUI;
    public GameObject winPanel; // [추가] 최종 승리 시 띄울 패널
    public CharacterPlacer characterPlacer;
    public RectTransform battleBackground; // [추가] 카메라 효과용 배경 RectTransform
    public Image backgroundDisplay;        // [신규] 배경 이미지를 실제 보여줄 Image 컴포넌트
    public StageData currentStage;          // [신규] 현재 도전 중인 스테이지 데이터
    public GameObject mapUIObject;          // [신규] 복귀할 맵 UI 게임 오브젝트

    //처음 전투 진입시 흰 화면을 방지하기 위한 검은 이미지
    public GameObject blackScreen;
    public GameObject invalidTargetNotice; // [추가] 잘못된 대상 선택 시 띄울 문구 오브젝트


    [Header("전투 참가 데이터")]
    [Tooltip("아군은 GameManager에서 가져옵니다. 여기의 리스트는 적군 생성용으로 사용됩니다.")]
    public List<CharacterData> enemyTemplates;

    [Header("디버그/테스트용 적군")]
    [Tooltip("여기에 캐릭터 데이터를 넣으면 강제로 적군으로 생성되어 전투에 참여합니다.")]
    public List<CharacterData> debugEnemyDatas;

    [Header("기본 아이템 (GameManager 없을 때의 fallback)")]
    public List<SkillData> initialItemDatas;
    private List<SkillData> currentItems = new();

    // -------------------------------------------------------
    // 전투 상태
    // -------------------------------------------------------
    public enum BattleState { Idle, PlayerTurn, SelectTarget, EnemyTurn, WaitAction, Win, Lose }
    public BattleState State { get; private set; } = BattleState.Idle;

    public SkillData CurrentSkill { get; set; } // [추가] 현재 실행 중인 스킬/아이템 데이터

    public static BattleManager Instance { get; private set; }

    Actiongaugesystem gaugeSystem;
    public List<BattleCharacter> allCharacters = new();
    public BattleCharacter currentActor;

    public bool isProcessingDualAttack = false; // [추가] 한 턴에 협공이 한 번만 발생하도록 제어하는 플래그
    // [추가] 줌 효과 상태 관리용
    private bool _isCurrentlyZoomed = false;
    private bool _lastZoomedPlayer = false;


    public bool isItemUse = false;

    private LinkedList<IEnumerator> extraActionQueue = new LinkedList<IEnumerator>();
    private bool _isBattleOverFlag = false; // [추가] 중복 종료 방지용 플래그
    private Coroutine _invalidTargetNoticeCoroutine; // [추가] 알림 타이머 제어용

    public void EnqueueExtraAction(IEnumerator action)
    {
        extraActionQueue.AddLast(action);
    }

    public void EnqueueExtraActionFront(IEnumerator action)
    {
        extraActionQueue.AddFirst(action);
    }

    /// <summary>
    /// 하나의 스킬 내에 여러 개의 DamageEffect가 있을 때, 2번째부터는 추가 타격으로 분리하여 실행합니다.
    /// </summary>
    private void ProcessEffectChain(BattleCharacter caster, BattleCharacter target, List<SkillEffect> effects, string skillName)
    {
        // [추가] 신규 효과 체인 실행 전에 모든 캐릭터의 마지막 회피 상태를 초기화하여,
        // 공격이 아닌 스킬이나 아이템 사용 시 과거의 빗나감 판정이 영향을 미치지 않도록 방지
        foreach (var bc in allCharacters)
        {
            if (bc != null) bc.LastReceivedAttackEvaded = false;
        }

        int damageCount = 0;
        foreach (var eff in effects)
        {
            if (eff == null) continue;

            if (eff is DamageEffect dmgEff)
            {
                damageCount++;
                if (damageCount > 1)
                {
                    // 2번째 이후의 데미지 이펙트는 추가 행동 큐에 삽입 (시각적 분리)
                    EnqueueExtraAction(ExecuteExtraDamageRoutine(caster, target, dmgEff, skillName));
                    continue;
                }
            }

            // 조건부 필터(IsCondition) 등은 Execute의 반환값으로 체인 중단 여부 결정
            if (!eff.Execute(caster, target)) break;
        }
    }

    private IEnumerator ExecuteExtraDamageRoutine(BattleCharacter caster, BattleCharacter target, DamageEffect eff, string skillName)
    {
        // [추가] 이전 행동의 연출(애니메이션 및 카메라)이 완전히 끝날 때까지 대기
        float syncTimeout = 3.0f;
        while (syncTimeout > 0)
        {
            bool anyBusy = false;
            foreach (var bc in allCharacters)
            {
                if (bc.IsAlive && (bc.IsAnimationPlaying("attack") || bc.IsAnimationPlaying("hit")))
                {
                    anyBusy = true;
                    break;
                }
            }

            // 캐릭터들이 바쁘지 않고, 카메라가 기본 상태(_isCurrentlyZoomed == false)일 때 시작
            if (!anyBusy && !_isCurrentlyZoomed) break;

            syncTimeout -= Time.deltaTime;
            yield return null;
        }

        if (!caster.IsAlive) yield break;
        if (eff.TargetType == EffectTargetType.Target && (target == null || !target.IsAlive)) yield break;

        Debug.Log($"[ExtraDamage] {caster.Name}의 추가 타격 실행 ({skillName})");

        // 1. 이펙트 및 문구 출력
        if (BattleVFXManager.Instance != null)
        {
            BattleVFXManager.Instance.SpawnVFX(VFXType.extraMove, caster.View.RetHitbox());
            if (caster.View != null) caster.View.ShowPassiveNotice("추가 공격");
        }

        // 2. 시전자 진영 포커싱
        StartCoroutine(SetCameraZoom(caster.IsPlayer, true));
        yield return new WaitForSeconds(0.2f);

        // 3. 공격 애니메이션 (캐릭터의 1번 스킬 애니메이션 재사용)
        var basicSkill = caster.ActiveSkills.Count > 0 ? caster.ActiveSkills[0] : null;
        if (basicSkill != null && !string.IsNullOrEmpty(basicSkill.RequiredAnimationTrigger) && caster.Animator != null)
        {
            _waitingForImpact = true;
            caster.Animator.SetTrigger(basicSkill.RequiredAnimationTrigger);

            // 타격 시점까지 대기
            float timeout = 2.0f;
            while (_waitingForImpact && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            // 타격 시점 피격자 진영 포커싱
            if (target != null && target.IsPlayer != caster.IsPlayer)
            {
                StartCoroutine(SetCameraZoom(target.IsPlayer, true));
            }
        }
        else
        {
            yield return new WaitForSeconds(0.4f);
        }

        // 4. 실제 효과 실행
        eff.Execute(caster, target);

        // [추가] 모든 애니메이션(공격자 및 피격자) 종료 대기
        yield return new WaitForSeconds(0.2f);
        float animTimeout = 2.0f;
        while (animTimeout > 0)
        {
            bool anyBusy = false;
            // 공격자가 아직 애니메이션 중인가?
            if (basicSkill != null && caster.IsAnimationPlaying(basicSkill.RequiredAnimationTrigger))
                anyBusy = true;

            // 피격자들이 아직 Hit 중인가?
            foreach (var bc in allCharacters)
            {
                if (bc.IsAlive && bc.IsAnimationPlaying("hit"))
                {
                    anyBusy = true;
                    break;
                }
            }

            if (!anyBusy) break;
            animTimeout -= Time.deltaTime;
            yield return null;
        }

        // 5. 마무리 대기 및 카메라 복귀
        yield return new WaitForSeconds(0.2f);
        StartCoroutine(SetCameraZoom(true, false)); // 기본 줌으로 복귀
        yield return new WaitForSeconds(0.4f);
    }

    void Awake()
    {
        Instance = this;
    }

    // -------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------
    void OnEnable()
    {
        // 패널이 활성화될 때마다 초기화 코루틴 시작
        StartCoroutine(SetupAndStartBattle());
    }

    IEnumerator SetupAndStartBattle()
    {
        yield return null;   // UI Layout 계산 완료 대기
        yield return StartCoroutine(InitBattle());
    }

    // -------------------------------------------------------
    // 초기화 (나중에 Stage/캐릭터 데이터에서 받아올 부분)
    // -------------------------------------------------------
    IEnumerator InitBattle()
    {
        State = BattleState.Idle;  // 상태 초기화
        _isBattleOverFlag = false; // 플래그 초기화
        isItemUse = false;         // 아이템 사용 상태 초기화
        _selectedItemIndex = -1;   // 선택된 아이템 인덱스 초기화

        if (winPanel != null) winPanel.SetActive(false); // [추가] 승리 패널 숨기기

        // Actiongaugesystem 초기화
        gaugeSystem = GetComponent<Actiongaugesystem>();
        if (gaugeSystem == null) gaugeSystem = gameObject.AddComponent<Actiongaugesystem>();
        gaugeSystem.ResetSystem();

        // [중요] 기존 캐릭터 리스트 비우기
        allCharacters = new List<BattleCharacter>();

        // 1. 아군 캐릭터 생성 (GameManager 우선)
        if (GameManager.Instance != null)
        {
            foreach (var state in GameManager.Instance.party)
            {
                if (state != null && state.template != null)
                {
                    var bc = new BattleCharacter(state);
                    bc.ActionGaugeSystem = gaugeSystem;
                    bc.SubscribeEvents();
                    allCharacters.Add(bc);
                }
            }
        }
        else if (enemyTemplates != null)
        {
            // GameManager가 없는 테스트 환경에서는 플레이어만 골라서 임시 생성
            foreach (var data in enemyTemplates)
            {
                if (data != null && data.isPlayer)
                {
                    var bc = new BattleCharacter(data);
                    bc.ActionGaugeSystem = gaugeSystem;
                    bc.SubscribeEvents();
                    allCharacters.Add(bc);
                }
            }
        }

        // 2. 적군 캐릭터 생성
        // (1) StageData가 있으면 해당 리스트 사용, 없으면 기존 템플릿 사용 (하위 호환)
        List<CharacterData> enemySource = (currentStage != null) ? currentStage.enemies : enemyTemplates;

        if (enemySource != null)
        {
            foreach (var data in enemySource)
            {
                if (data != null && !data.isPlayer)
                {
                    CreateEnemy(data);
                }
            }
        }

        // (2) 디버그용 리스트 활용 (있을 때만 추가 생성)
        if (debugEnemyDatas != null)
        {
            foreach (var data in debugEnemyDatas)
            {
                if (data != null)
                {
                    CreateEnemy(data);
                }
            }
        }

        // --- 2.5 배경 이미지 설정 ---
        if (currentStage != null && currentStage.backgroundSprite != null && backgroundDisplay != null)
        {
            backgroundDisplay.sprite = currentStage.backgroundSprite;
        }

        battleUI.Init(allCharacters);

        // --- 3. 아이템 초기화 (GameManager 우선) ---
        currentItems.Clear();
        if (GameManager.Instance != null && GameManager.Instance.playerItems.Count > 0)
        {
            // GameManager에서 현재 보유 중인 아이템 최대 3개 가져오기
            currentItems.AddRange(GameManager.Instance.playerItems.Take(3));
        }
        else if (initialItemDatas != null)
        {
            currentItems.AddRange(initialItemDatas.Take(3));
        }
        battleUI.RefreshItemSlots(currentItems);

        battleUI.SetSkillButtonsVisible(false);
        battleUI.SetItemVisible(false);
        battleUI.SetSideImageVisible(false, currentActor);


        if (!object.ReferenceEquals(characterPlacer, null))
            characterPlacer.PlaceCharacters(allCharacters);

        // --- [수정] 전투 시작 시 상시 패시브 연출 및 적용 ---
        foreach (var bc in allCharacters)
        {
            // [추가] 초기 행동 게이지 무작위 설정 (0~5 사이)
            bc.ActionGauge = UnityEngine.Random.Range(0f, 10.0f);

            for (int i = 0; i < bc.ActiveSkills.Count; i++)
            {
                var skill = bc.ActiveSkills[i];
                if (skill == null) continue;

                int level = bc.SkillLevels[i];
                if (skill.LevelDatas == null || skill.LevelDatas.Count < level) continue;

                var levelData = skill.LevelDatas[level - 1];
                if (levelData.ConstantEffects == null || levelData.ConstantEffects.Count == 0) continue;

                // 연출(애니메이션/컷신)이 있는 경우 큐에 추가
                if (!string.IsNullOrEmpty(skill.RequiredAnimationTrigger) || skill.UltimateCutsceneClip != null)
                {
                    EnqueueExtraAction(ExecuteConstantPassivesRoutine(bc, skill, level));
                }
                else
                {
                    // 연출이 없으면 즉시 실행
                    foreach (var eff in levelData.ConstantEffects)
                    {
                        if (eff != null)
                        {
                            // [추가] 패시브 이름 표시
                            if (bc.View != null) bc.View.ShowPassiveNotice(skill.SkillName);

                            Debug.Log($"[Passive-Silent] {bc.Name}의 {skill.SkillName} 상시 효과 즉시 적용");
                            eff.Execute(bc, bc);
                        }
                    }
                    bc.RefreshStats();
                }
            }
        }

        // 큐에 쌓인 패시브 연출들을 순차적으로 모두 실행
        if (extraActionQueue.Count > 0)
        {
            blackScreen.SetActive(false); // [추가] 연출 시작 전 검은 화면 제거 (문구가 보이도록)
            while (extraActionQueue.Count > 0)
            {
                var action = extraActionQueue.First.Value;
                extraActionQueue.RemoveFirst();
                yield return StartCoroutine(action);
            }
        }
        else
        {
            blackScreen.SetActive(false);
        }

        State = BattleState.Idle;

        // [추가] 전투 튜토리얼 진행 (완료할 때까지 대기)
        if (TutorialPanelUI.Instance != null)
        {
            bool isWaitingTutorial = true;
            TutorialPanelUI.Instance.StartTutorial(TutorialType.Battle, () => isWaitingTutorial = false);
            while (isWaitingTutorial) yield return null;
        }

        StartCoroutine(TurnLoop());
    }

    /// <summary>
    /// 적군 캐릭터 인스턴스를 생성하고 리스트에 추가합니다.
    /// </summary>
    private void CreateEnemy(CharacterData data)
    {
        var bc = new BattleCharacter(data);
        bc.IsPlayer = false; // 강제로 적군으로 설정 (디버그용 대비)
        bc.ActionGaugeSystem = gaugeSystem;
        bc.SubscribeEvents();
        allCharacters.Add(bc);
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

            // [추가] 턴 획득 연출: 100% 지점까지 이동 후 아이콘 숨기기
            currentActor.ActionGauge = Actiongaugesystem.MaxGauge;
            battleUI.UpdateGaugePositions(allCharacters);
            yield return new WaitForSeconds(0.2f);
            battleUI.SetVisible(currentActor, false);

            // [추가] 턴 시작 시 협공 처리 플래그 리셋 (한 턴에 최대 한 번만 발생하도록)
            isProcessingDualAttack = false; // [추가] 턴 시작 시 협공 플래그 리셋
            currentActor.OnTurnStart();

            // 턴 시작시 행동게이지 초기화 (여기서 실제 값 0으로 리셋)
            gaugeSystem.OnTurnStart(currentActor);

            // [추가] 턴 시작 시 발동된 패시브 처리
            yield return StartCoroutine(ProcessExtraActions());

            // [추가] 기절(Stun) 또는 수면(Sleep) 체크
            bool isSkipTurn = currentActor.HasStatusEffect(TurnRPG.SkillSystem.StatusEffectType.Stun) || currentActor.HasStatusEffect(TurnRPG.SkillSystem.StatusEffectType.Sleep);
            if (isSkipTurn)
            {
                Debug.Log($"{currentActor.Name} : 기절/수면 상태로 인해 턴을 스킵합니다.");

                // [추가] 행동 불가 알림 표시
                if (currentActor.View != null) currentActor.View.ShowPassiveNotice("행동불가");

                yield return new WaitForSeconds(0.8f); // 문구를 읽을 시간 추가
                currentActor.OnTurnEnd();

                // [추가] 스킵 시 발생한 턴 종료 패시브 처리
                yield return StartCoroutine(ProcessExtraActions());

                gaugeSystem.OnTurnEnd(currentActor);
                if (currentActor.IsAlive) battleUI.SetVisible(currentActor, true);
                continue;
            }

            // 만약 출혈/화상 등 턴 시작 데미지 혹은 패시브로 사망했다면 턴 즉시 스킵
            if (!currentActor.IsAlive)
            {
                currentActor.OnTurnEnd();
                yield return StartCoroutine(ProcessExtraActions());
                gaugeSystem.OnTurnEnd(currentActor);
                if (currentActor.IsAlive) battleUI.SetVisible(currentActor, true);
                continue;
            }

            battleUI.UpdateGaugePositions(allCharacters);


            // 짧은 연출 딜레이
            yield return new WaitForSeconds(0.3f);

            if (currentActor.IsPlayer)
            {
                // 2. 아군 턴 — 플레이어 입력 대기
                if (currentActor.Animator != null) currentActor.Animator.SetBool("isWaiting", true);

                State = BattleState.PlayerTurn;
                battleUI.SetSkillButtonsVisible(true, currentActor);
                battleUI.SetItemVisible(true);
                battleUI.SetSideImageVisible(true, currentActor);

                // [추가] 플레이어 턴 시작 시 1번 스킬(기본 공격)을 자동으로 선택
                OnSkillSelected(0);

                // OnSkillSelected() 및 타겟 지정 완료 시점까지 대기 (SelectTarget 상태도 포함해 대기)
                yield return new WaitUntil(() => State != BattleState.PlayerTurn && State != BattleState.SelectTarget);

                if (currentActor.Animator != null) currentActor.Animator.SetBool("isWaiting", false);

                battleUI.SetSkillButtonsVisible(false);
                battleUI.SetItemVisible(false);

                // 스킬 코루틴 등 애니메이션 대기
                yield return new WaitUntil(() => State == BattleState.Idle || State == BattleState.Win || State == BattleState.Lose);
            }
            else
            {
                // 3. 적 턴 — 자동 행동
                State = BattleState.EnemyTurn;
                battleUI.SetSkillButtonsVisible(false);
                battleUI.SetItemVisible(false);
                battleUI.SetSideImageVisible(true, currentActor);

                yield return new WaitForSeconds(0.5f);   // 적 행동 연출 딜레이
                EnemyAct(currentActor);

                // 적 스킬 사용(코루틴) 대기
                yield return new WaitUntil(() => State == BattleState.Idle || State == BattleState.Win || State == BattleState.Lose);
            }

            // [수정] 승패가 결정되었더라도 마지막 행동 캐릭터의 턴 종료 처리를 위해 여기서 break 하지 않음

            // [수정] 메인 행동 후 발생한 패시브(반격 등) 처리
            yield return StartCoroutine(ProcessExtraActions());

            // 4. 턴 종료 — 버프 지속시간 차감
            currentActor.OnTurnEnd();

            // [추가] 턴 종료 시 발동된 패시브 처리 (자신의 턴 종료 패시브 등)
            yield return StartCoroutine(ProcessExtraActions());

            battleUI.SetSideImageVisible(false, currentActor);
            if (currentActor.Animator != null) currentActor.Animator.SetBool("isWaiting", false); // 안전장치
            ResetAllCharactersDim(); // 턴 종료 시에도 확실히 복구

            gaugeSystem.OnTurnEnd(currentActor);
            if (currentActor.IsAlive) battleUI.SetVisible(currentActor, true);
            battleUI.UpdateGaugePositions(allCharacters);

            // [추가] 턴 정리가 완료된 후 승패 여부에 따라 루프 탈출
            if (State == BattleState.Win || State == BattleState.Lose) break;


            yield return new WaitForSeconds(0.2f);
        }

        OnBattleEnd();
    }

    /// <summary>
    /// 추가 액션 큐(패시브 연출 등)를 모두 비울 때까지 실행합니다.
    /// </summary>
    private IEnumerator ProcessExtraActions()
    {
        while (extraActionQueue.Count > 0)
        {
            var action = extraActionQueue.First.Value;
            extraActionQueue.RemoveFirst();


            yield return StartCoroutine(action);
        }
    }

    // -------------------------------------------------------
    // 아군 스킬 입력 (BattleUI 버튼에서 호출)
    // -------------------------------------------------------

    /// <summary>
    /// 스킬 버튼 클릭 시 BattleUI가 호출.
    /// skillIndex: 0 = 1스킬, 1 = 2스킬, 2 = 3스킬
    /// </summary>
    private int _selectedSkillIndex = -1;
    private int _selectedItemIndex = -1;
    private bool _waitingForImpact = false;

    public void OnAnimationImpact()
    {
        _waitingForImpact = false;
    }

    public void OnSkillSelected(int skillIndex)
    {
        // [추가] 툴팁 확인 중(롱프레스)인 경우 클릭 선택 무시
        if (GameManager.Instance != null && GameManager.Instance.IsTooltipPerforming) return;

        // 타겟 선택 모드로 전환 (PlayerTurn -> SelectTarget)
        if (State != BattleState.PlayerTurn && State != BattleState.SelectTarget) return;

        var skillData = currentActor.ActiveSkills.Count > skillIndex ? currentActor.ActiveSkills[skillIndex] : null;
        if (skillData == null) return;

        // [추가] 패시브 스킬은 수동 선택 불가
        if (skillData.Type == SkillType.Passive)
        {
            Debug.Log($"[Battle] {skillData.SkillName}은 패시브 스킬이므로 선택할 수 없습니다.");
            return;
        }

        if (currentActor.SkillCooldowns.Length > skillIndex && currentActor.SkillCooldowns[skillIndex] > 0)
        {
            Debug.LogWarning($"[Battle] {currentActor.Name}의 {skillIndex + 1}번 스킬은 쿨타임 중입니다.");
            return;
        }

        State = BattleState.SelectTarget;
        _selectedSkillIndex = skillIndex;
        _selectedItemIndex = -1; // 아이템 선택 해제

        battleUI.MoveSkillSelectIndicator(skillIndex);
        UpdateTargetDimming(skillData);
        HideInvalidTargetNotice(); // 스킬 변경 시 문구 숨기기
        Debug.Log($"[Battle] {skillData.SkillName} 선택됨! 대상을 클릭해주세요.");
    }

    private void UpdateTargetDimming(SkillData skill)
    {
        if (skill == null)
        {
            ResetAllCharactersDim();
            return;
        }

        // 스킬의 레벨 데이터를 가져와 타겟 타입을 확인 (1번 레벨 기준)
        var levelData = skill.LevelDatas != null && skill.LevelDatas.Count > 0 ? skill.LevelDatas[0] : null;
        if (levelData == null) return;

        bool isTargetingEnemy = false;
        switch (levelData.TargetType)
        {
            case SkillTargetType.SingleEnemy:
            case SkillTargetType.RandomEnemy:
            case SkillTargetType.AllEnemies:
                isTargetingEnemy = true;
                break;
            case SkillTargetType.SingleAlly:
            case SkillTargetType.AllAllies:
            case SkillTargetType.Self:
                isTargetingEnemy = false;
                break;
        }

        foreach (var bc in allCharacters)
        {
            if (bc.View == null) continue;

            // [추가] 현재 턴을 잡은 주인공(currentActor)은 타겟 여부와 관계없이 항상 밝게 유지
            if (bc == currentActor)
            {
                bc.View.SetDim(false);
                continue;
            }

            if (isTargetingEnemy)
            {
                if (bc.IsPlayer)
                {
                    // 적군 타겟인 경우 아군은 무조건 어둡게
                    bc.View.SetDim(true);
                }
                else
                {
                    // 적군 타겟인 경우: 은신 등 타겟팅 가능 여부에 따라 Dim 결정
                    // (은신 중이라도 모든 적이 은신이면 CanBeTargetedBy가 true를 반환함)
                    bool canTarget = bc.CanBeTargetedBy(currentActor, allCharacters);
                    bc.View.SetDim(!canTarget);
                }
            }
            else
            {
                // 아군 타겟인 경우 적군은 무조건 어둡게, 아군은 밝게
                bc.View.SetDim(!bc.IsPlayer);
            }
        }
    }

    private void ResetAllCharactersDim()
    {
        foreach (var bc in allCharacters)
        {
            if (bc.View != null) bc.View.SetDim(false);
        }
    }

    private void ShowInvalidTargetNotice()
    {
        if (invalidTargetNotice == null) return;

        if (_invalidTargetNoticeCoroutine != null) StopCoroutine(_invalidTargetNoticeCoroutine);
        _invalidTargetNoticeCoroutine = StartCoroutine(InvalidTargetNoticeRoutine());
    }

    private void HideInvalidTargetNotice()
    {
        if (invalidTargetNotice == null) return;
        if (_invalidTargetNoticeCoroutine != null)
        {
            StopCoroutine(_invalidTargetNoticeCoroutine);
            _invalidTargetNoticeCoroutine = null;
        }
        invalidTargetNotice.SetActive(false);
    }

    private IEnumerator InvalidTargetNoticeRoutine()
    {
        invalidTargetNotice.SetActive(true);
        yield return new WaitForSeconds(1.0f);
        invalidTargetNotice.SetActive(false);
        _invalidTargetNoticeCoroutine = null;
    }

    public void OnItemSelected(int itemIndex)
    {
        // [추가] 툴팁 확인 중(롱프레스)인 경우 클릭 선택 무시
        if (GameManager.Instance != null && GameManager.Instance.IsTooltipPerforming) return;

        if (isItemUse || (State != BattleState.PlayerTurn && State != BattleState.SelectTarget)) return;

        if (currentItems.Count <= itemIndex || currentItems[itemIndex] == null) return;

        State = BattleState.SelectTarget;
        _selectedItemIndex = itemIndex;
        _selectedSkillIndex = -1; // 스킬 선택 해제

        battleUI.MoveItemSelectIndicator(itemIndex);
        UpdateTargetDimming(currentItems[itemIndex]); // 아이템 타겟에 맞춰 디밍 처리
        HideInvalidTargetNotice(); // 아이템 변경 시 문구 숨기기
        Debug.Log($"[Battle] 아이템 {currentItems[itemIndex].SkillName} 선택됨! 대상을 클릭해주세요.");
    }

    // UI에서 직접 호출하는 대신 CharacterView 클릭으로 실행
    public void OnCharacterClicked(BattleCharacter target)
    {
        if (State != BattleState.SelectTarget) return;

        SkillData skillData = null;
        SkillLevelData levelData = null;
        bool isItem = false;

        // --- 1. 선택된 것이 아이템인지 스킬인지 판별 및 데이터 로드 ---
        if (_selectedItemIndex != -1)
        {
            isItem = true;
            skillData = currentItems.Count > _selectedItemIndex ? currentItems[_selectedItemIndex] : null;
            if (skillData != null && skillData.LevelDatas != null && skillData.LevelDatas.Count > 0)
            {
                levelData = skillData.LevelDatas[0];
            }
        }
        else if (_selectedSkillIndex != -1)
        {
            skillData = currentActor.ActiveSkills.Count > _selectedSkillIndex ? currentActor.ActiveSkills[_selectedSkillIndex] : null;
            if (skillData != null)
            {
                int level = currentActor.SkillLevels[_selectedSkillIndex];
                levelData = skillData.LevelDatas != null && skillData.LevelDatas.Count >= level ? skillData.LevelDatas[level - 1] : null;
            }
        }

        if (skillData == null || levelData == null) return;

        // --- 2. 타겟 유효성 검증 (아이템/스킬 공통) ---
        bool isValid = false;
        switch (levelData.TargetType)
        {
            case SkillTargetType.SingleEnemy:
            case SkillTargetType.RandomEnemy:
            case SkillTargetType.AllEnemies:
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
            ShowInvalidTargetNotice(); // [추가] 잘못된 대상 알림 표시
            return;
        }

        // --- 3. 은신(Stealth) 타겟팅 검사 ---
        if (!target.CanBeTargetedBy(currentActor, allCharacters))
        {
            Debug.Log($"[Battle] {target.Name}은(는) 은신 중이라 타겟으로 지정할 수 없습니다!");
            ShowInvalidTargetNotice(); // 은신 타겟도 유효하지 않은 대상으로 처리
            return;
        }

        // --- 4. 실행 결정 ---
        HideInvalidTargetNotice(); // 올바른 대상 선택 시 문구 숨기기
        battleUI.select_SkillRect.gameObject.SetActive(false); // 인디케이터 숨기기
        ResetAllCharactersDim(); // 타겟 클릭 시 즉시 원래 색상 복구

        if (isItem)
        {
            _selectedItemIndex = -1;
            StartCoroutine(ExecuteItemRoutine(currentActor, target, skillData));
        }
        else
        {
            int executionIndex = _selectedSkillIndex;
            _selectedSkillIndex = -1;
            State = BattleState.WaitAction;
            StartCoroutine(ExecuteSkillRoutine(skillData, executionIndex, target));
        }
    }

    // 기존의 OnSkillExecute는 더블클릭 대비용으로 혹시 모르니 남겨두지만 무용지물이 됨
    public void OnSkillExecute(int skillIndex)
    {
        // OnCharacterClicked 으로 일원화되어 사용하지 않음.
    }


    public IEnumerator ExecuteItemRoutine(BattleCharacter character, BattleCharacter target, SkillData skill)
    {
        CurrentSkill = skill; // [추가] 현재 아이템 스킬 저장
        //스킬창 숨기고 사용 불가능하게
        isItemUse = true;
        battleUI.SetSkillButtonsVisible(false);
        battleUI.SetItemVisible(false);

        Debug.Log($"[Item] {character.Name}이 {target.Name} 에게 {skill.SkillName} 아이템 사용");

        var levelData = (skill.LevelDatas != null && skill.LevelDatas.Count > 0)
                        ? skill.LevelDatas[0] : null;

        bool isUltimate = (int)skill.SlotIndex == 2;

        // --- 1. 카메라 줌 및 캐릭터 하이라이트 ---
        if (!isUltimate && !string.IsNullOrEmpty(skill.RequiredAnimationTrigger) && character.Animator != null)
        {
            StartCoroutine(SetCameraZoom(character.IsPlayer, true));
            yield return new WaitForSeconds(0.2f);
        }
        // --- 스킬 아이콘 제거 및, 터치 방지---
        //battleUI.SetSideImageVisible(true, character);

        // --- 2. 컷신 재생 (있는 경우) ---
        if (skill.UltimateCutsceneClip != null && battleUI.ultimateCutsceneRoot != null)
        {
            // [ExecuteSkillRoutine의 컷신 로직 재사용]
            Transform originalParent = null;
            Vector2 originalAnchoredPos = Vector2.zero;
            Vector3 originalScale = Vector3.one;
            bool enhancedSequence = false;

            if (character.View != null && character.View.illustration != null && battleUI.ultimateBlackScreen != null)
            {
                enhancedSequence = true;
                var illu = character.View.illustration.rectTransform;
                originalParent = illu.parent;
                originalAnchoredPos = illu.anchoredPosition;
                originalScale = illu.localScale;

                battleUI.ultimateBlackScreen.gameObject.SetActive(true);
                battleUI.ultimateBlackScreen.color = new Color(0, 0, 0, 0);

                illu.SetParent(battleUI.ultimateBlackScreen.transform, true);
                Vector2 targetPos = new Vector2(0, illu.anchoredPosition.y);

                Sequence seq = DOTween.Sequence();
                seq.Join(battleUI.ultimateBlackScreen.DOFade(1f, 0.4f));
                seq.Join(illu.DOAnchorPos(targetPos, 0.4f));
                seq.Join(illu.DOScale(originalScale * 1.2f, 0.4f));

                yield return seq.WaitForCompletion();
            }

            battleUI.ultimateCutsceneRoot.SetActive(true);
            var anim = battleUI.ultimateCutsceneRoot.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                // [추가] 궁극기 보이스 재생
                if (SoundManager.Instance != null && skill.UltimateVoiceClip != null)
                {
                    SoundManager.Instance.PlayVoice(skill.UltimateVoiceClip);
                }

                anim.Play(skill.UltimateCutsceneClip.name);
                if (enhancedSequence)
                {
                    yield return new WaitForSeconds(0.15f);
                    if (character.View != null)
                    {
                        var illu = character.View.illustration.rectTransform;
                        illu.SetParent(originalParent, true);
                        illu.anchoredPosition = originalAnchoredPos;
                        illu.localScale = originalScale;
                        battleUI.ultimateBlackScreen.gameObject.SetActive(false);
                    }
                    yield return new WaitForSeconds(Mathf.Max(0, skill.UltimateCutsceneClip.length - 0.15f));
                }
                else
                {
                    yield return new WaitForSeconds(skill.UltimateCutsceneClip.length);
                }
            }
            battleUI.ultimateCutsceneRoot.SetActive(false);
        }

        // --- 3. 애니메이션 재생 및 타격 시점 대기 ---
        if (!string.IsNullOrEmpty(skill.RequiredAnimationTrigger) && character.Animator != null)
        {
            _waitingForImpact = true;
            character.Animator.SetTrigger(skill.RequiredAnimationTrigger);

            yield return new WaitForSeconds(0.1f);

            var stateInfo = character.Animator.GetCurrentAnimatorStateInfo(0);
            float maxWait = stateInfo.length * 0.8f;
            float timer = 0f;

            while (_waitingForImpact && timer < maxWait)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        // --- 4. 실제 효과 실행 ---
        if (levelData != null && levelData.Effects != null)
        {
            ProcessEffectChain(character, target, levelData.Effects, skill.SkillName);
            character.RefreshStats();
        }

        // --- 5. 애니메이션 종료 대기 및 복귀 ---
        yield return new WaitForSeconds(0.3f);

        float timeout = 2.0f;
        while (timeout > 0)
        {
            bool anyBusy = false;
            if (!string.IsNullOrEmpty(skill.RequiredAnimationTrigger))
                if (character.IsAnimationPlaying(skill.RequiredAnimationTrigger))
                    anyBusy = true;

            if (!anyBusy) break;
            timeout -= Time.deltaTime;
            yield return null;
        }

        battleUI.SetSideImageVisible(false, character);
        if (!isUltimate)
        {
            StartCoroutine(SetCameraZoom(true, false));
            yield return new WaitForSeconds(0.4f);
        }

        Debug.Log($"[Item] {character.Name} 아이템 사용 연출 종료");

        // --- 아이템 소모 및 UI 갱신 ---
        if (currentItems.Contains(skill))
        {
            currentItems.Remove(skill);
            // [추가] GameManager의 실제 인벤토리에서도 삭제 (영구 소모)
            if (GameManager.Instance != null && GameManager.Instance.playerItems.Contains(skill))
            {
                GameManager.Instance.playerItems.Remove(skill);
            }
        }
        battleUI.RefreshItemSlots(currentItems);

        // [추가] 아이템 사용 후 전투 승패 여부 체크
        if (IsOver())
        {
            // 직접 OnBattleEnd를 호출하지 않고 상태만 변경하여 턴 루프가 정리하도록 유도
            bool playerDead = allCharacters.TrueForAll(c => !c.IsPlayer || !c.IsAlive);
            State = playerDead ? BattleState.Lose : BattleState.Win;
            yield break;
        }

        //스킬창 복구
        isItemUse = false;
        battleUI.SetSkillButtonsVisible(true, currentActor);
        battleUI.SetItemVisible(true);
        battleUI.SetSideImageVisible(true, currentActor);
    }





    public IEnumerator CounterAttackRoutine(BattleCharacter attacker, BattleCharacter target)
    {
        if (!attacker.IsAlive || !target.IsAlive) yield break;

        Debug.Log($"[Battle] {attacker.Name} → {target.Name} 반격 시작");

        var skillData = attacker.ActiveSkills.Count > 0 ? attacker.ActiveSkills[0] : null;
        if (skillData == null) yield break;

        CurrentSkill = skillData; // [추가] 현재 반격 스킬 저장

        int skillIndex = 0;
        int level = attacker.SkillLevels.Length > skillIndex ? attacker.SkillLevels[skillIndex] : 1;
        var levelData = skillData.LevelDatas != null && skillData.LevelDatas.Count >= level
            ? skillData.LevelDatas[level - 1] : null;

        // --- 애니메이션 재생 및 타격 시점 대기 ---
        if (!string.IsNullOrEmpty(skillData.RequiredAnimationTrigger) && attacker.Animator != null)
        {
            yield return new WaitForSeconds(0.4f);

            // idle 상태까지 대기
            float idleTimeout = 3.0f;
            while (idleTimeout > 0)
            {
                var currentState = target.Animator.GetCurrentAnimatorStateInfo(0);
                if (!currentState.IsName("attack") && !currentState.IsTag("hit"))
                    break;

                idleTimeout -= Time.deltaTime;
                yield return null;
            }

            StartCoroutine(SetCameraZoom(attacker.IsPlayer, true));


            // 반격 이펙트 및 문구 출력
            if (BattleVFXManager.Instance != null)
            {
                BattleVFXManager.Instance.SpawnVFX(VFXType.extraMove, attacker.View.RetHitbox());
                if (attacker.View != null) attacker.View.ShowPassiveNotice("반격");
            }


            _waitingForImpact = true;
            attacker.Animator.SetTrigger(skillData.RequiredAnimationTrigger);

            yield return new WaitForSeconds(0.1f);

            var stateInfo = attacker.Animator.GetCurrentAnimatorStateInfo(0);
            float maxWait = stateInfo.length * 0.8f;
            float timer = 0f;

            while (_waitingForImpact && timer < maxWait)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // 피격자 진영 포커싱
            if (target != null && target.IsPlayer != attacker.IsPlayer)
            {
                StartCoroutine(SetCameraZoom(target.IsPlayer, true));
            }
        }
        else
        {
            yield return new WaitForSeconds(0.4f);
        }

        // --- 실제 효과 실행 ---
        if (levelData != null)
        {
            if (levelData.Effects != null)
            {
                ProcessEffectChain(attacker, target, levelData.Effects, skillData.SkillName);
            }
        }

        BattleEventManager.TriggerSkillUsed(attacker, skillData);

        // --- 4. 애니메이션 종료 대기 ---
        yield return new WaitForSeconds(0.2f);

        float timeout = 2.0f;
        while (timeout > 0)
        {
            bool anyBusy = false;

            if (!string.IsNullOrEmpty(skillData.RequiredAnimationTrigger))
                if (attacker.IsAnimationPlaying(skillData.RequiredAnimationTrigger))
                    anyBusy = true;

            foreach (var bc in allCharacters)
            {
                if (bc.IsAlive && bc.IsAnimationPlaying("hit"))
                {
                    anyBusy = true;
                    break;
                }
            }

            if (!anyBusy) break;

            timeout -= Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.2f);

        // --- 5. 카메라 복귀 ---
        StartCoroutine(SetCameraZoom(true, false));
        yield return new WaitForSeconds(0.4f);


    }

    ///협공///
    public IEnumerator CombinationAttackRoutine(BattleCharacter attacker, BattleCharacter target)
    {
        if (!attacker.IsAlive || !target.IsAlive) yield break;

        Debug.Log($"[Battle] {attacker.Name} → {target.Name} 협공 시작");

        var skillData = attacker.ActiveSkills.Count > 0 ? attacker.ActiveSkills[0] : null;
        if (skillData == null) yield break;

        CurrentSkill = skillData; // [추가] 현재 협공 스킬 저장

        int skillIndex = 0;
        int level = attacker.SkillLevels.Length > skillIndex ? attacker.SkillLevels[skillIndex] : 1;
        var levelData = skillData.LevelDatas != null && skillData.LevelDatas.Count >= level
            ? skillData.LevelDatas[level - 1] : null;

        // --- 애니메이션 재생 및 타격 시점 대기 ---
        if (!string.IsNullOrEmpty(skillData.RequiredAnimationTrigger) && attacker.Animator != null)
        {
            yield return new WaitForSeconds(0.4f);

            // idle 상태까지 대기
            float idleTimeout = 3.0f;
            while (idleTimeout > 0)
            {
                var currentState = target.Animator.GetCurrentAnimatorStateInfo(0);
                if (!currentState.IsName("attack") && !currentState.IsTag("hit"))
                    break;

                idleTimeout -= Time.deltaTime;
                yield return null;
            }

            StartCoroutine(SetCameraZoom(attacker.IsPlayer, true));


            // 반격 이펙트 및 문구 출력
            if (BattleVFXManager.Instance != null)
            {
                BattleVFXManager.Instance.SpawnVFX(VFXType.extraMove, attacker.View.RetHitbox());
                if (attacker.View != null) attacker.View.ShowPassiveNotice("협공");
            }


            _waitingForImpact = true;
            attacker.Animator.SetTrigger(skillData.RequiredAnimationTrigger);

            yield return new WaitForSeconds(0.1f);

            var stateInfo = attacker.Animator.GetCurrentAnimatorStateInfo(0);
            float maxWait = stateInfo.length * 0.8f;
            float timer = 0f;

            while (_waitingForImpact && timer < maxWait)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // 피격자 진영 포커싱
            if (target != null && target.IsPlayer != attacker.IsPlayer)
            {
                StartCoroutine(SetCameraZoom(target.IsPlayer, true));
            }
        }
        else
        {
            yield return new WaitForSeconds(0.4f);
        }

        // --- 실제 효과 실행 ---
        if (levelData != null)
        {
            if (levelData.Effects != null)
            {
                ProcessEffectChain(attacker, target, levelData.Effects, skillData.SkillName);
            }
        }

        BattleEventManager.TriggerSkillUsed(attacker, skillData);

        // --- 4. 애니메이션 종료 대기 ---
        yield return new WaitForSeconds(0.2f);

        float timeout = 2.0f;
        while (timeout > 0)
        {
            bool anyBusy = false;

            if (!string.IsNullOrEmpty(skillData.RequiredAnimationTrigger))
                if (attacker.IsAnimationPlaying(skillData.RequiredAnimationTrigger))
                    anyBusy = true;

            foreach (var bc in allCharacters)
            {
                if (bc.IsAlive && bc.IsAnimationPlaying("hit"))
                {
                    anyBusy = true;
                    break;
                }
            }

            if (!anyBusy) break;

            timeout -= Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.2f);

        // --- 5. 카메라 복귀 ---
        StartCoroutine(SetCameraZoom(true, false));
        yield return new WaitForSeconds(0.4f);


    }





    /// <summary>
    /// 전투 시작 시 연출이 포함된 상시 패시브를 실행하는 루틴입니다.
    /// </summary>
    public IEnumerator ExecuteConstantPassivesRoutine(BattleCharacter character, SkillData skill, int level)
    {
        //캐릭터가 죽어 있다면 스킵
        if (!character.IsAlive) yield break;

        CurrentSkill = skill; // [추가] 현재 상시 패시브 스킬 저장



        Debug.Log($"[Passive-Show] {character.Name} → {skill.SkillName} 패시브 연출 시작");

        // [추가] 패시브 이름 표시
        if (character.View != null) character.View.ShowPassiveNotice(skill.SkillName);

        var levelData = skill.LevelDatas != null && skill.LevelDatas.Count >= level
                        ? skill.LevelDatas[level - 1] : null;

        bool isUltimate = (int)skill.SlotIndex == 2;

        // --- 1. 카메라 줌 및 캐릭터 하이라이트 ---
        if (!isUltimate && !string.IsNullOrEmpty(skill.RequiredAnimationTrigger) && character.Animator != null)
        {
            StartCoroutine(SetCameraZoom(character.IsPlayer, true));
            yield return new WaitForSeconds(0.2f);
        }
        //battleUI.HighlightActor(character);
        //battleUI.SetSideImageVisible(true, character);

        // --- 2. 컷신 재생 (있는 경우) ---
        if (skill.UltimateCutsceneClip != null && battleUI.ultimateCutsceneRoot != null)
        {
            // [ExecuteSkillRoutine의 컷신 로직 재사용]
            Transform originalParent = null;
            Vector2 originalAnchoredPos = Vector2.zero;
            Vector3 originalScale = Vector3.one;
            bool enhancedSequence = false;

            if (character.View != null && character.View.illustration != null && battleUI.ultimateBlackScreen != null)
            {
                enhancedSequence = true;
                var illu = character.View.illustration.rectTransform;
                originalParent = illu.parent;
                originalAnchoredPos = illu.anchoredPosition;
                originalScale = illu.localScale;

                battleUI.ultimateBlackScreen.gameObject.SetActive(true);
                battleUI.ultimateBlackScreen.color = new Color(0, 0, 0, 0);

                illu.SetParent(battleUI.ultimateBlackScreen.transform, true);
                Vector2 targetPos = new Vector2(0, illu.anchoredPosition.y);

                Sequence seq = DOTween.Sequence();
                seq.Join(battleUI.ultimateBlackScreen.DOFade(1f, 0.4f));
                seq.Join(illu.DOAnchorPos(targetPos, 0.4f));
                seq.Join(illu.DOScale(originalScale * 1.2f, 0.4f));

                yield return seq.WaitForCompletion();
            }

            battleUI.ultimateCutsceneRoot.SetActive(true);
            var anim = battleUI.ultimateCutsceneRoot.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                // [추가] 궁극기 보이스 재생
                if (SoundManager.Instance != null && skill.UltimateVoiceClip != null)
                {
                    SoundManager.Instance.PlayVoice(skill.UltimateVoiceClip);
                }

                anim.Play(skill.UltimateCutsceneClip.name);
                if (enhancedSequence)
                {
                    yield return new WaitForSeconds(0.15f);
                    if (character.View != null)
                    {
                        var illu = character.View.illustration.rectTransform;
                        illu.SetParent(originalParent, true);
                        illu.anchoredPosition = originalAnchoredPos;
                        illu.localScale = originalScale;
                        battleUI.ultimateBlackScreen.gameObject.SetActive(false);
                    }
                    yield return new WaitForSeconds(Mathf.Max(0, skill.UltimateCutsceneClip.length - 0.15f));
                }
                else
                {
                    yield return new WaitForSeconds(skill.UltimateCutsceneClip.length);
                }
            }
            battleUI.ultimateCutsceneRoot.SetActive(false);
        }

        // --- 3. 애니메이션 재생 및 타격 시점 대기 ---
        if (!string.IsNullOrEmpty(skill.RequiredAnimationTrigger) && character.Animator != null)
        {
            _waitingForImpact = true;
            character.Animator.SetTrigger(skill.RequiredAnimationTrigger);

            yield return new WaitForSeconds(0.1f);

            var stateInfo = character.Animator.GetCurrentAnimatorStateInfo(0);
            float maxWait = stateInfo.length * 0.8f;
            float timer = 0f;

            while (_waitingForImpact && timer < maxWait)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        // --- 4. 실제 효과 실행 ---
        if (levelData != null && levelData.ConstantEffects != null)
        {
            ProcessEffectChain(character, character, levelData.ConstantEffects, skill.SkillName);
            character.RefreshStats();
        }

        // --- 5. 애니메이션 종료 대기 및 복귀 ---
        yield return new WaitForSeconds(0.3f);

        float timeout = 2.0f;
        while (timeout > 0)
        {
            bool anyBusy = false;
            if (!string.IsNullOrEmpty(skill.RequiredAnimationTrigger))
                if (character.IsAnimationPlaying(skill.RequiredAnimationTrigger))
                    anyBusy = true;

            if (!anyBusy) break;
            timeout -= Time.deltaTime;
            yield return null;
        }

        battleUI.SetSideImageVisible(false, character);
        if (!isUltimate)
        {
            StartCoroutine(SetCameraZoom(true, false));
            yield return new WaitForSeconds(0.4f);
        }

        Debug.Log($"[Passive-Show] {character.Name} 패시브 연출 종료");
    }

    /// <summary>
    /// 전투 중 발생하는 반응형 패시브를 실행하는 루틴입니다. (연출 포함)
    /// </summary>
    public IEnumerator ExecuteReactivePassiveRoutine(BattleCharacter character, SkillData skill, int level, BattleCharacter target, BattleCharacter victim, PassiveTriggerType trigger)
    {
        //attker와 target이 죽어 있다면 스킵
        if (!character.IsAlive) yield break;



        // [추가] 큐에서 막 꺼냈을 때, 다시 한 번 쿨타임을 검사 (광역 공격 등에 의한 중복 발동 방지)
        int checkIndex = character.ActiveSkills.IndexOf(skill);
        if (checkIndex >= 0 && character.SkillCooldowns[checkIndex] > 0)
        {
            Debug.Log($"[Passive-Skip] {character.Name}의 {skill.SkillName}는 이미 최근 발동으로 인해 쿨타임 중입니다. 실행을 취소합니다.");
            yield break;
        }

        //죽어있고 , 기절 , 수면 상태면 패시브 발동 취소
        if (!character.IsAlive || character.HasStatusEffect(StatusEffectType.Stun) || character.HasStatusEffect(StatusEffectType.Sleep))
        { yield break; }

        CurrentSkill = skill; // [추가] 현재 패시브 스킬 저장


        Debug.Log($"[Passive-Show] {character.Name} → {skill.SkillName} 패시브 발동 연출 시작");

        // [추가] 패시브 이름 표시
        if (character.View != null) character.View.ShowPassiveNotice(skill.SkillName);

        var levelData = skill.LevelDatas != null && skill.LevelDatas.Count >= level
                        ? skill.LevelDatas[level - 1] : null;

        // [추가] 첫 번째 효과가 '조건 체크(IsCondition)'인 경우, 연출과 쿨타임 돌입 전 미리 검사
        if (levelData != null && levelData.Effects != null && levelData.Effects.Count > 0)
        {
            var firstEff = levelData.Effects[0];
            if (firstEff != null && firstEff.IsCondition)
            {
                // 실질적인 타겟은 보통 턴 종료 시점에서는 시전자 자신 혹은 아군 전체이므로, caster와 target을 넘김
                if (!firstEff.Execute(character, target))
                {
                    // 첫 번째 조건이 맞지 않으면 연출과 쿨타임 소모 없이 조용히 종료
                    yield break;
                }
            }
        }

        bool isUltimate = (int)skill.SlotIndex == 2;


        // --- 1. 카메라 줌 및 캐릭터 하이라이트 ---
        if (!isUltimate && !string.IsNullOrEmpty(skill.RequiredAnimationTrigger) && character.Animator != null)
        {
            StartCoroutine(SetCameraZoom(character.IsPlayer, true));

            if (BattleVFXManager.Instance != null)
                BattleVFXManager.Instance.SpawnVFX(VFXType.extraMove, character.View.RetHitbox());

            yield return new WaitForSeconds(0.2f);
        }

        // --- 2. 컷신 재생 (있는 경우) ---
        if (skill.UltimateCutsceneClip != null && battleUI.ultimateCutsceneRoot != null)
        {
            Transform originalParent = null;
            Vector2 originalAnchoredPos = Vector2.zero;
            Vector3 originalScale = Vector3.one;
            bool enhancedSequence = false;

            if (character.View != null && character.View.illustration != null && battleUI.ultimateBlackScreen != null)
            {
                enhancedSequence = true;
                var illu = character.View.illustration.rectTransform;
                originalParent = illu.parent;
                originalAnchoredPos = illu.anchoredPosition;
                originalScale = illu.localScale;

                battleUI.ultimateBlackScreen.gameObject.SetActive(true);
                battleUI.ultimateBlackScreen.color = new Color(0, 0, 0, 0);

                illu.SetParent(battleUI.ultimateBlackScreen.transform, true);
                Vector2 targetPos = new Vector2(0, illu.anchoredPosition.y);

                Sequence seq = DOTween.Sequence();
                seq.Join(battleUI.ultimateBlackScreen.DOFade(1f, 0.4f));
                seq.Join(illu.DOAnchorPos(targetPos, 0.4f));
                seq.Join(illu.DOScale(originalScale * 1.2f, 0.4f));

                yield return seq.WaitForCompletion();
            }

            battleUI.ultimateCutsceneRoot.SetActive(true);
            var anim = battleUI.ultimateCutsceneRoot.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                // [추가] 궁극기 보이스 재생
                if (SoundManager.Instance != null && skill.UltimateVoiceClip != null)
                {
                    SoundManager.Instance.PlayVoice(skill.UltimateVoiceClip);
                }

                anim.Play(skill.UltimateCutsceneClip.name);
                if (enhancedSequence)
                {
                    yield return new WaitForSeconds(0.15f);
                    if (character.View != null)
                    {
                        var illu = character.View.illustration.rectTransform;
                        illu.SetParent(originalParent, true);
                        illu.anchoredPosition = originalAnchoredPos;
                        illu.localScale = originalScale;
                        battleUI.ultimateBlackScreen.gameObject.SetActive(false);
                    }
                    yield return new WaitForSeconds(Mathf.Max(0, skill.UltimateCutsceneClip.length - 0.15f));
                }
                else
                {
                    yield return new WaitForSeconds(skill.UltimateCutsceneClip.length);
                }
            }
            battleUI.ultimateCutsceneRoot.SetActive(false);
        }

        // --- 3. 애니메이션 재생 및 타격 시점 대기 ---
        if (!string.IsNullOrEmpty(skill.RequiredAnimationTrigger) && character.Animator != null)
        {
            _waitingForImpact = true;
            character.Animator.SetTrigger(skill.RequiredAnimationTrigger);

            yield return new WaitForSeconds(0.1f);

            var stateInfo = character.Animator.GetCurrentAnimatorStateInfo(0);
            float maxWait = stateInfo.length * 0.8f;
            float timer = 0f;

            while (_waitingForImpact && timer < maxWait)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        // --- 4. 실제 효과 실행 ---
        if (levelData != null)
        {
            // 쿨타임 적용 (이 스킬 고유의 쿨타임 세팅)
            int skillIndex = character.ActiveSkills.IndexOf(skill);
            if (skillIndex >= 0) character.SkillCooldowns[skillIndex] = levelData.Cooldown;

            // Trigger 및 Victim 정보 동기화 (이펙트 내부에서 참조할 수 있도록)
            BattleEventManager.CurrentExecutingTrigger = trigger;
            BattleEventManager.LastVictim = victim;

            if (levelData.Effects != null)
            {
                ProcessEffectChain(character, target, levelData.Effects, skill.SkillName);
            }
            character.RefreshStats();
        }

        // --- 5. 애니메이션 종료 대기 및 복귀 ---
        yield return new WaitForSeconds(0.3f);

        float timeout = 2.0f;
        while (timeout > 0)
        {
            bool anyBusy = false;
            if (!string.IsNullOrEmpty(skill.RequiredAnimationTrigger))
                if (character.IsAnimationPlaying(skill.RequiredAnimationTrigger))
                    anyBusy = true;

            if (!anyBusy) break;
            timeout -= Time.deltaTime;
            yield return null;
        }

        battleUI.SetSideImageVisible(false, character);
        if (!isUltimate)
        {
            StartCoroutine(SetCameraZoom(true, false));
            yield return new WaitForSeconds(0.4f);
        }

        Debug.Log($"[Passive-Show] {character.Name} 패시브 연출 종료");
    }

    IEnumerator ExecuteSkillRoutine(SkillData skillData, int skillIndex, BattleCharacter manualTarget = null)
    {
        CurrentSkill = skillData; // [추가] 현재 사용 스킬 저장
        Debug.Log($"[Battle] {currentActor.Name} → {skillData.SkillName} 사용 시작");

        // 이 스킬을 이번 턴에 썼다고 마킹 (쿨타임 이중차감 방지)
        currentActor.CastedSkillIndexThisTurn = skillIndex;

        int level = currentActor.SkillLevels.Length > skillIndex ? currentActor.SkillLevels[skillIndex] : 1;
        var levelData = skillData.LevelDatas != null && skillData.LevelDatas.Count >= level ? skillData.LevelDatas[level - 1] : null;

        // --- 연출용 데이터 준비 ---
        bool isUltimate = (int)skillData.SlotIndex == 2; // Skill3 (Ultimate)

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
                    {
                        // [추가] 은신 필터링: 은신하지 않은 적이 있다면 그들 중에서 고르고, 전원 은신이면 전체에서 고름
                        var targetableEnemies = aliveEnemies.Where(c => c.CanBeTargetedBy(currentActor, allCharacters)).ToList();
                        if (targetableEnemies.Count > 0)
                            selectedTarget = targetableEnemies[UnityEngine.Random.Range(0, targetableEnemies.Count)];
                        else
                            selectedTarget = aliveEnemies[UnityEngine.Random.Range(0, aliveEnemies.Count)];
                    }
                    break;
                case SkillTargetType.SingleAlly:
                case SkillTargetType.AllAllies:
                    var aliveAllies = allCharacters.Where(c => c.IsAlive && c.IsPlayer == currentActor.IsPlayer).ToList();
                    if (aliveAllies.Count > 0)
                        selectedTarget = aliveAllies[UnityEngine.Random.Range(0, aliveAllies.Count)];
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
        // [추가] 일반 스킬 시전자 진영 포커싱 (3스킬 제외)
        if (!isUltimate)
        {
            StartCoroutine(SetCameraZoom(currentActor.IsPlayer, true));
        }

        if (!string.IsNullOrEmpty(skillData.RequiredAnimationTrigger) && currentActor.Animator != null)
        {
            _waitingForImpact = true; // 대기 시작
            currentActor.Animator.SetTrigger(skillData.RequiredAnimationTrigger);

            // 타격 시점(Animation Event) 또는 안전 장치( timeout )까지 대기
            // 0.1s는 초기 애니메이션 전환 시간 확보
            yield return new WaitForSeconds(0.1f);

            var stateInfo = currentActor.Animator.GetCurrentAnimatorStateInfo(0);
            float maxWait = stateInfo.length * 0.8f; // 애니메이션의 80% 정도를 최대 대기 시간으로 설정
            float timer = 0f;

            while (_waitingForImpact && timer < maxWait)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // 신호가 왔거나(false) 시간이 다 되었으면(timer >= maxWait) 루프탈출 -> 효과 실행

            // [추가] 일반 스킬 피격자 진영 포커싱 (3스킬 제외)
            if (!isUltimate && selectedTarget != null)
            {
                // 아군이 아군에게 쓰는 경우 등 동일 진영이면 이미 포커싱된 상태 유지됨
                if (selectedTarget.IsPlayer != currentActor.IsPlayer)
                {
                    StartCoroutine(SetCameraZoom(selectedTarget.IsPlayer, true));
                }
            }
        }

        // 컷씬 애니메이터 연동 (컷씬이 있는 경우 컷씬도 기다림)
        if (skillData.UltimateCutsceneClip != null && battleUI.ultimateCutsceneRoot != null)
        {
            // --- [추가] 컷신 전 연출: 캐릭터 중앙 이동 + 배경 어두워짐 ---
            Transform originalParent = null;
            Vector2 originalAnchoredPos = Vector2.zero;
            Vector3 originalScale = Vector3.one;
            bool enhancedSequence = false;

            if (currentActor.View != null && currentActor.View.illustration != null && battleUI.ultimateBlackScreen != null)
            {
                enhancedSequence = true;
                var illu = currentActor.View.illustration.rectTransform;
                originalParent = illu.parent;
                originalAnchoredPos = illu.anchoredPosition;
                originalScale = illu.localScale;

                // 검정 배경 활성화 및 초기화
                battleUI.ultimateBlackScreen.gameObject.SetActive(true);
                var color = battleUI.ultimateBlackScreen.color;
                color.a = 0;
                battleUI.ultimateBlackScreen.color = color;

                // 일러스트를 검정 배경의 자식으로 변경 (이때 worldPositionStays = true로 현재 위치 유지)
                illu.SetParent(battleUI.ultimateBlackScreen.transform, true);

                // [수정] X축만 중앙(0)으로 이동하고 Y축은 현재 위치 유지
                Vector2 targetPos = new Vector2(0, illu.anchoredPosition.y);

                // 연출 실행 (0.4초, 1.2배 확대)
                Sequence seq = DOTween.Sequence();
                seq.Join(battleUI.ultimateBlackScreen.DOFade(1f, 0.4f));
                seq.Join(illu.DOAnchorPos(targetPos, 0.4f));
                seq.Join(illu.DOScale(originalScale * 1.2f, 0.4f));

                yield return seq.WaitForCompletion();
            }

            // --- 기존 컷신 재생 ---
            battleUI.ultimateCutsceneRoot.SetActive(true);
            var anim = battleUI.ultimateCutsceneRoot.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                // [추가] 궁극기 보이스 재생
                if (SoundManager.Instance != null && skillData.UltimateVoiceClip != null)
                {
                    SoundManager.Instance.PlayVoice(skillData.UltimateVoiceClip);
                }

                anim.Play(skillData.UltimateCutsceneClip.name);

                // 컷신이 화면을 다 가릴 즈음 (약 0.1~0.2초 후) 배경과 캐릭터를 원래대로 복귀
                if (enhancedSequence)
                {
                    yield return new WaitForSeconds(0.15f);
                    if (currentActor.View != null)
                    {
                        var illu = currentActor.View.illustration.rectTransform;
                        illu.SetParent(originalParent, true);
                        illu.anchoredPosition = originalAnchoredPos;
                        illu.localScale = originalScale;
                        battleUI.ultimateBlackScreen.gameObject.SetActive(false);
                    }
                    yield return new WaitForSeconds(Mathf.Max(0, skillData.UltimateCutsceneClip.length - 0.15f));
                }
                else
                {
                    yield return new WaitForSeconds(skillData.UltimateCutsceneClip.length);
                }
            }
            battleUI.ultimateCutsceneRoot.SetActive(false);
        }
        else if (string.IsNullOrEmpty(skillData.RequiredAnimationTrigger))
        {
            // 컷씬도 애니메이션도 없을 경우만 기본 대기 (즉시 발동과 유사)
            // 비공격 스킬(버프 등) 타겟 진영 포커싱 추가
            if (!isUltimate && selectedTarget != null)
            {
                StartCoroutine(SetCameraZoom(selectedTarget.IsPlayer, true));
            }
            yield return new WaitForSeconds(0.4f);
        }

        // --- 3. 이펙트(피해/버프) 체인 실행 ---
        // (여기가 실제 데미지가 들어가는 시점!)
        if (levelData != null)
        {
            currentActor.SkillCooldowns[skillIndex] = levelData.Cooldown;

            if (levelData.Effects != null)
            {
                ProcessEffectChain(currentActor, selectedTarget, levelData.Effects, skillData.SkillName);
            }
        }

        BattleEventManager.TriggerSkillUsed(currentActor, skillData);

        // [추가] 글로벌 협공 트리거 체크
        // 조건: 1스킬 사용 + 협공 진행 중 아님 + 이번 턴에 아직 협공 안 함
        if (skillData.SlotIndex == SkillSlotIndex.Skill1 && !isProcessingDualAttack)
        {
            // '나를 제외한', '생존해있는', '같은 편', '행동 불가(기절/수면)가 아닌' 아군을 찾습니다.
            var eligibleAllies = allCharacters.Where(c =>
                c.IsAlive &&
                c.IsPlayer == currentActor.IsPlayer &&
                c != currentActor &&
                !c.HasStatusEffect(StatusEffectType.Stun) &&
                !c.HasStatusEffect(StatusEffectType.Sleep)
            ).ToList();

            BattleCharacter triggeredHelper = null;
            foreach (var ally in eligibleAllies)
            {
                // 각 아군 본인의 협공 확률로 주사위를 굴립니다.
                if (UnityEngine.Random.value < ally.DualAttackChance)
                {
                    triggeredHelper = ally;
                    break; // 한 명만 성공하면 즉시 중단
                }
            }

            if (triggeredHelper != null)
            {
                isProcessingDualAttack = true; // 이번 턴 협공 완료 마킹
                Debug.Log($"[GlobalDualAttack] {triggeredHelper.Name}이(가) {currentActor.Name}의 공격에 호응합니다! (확률: {triggeredHelper.DualAttackChance})");

                // 협공 루틴 실행
                var routine = CombinationAttackRoutine(triggeredHelper, selectedTarget);
                EnqueueExtraActionFront(routine);
            }
        }

        // --- 4. 모든 애니메이션(공격자 및 피격자) 종료 대기 ---
        // 최소한의 딜레이 확보 (애니메이션 상태 전환 대기)
        yield return new WaitForSeconds(0.2f);

        float turnEndTimeout = 2.0f;
        while (turnEndTimeout > 0)
        {
            bool anyBusy = false;

            // 1. 공격자(시전자)가 여전히 액션 중인지 체크
            if (!string.IsNullOrEmpty(skillData.RequiredAnimationTrigger))
            {
                if (currentActor.IsAnimationPlaying(skillData.RequiredAnimationTrigger))
                    anyBusy = true;
            }

            // 2. 모든 캐릭터 중 현재 'Hit' 애니메이션을 재생 중인 캐릭터가 있는지 체크
            foreach (var bc in allCharacters)
            {
                if (bc.IsAlive && bc.IsAnimationPlaying("hit"))
                {
                    anyBusy = true;
                    break;
                }
            }

            if (!anyBusy) break;

            turnEndTimeout -= Time.deltaTime;
            yield return null;
        }

        // 약간의 여유 딜레이 후 종료
        yield return new WaitForSeconds(0.2f);

        // [추가] 일반 스킬 진영 포커싱 해제 (0.4초)
        if (!isUltimate)
        {
            StartCoroutine(SetCameraZoom(true, false));
            yield return new WaitForSeconds(0.4f);
        }

        // 전투가 아예 끝났는지 검사
        if (IsOver())
        {
            // 직접 OnBattleEnd를 호출하지 않고 상태만 변경하여 턴 루프가 정리하도록 유도
            bool playerDead = allCharacters.TrueForAll(c => !c.IsPlayer || !c.IsAlive);
            State = playerDead ? BattleState.Lose : BattleState.Win;
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
        if (enemy == null || !enemy.IsAlive) return;

        int selectedIndex = 0;
        SkillData selectedSkill = null;

        // [추가] 침묵(Silence) 상태 체크
        bool isSilenced = enemy.HasStatusEffect(StatusEffectType.Silence);

        // 간단한 AI: 강한 스킬(인덱스 2 -> 1 -> 0) 우선순위 검사
        for (int i = enemy.ActiveSkills.Count - 1; i >= 0; i--)
        {
            var skill = enemy.ActiveSkills[i];
            if (skill != null && enemy.SkillCooldowns[i] <= 0)
            {
                // [추가] 패시브 스킬은 AI가 직접 사용하지 않음
                if (skill.Type == SkillType.Passive) continue;

                // 침묵 상태일 경우 1번 스킬(인덱스 0)만 선택 가능
                if (isSilenced && i > 0) continue;

                selectedIndex = i;
                selectedSkill = skill;
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
        if (_isBattleOverFlag) return; // 이미 종료 처리가 되었다면 무시
        _isBattleOverFlag = true;

        // [추가] 모든 캐릭터의 이벤트 구독 해제 (다음 전투에서 잔상이 남는 문제 방지)
        if (allCharacters != null)
        {
            foreach (var bc in allCharacters)
            {
                bc.UnsubscribeEvents();
            }
        }

        bool playerDead = allCharacters.TrueForAll(c => !c.IsPlayer || !c.IsAlive);
        State = playerDead ? BattleState.Lose : BattleState.Win;
        battleUI.SetSkillButtonsVisible(false);
        battleUI.SetItemVisible(false);
        battleUI.AnimateGaugeBar(false);

        if (State == BattleState.Lose)
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SfxType.Lose);
            Debug.Log("[Battle] 패배! 게임을 초기화합니다.");
            StartCoroutine(ResetAfterDelay(2.0f));
            return;
        }

        if (State == BattleState.Win && currentStage != null)
        {
            // [추가] 승리 SFX 및 보이스
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(SfxType.Win);

                // 생존한 아군 중 랜덤으로 승리 대사 출력
                var survivors = allCharacters.Where(c => c.IsPlayer && c.IsAlive).ToList();
                if (survivors.Count > 0)
                {
                    var luckyOne = survivors[UnityEngine.Random.Range(0, survivors.Count)];
                    if (luckyOne.Data != null && luckyOne.Data.winVoices.Count > 0)
                    {
                        var voice = luckyOne.Data.winVoices[UnityEngine.Random.Range(0, luckyOne.Data.winVoices.Count)];
                        SoundManager.Instance.PlayVoice(voice);
                    }
                }
            }

            // [수정] 비활성 객체 포함하여 MapUI 찾기 및 stageID 기반 보스전 판별
            var mapUI = GameObject.FindAnyObjectByType<MapUI>(FindObjectsInactive.Include);
            if (mapUI != null && mapUI.stageDatabase != null && mapUI.stageDatabase.bossStage != null)
            {
                if (currentStage.stageID == mapUI.stageDatabase.bossStage.stageID)
                {
                    Debug.Log($"[Battle] 최종 보스(ID:{currentStage.stageID}) 클리어! 보상 없이 게임을 초기화합니다.");

                    // [추가] 승리 패널 표시 (DOTween 페이드 인)
                    if (winPanel != null)
                    {
                        winPanel.SetActive(true);
                        var cg = winPanel.GetComponent<CanvasGroup>();
                        if (cg == null) cg = winPanel.AddComponent<CanvasGroup>();
                        cg.alpha = 0;
                        cg.DOFade(1f, 1.0f);
                    }

                    StartCoroutine(ResetAfterDelay(3.0f));
                    return; // 리워드 창을 띄우지 않고 바로 종료
                }
            }

            GrantBattleRewards();
        }

        // [추가] 전투 후 체력 데이터 GameManager에 저장
        if (GameManager.Instance != null)
        {
            var party = GameManager.Instance.party;
            int playerIdx = 0;
            foreach (var bc in allCharacters)
            {
                if (bc.IsPlayer)
                {
                    if (playerIdx < party.Length && party[playerIdx] != null)
                    {
                        // 생존 시 현재 체력, 사망 시 최소 1로 저장
                        party[playerIdx].currentHp = bc.IsAlive ? bc.CurrentHp : 1f;

                        // [추가] 스킬 쿨타임 저장
                        for (int i = 0; i < 3; i++)
                        {
                            party[playerIdx].skillCooldowns[i] = bc.SkillCooldowns[i];
                        }
                    }
                    playerIdx++;
                }
            }
        }
    }

    /// <summary>
    /// 전투 승리 보상을 계산하고 GameManager에 반영합니다. (로그 출력 포함)
    /// </summary>
    private void GrantBattleRewards()
    {
        if (GameManager.Instance == null || currentStage == null) return;

        // 1. 골드 및 강화 재료 계산
        int rewardGold = UnityEngine.Random.Range(currentStage.minGold, currentStage.maxGold + 1);
        int rewardSkillUp = UnityEngine.Random.Range(currentStage.minSkillUp, currentStage.maxSkillUp + 1);
        int rewardReroll = UnityEngine.Random.Range(currentStage.minReroll, currentStage.maxReroll + 1);
        int rewardHighReroll = UnityEngine.Random.Range(currentStage.minHighReroll, currentStage.maxHighReroll + 1);

        // [수정] GameManager에 즉시 추가하지 않음 (RewardPanelUI에서 클릭 시 추가)

        string rewardLog = $"<b>[전투 승리 - Stage {currentStage.stageID}]</b>\n";
        rewardLog += $"- 획득 골드: {rewardGold}\n";
        rewardLog += $"- 획득 강화석: {rewardSkillUp}\n";
        rewardLog += $"- 획득 리롤권: {rewardReroll}\n";
        rewardLog += $"- 획득 고급 리롤권: {rewardHighReroll}\n";

        // 2. 배틀 아이템 랜덤 획득 (1~2개)
        List<SkillData> droppedItems = new List<SkillData>();

        if (currentStage.potentialBattleItems != null && currentStage.potentialBattleItems.Count > 0)
        {
            int itemDropCount = UnityEngine.Random.Range(1, 3); // 1~2개
            rewardLog += "- 획득 아이템: ";

            for (int i = 0; i < itemDropCount; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, currentStage.potentialBattleItems.Count);
                var droppedItem = currentStage.potentialBattleItems[randomIndex];

                if (droppedItem != null)
                {
                    droppedItems.Add(droppedItem);
                    rewardLog += $"[{droppedItem.SkillName}]" + (i < itemDropCount - 1 ? ", " : "");
                }
            }
        }

        Debug.Log(rewardLog);
        Debug.Log($"[BattleManager] 보상 패널에 전달될 아이템 개수: {droppedItems.Count}");

        // 3. UI 팝업 노출 연동
        if (battleUI != null && battleUI.rewardPanel != null)
        {
            LobbyTopUI.Instance.Refresh();
            battleUI.rewardPanel.Setup(rewardGold, rewardSkillUp, rewardReroll, rewardHighReroll, droppedItems, OnRewardConfirmed);
            LobbyTopUI.Instance.ShowUI();
        }
    }

    /// <summary>
    /// 보상 확인 버튼 클릭 시 실행될 콜백 (맵으로 복귀)
    /// </summary>
    public void OnRewardConfirmed()
    {
        ReturnToMap();
    }

    private void ReturnToMap()
    {
        // 2. 맵 UI 다시 활성화
        if (mapUIObject != null)
            mapUIObject.SetActive(true);
    }

    // -------------------------------------------------------
    // [추가] 배경 조작을 통한 카메라 줌 연출 고도화
    // -------------------------------------------------------
    private IEnumerator SetCameraZoom(bool isPlayer, bool zoom, float duration = 0.4f)
    {
        if (battleBackground == null) yield break;

        if (zoom)
        {
            Vector2 targetPivot = isPlayer ? new Vector2(0f, 0f) : new Vector2(1f, 0f);

            if (!_isCurrentlyZoomed)
            {
                // [Phase 1/3] 처음 줌인 시작
                battleBackground.DOKill();

                // 피벗 설정 시 위치 점프 방지 (하지만 오프셋을 0으로 맞출 것이므로 초기 위치만 잡아줌)
                SetPivotCompensated(battleBackground, targetPivot);

                // 꼭짓점 고정 (0으로 보정)
                battleBackground.offsetMin = Vector2.zero;
                battleBackground.offsetMax = Vector2.zero;

                // 스케일 증가
                battleBackground.DOScale(Vector3.one * 1.2f, duration).SetEase(Ease.OutQuart);

                _isCurrentlyZoomed = true;
                _lastZoomedPlayer = isPlayer;
            }
            else if (_lastZoomedPlayer != isPlayer)
            {
                // [Phase 2-1/4-1] 아군 <-> 적군 전환 (패닝)
                battleBackground.DOKill();

                // 1. 현재 피벗(예: 좌하단)을 유지한 상태에서, 목표 진영(예: 우하단)이 화면에 들어오도록 오프셋 계산
                // 현재 스케일(1.2) 상태에서 반대쪽 끝으로 가려면 부모 너비의 20%만큼 이동해야 함
                // Stretch-Stretch 앵커이므로, 피벗이 (0,0)일 때 (1,0)을 보려면 x를 -0.2 * width 만큼 밀어야 함
                float targetOffsetX = isPlayer ? 0f : -(battleBackground.rect.width * 0.2f);
                if (battleBackground.pivot.x == 1f) // 현재 피벗이 우하단이면 반대
                    targetOffsetX = isPlayer ? (battleBackground.rect.width * 0.2f) : 0f;

                Vector2 targetOffsetMin = new Vector2(targetOffsetX, 0f);
                Vector2 targetOffsetMax = new Vector2(targetOffsetX, 0f);

                // 2. 부드럽게 패닝 시작
                DOTween.To(() => battleBackground.offsetMin, x => battleBackground.offsetMin = x, targetOffsetMin, duration).SetEase(Ease.OutQuart);
                yield return DOTween.To(() => battleBackground.offsetMax, x => battleBackground.offsetMax = x, targetOffsetMax, duration)
                    .SetEase(Ease.OutQuart)
                    .WaitForCompletion();

                // 3. 이동이 끝난 후, 피벗을 목표 진영으로 갈아끼우고 오프셋 0으로 동기화 (점프 방지)
                SetPivotCompensated(battleBackground, targetPivot);
                battleBackground.offsetMin = Vector2.zero;
                battleBackground.offsetMax = Vector2.zero;

                _lastZoomedPlayer = isPlayer;
            }
            // 같은 진영이면(_lastZoomedPlayer == isPlayer) 아무것도 하지 않음 (유지)
        }
        else
        {
            // [Phase 2/4] 원상 복구 (줌 아웃)
            if (_isCurrentlyZoomed)
            {
                battleBackground.DOKill();

                // 1. 스케일을 1로 부드럽게 변경
                yield return battleBackground.DOScale(Vector3.one, duration).SetEase(Ease.OutQuart).WaitForCompletion();

                // 2. 피벗 0.5, 0.5 복구 및 위치 초기화
                SetPivotCompensated(battleBackground, new Vector2(0.5f, 0.5f));
                battleBackground.offsetMin = Vector2.zero;
                battleBackground.offsetMax = Vector2.zero;
                battleBackground.anchoredPosition = Vector2.zero;

                _isCurrentlyZoomed = false;
            }
        }

        yield return null;
    }

    // 피벗 변경 시 위치가 튀지 않도록 보정하는 유틸리티
    private void SetPivotCompensated(RectTransform rectTransform, Vector2 pivot)
    {
        Vector2 size = rectTransform.rect.size;
        Vector2 deltaPivot = rectTransform.pivot - pivot;
        Vector3 deltaPosition = new Vector3(deltaPivot.x * size.x, deltaPivot.y * size.y, 0f);

        deltaPosition.x *= rectTransform.localScale.x;
        deltaPosition.y *= rectTransform.localScale.y;

        rectTransform.pivot = pivot;
        rectTransform.localPosition -= deltaPosition;
    }

    private IEnumerator ResetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetGameProgress();
        }
    }
}
