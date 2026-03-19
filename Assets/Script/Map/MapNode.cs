using System.Collections.Generic;

/// <summary>
/// 맵의 노드 하나를 나타내는 데이터 클래스.
/// 노드 타입은 int로 관리 — 0이면 미정, 이후 전투/상점/이벤트 등으로 확장.
/// </summary>
/// 
public class MapNode
{
    public int Id;
    public int Column;
    public int Type;

    public bool IsStart => Column == 1 && !IsHub;
    public bool IsGoal;
    public bool IsHub;

    // 경로 정보 (UI 세로 배치용)
    public int PathIndex;   // 이 노드가 속한 경로 번호 (0=위, 1=중간, 2=아래)
    public int PathCount;   // 전체 경로 수

    // 같은 경로 내 트랙 분기 정보
    public int TrackIndex;
    public int TrackCount;

    public List<MapNode> Next = new ();
    public List<MapNode> Prev = new ();

    public MapNode(int id, int column, int trackIndex = 0, int trackCount = 1, bool isGoal = false)
    {
        Id = id;
        Column = column;
        TrackIndex = trackIndex;
        TrackCount = trackCount;
        IsGoal = isGoal;
        Type = 0;
    }
}
