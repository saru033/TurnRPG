using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using DG.Tweening;

public class CharacterNamingUI : MonoBehaviour
{
    [Header("UI References")]
    public Image characterIllustration;
    public TMP_InputField nameInputField;
    public Button confirmButton;
    public GameObject errorText;
    private PlayerCharacterState _targetChar;
    private Action _onNamingComplete;

    private CanvasGroup _canvasGroup;
    private Vector2 _originalIllustrationPos;
    private bool _isInitialized = false;

    private void Awake()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClick);
        }

        if (nameInputField != null)
        {
            // 입력이 바뀔 때 에러 메시지 숨김
            nameInputField.onValueChanged.AddListener((_) =>
            {
                if (errorText != null) errorText.SetActive(false);
            });
        }

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// 캐릭터 명명 패널을 엽니다.
    /// </summary>
    public void Open(PlayerCharacterState charState, Action onComplete)
    {
        if (!_isInitialized && characterIllustration != null)
        {
            _originalIllustrationPos = characterIllustration.rectTransform.anchoredPosition;
            _isInitialized = true;
        }

        _targetChar = charState;
        _onNamingComplete = onComplete;

        if (characterIllustration != null && charState.template != null)
        {
            characterIllustration.sprite = charState.template.illustration;
        }

        if (nameInputField != null)
        {
            nameInputField.text = "";
        }

        if (errorText != null) errorText.SetActive(false);

        // [DOTween] 연출 초기화 및 시작
        gameObject.SetActive(true);

        // 배경 페이드 인
        _canvasGroup.DOKill();
        _canvasGroup.alpha = 0;
        _canvasGroup.DOFade(1f, 0.5f);

        // 일러스트 아래에서 위로 슬라이드
        if (characterIllustration != null)
        {
            characterIllustration.rectTransform.DOKill();
            characterIllustration.rectTransform.anchoredPosition = new Vector2(_originalIllustrationPos.x, _originalIllustrationPos.y - 300f);
            characterIllustration.rectTransform.DOAnchorPos(_originalIllustrationPos, 0.5f).SetEase(Ease.OutCubic);
        }
    }

    private void OnConfirmClick()
    {
        if (nameInputField == null || _targetChar == null) return;

        string newName = nameInputField.text.Trim();

        // 유효성 검사: 빈 문자열이 아니어야 하며, 8글자 이하여야 함
        if (string.IsNullOrEmpty(newName) || newName.Length > 8)
        {
            if (errorText != null) errorText.SetActive(true);
            // errorText의 문구 변경

            // 비어있는 이름
            if (string.IsNullOrEmpty(newName))
            {
                errorText.GetComponent<TextMeshProUGUI>().text = "이름이 없으면 너무 불쌍해요.";
            }
            // 8 글자 이상인 이름
            else
            {
                errorText.GetComponent<TextMeshProUGUI>().text = "이름이 너무 길면 부르기 곤란해요.";
            }
            return;
        }

        _targetChar.characterName = newName;
        Debug.Log($"[Naming] 캐릭터 이름 설정 완료: {newName}");

        // [DOTween] 퇴장 연출 후 완료 처리
        _canvasGroup.DOFade(0f, 0.3f);
        if (characterIllustration != null)
        {
            characterIllustration.rectTransform.DOAnchorPos(new Vector2(_originalIllustrationPos.x, _originalIllustrationPos.y - 300f), 0.3f)
                .SetEase(Ease.InCubic)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    _onNamingComplete?.Invoke();
                });
        }
        else
        {
            gameObject.SetActive(false);
            _onNamingComplete?.Invoke();
        }
    }
}
