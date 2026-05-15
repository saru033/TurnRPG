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

    [Header("Generation Settings")]
    [SerializeField] float crossLinkChance = 0.2f; // [추가] 인접 경로 간 연결 확률

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

        // [추가] 인접 경로 간 교차 연결 생성
        AddCrossLinks();

        // 타입 배정
        AssignTypes();
    }

    /// <summary>
    /// 인접한 경로(PathIndex ± 1)의 다음 열 노드와 연결하는 로직
    /// </summary>
    void AddCrossLinks()
    {
        // 1. 노드들을 (열, 경로, 트랙) 단위로 매핑
        var nodeMap = new Dictionary<(int col, int path, int track), MapNode>();
        int maxCol = 0;
        foreach (var node in AllNodes)
        {
            if (node.IsHub) continue;
            nodeMap[(node.Column, node.PathIndex, node.TrackIndex)] = node;
            if (node.Column > maxCol) maxCol = node.Column;
        }

        // 2. 모든 일반 노드를 순회하며 인접 경로 연결 시도
        for (int col = 1; col < maxCol; col++)
        {
            for (int path = 0; path < PathCount; path++)
            {
                // 현재 경로의 각 트랙(0, 1) 확인
                for (int track = 0; track < 2; track++)
                {
                    if (!nodeMap.TryGetValue((col, path, track), out MapNode currentNode)) continue;

                    // 위/아래 인접 경로 확인
                    int[] neighborPaths = { path - 1, path + 1 };
                    foreach (int nPath in neighborPaths)
                    {
                        if (nPath < 0 || nPath >= PathCount) continue;

                        if (Random.value < crossLinkChance)
                        {
                            bool isDown = nPath > path;

                            // [Smart Connection] 
                            // 아래 경로로 연결 시: 현재의 아래쪽 트랙에서 -> 대상의 위쪽 트랙으로
                            // 위 경로로 연결 시: 현재의 위쪽 트랙에서 -> 대상의 아래쪽 트랙으로

                            bool canStart = false;
                            if (isDown)
                                canStart = (currentNode.TrackCount == 1) || (track == 1);
                            else
                                canStart = (track == 0);

                            if (!canStart) continue;

                            MapNode targetNode = null;
                            if (isDown)
                            {
                                // 대상 경로의 위쪽 트랙(0) 탐색
                                nodeMap.TryGetValue((col + 1, nPath, 0), out targetNode);
                            }
                            else
                            {
                                // 대상 경로의 아래쪽 트랙 탐색 (트랙이 2개면 1번, 1개면 0번)
                                if (!nodeMap.TryGetValue((col + 1, nPath, 1), out targetNode))
                                    nodeMap.TryGetValue((col + 1, nPath, 0), out targetNode);
                            }

                            if (targetNode != null)
                            {
                                Link(currentNode, targetNode);
                            }
                        }
                    }
                }
            }
        }
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

        // 1-2. [추가] 마지막 직전 열 → 무조건 Shop
        int beforeLastCol = lastCol - 1;
        if (beforeLastCol > ForcedNormalColumns)
        {
            foreach (var node in candidates.Where(n => n.Column == beforeLastCol))
            {
                node.Type = NodeType.Shop;
            }
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
        var lateNodes = candidates.Where(n => n.Column > earlyEndCol && n.Column < beforeLastCol).ToList();

        // 4. 각 Era별 최소 보장 (Elite, Rest, Shop, Event)
        GuaranteeTypesInGroup(earlyNodes);
        GuaranteeTypesInGroup(lateNodes);

        // 5. 나머지 노드들 순차적 배정 (제약 조건 적용하며 채우기)
        var remainingNodes = candidates
            .Where(n => n.Column > ForcedNormalColumns && n.Column < beforeLastCol)
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