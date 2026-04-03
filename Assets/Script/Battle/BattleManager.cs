using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TurnRPG.SkillSystem;
using System.Linq;
using UnityEngine.Rendering;
using System;

public class BattleManager : MonoBehaviour
{
    [Header("References")]
    public BattleUI battleUI;
    public CharacterPlacer characterPlacer;
    public RectTransform battleBackground; // [추가] 카메라 효과용 배경 RectTransform

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
    public BattleCharacter currentActor;

    // [추가] 줌 효과 상태 관리용
    private bool _isCurrentlyZoomed = false;
    private bool _lastZoomedPlayer = false;


    private Queue<IEnumerator> extraActionQueue = new Queue<IEnumerator>();

    public void EnqueueExtraAction(IEnumerator action)
    {
        extraActionQueue.Enqueue(action);
    }

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

        // Actiongaugesystem은 MonoBehaviour이므로 컴포넌트 방식으로 초기화
        gaugeSystem = GetComponent<Actiongaugesystem>();
        if (gaugeSystem == null) gaugeSystem = gameObject.AddComponent<Actiongaugesystem>();

        if (initialCharacterDatas != null && initialCharacterDatas.Count > 0)
        {
            foreach (var data in initialCharacterDatas)
            {
                if (data != null)
                {
                    var bc = new BattleCharacter(data);
                    bc.ActionGaugeSystem = gaugeSystem; // [추가] 시스템 참조 연결
                    allCharacters.Add(bc);
                }
            }
        }

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

            // [추가] 기절(Stun) 또는 수면(Sleep) 체크
            bool isSkipTurn = currentActor.HasStatusEffect(TurnRPG.SkillSystem.StatusEffectType.Stun) || currentActor.HasStatusEffect(TurnRPG.SkillSystem.StatusEffectType.Sleep);
            if (isSkipTurn)
            {
                Debug.Log($"{currentActor.Name} : 기절/수면 상태로 인해 턴을 스킵합니다.");
                yield return new WaitForSeconds(0.5f);
                currentActor.OnTurnEnd();
                gaugeSystem.OnTurnEnd(currentActor);
                continue;
            }

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
                if (currentActor.Animator != null) currentActor.Animator.SetBool("isWaiting", true);

                State = BattleState.PlayerTurn;
                battleUI.SetSkillButtonsVisible(true, currentActor);
                battleUI.SetSideImageVisible(true, currentActor);

                // OnSkillSelected() 및 타겟 지정 완료 시점까지 대기 (SelectTarget 상태도 포함해 대기)
                yield return new WaitUntil(() => State != BattleState.PlayerTurn && State != BattleState.SelectTarget);

                if (currentActor.Animator != null) currentActor.Animator.SetBool("isWaiting", false);

                battleUI.SetSkillButtonsVisible(false);

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


            while (extraActionQueue.Count > 0)
            {
                var action = extraActionQueue.Dequeue();
                yield return StartCoroutine(action);
            }


            // 4. 턴 종료 — 버프 지속시간 차감
            currentActor.OnTurnEnd();
            battleUI.SetSideImageVisible(false, currentActor);
            if (currentActor.Animator != null) currentActor.Animator.SetBool("isWaiting", false); // 안전장치

            gaugeSystem.OnTurnEnd(currentActor);
            battleUI.UpdateGaugePositions(allCharacters);

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
    private bool _waitingForImpact = false;

    public void OnAnimationImpact()
    {
        _waitingForImpact = false;
    }

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

        // --- [추가] 은신(Stealth) 타겟팅 검사 ---
        if (!target.CanBeTargetedBy(currentActor, allCharacters))
        {
            Debug.Log($"[Battle] {target.Name}은(는) 은신 중이라 타겟으로 지정할 수 없습니다!");
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


    public IEnumerator CounterAttackRoutine(BattleCharacter attacker, BattleCharacter target)
    {
        var skillData = attacker.ActiveSkills.Count > 0 ? attacker.ActiveSkills[0] : null;
        if (skillData == null) yield break;

        int skillIndex = 0;
        int level = attacker.SkillLevels.Length > skillIndex ? attacker.SkillLevels[skillIndex] : 1;
        var levelData = skillData.LevelDatas != null && skillData.LevelDatas.Count >= level
            ? skillData.LevelDatas[level - 1] : null;

        // --- 애니메이션 재생 및 타격 시점 대기 ---
        if (!string.IsNullOrEmpty(skillData.RequiredAnimationTrigger) && attacker.Animator != null)
        {
            yield return new WaitForSeconds(1.0f);

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


            // 반격 이펙트 출력
            if (BattleVFXManager.Instance != null)
                BattleVFXManager.Instance.SpawnVFX(VFXType.extraMove, attacker.View.RetHitbox());


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
                foreach (var eff in levelData.Effects)
                {
                    if (eff != null)
                    {
                        if (!eff.Execute(attacker, target)) break;
                    }
                }
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



    IEnumerator ExecuteSkillRoutine(SkillData skillData, int skillIndex, BattleCharacter manualTarget = null)
    {
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
                foreach (var eff in levelData.Effects)
                {
                    if (eff != null)
                    {
                        // [설계] Execute가 false를 반환하는 경우는 '조건부 필터'에 의해 이후 체인을 중단해야 할 때 뿐입니다.
                        // 단순한 저항이나 빗나감은 Execute 내부에서 true를 반환하여 체인이 유지되도록 구현되어 있습니다.
                        if (!eff.Execute(currentActor, selectedTarget)) break;
                    }
                }
            }
        }

        BattleEventManager.TriggerSkillUsed(currentActor, skillData);

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
        if (enemy == null || !enemy.IsAlive) return;

        int selectedIndex = 0;
        SkillData selectedSkill = null;

        // [추가] 침묵(Silence) 상태 체크
        bool isSilenced = enemy.HasStatusEffect(StatusEffectType.Silence);

        // 간단한 AI: 강한 스킬(인덱스 2 -> 1 -> 0) 우선순위 검사
        for (int i = enemy.ActiveSkills.Count - 1; i >= 0; i--)
        {
            if (enemy.ActiveSkills[i] != null && enemy.SkillCooldowns[i] <= 0)
            {
                // 침묵 상태일 경우 1번 스킬(인덱스 0)만 선택 가능
                if (isSilenced && i > 0) continue;

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
}
