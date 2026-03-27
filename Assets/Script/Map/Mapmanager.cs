using System.Collections.Generic;
using System.Linq;
using UnityEngine;
 
public class Mapmanager : MonoBehaviour
{
    public int   N;
    public float EventChance;
    public int   PathCount = 3;
 
    // ── Inspector에서 확률 조절 ──────────────────────────────
    [Header("Node Type Weights (합산 기준 상대 가중치)")]
    [SerializeField] float weightNormal = 40f;
    [SerializeField] float weightElite  = 10f;
    [SerializeField] float weightRest   = 20f;
    [SerializeField] float weightEvent  = 20f;
    [SerializeField] float weightShop   = 10f;
 
    // ── 생성 결과 ────────────────────────────────────────────
    public MapNode              StartHub;
    public MapNode              GoalHub;
    public List<MapGenerator>   Generators = new();
    public List<MapNode>        AllNodes   = new();
 
    // 첫 몇 열을 무조건 Normal로 고정할지
    const int ForcedNormalColumns = 3;
 
    public Mapmanager(int n = 6, float eventChance = 0.2f, int pathCount = 3)
    {
        N           = n;
        EventChance = eventChance;
        PathCount   = pathCount;
    }
 
    // ════════════════════════════════════════════════════════
    // 1. 그래프 생성 (기존 로직 그대로)
    // ════════════════════════════════════════════════════════
    public void Generate()
    {
        Generators.Clear();
        AllNodes.Clear();
 
        int idOffset = 0;
 
        // Start Hub
        StartHub = new MapNode(idOffset++, column: 0, trackIndex: 0, trackCount: 1)
        {
            IsHub = true,
            Type  = NodeType.StartHub
        };
        AllNodes.Add(StartHub);
 
        // 각 경로 생성
        for (int i = 0; i < PathCount; i++)
        {
            var gen = new MapGenerator(N, EventChance);
            gen.GenerateWithIdOffset(idOffset, pathIndex: i, pathCount: PathCount);
            idOffset += gen.AllNodes.Count;
 
            Generators.Add(gen);
            AllNodes.AddRange(gen.AllNodes);
 
            Link(StartHub, gen.StartNode);
        }
 
        // Goal Hub
        int maxCol = 0;
        foreach (var g in Generators)
            foreach (var n in g.AllNodes)
                if (n.Column > maxCol) maxCol = n.Column;
 
        GoalHub = new MapNode(idOffset++, column: maxCol + 1, trackIndex: 0, trackCount: 1)
        {
            IsHub = true,
            Type  = NodeType.GoalHub
        };
        AllNodes.Add(GoalHub);
 
        foreach (var gen in Generators)
            Link(gen.GoalNode, GoalHub);
 
        // 타입 배정
        AssignTypes();
    }
 
    // ════════════════════════════════════════════════════════
    // 2. 타입 배정
    // ════════════════════════════════════════════════════════
    void AssignTypes()
    {
        // 허브 제외, column 오름차순 정렬
        var candidates = AllNodes
            .Where(n => !n.IsHub)
            .OrderBy(n => n.Column)
            .ToList();
 
        // ① 첫 3열 → 무조건 Normal
        foreach (var node in candidates.Where(n => n.Column <= ForcedNormalColumns))
            node.Type = NodeType.Normal;
 
        // ② 나머지 슬롯
        var freeSlots = candidates
            .Where(n => n.Column > ForcedNormalColumns)
            .OrderBy(_ => Random.value)   // 셔플
            .ToList();
 
        // ③ 최소 보장 (노드 수 >= 8 일 때 Elite/Rest/Event/Shop 각 1개)
        var mustInclude = new List<NodeType>();
        if (candidates.Count >= 8)
        {
            mustInclude.Add(NodeType.Elite);
            mustInclude.Add(NodeType.Rest);
            mustInclude.Add(NodeType.Event);
            mustInclude.Add(NodeType.Shop);
        }
 
        var guaranteedIds = new HashSet<int>();
        for (int i = 0; i < mustInclude.Count && i < freeSlots.Count; i++)
        {
            freeSlots[i].Type = mustInclude[i];
            guaranteedIds.Add(freeSlots[i].Id);
        }
 
        // ④ 나머지 → 가중치 랜덤
        foreach (var node in freeSlots)
        {
            if (!guaranteedIds.Contains(node.Id))
                node.Type = PickRandomType();
        }
    }
 
    // ────────────────────────────────────────────────────────
    // 가중치 기반 랜덤 타입 (Inspector 값 반영)
    // ────────────────────────────────────────────────────────
    NodeType PickRandomType()
    {
        float[] weights = { weightNormal, weightElite, weightRest, weightEvent, weightShop };
        float total = 0f;
        foreach (var w in weights) total += w;
 
        float roll = Random.value * total;
        float acc  = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            acc += weights[i];
            if (roll <= acc) return (NodeType)i;
        }
        return NodeType.Normal;
    }
 
    void Link(MapNode a, MapNode b)
    {
        a.Next.Add(b);
        b.Prev.Add(a);
    }
}