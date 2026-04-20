using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 스테이지 데이터(StageData)를 관리하고, 요청에 따라 특정 또는 랜덤 스테이지를 제공하는 데이터베이스 클래스입니다.
/// </summary>
public class StageDatabase : MonoBehaviour
{
    [Header("Stage Collection")]
    [Tooltip("전체 스테이지 리스트를 여기에 등록합니다.")]
    public List<StageData> allStages = new List<StageData>();

    /// <summary>
    /// 등록된 모든 스테이지 중 하나를 랜덤으로 반환합니다.
    /// </summary>
    public StageData GetRandomStage()
    {
        if (allStages == null || allStages.Count == 0)
        {
            Debug.LogWarning("[StageDatabase] 등록된 스테이지가 없습니다.");
            return null;
        }

        int randomIndex = Random.Range(0, allStages.Count);
        return allStages[randomIndex];
    }

    /// <summary>
    /// 특정 ID와 일치하는 스테이지를 찾아 반환합니다. (향후 사용을 위해 추가)
    /// </summary>
    public StageData GetStageByID(int id)
    {
        return allStages.Find(s => s.stageID == id);
    }
}
