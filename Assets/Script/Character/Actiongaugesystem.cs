using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Actiongaugesystem : MonoBehaviour
{
    public const float MaxGauge = 100f;

    private Queue<BattleCharacter> _readyQueue = new ();

    public int ReadyCount => _readyQueue.Count;

    // -------------------------------------------------------
    // 다음 행동 캐릭터 결정
    // -------------------------------------------------------
    public void Advance(List<BattleCharacter> characters)
    {
        var alive = characters.Where(c => c.IsAlive).ToList();
        if (alive.Count == 0) return;

        // Speed 0 방지
        foreach (var c in alive)
        {
            if (c.Speed <= 0f)
            {
                Debug.LogError($"[ActionGaugeSystem] {c.Name}의 Speed가 0 이하입니다. 1로 보정합니다.");
                c.Speed = 1f;
            }
        }

        if (_readyQueue.Count > 0) return;

        int safetyLimit = 10000;

        while (_readyQueue.Count == 0)
        {
            if (--safetyLimit <= 0)
            {
                Debug.LogError("[ActionGaugeSystem] 무한루프 감지. 강제 탈출합니다.");
                break;
            }

            // 1. 남은 걸음 수 최솟값 계산
            float minSteps = float.MaxValue;
            foreach (var c in alive)
            {
                float steps = (MaxGauge - c.ActionGauge) / c.Speed;
                if (steps < minSteps) minSteps = steps;
            }

            // 2. 전진 — 가장 빠른 캐릭터의 게이지를 정확히 100으로 고정해 오차 방지
            BattleCharacter fastest = null;
            foreach (var c in alive)
            {
                c.ActionGauge += c.Speed * minSteps;
                if (fastest == null || c.ActionGauge > fastest.ActionGauge)
                    fastest = c;
            }
            if (fastest != null && fastest.ActionGauge >= MaxGauge - 0.5f)
                fastest.ActionGauge = MaxGauge;


            

            var reached = alive
                .Where(c => !object.ReferenceEquals(c, null) && c.ActionGauge >= MaxGauge - 0.5f)
                .OrderByDescending(c => c.ActionGauge)
                .ThenByDescending(c => c.Speed)
                .ToList();


            if (reached.Count > 1)
                reached = BreakTies(reached);

            foreach (var c in reached)
            {
                if (object.ReferenceEquals(c, null))
                {

                    continue;
                }
                _readyQueue.Enqueue(c);
            }

        }
    }

    // -------------------------------------------------------
    // 큐에서 다음 캐릭터 꺼내기
    // -------------------------------------------------------
    public BattleCharacter DequeueReady()
    {
        if (_readyQueue.Count == 0) return null;
        var c = _readyQueue.Dequeue();
        
        return object.ReferenceEquals(c, null) ? null : c;
    }

    // -------------------------------------------------------
    // 턴 종료 — 게이지 리셋
    // -------------------------------------------------------
    public void OnTurnEnd(BattleCharacter character)
    {
        character.ActionGauge = 0f;
    }

    // -------------------------------------------------------
    // 게이지 조작 (스킬용)
    // -------------------------------------------------------
    public void ModifyGauge(BattleCharacter character, float delta)
    {
        character.ActionGauge = Mathf.Clamp(character.ActionGauge + delta, 0f, MaxGauge);
    }

    public void SetGaugeRatio(BattleCharacter character, float ratio)
    {
        character.ActionGauge = Mathf.Clamp01(ratio) * MaxGauge;
    }

    // -------------------------------------------------------
    // 완전 동률 셔플
    // -------------------------------------------------------
    private List<BattleCharacter> BreakTies(List<BattleCharacter> list)
    {
        var result = new List<BattleCharacter>();
        int i = 0;

        while (i < list.Count)
        {
            int j = i + 1;
            while (j < list.Count &&
                   Mathf.Approximately(list[j].Speed, list[i].Speed) &&
                   Mathf.Approximately(list[j].ActionGauge, list[i].ActionGauge))
                j++;

            var group = list.GetRange(i, j - i);
            for (int k = group.Count - 1; k > 0; k--)
            {
                int r = Random.Range(0, k + 1);
                (group[k], group[r]) = (group[r], group[k]);
            }
            result.AddRange(group);
            i = j;
        }

        return result;
    }
}
