using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterPlacer : MonoBehaviour
{
    [Header("References")]
    public RectTransform panelRect;
    public GameObject characterPrefab;

    [Header("기준 해상도")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("아군 슬롯 위치 (패널 기준 비율 x, y)")]
    public Vector2 playerSlot1Ratio = new Vector2(0.42f, 0.05f);  // 오른쪽 뒤
    public Vector2 playerSlot2Ratio = new Vector2(0.32f, 0.10f);  // 가운데 앞
    public Vector2 playerSlot3Ratio = new Vector2(0.22f, 0.05f);  // 왼쪽 뒤

    [Header("적군 슬롯 위치 (패널 기준 비율 x, y)")]
    public Vector2 enemySlot1Ratio = new Vector2(0.58f, 0.05f);  // 왼쪽 뒤
    public Vector2 enemySlot2Ratio = new Vector2(0.68f, 0.10f);  // 가운데 앞
    public Vector2 enemySlot3Ratio = new Vector2(0.78f, 0.05f);  // 오른쪽 뒤

    [Header("렌더 순서 (앞 캐릭터가 위에 그려짐)")]
    // slot2(앞)가 slot1,3(뒤)보다 위에 그려지도록 siblingIndex 조정
    public bool useSortingOrder = true;

    // -------------------------------------------------------
    // Runtime
    // -------------------------------------------------------
    // 슬롯 인덱스(0~2) → 스폰된 GameObject
    Dictionary<int, GameObject> _playerSlots = new();
    Dictionary<int, GameObject> _enemySlots = new();

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------
    public void PlaceCharacters(List<BattleCharacter> characters)
    {
        Clear();

        if (panelRect == null) { Debug.LogError("[CharacterPlacer] panelRect 미할당"); return; }
        if (characterPrefab == null) { Debug.LogError("[CharacterPlacer] characterPrefab 미할당"); return; }

        float panelW = panelRect.rect.width;
        float panelH = panelRect.rect.height;
        float scaleFactor = panelH / referenceResolution.y;

        // 슬롯 정의
        Vector2[] playerSlots = { playerSlot1Ratio, playerSlot2Ratio, playerSlot3Ratio };
        Vector2[] enemySlots = { enemySlot1Ratio, enemySlot2Ratio, enemySlot3Ratio };

        int playerIndex = 0;
        int enemyIndex = 0;

        foreach (var c in characters)
        {
            bool isPlayer = c.IsPlayer;
            int slotIndex = isPlayer ? playerIndex : enemyIndex;
            var slots = isPlayer ? playerSlots : enemySlots;

            if (slotIndex >= slots.Length)
            {
                Debug.LogWarning($"[CharacterPlacer] 슬롯 초과: {c.Name} (슬롯 최대 {slots.Length}명)");
                continue;
            }

            // CharacterData의 프리팹 사용, 없으면 fallback
            GameObject prefabToUse = c.Data.characterPrefab != null
                ? c.Data.characterPrefab
                : characterPrefab;

            var go = Instantiate(prefabToUse, panelRect);
            var rt = go.GetComponent<RectTransform>();

            if (rt == null) { Debug.LogError($"[CharacterPlacer] RectTransform 없음: {c.Name}"); continue; }

            //나중에 제어를 위해 애니메이터 연결
            var animator = go.GetComponent<Animator>();
            if (animator == null)
                animator = go.GetComponentInChildren<Animator>();

            // BattleCharacter에 Animator 연결
            c.SetAnimator(animator);


            // pivot (0.5, 0) 강제 설정
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);

            // 스케일
            go.transform.localScale = Vector3.one * scaleFactor;

            // 위치
            Vector2 ratio = slots[slotIndex];
            rt.anchoredPosition = new Vector2(panelW * ratio.x, panelH * ratio.y);

            // 렌더 순서 — slot2(앞)가 뒤에 그려지도록
            if (useSortingOrder)
            {
                // 뒤(0,2번) → 나중에, 앞(1번) → 먼저
                bool isFront = slotIndex == 1;
                go.transform.SetSiblingIndex(isFront ? 0 : panelRect.childCount - 1);
            }

            // CharacterView 초기화
            var view = go.GetComponent<CharacterView>();
            if (!object.ReferenceEquals(view, null))
            {
                c.DistinguishNum = slotIndex + 1; // [추가] 슬롯 번호(1, 2, 3) 부여
                view.Init(c);
                c.View = view; // [추가] BattleCharacter에 View 연결
            }

            if (isPlayer)
            {
                _playerSlots[slotIndex] = go;
                playerIndex++;
            }
            else
            {
                _enemySlots[slotIndex] = go;
                enemyIndex++;
            }
        }
    }

    /// <summary>슬롯 번호(1~3)로 아군 CharacterView 반환</summary>
    public CharacterView GetPlayerView(int slotNumber)
    {
        int idx = slotNumber - 1;
        return _playerSlots.TryGetValue(idx, out var go) ? go.GetComponent<CharacterView>() : null;
    }

    /// <summary>슬롯 번호(1~3)로 적군 CharacterView 반환</summary>
    public CharacterView GetEnemyView(int slotNumber)
    {
        int idx = slotNumber - 1;
        return _enemySlots.TryGetValue(idx, out var go) ? go.GetComponent<CharacterView>() : null;
    }

    public void Clear()
    {
        foreach (var kv in _playerSlots)
            if (!object.ReferenceEquals(kv.Value, null)) Destroy(kv.Value);
        foreach (var kv in _enemySlots)
            if (!object.ReferenceEquals(kv.Value, null)) Destroy(kv.Value);

        _playerSlots.Clear();
        _enemySlots.Clear();
    }
}
