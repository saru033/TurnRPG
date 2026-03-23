using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class BattleUI : MonoBehaviour
{
    [Header("References")]
    public BattleManager battleManager;
    public RectTransform panelRect;        // 비율 계산 기준 패널
    public RectTransform gaugeBarRect;     // 행동게이지 바
    public RectTransform skillAreaRect;    // 스킬 버튼 영역
    public GameObject portraitPrefab;
    public GameObject skillButtonRoot;
    public Button[] skillButtons;
    public TextMeshProUGUI[] skillLabels;
    public RectTransform ImgAreaRect;


    // -------------------------------------------------------
    // 레이아웃 비율 (패널 기준)
    // -------------------------------------------------------
    [Header("Layout — ActionBar (패널 기준 비율)")]
    [Range(0f, 0.2f)] public float gaugeBarLeftRatio = 0.02f;  // 좌측에서 n% 위치
    [Range(0.01f, 0.1f)] public float gaugeBarWidthRatio = 0.03f;  // 가로 크기 k%
    [Range(0.1f, 1.0f)] public float gaugeBarHeightRatio = 0.8f;   // 세로 크기 h%

    [Header("Layout — Skill Area (패널 기준 비율)")]
    [Range(0.1f, 0.6f)] public float skillAreaWidthRatio = 0.30f; // 스킬 영역 가로 a%
    [Range(0.1f, 0.5f)] public float skillAreaHeightRatio = 0.18f; // 스킬 영역 높이 b%
    [Range(0f, 0.1f)] public float skillAreaBottomRatio = 0.02f; // 하단 여백

    [Header("Layout — Portrait Icon (패널 기준 비율)")]
    [Range(0.02f, 0.12f)] public float portraitSizeRatio = 0.06f;  // 아이콘 크기 c%


    [Header("Layout — image Area (패널 기준 비율)")]
    [Range(0.1f, 0.5f)] public float imageAreaHeightRatio = 0.2f; // 이미지 영역 높이 b%

    // -------------------------------------------------------
    // 런타임 계산값
    // -------------------------------------------------------
    float _panelW;
    float _panelH;
    float _portraitSize;

    // -------------------------------------------------------
    // 캐릭터/포트레이트 목록
    // -------------------------------------------------------
    List<GaugePortrait> portraits = new ();
    List<BattleCharacter> characters = new ();

    // -------------------------------------------------------
    // 레이아웃 계산
    // -------------------------------------------------------
    void ComputeLayout()
    {
        if (panelRect == null)
        {
            _panelW = Screen.width;
            _panelH = Screen.height;
        }
        else
        {
            _panelW = panelRect.rect.width;
            _panelH = panelRect.rect.height;
        }

        // --- ActionBar ---
        if (gaugeBarRect != null)
        {
            float barW = _panelW * gaugeBarWidthRatio;
            float barH = _panelH * gaugeBarHeightRatio;
            float barX = _panelW * gaugeBarLeftRatio;

            // 앵커/피벗은 씬에서 설정한 값 그대로 유지하되,
            // sizeDelta 직접 수정 시 앵커가 Stretch로 되어 있으면 값이 튀는 현상 방지
            gaugeBarRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barW);
            gaugeBarRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, barH);
            
            gaugeBarRect.anchoredPosition = new Vector2(barX, gaugeBarRect.anchoredPosition.y);
        }

        // --- Skill Area ---
        if (skillAreaRect != null)
        {
            float areaW = _panelW * skillAreaWidthRatio;
            float areaH = _panelH * skillAreaHeightRatio;

            // 앵커: 우하단 기준
            skillAreaRect.anchorMin = new Vector2(1f, 0f);
            skillAreaRect.anchorMax = new Vector2(1f, 0f);
            skillAreaRect.pivot = new Vector2(1f, 0f);

            skillAreaRect.sizeDelta = new Vector2(areaW, areaH);
            skillAreaRect.anchoredPosition = new Vector2(0f, _panelH * skillAreaBottomRatio);

            // 스킬 버튼 크기 = 스킬 영역 높이 (정사각형)
            float btnSize = areaH;
            if (skillButtons != null)
            {
                for (int i = 0; i < skillButtons.Length; i++)
                {
                    var rt = skillButtons[i].GetComponent<RectTransform>();
                    if (rt == null) continue;

                    rt.sizeDelta = new Vector2(btnSize, btnSize);

                    // 버튼을 왼쪽부터 간격 없이 배치
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot = new Vector2(0f, 0f);
                    rt.anchoredPosition = new Vector2(i * btnSize, 0f);
                }
            }
        }

        if (ImgAreaRect != null)
        {
            float areaH = _panelH * imageAreaHeightRatio;
            float areaW = areaH * (2250f / 1250f); // 비율 고정 (1.8배)

            ImgAreaRect.anchorMin = new Vector2(0f, 0f);
            ImgAreaRect.anchorMax = new Vector2(0f, 0f);
            ImgAreaRect.pivot = new Vector2(0f, 0f);

            ImgAreaRect.sizeDelta = new Vector2(areaW, areaH);
        }


        // --- Portrait 크기 ---
        _portraitSize = _panelH * portraitSizeRatio;

        // 조정이 끝난 이후 활성화
        gaugeBarRect.gameObject.SetActive(true);
    }

    // -------------------------------------------------------
    // 초기화
    // -------------------------------------------------------
    public void Init(List<BattleCharacter> chars)
    {
        if (gaugeBarRect == null) { Debug.LogError("[BattleUI] gaugeBarRect 미할당"); return; }
        if (portraitPrefab == null) { Debug.LogError("[BattleUI] portraitPrefab 미할당"); return; }

        ComputeLayout();

        // 기존 아이콘 정리
        foreach (var p in portraits)
            if (!object.ReferenceEquals(p, null)) Destroy(p.gameObject);
        portraits.Clear();
        characters.Clear();

        // 아이콘 생성
        for (int i = 0; i < chars.Count; i++)
        {
            var c = chars[i];
            var go = Instantiate(portraitPrefab, gaugeBarRect);

            var portrait = go.GetComponent<GaugePortrait>();

            var img = go.transform.Find("Icon")?.GetComponent<Image>();
                if (img != null && c.Data.iconImage != null)
             img.sprite = c.Data.iconImage;


             
            if (object.ReferenceEquals(portrait, null))
            {
                Debug.LogError($"[BattleUI] GaugePortrait 컴포넌트 없음: {c.Name}");
                continue;
            }

            // 아이콘 크기 적용
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(_portraitSize, _portraitSize);

            // 아군 왼쪽 / 적 오른쪽 x 오프셋
            portrait.SetXOffset(c.IsPlayer ? -i * (_portraitSize * 0.6f) : i * (_portraitSize * 0.6f));
            portrait.Init(c);

            portraits.Add(portrait);
            characters.Add(c);
        }

        UpdateGaugePositions(chars);

        // 스킬 버튼 연결
        if (skillButtons != null)
        {
            var bm = battleManager;
            for (int i = 0; i < skillButtons.Length; i++)
            {
                int idx = i;
                skillButtons[i].onClick.RemoveAllListeners();
                skillButtons[i].onClick.AddListener(() =>
                {
                    if (object.ReferenceEquals(bm, null)) { Debug.LogError("[BattleUI] battleManager null"); return; }
                    bm.OnSkillSelected(idx);
                });

                if (skillLabels != null && i < skillLabels.Length && skillLabels[i] != null)
                    skillLabels[i].text = $"{i + 1}스킬";
            }
        }
    }

    // -------------------------------------------------------
    // 게이지 위치 갱신
    // -------------------------------------------------------
    public void UpdateGaugePositions(List<BattleCharacter> chars)
    {
        for (int i = 0; i < portraits.Count && i < characters.Count; i++)
            portraits[i].UpdatePosition(gaugeBarRect);
    }

    // -------------------------------------------------------
    // 하이라이트
    // -------------------------------------------------------
    public void HighlightActor(BattleCharacter actor)
    {
        for (int i = 0; i < characters.Count; i++)
            portraits[i].SetHighlight(object.ReferenceEquals(characters[i], actor));
    }

    // -------------------------------------------------------
    // 스킬 버튼 표시/숨김
    // -------------------------------------------------------
    public void SetSkillButtonsVisible(bool visible)
    {
        if (skillButtonRoot == null) return;

        var rt = skillButtonRoot.GetComponent<RectTransform>();
        if (rt == null) return;

        float areaW = _panelW * skillAreaWidthRatio;

        rt.DOKill();

        if (visible)
        {
            // 오른쪽 바깥에서 시작해서 제자리로 슬라이드
            skillButtonRoot.SetActive(true);
            rt.anchoredPosition = new Vector2(areaW, rt.anchoredPosition.y);
            rt.DOAnchorPosX(0f, 0.2f).SetEase(Ease.OutCubic);
        }
        else
        {
            // 오른쪽 바깥으로 슬라이드 후 비활성화
            rt.DOAnchorPosX(areaW, 0.15f)
              .SetEase(Ease.InCubic)
              .OnComplete(() => skillButtonRoot.SetActive(false));
        }
    }


    // -------------------------------------------------------
    // 이미지 칸 표시/숨김
    // -------------------------------------------------------
