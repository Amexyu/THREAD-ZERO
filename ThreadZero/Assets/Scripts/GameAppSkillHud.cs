using UnityEngine;

namespace ThreadZero
{
    public sealed partial class GameApp
    {
        // 角色脚下的技能充能：每次攻击触发 = 一格一格亮；冷却/概率保底 = 一条进度。充满时变橙。
        private void DrawSkillCharge(float feetX)
        {
            SkillDef skill = combat.EquippedSkill;
            if (skill == null) return;
            float y = GroundY + 12;
            float ready = combat.SkillReadiness;
            Color on = ready >= .999f ? Ui.Orange : Ui.Ink, off = Ui.Alpha(Ui.Ink, .15f);
            if (skill.trigger == SkillTrigger.EveryNAttacks && skill.attacks > 0)
            {
                const float w = 14, gap = 4;
                float total = skill.attacks * w + (skill.attacks - 1) * gap, x = feetX - total / 2;
                for (int i = 0; i < skill.attacks; i++)
                    Ui.Fill(x + i * (w + gap), y, w, 5, i < combat.SkillCharge ? on : off);
            }
            else
            {
                Ui.Fill(feetX - 40, y, 80, 5, off);
                Ui.Fill(feetX - 40, y, 80 * Mathf.Clamp01(ready), 5, on);
            }
        }
    }
}
