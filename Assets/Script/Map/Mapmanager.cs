using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Mapmanager : MonoBehaviour
{
    public int N;
    public float EventChance;
    public int PathCount = 3;

    // ── Inspector에서 확률 조절 ──────────────────────────────
    [Header("Node Type Weights (합산 기준 상대 가중치)")]
    [SerializeField] float weightNormal = 40f;
    [SerializeField] float weightElite = 10f;
    [SerializeField] float weightRest = 20f;
    [SerializeField] float weightEvent = 20f;
    [SerializeField] float weightShop = 10f;

    // ── 생성 결과 ────────────────────────────────────────────
    public MapNode StartHub;
    public MapNode GoalHub;
    public List<MapGenerator> Generators = new();
    public List<MapNode> AllNodes = new();

    // 첫 몇 열을 무조건 Normal로 고정할지
    const int ForcedNormalColumns = 2;

    public Mapmanager(int n = 6, float eventChance = 0.2f, int pathCount = 3)
    {
        N = n;
        EventChance = eventChance;
        PathCount = pathCount;
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
            Type = NodeType.StartHub
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
            Type = NodeType.GoalHub
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

        // 1. 마지막 열(GoalHub 직전) → 무조건 Rest
        int lastCol = candidates.Max(n => n.Column);
        foreach (var node in candidates.Where(n => n.Column == lastCol))
        {
            node.Type = NodeType.Rest;
        }

        // 2. 첫 3열 → 무조건 Normal
        foreach (var node in candidates.Where(n => n.Column <= ForcedNormalColumns))
        {
            node.Type = NodeType.Normal;
        }

        // 3. Era 구분 (StageDatabase와 동일한 로직)
        int threshold = N / 2;
        int earlyEndCol = threshold + 1;

        var earlyNodes = candidates.Where(n => n.Column > ForcedNormalColumns && n.Column <= earlyEndCol).ToList();
        var lateNodes = candidates.Where(n => n.Column > earlyEndCol && n.Column < lastCol).ToList();

        // 4. 각 Era별 최소 보장 (Elite, Rest, Shop, Event)
        GuaranteeTypesInGroup(earlyNodes);
        GuaranteeTypesInGroup(lateNodes);

        // 5. 나머지 노드들 순차적 배정 (제약 조건 적용하며 채우기)
        var remainingNodes = candidates
            .Where(n => n.Column > ForcedNormalColumns && n.Column < lastCol)
            .OrderBy(n => n.Column)
            .ToList();

        foreach (var node in remainingNodes)
        {
            // 이미 보장 로직으로 배정된 경우(Normal이 아닌 경우) 패스
            if (node.Type != NodeType.Normal) continue;

            // 제약 조건 체크
            if (IsThirdConsecutiveSpecial(node))
            {
                node.Type = NodeType.Normal;
            }
            else
            {
                node.Type = PickRandomType();
            }
        }
    }

    /// <summary>
    /// 주어진 노드 리스트 내에서 Elite, Rest, Shop, Event가 최소 1회씩은 나오도록 우선 배정
    /// </summary>
    void GuaranteeTypesInGroup(List<MapNode> group)
    {
        if (group.Count == 0) return;

        NodeType[] toGuarantee = { NodeType.Elite, NodeType.Rest, NodeType.Shop, NodeType.Event };
        var shuffled = group.OrderBy(_ => Random.value).ToList();

        int assignedCount = 0;
        foreach (var type in toGuarantee)
        {
            // 이미 이 그룹에 해당 타입이 (우연히라도) 배정되어 있는지 체크 (현재는 모두 Normal이겠지만 확장성 대비)
            if (group.Any(n => n.Type == type)) continue;

            // 배정 가능한 노드 찾기 (연속 3회 제한을 '현재' 상태 기준으로 체크)
            foreach (var node in shuffled)
            {
                if (node.Type == NodeType.Normal && !IsThirdConsecutiveSpecial(node))
                {
                    node.Type = type;
                    assignedCount++;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 노드에 도달하는 모든 경로 중 하나라도 '연속 2회 특수 노드'인 경우가 있는지 체크
    /// </summary>
    bool IsThirdConsecutiveSpecial(MapNode node)
    {
        if (node.Prev.Count == 0) return false;

        foreach (var prev in node.Prev)
        {
            // 직전이 Normal이면 통과
            if (prev.Type == NodeType.Normal) continue;

            // 직전이 특수라면, 그 전꺼까지 확인
            foreach (var prevPrev in prev.Prev)
            {
                if (prevPrev.Type != NodeType.Normal && !prevPrev.IsHub)
                {
                    // [Prev -> 특수] AND [PrevPrev -> 특수] 이면 현재는 반드시 Normal이어야 함
                    return true;
                }
            }
        }
        return false;
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
        float acc = 0f;
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