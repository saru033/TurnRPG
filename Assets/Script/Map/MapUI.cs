using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 맵을 Canvas 위에 렌더링하고 플레이어 이동을 처리하는 MonoBehaviour.
///

public class MapUI : MonoBehaviour
{
    [Header("References")]
    public RectTransform mapRoot;
    public RectTransform panelRect;          // 비율 계산 기준이 되는 Panel
    public GameObject nodeButtonPrefab;
    public GameObject hubPrefab;
    public GameObject edgeImagePrefab;

    [Header("Map Settings")]
    [Range(3, 30)] public int nodeCount = 6;
    [Range(0f, 1f)] public float splitChance = 0.2f;
    [Range(2, 4)] public int pathCount = 3;

    [Header("Layout (Panel 높이 기준 비율 0~1)")]
    [Range(0.05f, 0.4f)] public float columnSpacingRatio = 0.18f;  // 열 간 가로 간격
    [Range(0.03f, 0.2f)] public float trackSpacingRatio = 0.08f;  // 트랙 세로 간격
    [Range(0.1f, 0.5f)] public float pathSpacingRatio = 0.28f;  // 경로 간 세로 간격
    [Range(0.04f, 0.18f)] public float nodeSizeRatio = 0.09f;  // 노드 크기
    [Range(0.05f, 0.8f)] public float hubWidthRatio = 0.10f;  // 허브 너비
    [Range(0.2f, 0.8f)] public float hubHeightRatio = 0.55f;  // 허브 높이
    [Range(0.002f, 0.01f)] public float edgeThicknessRatio = 0.005f; // 엣지 두께

    // ── 노드 타입별 스프라이트 ─────────────────────────────────
    [Header("Node Type Sprites")]
    [Tooltip("인덱스 순서: 0=Normal 1=Elite 2=Rest 3=Event 4=Shop 5=StartHub 6=GoalHub")]
    public Sprite[] nodeTypeSprites = new Sprite[7];

    [Header("Colors")]
    public Color visitedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    public Color currentColor = new Color(0.20f, 0.75f, 0.45f, 1f);
    public Color reachableColor = new Color(0.95f, 0.85f, 0.30f, 1f);
    public Color lockedColor = new Color(0.55f, 0.53f, 0.50f, 1f);
    public Color hubColor = new Color(0.18f, 0.40f, 0.72f, 1f);
    public Color edgeVisited = new Color(0.25f, 0.25f, 0.25f, 1f);
    public Color edgeDefault = new Color(0.55f, 0.53f, 0.50f, 0.6f);

    // -------------------------------------------------------
    // 런타임에 계산된 실제 픽셀 크기
    // -------------------------------------------------------
    float columnSpacing;
    float trackSpacing;
    float pathSpacing;
    float nodeSize;
    float hubWidth;
    float hubHeight;
    float edgeThickness;



    // -------------------------------------------------------
    // 실제로 불러올 게임 패널
    // -------------------------------------------------------
    [Header("Game Panel")]
    public GameObject BattlePanel;

    void ComputeLayout()
    {
        // panelRect가 없으면 Screen 높이로 fallback
        float h = panelRect != null ? panelRect.rect.height : Screen.height;

        columnSpacing = h * columnSpacingRatio;
        trackSpacing = h * trackSpacingRatio;
        pathSpacing = h * pathSpacingRatio;
        nodeSize = h * nodeSizeRatio;
        hubWidth = h * hubWidthRatio;
        hubHeight = h * hubHeightRatio;
        edgeThickness = h * edgeThicknessRatio;
    }

    // ── 런타임 상태 ──────────────────────────────────────────
    Mapmanager manager;
    MapNode currentNode;
    HashSet<int>        visitedIds   = new();
    HashSet<(int, int)> visitedEdges = new();
 
    Dictionary<int, Button> nodeButtons = new();
    Dictionary<int, Image>  nodeImages  = new();
    List<EdgeVisual>         edgeVisuals = new();
 
    struct EdgeVisual
    {
        public Image image;
        public int   fromId;
        public int   toId;
    }
 
    // ── Unity lifecycle ───────────────────────────────────
    void Start() => GenerateAndDraw();
 
