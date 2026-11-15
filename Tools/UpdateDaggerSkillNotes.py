from pathlib import Path

root = Path(__file__).resolve().parent.parent

def transform(relative, action):
    path = root / relative
    text = path.read_bytes().decode('utf-8-sig')
    updated = action(text)
    if updated != text:
        path.write_bytes(updated.encode('utf-8'))
        print(relative)

notice = ('> 2026-11-16 用户追加确定：迅切连击仅作为初始匕首的装备技能，普通攻击仍为单次短刺。'
          '后续其他稀有度匕首的招牌技能必须有独特动作模组及配套特效，不能只复用三连击加换色。'
          '第一刀须斜切经过前方目标胸腹，禁止朝地面下扎。此项覆盖旧品质规范中与匕首技能冲突的“品质不改变装备固有功能”；'
          '现有数值、ID和存档不在素材会话中直接修改。详见 `Docs/DaggerSkillProduction_v12.md`（Docs目录内见同名文件）。\n\n')

for rel in ['THREAD_ZERO_完整企划书.md', 'Docs/THREAD_ZERO_完整企划书.md']:
    transform(rel, lambda text: text.replace('\n', '\n' + notice, 1) if '用户追加确定：迅切连击仅作为初始匕首' not in text else text)

quality_notice = ('> 2026-11-16 匕首技能新规则：初始匕首独占迅切连击；其他稀有度匕首须有独特技能动作与特效。'
                  '下文“品质不改变装备固有功能”不再用于推导所有匕首共用同一招牌技能。'
                  '属性倍率与掉落表本轮不改；模板/品质到具体技能的程序映射尚待落实。见 `DaggerSkillProduction_v12.md`。\n\n')
transform('Docs/QualityStatsGlow.md', lambda text: text.replace('\n', '\n' + quality_notice, 1) if '2026-11-16 匕首技能新规则' not in text else text)

handoff_notice = ('> 2026-11-16 新增交接：先读 `DaggerSkillProduction_v12.md` 与 '
                  '`EquipmentSkills_MechanicalAscension_v12.md`。初始匕首独占三连击；'
                  '其他稀有度匕首必须有独特动作及特效。新版动作审查见 '
                  '`../Deliverables/ArtPack_v12/Review/HANDOFF.md`，不是已完成运行资产。\n\n')
transform('Docs/AI协作交接与素材接口.md', lambda text: text.replace('\n', '\n' + handoff_notice, 1) if '2026-11-16 新增交接' not in text else text)
