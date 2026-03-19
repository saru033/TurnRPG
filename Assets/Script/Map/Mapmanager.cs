using System.Collections.Generic;
using UnityEngine;

public class Mapmanager : MonoBehaviour
{
    public int N;
    public float EventChance;
    public int PathCount = 3;   // 허브에서 뻗는 경로 수 (기본 3)

    public MapNode StartHub;
    public MapNode GoalHub;
    public List<MapGenerator> Generators = new ();
    public List<MapNode> AllNodes = new ();   // 전체 노드 (허브 포함)

    public Mapmanager(int n = 6, float eventChance = 0.2f, int pathCount = 3)
    {
        N = n;
        EventChance = eventChance;
        PathCount = pathCount;
    }

    public void Generate()
    {
        Generators.Clear();
        AllNodes.Clear();

        int idOffset = 0;

        // --- Start Hub ---
        StartHub = new MapNode(idOffset++, column: 0, trackIndex: 0, trackCount: 1)
        {
            IsHub = true
        };
        AllNodes.Add(StartHub);

        // --- 각 경로 생성 ---
        for (int i = 0; i < PathCount; i++)
        {
            var gen = new MapGenerator(N, EventChance);
            gen.GenerateWithIdOffset(idOffset, pathIndex: i, pathCount: PathCount);
            idOffset += gen.AllNodes.Count;

            Generators.Add(gen);
            AllNodes.AddRange(gen.AllNodes);

            // StartHub → 경로 첫 노드
            Link(StartHub, gen.StartNode);
        }

        // --- Goal Hub ---
        // column은 가장 큰 column + 1
        int maxCol = 0;
        foreach (var g in Generators)
            foreach (var n in g.AllNodes)
                if (n.Column > maxCol) maxCol = n.Column;

        GoalHub = new MapNode(idOffset++, column: maxCol + 1, trackIndex: 0, trackCount: 1)
        {
            IsHub = true
        };
        AllNodes.Add(GoalHub);

        // 각 경로 마지막 노드 → GoalHub
        foreach (var gen in Generators)
            Link(gen.GoalNode, GoalHub);
    }

    void Link(MapNode a, MapNode b)
    {
        a.Next.Add(b);
        b.Prev.Add(a);
    }
}
