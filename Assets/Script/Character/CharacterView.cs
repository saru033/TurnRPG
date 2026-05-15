using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using TurnRPG.SkillSystem;
using DG.Tweening;
using System.Linq; // [추가] LINQ 확장 메서드 사용

/// <summary>
/// 캐릭터 프리팹 루트에 붙는 컴포넌트.
/// 일러스트, 체력바, 데미지 텍스트, 버프 패널을 관리.
///


public class CharacterView : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    public Image illustration;
    public Image hitbox;      // 유저가 따로 만든 히트박스 전용 이미지
    public Image hpBar;       // pivot (0, 0.5), anchor middle-left
    public Image hpBarBg;     // 체력바 배경 (최대 너비 참조용)
    public Image shieldBar;   // [추가] 보호막 바 (hpBar 뒤에 배치 권장)
    public TextMeshProUGUI damageText;
    public GameObject buffPanel;
    [Tooltip("상태이상(버프/디버프) 아이콘 프리팹. StatusEffectIcon 스크립트가 붙어있어야 함")]
    public GameObject statusEffectPrefab;

    [Header("Passive Notice")]
    public Transform passiveNoticeContainer; // [추가] 패시브 문구가 뜰 부모 (Vertical Layout Group 권장)
    public GameObject passiveNoticePrefab;   // [추가] 패시브 이름이 적힌 TMP 프리팹

    [Header("Distinguish Number")]
    public GameObject distinguishNumObj;      // dintinguse_num 오브젝트
    public TextMeshProUGUI distinguishNumText; // 번호 TMP
    public Color allyNumColor = Color.blue;
    public Color enemyNumColor = Color.red;

    BattleCharacter _character;
    public BattleCharacter character => _character;
    CanvasGroup _canvasGroup; // [추가] 전체 투명도 관리를 위한 CanvasGroup
    Dictionary<StatusEffect, GameObject> _buffIcons = new Dictionary<StatusEffect, GameObject>();
    Dictionary<StatusEffect, GameObject> _vfxInstances = new Dictionary<StatusEffect, GameObject>(); // [추가] VFX 인스턴스 관리
    float _maxBarWidth;   // 체력 100%일 때 너비
    Color _originalColor; // [추가] 원래 일러스트 색상 (색깔놀이 대응)
    Vector3 _originalDamageTextPos; // [추가] 데미지 텍스트 원래 위치 저장용

    // -------------------------------------------------------
    // 초기화
    // -------------------------------------------------------
    public void Init(BattleCharacter character)
    {
        _character = character;
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // [추가] 원래 색상 저장 (색깔놀이 캐릭터 대응)
        if (illustration != null) _originalColor = illustration.color;
        else _originalColor = Color.white;

        // 프리팹에서 설정한 최초 길이를 100% 기준으로 사용 (배경 이미지에 맞추지 않음)
        if (hpBar != null)
        {
            _maxBarWidth = hpBar.rectTransform.rect.width;
            // 찌그러짐 방지 (필드 타입 설정 유도)
            if (hpBar.type != Image.Type.Filled)
            {
                // 실시간으로 설정을 바꾸면 에디터에서 보이지 않을 수 있으므로 경고만 표시하거나
                // 필요 시 코드에서 강제 설정 (유저가 직접 조절한다고 했으므로 주석 처리 가능)
                // hpBar.type = Image.Type.Filled;
                // hpBar.fillMethod = Image.FillMethod.Horizontal;
            }
        }

        UpdateHp();

        if (damageText != null)
        {
            _originalDamageTextPos = damageText.transform.localPosition;
            damageText.gameObject.SetActive(false);
        }

        if (buffPanel != null)
            buffPanel.SetActive(true); // GridLayout 정렬을 위해 활성화 유지

        // 히트박스 방식 적용 (기존 일러스트는 클릭 무시)
        if (hitbox != null)
        {
            hitbox.raycastTarget = true;
            if (illustration != null) illustration.raycastTarget = false;
        }
        else if (illustration != null)
        {
            // 히트박스가 할당되지 않았을 때만 일러스트를 히트박스로 사용
            illustration.raycastTarget = true;
        }

        BattleEventManager.OnStatusEffectChanged += HandleStatusEffectChanged;
        BattleEventManager.OnStatusEffectApplied += HandleStatusEffectApplied; // 신규 효과 부여 이벤트 구독
        BattleEventManager.OnDamageTaken += HandleDamageTaken;
        BattleEventManager.OnHealed += HandleHealed;

        // [추가] 인식표 초기화
        if (distinguishNumObj != null)
        {
            distinguishNumObj.SetActive(true);
            if (distinguishNumText != null)
            {
                distinguishNumText.text = _character.DistinguishNum.ToString();
            }

            // 아군/적군 색상 변경 (Image 컴포넌트가 있다면)
            var img = distinguishNumObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = _character.IsPlayer ? allyNumColor : enemyNumColor;
            }
        }
    }

    private void OnDestroy()
    {
        BattleEventManager.OnStatusEffectChanged -= HandleStatusEffectChanged;
        BattleEventManager.OnStatusEffectApplied -= HandleStatusEffectApplied;
        BattleEventManager.OnDamageTaken -= HandleDamageTaken;
        BattleEventManager.OnHealed -= HandleHealed;

        // [추가] 관리 중인 모든 VFX 정리
        foreach (var vfx in _vfxInstances.Values)
        {
            if (vfx != null) Destroy(vfx);
        }
        _vfxInstances.Clear();
    }

    private void HandleDamageTaken(BattleCharacter victim, BattleCharacter attacker, float damage, bool cannotBeCountered, bool isEvaded, bool isCritical, bool isItem)
    {
        if (victim != _character) return;

        UpdateHp();

        // 회피 여부에 상관없이 항상 데미지 연출 수행 (isEvaded 전달)
        ShowDamage(damage, isCritical, isEvaded);

        if (!isEvaded)
        {
            // [추가] 시전자가 있는 직접 타격인 경우 공용 타격 VFX 생성
            if (damage > 0 && attacker != null && BattleVFXManager.Instance != null)
            {
                // 히트박스가 있으면 히트박스 위치에, 없으면 일러스트나 루트 위치에 생성
                Transform spawnTarget = hitbox != null ? hitbox.transform : (illustration != null ? illustration.transform : this.transform);
                BattleVFXManager.Instance.SpawnVFX(VFXType.Hit, spawnTarget, _character.IsPlayer);
                Debug.Log($"[VFX] Hit VFX spawned on {_character.Name}");
            }
        }
    }

    private void HandleHealed(BattleCharacter victim, float amount)
    {
        if (victim != _character) return;

        UpdateHp();

        if (damageText != null)
        {
            StopCoroutine("AnimateTextPop");
            StartCoroutine(AnimateTextPop($"{Mathf.RoundToInt(amount)}", Color.green));
        }
    }

    public void HandleDamageTaken(BattleCharacter target, float damage, bool isCrit)
    {
        if (target == _character)
        {
            // 피격 시 뷰 연출 (필요 시 추가)
        }
    }

    /// <summary>
    /// [신규] 캐릭터 일러스트를 어둡게 하거나 원래대로 되돌립니다. (타겟팅 연출용)
    /// </summary>
    public void SetDim(bool isDim)
    {
        if (illustration == null) return;

        if (isDim)
        {
            // 현재 색상(색깔놀이 대응)의 절반 수치로 어둡게 변경
            illustration.color = new Color(_originalColor.r * 0.4f, _originalColor.g * 0.4f, _originalColor.b * 0.4f, _originalColor.a);
        }
        else
        {
            // 저장된 원래 색상으로 복구
            illustration.color = _originalColor;
        }
    }

    private void HandleStatusEffectChanged(BattleCharacter target, StatusEffect effect)
    {
        if (target != _character) return;

        // 효과 소유 중 (부여 또는 시간 갱신)
        if (target.ActiveStatusEffects.Contains(effect))
        {
            if (_buffIcons.TryGetValue(effect, out var go))
            {
                var iconScript = go.GetComponent<StatusEffectIcon>();
                if (iconScript != null) iconScript.UpdateUI();
            }
            else if (statusEffectPrefab != null && buffPanel != null)
            {
                var newGo = Instantiate(statusEffectPrefab, buffPanel.transform);
                var iconScript = newGo.GetComponent<StatusEffectIcon>();
                if (iconScript != null) iconScript.Init(effect);
                _buffIcons[effect] = newGo;
            }

            // [추가] 상시 VFX 처리
            if (effect.Data.VFXPrefab != null && !_vfxInstances.ContainsKey(effect))
            {
                Transform spawnTarget = RetHitbox();

                // 프리팹이 비활성화 상태이므로, 생성 시 부모를 즉시 지정하고 활성화
                GameObject vfxGo = Instantiate(effect.Data.VFXPrefab, spawnTarget);
                vfxGo.SetActive(true);

                // KeepVFXAttached가 false면 부모 관계 해제 (위치는 유지)
                if (!effect.Data.KeepVFXAttached)
                {
                    vfxGo.transform.SetParent(spawnTarget.parent); // 캐릭터가 아니라 전체 캐릭터 UI 루트 하위로 이동
                }

                _vfxInstances[effect] = vfxGo;
            }
        }
        else // 만료 또는 해제됨
        {
            if (_buffIcons.TryGetValue(effect, out var go))
            {
                Destroy(go);
                _buffIcons.Remove(effect);
            }

            // [추가] 연동된 VFX 제거 처리
            if (_vfxInstances.TryGetValue(effect, out var vfxGo))
            {
                if (vfxGo != null)
                {
                    var controller = vfxGo.GetComponent<EffectController>();
                    if (controller != null)
                    {
                        // Looping인 경우 Stop()으로 페이드아웃 유도, 아니면 즉시 제거
                        controller.Stop();
                    }
                    else
                    {
                        Destroy(vfxGo);
                    }
                }
                _vfxInstances.Remove(effect);
            }
        }

        // [추가] 상태 효과(특히 보호막) 변경 시 체력바 즉시 갱신
        UpdateHp();
    }

    private void HandleStatusEffectApplied(BattleCharacter target, StatusEffect effect)
    {
        if (target != _character || BattleVFXManager.Instance == null) return;

        // 히트박스가 있으면 히트박스 위치에 생성
        Transform spawnTarget = hitbox != null ? hitbox.transform : (illustration != null ? illustration.transform : this.transform);

        // 효과의 카테고리에 맞춰 공용 VFX 생성
        if (effect.Category == StatusEffectCategory.Buff)
        {
            BattleVFXManager.Instance.SpawnVFX(VFXType.Buff, spawnTarget);
            Debug.Log($"[VFX] Buff VFX spawned on {_character.Name} ({effect.Type})");
        }
        else if (effect.Category == StatusEffectCategory.Debuff)
        {
            BattleVFXManager.Instance.SpawnVFX(VFXType.Debuff, spawnTarget);
            Debug.Log($"[VFX] Debuff VFX spawned on {_character.Name} ({effect.Type})");
        }

    }

    // -------------------------------------------------------
    // 체력 갱신
    // -------------------------------------------------------
    public void UpdateHp()
    {
        if (object.ReferenceEquals(_character, null)) return;
        if (hpBar == null) return;

        // 1. 현재 보호막 총 합량 계산
        float totalShield = _character.ActiveStatusEffects
            .Where(e => e.Data != null && e.Data.EffectType == StatusEffectType.Shield)
            .Sum(e => e.DynamicValue);

        // 2. 전체 길이의 기준점(Denom) 산출
        // 현재 HP + 보호막이 최대 체력을 넘어가면 그 합을 기준으로 비율 계산
        float denom = Mathf.Max(_character.MaxHp, _character.CurrentHp + totalShield);

        float hpRatio = _character.CurrentHp / denom;
        float totalRatio = (_character.CurrentHp + totalShield) / denom;

        // 3. 게이지 애니메이션 (DOTween)
        hpBar.DOKill();
        hpBar.DOFillAmount(hpRatio, 0.3f).SetEase(Ease.OutCubic);

        if (shieldBar != null)
        {
            shieldBar.DOKill();
            if (totalShield > 0)
            {
                shieldBar.gameObject.SetActive(true);
                shieldBar.DOFillAmount(totalRatio, 0.3f).SetEase(Ease.OutCubic);
            }
            else
            {
                // 보호막이 없으면 0으로 줄어든 뒤 비활성화
                shieldBar.DOFillAmount(0f, 0.2f).OnComplete(() => shieldBar.gameObject.SetActive(false));
            }
        }
    }

    // -------------------------------------------------------
    // 데미지 텍스트 표시
    // -------------------------------------------------------
    public void ShowDamage(float damage, bool isCrit, bool isEvaded = false)
    {
        if (damageText == null) return;

        if (isEvaded)
        {
            ShowPassiveNotice("빗나감");
            damageText.gameObject.SetActive(false);
        }
        else if (damage <= 0)
        {
            ShowPassiveNotice("흡수");
            damageText.gameObject.SetActive(false);
        }
        else
        {
            string dmgStr = isCrit ? $"{Mathf.RoundToInt(damage)}" : Mathf.RoundToInt(damage).ToString();
            Color targetColor = isCrit ? Color.yellow : Color.white;

            StopCoroutine("AnimateTextPop");
            StartCoroutine(AnimateTextPop(dmgStr, targetColor, isCrit));
        }
    }

    /// <summary>
    /// [수정] 숫자 하나하나가 튀어나오며 빨간색에서 원래 색으로 변하는 연출 코루틴
    /// </summary>
    private System.Collections.IEnumerator AnimateTextPop(string fullText, Color finalColor, bool isCrit = false)
    {
        damageText.gameObject.SetActive(true);
        damageText.transform.SetAsLastSibling();

        // 1. 위치: 무작위성 제거, 정해진 원래 위치에서 시작
        damageText.transform.localPosition = _originalDamageTextPos;

        damageText.text = "";
        damageText.transform.localScale = Vector3.one;
        damageText.DOKill();
        damageText.alpha = 1f;

        string prefix = isCrit ? "<b>" : "";
        string suffix = isCrit ? "</b>" : "";

        // 현재까지 쌓인 문자열을 담을 변수
        string accumulatedText = "";

        for (int i = 0; i < fullText.Length; i++)
        {
            // 방금 추가된 글자에 대해 무작위 폰트 크기(+5 ~ -5) 적용 (TMP Rich Text 활용)
            int sizeOffset = Random.Range(-5, 6);
            string sizeTag = sizeOffset >= 0 ? $"+{sizeOffset}" : sizeOffset.ToString();

            accumulatedText += $"<size={sizeTag}>{fullText[i]}</size>";
            damageText.text = prefix + accumulatedText + suffix;

            // 2. 색상 변화: 빨강 -> 원래 색상
            damageText.color = Color.red;
            damageText.DOColor(finalColor, 0.15f);

            // 3. 팝 연출: 크기가 툭 튀어나오게 (커졌다가 다시 1.0으로 복귀)
            damageText.transform.localScale = Vector3.one * 0.8f;
            damageText.transform.DOScale(isCrit ? 2.5f : 2.0f, 0.1f).SetEase(Ease.OutQuad)
                .OnComplete(() => damageText.transform.DOScale(1f, 0.15f).SetEase(Ease.OutBack));

            yield return new WaitForSeconds(0.08f);
        }


        // 1.2초 뒤 페이드 아웃
        yield return new WaitForSeconds(1.2f);

        // 부드럽게 사라짐
        damageText.DOFade(0f, 0.5f).OnComplete(() =>
        {
            damageText.gameObject.SetActive(false);
            damageText.alpha = 1f;
        });
    }

    private System.Collections.IEnumerator FadeOutDamageText()
    {
        yield break;
    }

    // -------------------------------------------------------
    // UI 직접 지목 클릭 이벤트
    // -------------------------------------------------------
    public void OnPointerClick(PointerEventData eventData)
    {
        // [추가] 툴팁 확인 중(롱프레스)인 경우 클릭 선택 무시
        if (GameManager.Instance != null && GameManager.Instance.IsTooltipPerforming) return;

        if (BattleManager.Instance != null && _character != null)
        {
            BattleManager.Instance.OnCharacterClicked(_character);
        }
    }

    // -------------------------------------------------------
    // 시각 효과 - 피격 컬러 플래시 (애니메이션 이벤트 연동)
    // -------------------------------------------------------
    public void OnHitFlash()
    {
        if (illustration == null) return;

        // 이전 연출이 진행 중이라면 즉시 중단 및 초기화
        illustration.DOKill();
        illustration.color = _originalColor;

        // Sequence를 사용하여 빨강 -> 원래색으로 번쩍이는 효과
        Sequence seq = DOTween.Sequence();
        seq.Append(illustration.DOColor(Color.red, 0.1f));
        seq.Append(illustration.DOColor(_originalColor, 0.1f));

        seq.Play();
    }


    // 외부에서 피격 위치를 가져오기 위한 함수
    public Transform RetHitbox()
    {
        Transform spawnTarget = hitbox != null ? hitbox.transform : (illustration != null ? illustration.transform : this.transform);
        return spawnTarget;
    }
    // -------------------------------------------------------
    // 사망 및 부활 연출
    // -------------------------------------------------------

    /// <summary>
    /// 캐릭터 사망 연출 (전체 페이드 아웃 후 비활성화)
    /// </summary>
    public void PlayDeathAnimation()
    {
        // [추가] 사망 시 버프/디버프 아이콘 및 연동된 VFX 즉시 제거
        foreach (var icon in _buffIcons.Values)
        {
            if (icon != null) Destroy(icon);
        }
        _buffIcons.Clear();

        foreach (var vfx in _vfxInstances.Values)
        {
            if (vfx != null) Destroy(vfx);
        }
        _vfxInstances.Clear();

        if (_canvasGroup == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // 전체 UI를 0.5초 동안 투명하게 만든 뒤 오브젝트 비활성화
        _canvasGroup.DOKill();
        _canvasGroup.DOFade(0f, 0.5f).OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }

    /// <summary>
    /// 캐릭터 부활 연출 (활성화 후 전체 페이드 인)
    /// </summary>
    public void PlayReviveAnimation()
    {
        gameObject.SetActive(true);

        if (_canvasGroup == null) return;

        // 투명도 0에서 시작하여 0.3초 동안 페이드 인
        _canvasGroup.DOKill();
        _canvasGroup.alpha = 0f;
        _canvasGroup.DOFade(1f, 0.3f);
    }

    /// <summary>
    /// [신규] 패시브 발동 혹은 효과 부여 시 화면에 이름과 아이콘을 띄웁니다.
    /// </summary>
    public void ShowPassiveNotice(string passiveName, Sprite icon = null)
    {
        if (passiveNoticeContainer == null || passiveNoticePrefab == null) return;

        // [추가] 컨테이너 자체를 현재 부모의 최상단으로 이동 (이미지 등에 가려지는 현상 방지)
        passiveNoticeContainer.SetAsLastSibling();

        GameObject notice = Instantiate(passiveNoticePrefab, passiveNoticeContainer);
        notice.transform.SetAsLastSibling();

        // 텍스트 설정
        var tmp = notice.transform.Find("passiveText")?.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = notice.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = passiveName;

        // [추가] 아이콘 설정
        var iconImg = notice.transform.Find("icon")?.GetComponent<UnityEngine.UI.Image>();
        if (iconImg == null)
        {
            // 자식 중에 Image가 있다면 그게 아이콘일 가능성이 높음 (단, 배경 이미지는 제외해야 함)
            var images = notice.GetComponentsInChildren<UnityEngine.UI.Image>();
            foreach (var img in images)
            {
                if (img.gameObject.name.ToLower().Contains("icon")) { iconImg = img; break; }
            }
        }

        if (iconImg != null)
        {
            if (icon != null)
            {
                iconImg.gameObject.SetActive(true);
                iconImg.sprite = icon;
            }
            else
            {
                iconImg.gameObject.SetActive(false);
            }
        }

        // [추가] LayoutGroup의 간섭을 피하기 위해 내부 콘텐츠만 애니메이션
        // 1. 공오브젝트(VisualRoot)를 생성하여 기존 프리팹의 모든 자식을 그 아래로 이동
        GameObject visualRoot = new GameObject("VisualRoot", typeof(RectTransform));
        visualRoot.transform.SetParent(notice.transform, false);

        // RectTransform 설정 (부모 크기에 맞춤)
        RectTransform visualRt = visualRoot.GetComponent<RectTransform>();
        visualRt.anchorMin = Vector2.zero;
        visualRt.anchorMax = Vector2.one;
        visualRt.sizeDelta = Vector2.zero;
        visualRt.anchoredPosition = Vector2.zero;

        // 원래 자식들을 visualRoot로 이동 (단, visualRoot 자신은 제외)
        List<Transform> children = new List<Transform>();
        foreach (Transform child in notice.transform)
        {
            if (child != visualRoot.transform) children.Add(child);
        }
        foreach (var child in children) child.SetParent(visualRoot.transform, false);

        // 2. 이제 LayoutGroup의 통제를 받는 notice 대신, 그 자식인 visualRoot를 애니메이션
        CanvasGroup cg = notice.GetComponent<CanvasGroup>();
        if (cg == null) cg = notice.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        visualRt.anchoredPosition = new Vector2(-100f, 0f); // 왼쪽에서 시작

        Sequence seq = DOTween.Sequence();
        seq.Append(cg.DOFade(1f, 0.2f));
        seq.Join(visualRt.DOAnchorPosX(0f, 0.4f).SetEase(Ease.OutCubic));

        // 1.3초 뒤에 스스로 파괴
        Destroy(notice, 1.3f);
    }

    /// <summary>
    /// 준비 대사 출력 플래그를 초기화합니다.
    /// </summary>
    public void ResetReadyVoice()
    {
        var receiver = GetComponentInChildren<TurnRPG.Battle.AnimationEventReceiver>();
        if (receiver != null) receiver.ResetReadyVoice();
    }
}
