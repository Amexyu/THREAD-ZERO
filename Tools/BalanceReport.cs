using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ThreadZero;

public static class BalanceReport
{
    private static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
    private static string F(double n,string format="F2") { return n.ToString(format,Invariant); }
    private sealed class Scenario
    {
        public string name;
        public string[] gear;
        public Quality quality;
        public int training,region;
        public Scenario(string title,string[] ids,Quality q,int train,int r) { name=title; gear=ids; quality=q; training=train; region=r; }
    }
    private static GameModel Setup(Scenario s,int seed)
    {
        var model=new GameModel(null,seed); model.state.training=s.training;
        if(s.gear!=null) foreach(string id in s.gear)
        {
            Item item=model.Create(id,s.quality,Catalog.SourceRegion(id)+1);
            model.state.items.Add(item); model.Unlock(id); model.Equip(item.uid);
        }
        model.state.region=s.region; model.state.unlockedRegion=s.region; return model;
    }
    public static void Main(string[] args)
    {
        if(args.Length!=1) throw new ArgumentException("Expected report path");
        var text=new StringBuilder();
        text.AppendLine("# v0.5 数值模拟报告");
        text.AppendLine("\n由 Tools/BalanceReport.cs 直接读取当前 C# 规则生成；结果可由 Tools/GenerateBalanceReport.ps1 重现。它是无画面的规则模拟，不代表 Unity 实机性能或玩家体验验收。\n");
        text.AppendLine("## 品质与主属性\n");
        text.AppendLine("| 品质 | 主属性倍率 | 随机词条数 | 词条基值区间 | Lv.1 勤务短铳基础攻击 | 局部辉光强度 | 粒子/发光锚点 |\n|---|---:|---:|---|---:|---:|---:|");
        foreach(Quality q in Enum.GetValues(typeof(Quality)))
        {
            QualityTier t=Balance.Tier(q);
            text.AppendLine("|"+t.name+"|"+F(t.multiplier)+"|"+t.affixCount+"|"+(t.affixCount==0?"无":F(t.affixMin)+"–"+F(t.affixMax))+"|"+F(7*t.multiplier)+"|"+F(t.glow)+"|"+t.particles+"|");
        }
        text.AppendLine("\n主属性示例不含随机攻击词条；同一物品实例的词条保存后不重新掷值。当前区域产出物品等级为 1/2/3，主属性等级倍率为 1.00/1.15/1.30。\n");
        text.AppendLine("## Lv.1 随机词条实际范围\n");
        text.AppendLine("| 属性 | 精良 | 稀有 | 史诗 | 传说 |\n|---|---|---|---|---|");
        for(int stat=0;stat<6;stat++)
        {
            string row="|"+Catalog.StatNames[stat]+"|";
            for(int q=1;q<5;q++)
            {
                float a=Balance.AffixValue(stat,(Quality)q,1,0),b=Balance.AffixValue(stat,(Quality)q,1,1);
                bool percentage=stat==3 || stat==4;
                row+=F(percentage?a*100:a)+"–"+F(percentage?b*100:b)+(percentage?"%":"")+"|";
            }
            text.AppendLine(row);
        }
        text.AppendLine("\nLv.2/3 词条整体再乘 1.12/1.24。随机上端实际为开区间；表格显示配置上界。旧版已保存词条保持原值，v0.3 新掉落使用新范围。\n");
        text.AppendLine("## 敌人基准\n");
        text.AppendLine("| 街区 | 第一波生命/攻击/防御 | Boss 生命/攻击/防御 |\n|---|---|---|");
        for(int r=0;r<3;r++) text.AppendLine("|"+Catalog.RegionNames[r]+"|"+F(Balance.EnemyHealth(r,1))+" / "+F(Balance.EnemyAttack(r,1))+" / "+F(Balance.EnemyDefense(r,false))+"|"+F(Balance.EnemyHealth(r,5))+" / "+F(Balance.EnemyAttack(r,5))+" / "+F(Balance.EnemyDefense(r,true))+"|");
        text.AppendLine("\n## 固定搭配通关模拟\n");
        text.AppendLine("每种情景采用种子 1000–1019，20 个样本各模拟 180 秒，步长 0.05 秒。不自动换装、不升级训练、不修改掉率；每件测试装备来自其真实产地等级。掉落进背包但不穿戴。通关时间是本情景街区首个 Boss 的击败时刻，统计只包含成功样本。\n");
        text.AppendLine("| 情景 | 180 秒内首个 Boss 成功率 | 成功样本首通中位秒数 | 平均击杀/分钟 | 平均维修次数 |\n|---|---:|---:|---:|---:|");
        Scenario[] scenarios={
            new Scenario("初始四件 / 雨巷 / 训练0",null,Quality.Common,0,0),
            new Scenario("雨巷稀有装 / 高架 / 训练3",new[]{"w1","h1","c0","b0"},Quality.Rare,3,1),
            new Scenario("推进近战稀有 / 高架 / 训练3",new[]{"w1","h3","c2","b4"},Quality.Rare,3,1),
            new Scenario("高架史诗防御 / 禁区 / 训练6",new[]{"w3","h3","c2","b3"},Quality.Epic,6,2),
            new Scenario("标记双电弧稀有 / 禁区 / 训练6",new[]{"w2","h2","c2","b2"},Quality.Rare,6,2),
            new Scenario("标记双电弧史诗 / 禁区 / 训练6",new[]{"w2","h2","c2","b2"},Quality.Epic,6,2),
            new Scenario("标记双电弧传说 / 禁区 / 训练6",new[]{"w2","h2","c2","b2"},Quality.Legendary,6,2)
        };
        foreach(Scenario s in scenarios)
        {
            var victories=new List<float>(); int totalKills=0,totalRepairs=0;
            for(int seed=1000;seed<1020;seed++)
            {
                GameModel model=Setup(s,seed); var battle=new CombatSession(model);
                bool recorded=false,wasRepair=false;
                for(int step=1;step<=3600;step++)
                {
                    battle.Tick(.05f);
                    bool repair=battle.recovery>0; if(repair && !wasRepair) totalRepairs++; wasRepair=repair;
                    if(!recorded && model.state.bosses>0) { victories.Add(step*.05f); recorded=true; }
                }
                totalKills+=model.state.kills;
            }
            victories.Sort();
            double median=victories.Count==0?0:(victories[(victories.Count-1)/2]+victories[victories.Count/2])/2;
            text.AppendLine("|"+s.name+"|"+victories.Count+" / 20|"+(victories.Count==0?"—":F(median))+"|"+F(totalKills/60.0)+"|"+F(totalRepairs/20.0)+"|");
        }
        text.AppendLine("\n这些是固定搭配的诊断样本，不代表自动通关承诺。高架史诗防御与禁区稀有电弧并非等预算比较。将结合实际游戏中的掉落、换装选择和展览节奏继续调优。\n");
        text.AppendLine("## 独立品质抽取模拟\n");
        text.AppendLine("每组种子 4831、100,000 件，包含品质保底。普通敌人的 65% 装备掉率不计入本表，因为表格分母是已生成装备件数。Boss 最低稀有直接生效。\n");
        text.AppendLine("| 抽取场景 | 普通 | 精良 | 稀有 | 史诗 | 传说 |\n|---|---:|---:|---:|---:|---:|");
        foreach(bool boss in new[]{false,true})
        {
            var model=new GameModel(null,4831); int[] counts=new int[5];
            for(int i=0;i<100000;i++) counts[(int)model.RollQuality(boss)]++;
            text.AppendLine("|"+(boss?"全 Boss（初始幸运1）":"全普通装备（初始幸运1）")+"|"+string.Join("|",counts.Select(c=>F(c/1000.0)+"%"))+"|");
        }
        text.AppendLine("\n以上百分比是模拟结果，区别于基础权重 50/28/15/6/1。幸运提高高档权重后归一化，品质保底进一步改变实际频率。Boss 和普通掉落混合时，长期品质分布也会改变。\n");
        text.AppendLine("## 训练与经济\n");
        text.AppendLine("训练每级攻击 +1.5、生命 +8，上限20级。等级 n 升 n+1 花费 20+15n 零件；从0升3共105零件，从0升6共345，从0升20共3250。普通敌人击杀 +1、Boss +8；分解=(2+3×品质序号)×物品等级。\n");
        text.AppendLine("普通波次击败后恢复最大生命的4%，Boss击败后回满，降低循环刷宝时的无意义维修。v0.3将街区攻击成长从每区+8调为+6，保持生命和防御不变。\n");
        text.AppendLine("## 后续实机验收\n\n本报告采用纯 C# 规则模拟，包含攻击前摇与延迟命中；不能据此推断 Unity 内实际帧率、辉光观感、音效混音和鼠标交互已经通过。下一步需要检查四部位辉光的可读性，以及初始 Boss 的失败反馈和训练引导。");
        File.WriteAllText(args[0],text.ToString(),new UTF8Encoding(false));
        Console.WriteLine(text.ToString());
    }
}
