using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using System.Collections;
/// <summary>
/// 상점 UI의 전반적인 열기/닫기 애니메이션과 스테이지 진행 버튼을 관리하는 컨트롤러입니다.
/// </summary>
public class ShopUIController : MonoBehaviour
{
    [Header("UI Panels")]
    public RectTransform shopPanel;     // 상점 메인 패널
    public RectTransform nextButton;    // 다음 스테이지 진행 버튼

    [Header("References")]
    public ShopPanelUI shopItemGenerator; // 아이템 생성 로직 스크립트
    public GameObject dialogPanel;
    public GameObject mapui;

    [Header("Settings")]
    public float animationDuration = 0.4f;
    public float slideOffset = 1500f; // 아래에서 올라올 때의 오프셋

    private Vector2 _originalShopPos;
    private Vector2 _originalButtonPos;
    private bool _isInitialized = false;

    private bool shopUsable = true;

    [Header("Dialogue Strings")]
    public string[] idleDialogs = { "좋은 물건이 많다구!", "어서와, 구경은 공짜야.", "어이, 거기! 한 번 보고 가지?" };
    public string[] closedDialogs = { "오늘은 장사 끝났어.", "다음에 다시 오라구.", "피곤하구먼, 다음에 보세." };

    private Coroutine _idleCoroutine;
    private Coroutine _hideCoroutine;

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        if (shopPanel != null)
        {
            _originalShopPos = shopPanel.anchoredPosition;
            // 시작 시 화면 아래에 배치
            shopPanel.anchoredPosition = new Vector2(_originalShopPos.x, _originalShopPos.y - slideOffset);
            shopPanel.gameObject.SetActive(false);
        }

        if (nextButton != null)
        {
            _originalButtonPos = nextButton.anchoredPosition;
            // 시작 시 화면 아래에 배치
            nextButton.anchoredPosition = new Vector2(_originalButtonPos.x, _originalButtonPos.y - slideOffset);
            nextButton.gameObject.SetActive(false);
        }
    }
    // 활성화 될 때 사용가능하게 초기화
    private void OnEnable()
    {
        shopUsable = true;
        if (dialogPanel != null) dialogPanel.SetActive(false);
        nextButton.gameObject.SetActive(false);


        // 주기적으로 대사를 띄우는 코루틴 시작
        _idleCoroutine = StartCoroutine(IdleDialogRoutine());
    }

    private void OnDisable()
    {
        // 모든 코루틴 중지
        if (_idleCoroutine != null) StopCoroutine(_idleCoroutine);
        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
    }

    private IEnumerator IdleDialogRoutine()
    {
        while (true)
        {
            // 3 ~ 8초 대기
            yield return new WaitForSeconds(Random.Range(3f, 8f));

            // 상점 이용 가능하고, 상점 패널이 닫혀 있을 때만 말하기
            if (shopUsable && (shopPanel == null || !shopPanel.gameObject.activeSelf))
            {
                ShowDialog(idleDialogs[Random.Range(0, idleDialogs.Length)]);
            }
        }
    }

    private void ShowDialog(string text)
    {
        if (dialogPanel == null) return;

        TextMeshProUGUI dialogText = dialogPanel.GetComponentInChildren<TextMeshProUGUI>();
        if (dialogText != null)
        {
            dialogText.text = text;
        }

        dialogPanel.SetActive(true);

        // 3초 뒤 비활성화를 위한 코루틴 (기존 것 있으면 중지)
        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
        _hideCoroutine = StartCoroutine(HideDialogAfterDelay());
    }

    private IEnumerator HideDialogAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        dialogPanel.SetActive(false);
    }


    /// <summary>
    /// 상점 UI를 열고 아이템을 생성합니다.
    /// </summary>
    public void OpenShop()
    {
        if (!shopUsable)
        {
            // 이용 불가 문구 랜덤 출력
            if (closedDialogs != null && closedDialogs.Length > 0)
            {
                ShowDialog(closedDialogs[Random.Range(0, closedDialogs.Length)]);
            }
            return;
        }

        Init();

        if (shopPanel == null) return;

        shopPanel.gameObject.SetActive(true);
        shopPanel.DOKill();

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(SfxType.Coindrop);
        }

        // 상점이 열려있는 동안에는 자동 대사 중지
        if (dialogPanel != null) dialogPanel.SetActive(false);
        // 아이템 생성 호출
        if (shopItemGenerator != null)
        {
            shopItemGenerator.GenerateShopItems();
        }

        // 아래에서 위로 슬라이드 업
        shopPanel.DOAnchorPos(_originalShopPos, animationDuration).SetEase(Ease.OutCubic);
    }

    /// <summary>
    /// 상점 UI를 닫습니다.
    /// </summary>
    public void CloseShop()
    {
        shopUsable = false;

        if (shopPanel == null) return;

        shopPanel.DOKill();
        shopPanel.DOAnchorPos(new Vector2(_originalShopPos.x, _originalShopPos.y - slideOffset), animationDuration)
            .SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                shopUsable = false;
                shopPanel.gameObject.SetActive(false);
                SetNextButtonVisible(true);
            });
    }

    /// <summary>
    /// 다음 스테이지 진행 버튼을 활성화하고 애니메이션을 재생합니다.
    /// </summary>
    /// <param name="visible">활성화 여부</param>
    public void SetNextButtonVisible(bool visible)
    {
        Init();

        if (nextButton == null) return;

        nextButton.DOKill();

        if (visible)
        {
            nextButton.gameObject.SetActive(true);
            nextButton.DOAnchorPos(_originalButtonPos, animationDuration).SetEase(Ease.OutCubic);
        }
        else
        {
            nextButton.DOAnchorPos(new Vector2(_originalButtonPos.x, _originalButtonPos.y - slideOffset), animationDuration)
                .SetEase(Ease.InCubic)
                .OnComplete(() => nextButton.gameObject.SetActive(false));
        }
    }

    public void openMap()
    {
        mapui.SetActive(true);
    }
}
