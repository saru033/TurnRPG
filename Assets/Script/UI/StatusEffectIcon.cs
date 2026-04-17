using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TurnRPG.SkillSystem;

public class StatusEffectIcon : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI durationText;

    public StatusEffect Effect { get; private set; }

    public void Init(StatusEffect effect)
    {
        Effect = effect;
        if (iconImage != null && effect.Data != null && effect.Data.Icon != null)
        {
            iconImage.sprite = effect.Data.Icon;
        }

        // [추가] 툴팁 트리거 설정
        var trigger = GetComponent<StatusEffectTooltipTrigger>();
        if (trigger == null) trigger = gameObject.AddComponent<StatusEffectTooltipTrigger>();
        trigger.Init(effect);

        // [추가] 텍스트가 마우스 클릭(레이캐스트)을 방해하지 않도록 설정
        if (durationText != null)
        {
            durationText.raycastTarget = false;
        }

        UpdateUI();
    }

    public void UpdateUI()
    {
        if (Effect == null) return;
        
        if (durationText != null)
        {
            durationText.text = Effect.RemainingDuration.ToString();
        }
    }
}
