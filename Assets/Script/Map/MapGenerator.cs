using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 구조를 생성하는 클래스.
/// N+2개 노드(Start + N개 중간 + Goal)를 만들고,
/// 각 중간 열에서 splitChance 확률로 갈림길을 만든다.
/// 갈라진 경우 다음 열에서 즉시 합류.
/// </summary>

public class MapGenerator : MonoBehaviour
{
    public int N;
    public float EventChance;

    public List<MapNode> AllNodes = new ();
    public MapNode StartNode;
    public MapNode GoalNode;
    public Dictionary<int, List<MapNode>> ColumnMap = new ();

    // UI 배치용 — 이 경로가 전체 경로 중 몇 번째인지
    public int PathIndex;
    public int PathCount;

    public MapGenerator(int n = 6, float eventChance = 0.2f)
    {
        N = n;
        EventChance = eventChance;
    }

    /// <summary>단독 사용 시 (MapManager 없이)</summary>
    public void Generate() => GenerateWithIdOffset(0, 0, 1);

    /// <summary>
    /// MapManager에서 호출. idOffset으로 전체 Id 충돌 방지.
    /// pathIndex / pathCount는 UI 배치(세로 위치)에 사용.
    /// </summary>
    public void GenerateWithIdOffset(int idOffset, int pathIndex, int pathCount)
    {
        AllNodes.Clear();
        ColumnMap.Clear();

        PathIndex = pathIndex;
        PathCount = pathCount;

        int id = idOffset;

        // --- Start 노드 ---
        StartNode = new MapNode(id++, column: 1, trackIndex: 0, trackCount: 1)
        {
            PathIndex = pathIndex,
            PathCount = pathCount
        };
        RegisterNode(StartNode);

        List<MapNode> currentTracks = new () { StartNode };

        // --- 중간 노드 (column 2 ~ N+1) ---
        for (int col = 2; col <= N + 1; col++)
        {
            int prevCount = currentTracks.Count;
            bool triggered = Random.value < EventChance;

            List<MapNode> nextTracks;

            if (triggered && prevCount == 1)
            {
                // Split: 1 → 2
                var top = MakeNode(id++, col, 0, 2, pathIndex, pathCount);
                var bot = MakeNode(id++, col, 1, 2, pathIndex, pathCount);

                Link(currentTracks[0], top);
                Link(currentTracks[0], bot);

                nextTracks = new () { top, bot };
            }
            else if (triggered && prevCount == 2)
            {
                // Merge: 2 → 1
                var merged = MakeNode(id++, col, 0, 1, pathIndex, pathCount);

                Link(currentTracks[0], merged);
                Link(currentTracks[1], merged);

                nextTracks = new () { merged };
            }
            else
            {
                // 유지
                nextTracks = new ();
                for (int t = 0; t < prevCount; t++)
                {
                    var node = MakeNode(id++, col, t, prevCount, pathIndex, pathCount);
                    Link(currentTracks[t], node);
                    nextTracks.Add(node);
                }
            }

            currentTracks = nextTracks;
        }

        // --- Goal 노드 (column N+2) ---
        GoalNode = new MapNode(id++, column: N + 2, trackIndex: 0, trackCount: 1, isGoal: true)
        {
            PathIndex = pathIndex,
            PathCount = pathCount
        };
        RegisterNode(GoalNode);

        foreach (var track in currentTracks)
            Link(track, GoalNode);
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    MapNode MakeNode(int id, int col, int trackIdx, int trackCount, int pathIdx, int pathCnt)
    {
        var node = new MapNode(id, col, trackIdx, trackCount)
        {
            PathIndex = pathIdx,
            PathCount = pathCnt
        };
        RegisterNode(node);
        return node;
    }

    void RegisterNode(MapNode node)
    {
        AllNodes.Add(node);
        if (!ColumnMap.ContainsKey(node.Column))
            ColumnMap[node.Column] = new List<MapNode>();
        ColumnMap[node.Column].Add(node);
    }

    void Link(MapNode a, MapNode b)
    {
        a.Next.Add(b);
        b.Prev.Add(a);
    }
}
