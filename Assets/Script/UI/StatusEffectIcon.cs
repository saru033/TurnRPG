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
