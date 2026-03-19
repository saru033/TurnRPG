using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    [Header("References")]
    public BattleUI battleUI;

    // -------------------------------------------------------
    // 전투 상태
    // -------------------------------------------------------
    public enum BattleState { Idle, PlayerTurn, EnemyTurn, Win, Lose }
    public BattleState State { get; private set; } = BattleState.Idle;

    Actiongaugesystem gaugeSystem;
    List<BattleCharacter> allCharacters = new ();
    BattleCharacter currentActor;

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
        // 임시 캐릭터 생성 — 추후 외부 데이터로 교체
        allCharacters = new List<BattleCharacter>
        {
            new BattleCharacter("아군A", isPlayer: true,  maxHp: 1000, defense: 50,  speed: 321),
            new BattleCharacter("아군B", isPlayer: true,  maxHp: 900,  defense: 40,  speed: 268),
            new BattleCharacter("적A",   isPlayer: false, maxHp: 800,  defense: 30,  speed: 198),
            new BattleCharacter("적B",   isPlayer: false, maxHp: 600,  defense: 20,  speed: 222),
        };

        gaugeSystem = new Actiongaugesystem();

        battleUI.Init(allCharacters);
        battleUI.SetSkillButtonsVisible(false);

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

            battleUI.UpdateGaugePositions(allCharacters);
            battleUI.HighlightActor(currentActor);

            // 짧은 연출 딜레이
            yield return new WaitForSeconds(0.3f);

            if (currentActor.IsPlayer)
            {
                // 2. 아군 턴 — 플레이어 입력 대기
                State = BattleState.PlayerTurn;
                battleUI.SetSkillButtonsVisible(true);

                // OnSkillSelected()가 호출될 때까지 대기
                yield return new WaitUntil(() => State != BattleState.PlayerTurn);
            }
            else
            {
                // 3. 적 턴 — 자동 행동
                State = BattleState.EnemyTurn;
                battleUI.SetSkillButtonsVisible(false);

                yield return new WaitForSeconds(0.5f);   // 적 행동 연출 딜레이
                EnemyAct(currentActor);
            }

            // 4. 턴 종료
            gaugeSystem.OnTurnEnd(currentActor);
            battleUI.UpdateGaugePositions(allCharacters);
            battleUI.SetSkillButtonsVisible(false);

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
    public void OnSkillSelected(int skillIndex)
    {
        if (State != BattleState.PlayerTurn) return;

        Debug.Log($"[Battle] {currentActor.Name} → {skillIndex + 1}번 스킬 사용");

        // TODO: 실제 스킬 로직 적용

        State = BattleState.Idle;   // WaitUntil 조건 해제 → 턴 루프 재개
    }

    // -------------------------------------------------------
    // 적 자동 행동
    // -------------------------------------------------------
    void EnemyAct(BattleCharacter enemy)
    {
        // 현재는 로그만 — 추후 스킬 시스템 연동
        // 강한 스킬 우선 (3 → 2 → 1스킬) 로직은 스킬 시스템 추가 시 구현
        Debug.Log($"[Battle] {enemy.Name} → 스킬 사용 (자동)");
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
