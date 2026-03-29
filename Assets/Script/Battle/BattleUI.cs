using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Linq;

public class BattleUI : MonoBehaviour
{
    [Header("References")]
    public BattleManager battleManager;
    public RectTransform panelRect;        // 비율 계산 기준 패널
    public RectTransform gaugeBarRect;     // 행동게이지 바
    public RectTransform skillAreaRect;    // 스킬 버튼 영역
    [Header("UI Panels")]
    [Tooltip("3스킬 컷씬 연출용 GameObject")]
    public GameObject ultimateCutsceneRoot;
    public Image ultimateBlackScreen;       // [추가] 3스킬 배경 어두워짐용
    public GameObject portraitPrefab;
    public GameObject skillButtonRoot;
    public Button[] skillButtons;
    public RectTransform ImgAreaRect;
    public RectTransform select_SkillRect;

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
    [Range(0f, 30f)] public float skillButtonSpacing = 10f;

    [Header("Layout — Portrait Icon (패널 기준 비율)")]
    [Range(0.02f, 0.12f)] public float portraitSizeRatio = 0.06f;  // 아이콘 크기 c%


    [Header("Layout — image Area (패널 기준 비율)")]
    [Range(0.1f, 0.5f)] public float imageAreaHeightRatio = 0.2f; // 이미지 영역 높이 b%

    [Header("Layout — select_Skill (패널 기준 비율)")]
    [Range(0.1f, 0.5f)] public float select_SkillWidthRatio = 0.2f; // 이미지 영역 높이 b%

    [Header("따닥 버튼 클릭")]
    private float lastClickTime = 0f;
    private int lastClickedIndex = -1;
    [SerializeField] private float doubleClickThreshold = 0.3f; // 따닥 인식 시간

    // -------------------------------------------------------
    // 런타임 계산값
    // -------------------------------------------------------
    float _panelW;
    float _panelH;
    float _portraitSize;

    // -------------------------------------------------------
    // 캐릭터/포트레이트 목록
    // -------------------------------------------------------
    List<GaugePortrait> portraits = new();
    List<BattleCharacter> characters = new();

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
                    rt.anchoredPosition = new Vector2(i * (btnSize + skillButtonSpacing), 0f);
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


        if (select_SkillRect != null && skillButtons != null && skillButtons.Length > 0)
        {
            float areaH = _panelH * select_SkillWidthRatio;
            select_SkillRect.sizeDelta = new Vector2(areaH, areaH); // 정사각형

            // 첫 번째 스킬 버튼의 자식으로 이동
            select_SkillRect.SetParent(skillButtons[0].transform, false);
            var sr = skillButtons[0].GetComponent<RectTransform>();
            sr.SetAsLastSibling();

            // 앵커 중앙, 위치 0,0
            select_SkillRect.anchorMin = new Vector2(0.5f, 0.5f);
            select_SkillRect.anchorMax = new Vector2(0.5f, 0.5f);
            select_SkillRect.anchoredPosition = Vector2.zero;
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

                    float timeSinceLastClick = Time.time - lastClickTime;
                    bool isDoubleClick = (lastClickedIndex == idx) && (timeSinceLastClick <= doubleClickThreshold);

                    lastClickTime = Time.time;
                    lastClickedIndex = idx;

                    if (isDoubleClick)
                        bm.OnSkillExecute(idx);   // 따닥 → 실행
                    else
                        bm.OnSkillSelected(idx);  // 단일 클릭 → 선택만
                });

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


        // ActionGauge 높을수록 앞에(SiblingIndex 높게)
        var sorted = chars
            .Select((c, i) => new { character = c, index = i })
            .Where(x => x.index < portraits.Count)
            .OrderBy(x => x.character.ActionGauge) // 낮은게 먼저 → 높은게 나중(앞)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
            portraits[sorted[i].index].RectTransform.SetSiblingIndex(i);
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
    public void SetSkillButtonsVisible(bool visible, BattleCharacter actor = null)
    {
        if (skillButtonRoot == null) return;

        var rt = skillButtonRoot.GetComponent<RectTransform>();
        if (rt == null) return;

        float areaW = _panelW * skillAreaWidthRatio;

        rt.DOKill();

        if (visible)
        {
            // 스킬 버튼 UI 업데이트 (아이콘 및 쿨타임/빈칸 상태 처리)
            if (actor != null && skillButtons != null)
            {
                for (int i = 0; i < skillButtons.Length; i++)
                {
                    var skill = actor.ActiveSkills.Count > i ? actor.ActiveSkills[i] : null;
                    var btn = skillButtons[i];
                    var img = btn.GetComponent<Image>();
                    float cd = (actor.SkillCooldowns.Length > i) ? actor.SkillCooldowns[i] : 0;

                    Transform coolTimeObj = btn.transform.Find("CoolTime");
                    if (coolTimeObj != null)
                    {
                        var textObj = coolTimeObj.GetComponentInChildren<TextMeshProUGUI>();
                        if (cd > 0)
                        {
                            coolTimeObj.gameObject.SetActive(true);
                            if (textObj != null) textObj.text = cd.ToString("F0");
                        }
                        else
                        {
                            coolTimeObj.gameObject.SetActive(false);
                            if (textObj != null) textObj.text = "";
                        }
                    }

                    if (skill == null || cd > 0)
                    {
                        btn.interactable = false;
                        img.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                    }
                    else
                    {
                        btn.interactable = true;
                        img.color = Color.white;
                    }

                    if (skill != null && skill.SkillIcon != null) img.sprite = skill.SkillIcon;
                }
            }

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
              .OnComplete(() => { skillButtonRoot.SetActive(false); select_SkillRect.gameObject.SetActive(false); });
        }
    }

    // -------------------------------------------------------
    // 선택한 스킬 표시
    // -------------------------------------------------------
    public void MoveSkillSelectIndicator(int skillIndex)
    {
        if (select_SkillRect == null || skillButtons == null || skillIndex >= skillButtons.Length) return;

        select_SkillRect.gameObject.SetActive(true);
        Transform targetParent = skillButtons[skillIndex].transform;

        // 부모 이동 후 DOAnchorPos로 0,0으로 부드럽게 이동
        select_SkillRect.SetParent(targetParent, true); // 월드 좌표 유지하며 부모 변경

        //버튼 레이어 변경
        targetParent.SetAsLastSibling();

        select_SkillRect.DOAnchorPos(Vector2.zero, 0.2f).SetEase(Ease.OutQuad);
    }


    // -------------------------------------------------------
    // 이미지 칸 표시/숨김
    // -------------------------------------------------------
    public void SetSideImageVisible(bool visible, BattleCharacter actor)
    {
        if (ImgAreaRect == null) return;

        var rt = ImgAreaRect.GetComponent<RectTransform>();
        if (rt == null) return;

        float areaW = _panelW * skillAreaWidthRatio;

        rt.DOKill();

        if (visible)
        {
            var img = ImgAreaRect.transform.Find("Image")?.GetComponent<Image>();
            if (img != null && actor.Data.sideImage != null)
            {
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
