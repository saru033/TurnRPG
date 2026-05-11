using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 모든 스테이지 데이터(StageData)를 관리하고, 요청에 따라 특정 또는 랜덤 스테이지를 제공하는 데이터베이스 클래스입니다.
/// </summary>
public class StageDatabase : MonoBehaviour
{
    [Header("Stage Collection")]
    [Tooltip("전체 스테이지 리스트를 여기에 등록합니다.")]
    public List<StageData> allStages = new List<StageData>();

    // 내부 분류 리스트
    private List<StageData> _earlyNormal = new();
    private List<StageData> _earlyElite = new();
    private List<StageData> _lateNormal = new();
    private List<StageData> _lateElite = new();

    // 셔플 백용 사용된 스테이지 관리
    private HashSet<int> _usedStageIds = new();

    private void Awake()
    {
        CategorizeStages();
    }

    private void CategorizeStages()
    {
        _earlyNormal.Clear();
        _earlyElite.Clear();
        _lateNormal.Clear();
        _lateElite.Clear();

        foreach (var stage in allStages)
        {
            if (stage == null) continue;

            if (stage.era == StageEra.Early)
            {
                if (stage.difficulty == StageDifficulty.Normal) _earlyNormal.Add(stage);
                else _earlyElite.Add(stage);
            }
            else
            {
                if (stage.difficulty == StageDifficulty.Normal) _lateNormal.Add(stage);
                else _lateElite.Add(stage);
            }
        }
    }

    /// <summary>
    /// 노드 타입과 현재 열(깊이), 그리고 총 노드 수(N)에 맞는 랜덤 스테이지를 반환합니다. (셔플 백 적용)
    /// </summary>
    public StageData GetRandomStage(NodeType type, int column, int totalN)
    {
        // 1. 대상 그룹 결정 (최대 노드의 절반 기준)
        List<StageData> targetPool;
        int threshold = totalN / 2;
        bool isEarly = column <= threshold + 1; // StartHub(1) + N/2

        if (type == NodeType.Elite)
        {
            targetPool = isEarly ? _earlyElite : _lateElite;
        }
        else
        {
            targetPool = isEarly ? _earlyNormal : _lateNormal;
        }

        if (targetPool.Count == 0)
        {
            Debug.LogWarning($"[StageDatabase] {type} (Era: {(isEarly ? "Early" : "Late")}) 풀이 비어있습니다. 전체 리스트에서 찾습니다.");
            return GetRandomStageFallback();
        }

        // 2. 셔플 백 로직: 풀 내에서 사용되지 않은 스테이지들 추출
        var available = targetPool.Where(s => !_usedStageIds.Contains(s.stageID)).ToList();

        // 3. 만약 다 썼다면 해당 풀의 ID들만 비우고 다시 시작
        if (available.Count == 0)
        {
            foreach (var s in targetPool) _usedStageIds.Remove(s.stageID);
            available = new List<StageData>(targetPool);
        }

        // 4. 랜덤 선택
        StageData selected = available[Random.Range(0, available.Count)];
        _usedStageIds.Add(selected.stageID);

        return selected;
    }

    private StageData GetRandomStageFallback()
    {
        if (allStages.Count == 0) return null;
        return allStages[Random.Range(0, allStages.Count)];
    }

    /// <summary>
    /// 등록된 모든 스테이지 중 하나를 랜덤으로 반환합니다. (구버전 호환용)
    /// </summary>
    public StageData GetRandomStage()
    {
        return GetRandomStageFallback();
    }

    /// <summary>
    /// 특정 ID와 일치하는 스테이지를 찾아 반환합니다.
    /// </summary>
    public StageData GetStageByID(int id)
    {
        return allStages.Find(s => s.stageID == id);
    }
}
