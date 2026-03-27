using System.Collections.Generic;
 
/// <summary>
/// 맵의 노드 하나를 나타내는 데이터 클래스.
/// </summary>
public class MapNode
{
    public int      Id;
    public int      Column;
    public NodeType Type;   // int → NodeType으로 변경
 
    public bool IsStart => Column == 1 && !IsHub;
    public bool IsGoal;
    public bool IsHub;
 
    // 경로 정보 (UI 세로 배치용)
    public int PathIndex;
    public int PathCount;
 
    // 같은 경로 내 트랙 분기 정보
    public int TrackIndex;
    public int TrackCount;
 
    public List<MapNode> Next = new();
    public List<MapNode> Prev = new();
 
    public MapNode(int id, int column, int trackIndex = 0, int trackCount = 1, bool isGoal = false)
    {
        Id         = id;
        Column     = column;
        TrackIndex = trackIndex;
        TrackCount = trackCount;
        IsGoal     = isGoal;
        Type       = NodeType.Normal;
    }
}