    // ── Public API ────────────────────────────────────────
    public void GenerateAndDraw()
    {
        ComputeLayout();
        ClearUI();
 
        manager = new Mapmanager(nodeCount, splitChance, pathCount);
        manager.Generate();
 
        visitedIds.Clear();
        visitedEdges.Clear();
 
        currentNode = manager.StartHub;
        visitedIds.Add(currentNode.Id);
 
        DrawEdges();
        DrawNodes();
        ResizeContent();
        RefreshUI();
    }
 
    // ── 위치 계산 ─────────────────────────────────────────
    Vector2 NodePosition(MapNode node)
    {
        float x = node.Column * columnSpacing + columnSpacing * 0.5f;
        float y;
 
        if (node.IsHub)
        {
            y = 0f;
        }
        else
        {
            float pathTotalHeight  = (node.PathCount  - 1) * pathSpacing;
            float pathCenterY      = node.PathIndex * pathSpacing - pathTotalHeight / 2f;
            float trackTotalHeight = (node.TrackCount - 1) * trackSpacing;
            float trackOffsetY     = node.TrackIndex * trackSpacing - trackTotalHeight / 2f;
            y = pathCenterY + trackOffsetY;
        }
 
        return new Vector2(x, -y);
    }
 
    // ── Content 크기 자동 조정 ────────────────────────────
    void ResizeContent()
    {
        float maxX = 0f;
        foreach (var node in manager.AllNodes)
        {
            float rightEdge = NodePosition(node).x + (node.IsHub ? hubWidth : nodeSize);
            if (rightEdge > maxX) maxX = rightEdge;
        }
        mapRoot.sizeDelta = new Vector2(maxX + columnSpacing, mapRoot.sizeDelta.y);
    }
 
    void ClearUI()
    {
        foreach (Transform child in mapRoot) Destroy(child.gameObject);
        nodeButtons.Clear();
        nodeImages.Clear();
        edgeVisuals.Clear();
    }
 
    // ── 노드 생성 ─────────────────────────────────────────
    void DrawNodes()
    {
        foreach (var node in manager.AllNodes)
        {
            var prefab = node.IsHub ? hubPrefab : nodeButtonPrefab;
            var go     = Instantiate(prefab, mapRoot);
            var rt     = go.GetComponent<RectTransform>();
            var btn    = go.GetComponent<Button>();
            var label  = go.GetComponentInChildren<TextMeshProUGUI>();
            var img    = go.GetComponent<Image>() ?? go.GetComponentInChildren<Image>();
 
            if (rt == null || btn == null)
            {
                Debug.LogError($"[MapUI] 프리팹에 RectTransform 또는 Button이 없습니다. NodeId={node.Id}");
                continue;
            }
 
            rt.anchorMin       = new Vector2(0f, 0.5f);
            rt.anchorMax       = new Vector2(0f, 0.5f);

            if(node.Type == NodeType.StartHub)
            {
                rt.pivot = new Vector2(0.7f, 0.5f);
            }
            else if(node.Type == NodeType.GoalHub)
            {
                rt.pivot = new Vector2(0.3f, 0.5f);
            }
            else
            {
                rt.pivot = new Vector2(0.5f, 0.5f);
            }

            rt.anchoredPosition = NodePosition(node);
            rt.sizeDelta       = node.IsHub
                ? new Vector2(hubWidth, hubHeight)
                : new Vector2(nodeSize,  nodeSize);
 
            // ── 스프라이트 적용 ──────────────────────────
            if (img != null && nodeTypeSprites != null)
            {
                int idx = (int)node.Type;
                if (idx >= 0 && idx < nodeTypeSprites.Length && nodeTypeSprites[idx] != null)
                    img.sprite = nodeTypeSprites[idx];
            }
 
            // ── 라벨 텍스트 ──────────────────────────────
            if (label != null)
            {
                label.text = node.Type switch
                {
                    NodeType.Normal   => "MON",
                    NodeType.Elite    => "ELT",
                    NodeType.Rest     => "REST",
                    NodeType.Event    => "EVT",
                    NodeType.Shop     => "SHOP",
                    NodeType.StartHub => "START",
                    NodeType.GoalHub  => "GOAL",
                    _                 => "?",
                };
            }
 
            MapNode captured = node;
            btn.onClick.AddListener(() => OnNodeClicked(captured));
 
            nodeButtons[node.Id] = btn;
            if (img != null) nodeImages[node.Id] = img;
        }
    }
 