public void SetSideImageVisible(bool visible , BattleCharacter actor)
    {
        if (ImgAreaRect == null) return;

        var rt = ImgAreaRect.GetComponent<RectTransform>();
        if (rt == null) return;

        float areaW = _panelW * skillAreaWidthRatio;

        rt.DOKill();

        if (visible)
        {
            var img = ImgAreaRect.transform.Find("Image")?.GetComponent<Image>();
            if (img != null && actor.Data.sideImage != null){
                img.sprite = actor.Data.sideImage;
                img.preserveAspect = true;

                float h = ImgAreaRect.rect.height;
        float ratio = (float)actor.Data.sideImage.texture.width / actor.Data.sideImage.texture.height;
        float w = h * ratio;

        var imgRt = img.GetComponent<RectTransform>();
        imgRt.anchorMin = new Vector2(0f, 0f);
        imgRt.anchorMax = new Vector2(0f, 0f);
        imgRt.pivot = new Vector2(0f, 0f);
        imgRt.sizeDelta = new Vector2(w, h);
        imgRt.anchoredPosition = Vector2.zero; // 왼쪽 하단 기준
        
            }
            // 왼쪽 바깥에서 시작해서 제자리로 슬라이드
            ImgAreaRect.gameObject.SetActive(true);
            rt.anchoredPosition = new Vector2(-areaW, rt.anchoredPosition.y);
            rt.DOAnchorPosX(0f, 0.2f).SetEase(Ease.OutCubic);
        }
        else
        {
            // 왼쪽 바깥으로 슬라이드 후 비활성화
            rt.DOAnchorPosX(-areaW, 0.15f)
              .SetEase(Ease.InCubic)
              .OnComplete(() => ImgAreaRect.gameObject.SetActive(false));
        }
    }
}
