using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class EquipmentTooltipUI : MonoBehaviour
{
    [Header("UI References")]
    public Image equipIcon;
    public TMP_Text txtPartName;
    public TMP_Text txtStatList;

    public void SetData(EquipmentState state, Sprite icon)
    {
        if (state == null) return;

        // 1. 아이콘 설정
        if (equipIcon != null) equipIcon.sprite = icon;

        // 2. 부위 이름 설정
        if (txtPartName != null)
        {
            switch (state.part)
            {
                case EquipmentPart.Head: txtPartName.text = "머리 장비"; break;
                case EquipmentPart.Body: txtPartName.text = "상체 장비"; break;
                case EquipmentPart.Shoes: txtPartName.text = "신발 장비"; break;
            }
        }

        // 3. 스탯 목록 구성 (줄바꿈 포함)
        if (txtStatList != null)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < state.subStats.Count; i++)
            {
                sb.Append(state.subStats[i].GetStatString());
                if (i < state.subStats.Count - 1) sb.Append("\n");
            }
            txtStatList.text = sb.ToString();
        }
    }
}