    // ── 엣지 생성 ─────────────────────────────────────────
    void DrawEdges()
    {
        foreach (var node in manager.AllNodes)
        {
            foreach (var next in node.Next)
            {
                var go  = Instantiate(edgeImagePrefab, mapRoot);
                var img = go.GetComponent<Image>();
                var rt  = go.GetComponent<RectTransform>();
 
                go.transform.SetAsFirstSibling();
                PositionEdge(rt, NodePosition(node), NodePosition(next));
 
                edgeVisuals.Add(new EdgeVisual { image = img, fromId = node.Id, toId = next.Id });
            }
        }
    }
 
    void PositionEdge(RectTransform rt, Vector2 from, Vector2 to)
    {
        rt.anchorMin        = new Vector2(0f, 0.5f);
        rt.anchorMax        = new Vector2(0f, 0.5f);
        rt.pivot            = new Vector2(0f, 0.5f);
        rt.anchoredPosition = from;
 
        Vector2 dir   = to - from;
        float   len   = dir.magnitude;
        float   angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
 
        rt.sizeDelta     = new Vector2(len, edgeThickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
 
    // ── 상태 → UI 갱신 ───────────────────────────────────
    void RefreshUI()
    {
        var reachableIds = new HashSet<int>();
        foreach (var next in currentNode.Next)
            reachableIds.Add(next.Id);
 
        foreach (var node in manager.AllNodes)
        {
            if (!nodeButtons.TryGetValue(node.Id, out var btn)) continue;
 
            bool isVisited  = visitedIds.Contains(node.Id);
            bool isCurrent  = node.Id == currentNode.Id;
            bool isReachable= reachableIds.Contains(node.Id);
 
            if (nodeImages.TryGetValue(node.Id, out var img))
            {
                // 허브는 hubColor, 일반 노드는 상태에 따라 색상 오버레이
                img.color = node.IsHub    ? hubColor
                          : isCurrent    ? currentColor
                          : isVisited    ? visitedColor
                          : isReachable  ? reachableColor
                          : lockedColor;
            }
 
            btn.interactable = isReachable;
        }
 
        foreach (var ev in edgeVisuals)
        {
            if (ev.image != null)
                ev.image.color = visitedEdges.Contains((ev.fromId, ev.toId))
                    ? edgeVisited : edgeDefault;
        }
    }
 
    // ── 인터랙션 ──────────────────────────────────────────
    void OnNodeClicked(MapNode target)
    {
        bool canMove = false;
        foreach (var next in currentNode.Next)
            if (next.Id == target.Id) { canMove = true; break; }
        if (!canMove) return;
 
        visitedEdges.Add((currentNode.Id, target.Id));
        currentNode = target;
        visitedIds.Add(currentNode.Id);
 
        RefreshUI();
        
        switch(currentNode.Type)
        {
            case NodeType.Normal:
                BattlePanel.SetActive(true);
                gameObject.SetActive(false);
                break;
            case NodeType.Elite:
                UnityEngine.Debug.Log("[MapUI] 엘리트 노드");
                break;
            case NodeType.Rest:
                UnityEngine.Debug.Log("[MapUI] 휴식 노드");
                break;
            case NodeType.Event:
                UnityEngine.Debug.Log("[MapUI] 이벤트 노드");
                break;
            case NodeType.Shop:
                UnityEngine.Debug.Log("[MapUI] 상점 노드");
                break;
            case NodeType.StartHub:
                UnityEngine.Debug.Log("[MapUI] 시작 허브 노드");
                break;
            case NodeType.GoalHub:
                UnityEngine.Debug.Log("[MapUI] 목표 허브 노드");
                break;
        }

        if (currentNode.IsHub && currentNode.Id == manager.GoalHub.Id)
            OnGoalReached();
    }
 
    void OnGoalReached()
    {
        Debug.Log("[MapUI] 목적지 도달!");
        // TODO: 다음 씬 전환 or 결과 처리
    }
}