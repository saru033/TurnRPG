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
    [Range(3, 14)] public int nodeCount = 6;
    [Range(0f, 1f)] public float splitChance = 0.2f;
    [Range(2, 4)] public int pathCount = 3;

    [Header("Layout (Panel 높이 기준 비율 0~1)")]
    [Range(0.05f, 0.4f)] public float columnSpacingRatio = 0.18f;  // 열 간 가로 간격
    [Range(0.03f, 0.2f)] public float trackSpacingRatio = 0.08f;  // 트랙 세로 간격
    [Range(0.1f, 0.5f)] public float pathSpacingRatio = 0.28f;  // 경로 간 세로 간격
    [Range(0.04f, 0.18f)] public float nodeSizeRatio = 0.09f;  // 노드 크기
    [Range(0.05f, 0.2f)] public float hubWidthRatio = 0.10f;  // 허브 너비
    [Range(0.2f, 0.8f)] public float hubHeightRatio = 0.55f;  // 허브 높이
    [Range(0.002f, 0.01f)] public float edgeThicknessRatio = 0.005f; // 엣지 두께

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

    // -------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------
    Mapmanager manager;
    MapNode currentNode;
    HashSet<int> visitedIds = new ();
    HashSet<(int, int)> visitedEdges = new ();

    Dictionary<int, Button> nodeButtons = new ();
    Dictionary<int, Image> nodeImages = new ();
    List<EdgeVisual> edgeVisuals = new ();

    struct EdgeVisual
    {
        public Image image;
        public int fromId;
        public int toId;
    }

    // -------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------
    void Start() => GenerateAndDraw();

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------
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

    // -------------------------------------------------------
    // Position helpers
    // -------------------------------------------------------

    /// <summary>
    /// 노드의 Canvas 상 위치 계산.
    ///
    /// X: column * columnSpacing
    /// Y: 허브는 0, 일반 노드는 경로 인덱스 기반 세로 위치
    ///    + 같은 경로 내 트랙 분기(trackIndex)에 의한 미세 offset
    /// </summary>
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
            // 경로 세로 중심: 위(-pathSpacing) / 중(0) / 아래(+pathSpacing)
            float pathTotalHeight = (node.PathCount - 1) * pathSpacing;
            float pathCenterY = node.PathIndex * pathSpacing - pathTotalHeight / 2f;

            // 트랙 분기 offset (같은 경로 내)
            float trackTotalHeight = (node.TrackCount - 1) * trackSpacing;
            float trackOffsetY = node.TrackIndex * trackSpacing - trackTotalHeight / 2f;

            y = pathCenterY + trackOffsetY;
        }

        return new Vector2(x, -y);   // Unity UI는 아래가 -y
    }

    // -------------------------------------------------------
    // Content 크기 자동 조정
    // -------------------------------------------------------

    /// <summary>
    /// 노드 배치 후 mapRoot(Content)의 너비를 가장 오른쪽 노드 기준으로 설정.
    /// Scroll Rect가 올바른 스크롤 범위를 갖도록 한다.
    /// </summary>
    void ResizeContent()
    {
        float maxX = 0f;
        foreach (var node in manager.AllNodes)
        {
            float rightEdge = NodePosition(node).x + nodeSize;
            if (node.IsHub) rightEdge = NodePosition(node).x + hubWidth;
            if (rightEdge > maxX) maxX = rightEdge;
        }

        // 오른쪽 여백 추가
        float padding = columnSpacing;
        mapRoot.sizeDelta = new Vector2(maxX + padding, mapRoot.sizeDelta.y);
    }

    // -------------------------------------------------------
    void ClearUI()
    {
        foreach (Transform child in mapRoot)
            Destroy(child.gameObject);

        nodeButtons.Clear();
        nodeImages.Clear();
        edgeVisuals.Clear();
    }

    void DrawNodes()
    {
        foreach (var node in manager.AllNodes)
        {
            var prefab = node.IsHub ? hubPrefab : nodeButtonPrefab;
            var go = Instantiate(prefab, mapRoot);
            var rt = go.GetComponent<RectTransform>();
            var btn = go.GetComponent<Button>();
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            var img = go.GetComponent<Image>() ?? go.GetComponentInChildren<Image>();

            if (rt == null || btn == null)
            {
                Debug.LogError($"[MapUI] 프리팹에 RectTransform 또는 Button이 없습니다. NodeId={node.Id}");
                continue;
            }

            // 자식 앵커를 왼쪽 중앙으로 통일
            // Content pivot=(0,0.5)이므로 anchoredPosition.x=0이 Content 왼쪽 끝
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = NodePosition(node);

            if (node.IsHub)
                rt.sizeDelta = new Vector2(hubWidth, hubHeight);
            else
                rt.sizeDelta = new Vector2(nodeSize, nodeSize);

            if (label != null)
            {
                if (node.IsHub && node.Column == 0) label.text = "START";
                else if (node.IsHub) label.text = "GOAL";
                else if (node.IsGoal) label.text = "G";
                else if (node.IsStart) label.text = "S";
                else label.text = node.Type.ToString();
            }

            MapNode captured = node;
            btn.onClick.AddListener(() => OnNodeClicked(captured));

            nodeButtons[node.Id] = btn;
            if (img != null) nodeImages[node.Id] = img;
        }
    }

    void DrawEdges()
    {
        foreach (var node in manager.AllNodes)
        {
            foreach (var next in node.Next)
            {
                var go = Instantiate(edgeImagePrefab, mapRoot);
                var img = go.GetComponent<Image>();
                var rt = go.GetComponent<RectTransform>();

                go.transform.SetAsFirstSibling();

                PositionEdge(rt, NodePosition(node), NodePosition(next));

                edgeVisuals.Add(new EdgeVisual
                {
                    image = img,
                    fromId = node.Id,
                    toId = next.Id
                });
            }
        }
    }

    void PositionEdge(RectTransform rt, Vector2 from, Vector2 to)
    {
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = from;

        Vector2 dir = to - from;
        float len = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rt.sizeDelta = new Vector2(len, edgeThickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    // -------------------------------------------------------
    // State → UI refresh
    // -------------------------------------------------------
    void RefreshUI()
    {
        HashSet<int> reachableIds = new ();
        foreach (var next in currentNode.Next)
            reachableIds.Add(next.Id);

        foreach (var node in manager.AllNodes)
        {
            if (!nodeButtons.TryGetValue(node.Id, out var btn)) continue;

            bool isVisited = visitedIds.Contains(node.Id);
            bool isCurrent = node.Id == currentNode.Id;
            bool isReachable = reachableIds.Contains(node.Id);

            if (nodeImages.TryGetValue(node.Id, out var img))
            {
                if (node.IsHub)
                    img.color = hubColor;
                else if (isCurrent)
                    img.color = currentColor;
                else if (isVisited)
                    img.color = visitedColor;
                else if (isReachable)
                    img.color = reachableColor;
                else
                    img.color = lockedColor;
            }

            // 허브는 항상 클릭 가능 (현재 위치가 아닐 때만 reachable 체크)
            btn.interactable = isReachable;
        }

        // 엣지 색상
        foreach (var ev in edgeVisuals)
        {
            bool isVisitedEdge = visitedEdges.Contains((ev.fromId, ev.toId));
            if (ev.image != null)
                ev.image.color = isVisitedEdge ? edgeVisited : edgeDefault;
        }
    }

    // -------------------------------------------------------
    // Interaction
    // -------------------------------------------------------
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

        if (currentNode.IsHub && currentNode.Id == manager.GoalHub.Id)
            OnGoalReached();
    }

    void OnGoalReached()
    {
        Debug.Log("[MapUI] 목적지 도달!");
        // TODO: 다음 씬 전환 or 결과 처리
    }
}
