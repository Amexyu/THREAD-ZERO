using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Runtime.Serialization.Json;
using ThreadZero;

public static class ModelTests
{
    private static int passed;
    private static void Check(bool condition,string name)
    { if(!condition) throw new Exception("FAIL: "+name); passed++; Console.WriteLine("PASS: "+name); }
    public static void Main()
    {
        var m=new GameModel(null,20261114);
        Check(m.state.items.Count==4 && m.state.collection.Count==4,"Starter equipment and collection");
        Check(m.state.items.Select(i=>i.uid).Distinct().Count()==4,"Unique item instance IDs");
        Check(m.Equipped(Slot.Head)==null && new[]{Slot.Weapon,Slot.Coat,Slot.Back}.All(s=>m.Equipped(s)!=null),"Masked-hair default keeps head slot empty and other starter slots equipped");
        Check(Catalog.All.Length==20 && Catalog.All.Select(x=>x.id).Distinct().Count()==20,"20 unique catalog templates");
        Check(Enum.GetValues(typeof(Slot)).Cast<Slot>().All(s=>Catalog.All.Count(x=>x.slot==s)==5),"Five appearances per slot");
        Stats before=m.Total();
        Item upgrade=m.Create("w2",Quality.Legendary,3); m.state.items.Add(upgrade); m.Unlock(upgrade.baseId);
        Check(upgrade.affixes.Count==4 && upgrade.affixes.Select(a=>a.stat).Distinct().Count()==4,"Legendary rolls four different affixes");
        Check(m.Equip(upgrade.uid) && m.Total().attack>before.attack,"Equipping modifies real attack");
        Check(m.EffectCount(Effect.Arc)==1,"Equipped effects are active");
        Check(!m.Salvage(upgrade.uid),"Equipped item cannot be salvaged");
        Stats equipped=m.Total();
        Check(m.SetAppearance("w0"),"Owned appearance can override equipment");
        Check(m.Appearance(Slot.Weapon).id=="w0" && m.Total().attack==equipped.attack,"Transmog affects appearance, not stats");
        Check(!m.SetAppearance("w3"),"Unowned appearance cannot be used");
        Item old=m.state.items.First(i=>i.baseId=="w0"); old.locked=true;
        Check(!m.Salvage(old.uid),"Locked item cannot be salvaged"); old.locked=false;
        int scrap=m.state.parts.Sum();
        Check(m.Salvage(old.uid) && m.state.parts.Sum()>scrap,"Salvage grants scrap");
        Check(m.state.collection.Contains("w0") && m.Appearance(Slot.Weapon).id=="w0","Salvage retains collection and transmog");
        Check(!m.Equip("missing") && !m.Salvage("missing"),"Unknown item commands fail safely");
        Check(!m.Train(),"Cannot train without sufficient scrap");
        m.state.scrap=1000; float atk=m.Total().attack; int cost=m.TrainingCost;
        Check(m.Train() && m.state.scrap==1000-cost && m.Total().attack>atk,"Training spends exact cost and increases stats");
        m.state.training=20; Check(!m.Train(),"Training level cap enforced");
        Check(!m.ChangeRegion(1) && !m.ChangeRegion(-1),"Locked and invalid regions rejected");
        m.state.unlockedRegion=2; Check(m.ChangeRegion(2),"Unlocked region selectable");
        for(int q=0;q<5;q++)
        { Item item=m.Create("c0",(Quality)q,1); Check(item.affixes.Count==q,"Quality "+q+" affix count"); }
        m.state.sinceRare=12;
        Check((int)m.RollQuality(false)>=2 && m.state.sinceRare==0,"Rare hard pity and reset");
        m.state.sinceEpic=45;
        Check((int)m.RollQuality(false)>=3 && m.state.sinceEpic==0,"Epic hard pity and reset");
        m.state.sinceLegendary=120;
        Check(m.RollQuality(false)==Quality.Legendary && m.state.sinceLegendary==0,"Legendary hard pity and reset");
        Check(m.state.sinceEpic==0 && m.state.sinceRare==0,"Legendary resets lower quality pity");
        var baseline=new GameModel(null,88); int[] counts=new int[5];
        for(int i=0;i<30000;i++)
        {
            Quality q=baseline.RollQuality(false); counts[(int)q]++;
            if(baseline.state.sinceRare>12 || baseline.state.sinceEpic>45 || baseline.state.sinceLegendary>120) throw new Exception("Pity limit violated");
        }
        Check(counts.All(x=>x>0),"All quality tiers obtainable in 30,000 seeded rolls");
        Check(counts[0]>counts[1] && counts[1]>counts[2] && counts[2]>counts[3] && counts[3]>counts[4],"Quality distribution ordered");
        Console.WriteLine("Quality counts: "+string.Join(",",counts));
        for(int r=0;r<3;r++)
        {
            var region=new GameModel(null,77+r); region.state.region=r;
            string target=new[]{"c1","c2","w2"}[r]; int special=0;
            for(int i=0;i<200;i++) { Item d=region.Drop(true); if(d==null || (int)d.quality<2 || d.level!=r*5+3) throw new Exception("Boss drop invalid"); if(d.baseId==target) special++; }
            Check(special>40 && special<120,"Region "+r+" boss guaranteed rare+, level, exclusive pool");
        }
        var battle=new GameModel(null,704); var combat=new CombatSession(battle); int dropped=0;
        combat.OnDrop=(i,fresh)=>dropped++;
        for(int i=0;i<3000;i++) combat.Tick(.1f);
        Check(battle.state.kills>0 && dropped>0,"Five minutes of simulation produces kills and loot");
        Check(combat.health>=0 && combat.health<=battle.Total().health,"Combat health remains bounded");
        battle.state.unlockedRegion=2; battle.ChangeRegion(2); combat.ResetRegion();
        Check(combat.wave==1 && combat.health==battle.Total().health,"Region changes reset combat safely");
        var high=new GameModel(null,9);
        foreach(Slot s in Enum.GetValues(typeof(Slot)))
        { Item item=high.Create(Catalog.All.First(b=>b.slot==s).id,Quality.Legendary,100); high.state.items.Add(item); high.Equip(item.uid); }
        Check(high.Total().crit<=.75f && high.Total().haste<=2 && high.Total().luck<=30,"High-level stats respect caps");
        Check(GameModel.Damage(-2,500)==1 && GameModel.Damage(100,100)==50,"Damage minimum and mitigation formula");
        var strongCombat=new CombatSession(high); high.state.region=2;
        for(int i=0;i<1000;i++) strongCombat.Tick(.1f);
        Check(high.state.bosses>0 && high.state.unlockedRegion==3,"Boss victory and progression path");
        high.state.appearance[0]="w0";
        var reloaded=new GameModel(high.state,1);
        Check(reloaded.Equipped(Slot.Weapon).uid==high.Equipped(Slot.Weapon).uid,"Model reload retains exact equipped instance");
        bool future=false; try { new GameModel(new GameState{version=2},1); } catch(InvalidOperationException) { future=true; }
        Check(future,"Future save versions rejected");
        bool duplicates=false;
        var bad=new GameState(); var badItem=new Item{uid="same",baseId="w0",level=1}; bad.items.Add(badItem); bad.items.Add(badItem);
        try { new GameModel(bad,1); } catch(InvalidOperationException) { duplicates=true; }
        Check(duplicates,"Duplicate save IDs rejected");
        ModuleChecks();
        TargetChecks();
        QualityChecks();
        AnimationChecks();
        StorageChecks();
        Console.WriteLine("ALL PASSED: "+passed+" checks");
    }
    private static void Give(GameModel model,string id)
    {
        Item item=model.Create(id,Quality.Common,1); model.state.items.Add(item); model.Unlock(id); model.Equip(item.uid);
    }
    private static CombatSession DummyCombat(GameModel model)
    {
        var c=new CombatSession(model); c.enemyHealth=c.enemyMaxHealth=100000; c.ClearPack(); return c;
    }
    private static void Simulate(CombatSession c,float seconds)
    { for(int i=0;i<(int)(seconds*20);i++) c.Tick(.05f); }
    private static void ModuleChecks()
    {
        var drone=new GameModel(null,101); var dc=DummyCombat(drone);
        Simulate(dc,8);
        Check(dc.droneHits>=3 && dc.lastDroneDamage>0,"Equipped backpack launches damaging drone attacks");
        Check(dc.dashHits==0 && dc.arcHits==0,"Inactive modules cannot proc");
        drone.Unlock("b4"); Stats before=drone.Total(); drone.SetAppearance("b4");
        int shots=dc.droneHits; Simulate(dc,3);
        Check(dc.droneHits>shots && dc.dashHits==0 && drone.Total().attack==before.attack,"Thruster transmog keeps real drone behavior and stats");
        Give(drone,"b4"); dc.SyncLoadout(); shots=dc.droneHits; Simulate(dc,5);
        Check(dc.droneHits==shots && dc.dashHits>0,"Real backpack swap replaces drone with thruster");
        Check(dc.markTime==0 && dc.guardCharge==0,"Equipment swap clears incompatible transient state");

        var ranged=new GameModel(null,202); Give(ranged,"b4"); var rc=DummyCombat(ranged); Simulate(rc,7);
        var melee=new GameModel(null,202); Give(melee,"b4"); Give(melee,"w1"); var mc=DummyCombat(melee); Simulate(mc,7);
        Check(rc.dashHits>0 && mc.dashHits>0 && mc.combos>0 && rc.combos==0,"Thruster and melee combination triggers assault");
        float rangedFactor=rc.lastDashDamage/ranged.Total().attack;
        float meleeFactor=mc.lastDashDamage/melee.Total().attack;
        Check(Math.Abs(meleeFactor/rangedFactor-1.5f)<.001f,"Melee assault deals 50 percent more module damage");

        var arc=new GameModel(null,303); Give(arc,"w2"); Give(arc,"b2");
        var ac=DummyCombat(arc); var arcEvents=new List<AbilityEvent>(); ac.OnAbility=arcEvents.Add; Simulate(ac,8);
        Check(ac.arcHits>0 && !arcEvents.Any(e=>e.ability==Ability.Surge),"Unmarked electric modules trigger ordinary arcs");
        Give(arc,"h2"); ac.SyncLoadout(); arcEvents.Clear(); Simulate(ac,8);
        var surge=arcEvents.FirstOrDefault(e=>e.ability==Ability.Surge);
        Check(surge!=null && ac.combos>0,"Signal marker plus electric modules triggers surge");
        Check(Math.Abs(surge.damage-arc.Total().attack*1.4f)<.001f,"Marked arc doubles the two-module arc payload");
        ac.markTime=4; ac.attackNumber=10; arc.Unlock("h1"); arc.SetAppearance("h1"); ac.SyncLoadout();
        Check(ac.markTime==4 && ac.attackNumber==10,"Appearance-only changes do not reset combat or consume marks");
        Give(arc,"h1"); ac.SyncLoadout(); Check(ac.markTime==0 && !arc.Has(Effect.Mark),"Removing real marker clears old target mark");

        var guard=new GameModel(null,404); Give(guard,"b3"); Give(guard,"c2"); var gc=DummyCombat(guard);
        var guardEvents=new List<AbilityEvent>(); gc.OnAbility=guardEvents.Add; gc.attackTimer=10;
        Simulate(gc,1.5f);
        Check(gc.guardCharge>0 && gc.guardCharge<=guard.Total().attack,"Guard converts blocked damage into bounded charge");
        gc.attackTimer=0; gc.Tick(.4f);
        Check(guardEvents.Any(e=>e.ability==Ability.GuardRelease && e.damage>0) && gc.guardCharge==0,"Next normal attack consumes guard charge");
        gc.guardCharge=20; gc.markTime=4; gc.ResetRegion();
        Check(gc.guardCharge==0 && gc.markTime==0 && gc.attackNumber==0,"Region reset clears all temporary module state");
        Check(guard.LoadoutSummary.Contains("护盾反击") && arc.LoadoutSummary.Contains("电弧"),"Loadout summary reports real equipment abilities");
        // Normal enemy resolution and loot still work when a module delivers the lethal hit.
        var finisher=new GameModel(null,505); var fc=DummyCombat(finisher); fc.attackTimer=100; fc.enemyHealth=1;
        Simulate(fc,1.15f);
        Check(finisher.state.kills==1 && fc.wave==2,"Drone lethal hit uses common kill and progression pipeline");
    }
    private static void TargetChecks()
    {
        Check(Catalog.All.All(b=>Catalog.SourceRegion(b.id)>=0 && Catalog.SourceRegion(b.id)<=2),"Every appearance has a valid source region");
        for(int region=0;region<3;region++)
        {
            var m=new GameModel(null,610+region); m.state.region=region;
            var boss=new[]{"c1","c2","w2"}[region];
            Check(Catalog.SourceRegion(boss)==region,"Region "+region+" featured boss template is locally available");
            bool local=true;
            for(int i=0;i<150;i++) { Item item=m.Drop(i%3==0); if(item!=null && Catalog.SourceRegion(item.baseId)!=region) local=false; }
            Check(local,"Region "+region+" drops stay inside its source pool");
        }
        var target=new GameModel(null,710);
        Check(target.SetRecoveryTarget("b4") && !target.state.collection.Contains("b4"),"Unowned appearance can be tracked without being unlocked");
        Check(!target.TargetActive && !target.ChangeRegion(1),"Tracking locked content does not unlock or teleport");
        for(int i=0;i<15;i++) target.Drop(true);
        Check(target.state.targetBossMisses==0 && !target.state.items.Any(i=>i.baseId=="b4"),"Wrong-region bosses neither advance nor award target");
        target.state.unlockedRegion=2; target.ChangeRegion(1);
        Check(target.TargetActive,"Target activates in its source region");
        target.state.targetBossMisses=7; target.state.sinceLegendary=120;
        Item forced=target.Drop(true);
        Check(forced.baseId=="b4" && forced.quality==Quality.Legendary && forced.level==8,"Target guarantee and quality pity apply to same single drop");
        Check(target.state.targetBossMisses==0 && target.state.collection.Contains("b4"),"Target acquisition resets misses and unlocks appearance");
        target.state.targetBossMisses=4; target.SetRecoveryTarget("b4");
        Check(target.state.targetBossMisses==4,"Reselecting current target preserves progress");
        target.ChangeRegion(0); target.Drop(true);
        Check(target.state.targetBossMisses==4,"Changing regions pauses target progress");
        target.ChangeRegion(1); int gap=0,maxGap=0;
        for(int i=0;i<1000;i++)
        { Item item=target.Drop(true); gap++; if(item.baseId=="b4") { maxGap=Math.Max(maxGap,gap); gap=0; } if(gap>7) throw new Exception("Target guarantee exceeded"); }
        Check(maxGap<=8,"1,000 boss drops never exceed eight eligible kills between targets");
        target.SetRecoveryTarget("w4"); Check(target.state.targetBossMisses==0,"Changing target explicitly resets that target's progress");
        Check(!target.SetRecoveryTarget("not-real") && target.state.recoveryTarget=="w4","Invalid target rejected without losing valid selection");
        target.SetRecoveryTarget(null); Check(!target.TargetActive && target.state.targetBossMisses==0,"Target can be cleared safely");
        target.state.recoveryTarget="not-real"; target.state.targetBossMisses=100;
        new GameModel(target.state,1); Check(target.state.recoveryTarget==null && target.state.targetBossMisses==0,"Invalid saved target sanitizes on load");
    }
    private static string Serialize(GameState data)
    {
        using(var stream=new MemoryStream())
        { new DataContractJsonSerializer(typeof(GameState)).WriteObject(stream,data); return Encoding.UTF8.GetString(stream.ToArray()); }
    }
    private static GameState Deserialize(string json)
    {
        using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))
            return (GameState)new DataContractJsonSerializer(typeof(GameState)).ReadObject(stream);
    }
    private static void QualityChecks()
    {
        Check(Balance.Qualities.Length==6,"Five quality tiers have one centralized definition");
        Check(Balance.Tier(Quality.Common).glow==0 && Balance.Tier(Quality.Uncommon).glow==0,"Common and uncommon equipment emit no continuous glow");
        Check(Balance.Tier(Quality.Rare).particles==0 && Balance.Tier(Quality.Legendary).particles==3,"Particles reserved for higher tiers with bounded count");
        Check(Balance.Qualities.All(q=>q.glow<=.5f && (q.glow==0 || q.pulseSeconds>=2.4f)),"Glow intensity and pulse frequency stay bounded");
        for(int q=1;q<5;q++)
        {
            Check(Balance.Qualities[q].multiplier>Balance.Qualities[q-1].multiplier,"Quality "+q+" primary multiplier improves");
            if(q>1) Check(Balance.Qualities[q].affixMin>Balance.Qualities[q-1].affixMin,"Quality "+q+" affix floor improves");
        }
        var model=new GameModel(null,887);
        for(int q=0;q<5;q++)
        {
            bool valid=true;
            for(int i=0;i<400;i++)
            {
                Item item=model.Create("w0",(Quality)q,3);
                foreach(Affix a in item.affixes)
                {
                    float min=Balance.AffixValue(a.stat,(Quality)q,3,0), max=Balance.AffixValue(a.stat,(Quality)q,3,1);
                    if(a.value<min-.0001f || a.value>max+.0001f) valid=false;
                }
            }
            Check(valid,"400 quality "+q+" items stay inside specified level-3 affix ranges");
        }
        Item ordinary=model.Create("b4",Quality.Common,1);
        Item legendary=model.Create("b4",Quality.Legendary,3); legendary.affixes.Clear();
        Check(GameModel.ItemStats(ordinary).haste==GameModel.ItemStats(legendary).haste,"Quality and level do not multiply innate attack-speed traits");
        Check(Math.Abs(GameModel.ItemStats(legendary).health-8*2*Balance.LevelFactor(3))<.001f,"Quality and level scale primary stats exactly once");
        bool invalid=false; try { model.Create("w0",(Quality)99,1); } catch(ArgumentOutOfRangeException) { invalid=true; }
        Check(invalid,"Invalid quality rejected before generating item");
        Item stored=model.Create("w2",Quality.Epic,2); model.state.items.Add(stored); model.Unlock(stored.baseId);
        float original=stored.affixes[0].value;
        new GameModel(model.state,321);
        Check(stored.affixes[0].value==original,"Reloading does not reroll or upgrade existing affix values");
        model.state.glowMode=1; Stats before=model.Total();
        model.state.glowMode=0; Stats after=model.Total();
        Check(before.attack==after.attack && before.crit==after.crit && before.health==after.health,"Glow preference changes no gameplay stats");
        model.state.glowMode=999; new GameModel(model.state,1);
        Check(model.state.glowMode==2,"Saved glow setting safely clamped");
        double[] weights=Balance.QualityWeights(999),cap=Balance.QualityWeights(30);
        Check(weights.SequenceEqual(cap),"Luck over cap cannot increase rarity weights indefinitely");
        Check(Balance.TrainingCost(0)==20 && Enumerable.Range(0,20).Sum(Balance.TrainingCost)==3250,"Training economy cumulative maximum cost verified");
        var recoveryModel=new GameModel(null,441); var recovery=DummyCombat(recoveryModel);
        recovery.health=20; recovery.enemyHealth=1; recovery.attackTimer=0;
        float expected=20+recoveryModel.Total().health*Balance.WaveRecovery;
        recovery.Tick(.4f);
        Check(Math.Abs(recovery.health-expected)<.001f,"Normal wave clear restores exactly four percent max health");
        recovery.wave=5; recovery.health=20; recovery.enemyHealth=1; recovery.attackTimer=0;
        recovery.Tick(.4f);
        Check(recovery.health==recoveryModel.Total().health && recovery.wave==1,"Boss clear fully repairs before next farming cycle");
        var starter=new GameModel(null,1000); var starterBattle=new CombatSession(starter); Simulate(starterBattle,60);
        Check(starter.state.bosses>0 && starterBattle.recovery==0,"Starter setup clears and continues farming without a legendary gate");
    }
    private static void AnimationChecks()
    {
        var animation=new DollAnimation();
        Check(animation.Pose(false).weaponAngle==0,"Animation starts at rest");
        animation.TriggerAttack(false);animation.Tick(.12f);
        DollPose aim=animation.Pose(false);
        Check(aim.rightArm < -60 && aim.leftArm < -40,"Ranged attack raises both arms");
        Check(animation.Pose(false).rightArm==aim.rightArm,"Rendering never advances animation");
        animation.Tick(0);Check(animation.Pose(false).rightArm==aim.rightArm,"Pause freezes complete pose");
        animation.Tick(.06f);DollPose shot=animation.Pose(false);
        Check(shot.muzzle>0 && shot.projectile>0,"Muzzle and projectile appear after anticipation");
        animation.Tick(.5f);Check(animation.Pose(false).weaponAngle==0,"Ranged attack returns to rest");
        animation.TriggerAttack(true);animation.Tick(.22f);DollPose windup=animation.Pose(false);
        Check(windup.rightArm < -145 && windup.rootX<0,"Melee visibly winds up before forward strike");
        animation.Tick(.1f);DollPose strike=animation.Pose(false);
        Check(strike.rootX>15 && strike.rightArm>windup.rightArm+90 && strike.slash>0,"Melee drives arm torso legs forward with slash");
        animation.Clear();animation.TriggerHit();animation.Tick(.1f);DollPose hurt=animation.Pose(false);
        Check(hurt.damageTint>0 && Math.Abs(hurt.rootX)<=1.5f,"Damage response has bounded shake and tint");
        Check(animation.Pose(true).scaleY==.75f && animation.Pose(true).weaponAngle==0,"Repair suppresses attack pose");
        animation.Clear();Check(animation.Pose(false).weaponAngle==0 && animation.Pose(false).damageTint==0,"Clearing cancels attack and hurt");
        Check(SpriteAtlasLayout.Equipment.GetLength(0)==20 && SpriteAtlasLayout.Rig.GetLength(0)==6,"All twenty equipment sprites and six body parts mapped");
        for(int i=0;i<20;i++)Check(SpriteAtlasLayout.Equipment[i,0]>=0 && SpriteAtlasLayout.Equipment[i,1]>=0 && SpriteAtlasLayout.Equipment[i,0]+SpriteAtlasLayout.Equipment[i,2]<=SpriteAtlasLayout.EquipmentWidth && SpriteAtlasLayout.Equipment[i,1]+SpriteAtlasLayout.Equipment[i,3]<=SpriteAtlasLayout.EquipmentHeight,"Equipment bounds "+i);
        Check(Catalog.All.All(b=>SpriteAtlasLayout.Equipment[(int)b.slot*5+b.style,2]>0),"Every catalog ID resolves to an actual texture sample");
        bool grips=true,muzzles=true;
        foreach(bool melee in new[]{false,true})foreach(float time in new[]{0f,.12f,.22f,.28f,.38f,.65f})
        {
            var sample=new DollAnimation();sample.TriggerAttack(melee);sample.Tick(time);DollPose p=sample.Pose(false);
            var commands=PuppetGeometry.Build(melee?1:0,0,0,0,p);
            PuppetSprite weapon=commands.First(v=>v.atlas==0 && v.index==(melee?1:0));
            PuppetSprite arm=commands.First(v=>v.atlas==1 && v.index==3);
            double r=arm.angle*Math.PI/180;float vx=arm.x+arm.w*.5f-arm.px,vy=arm.y+arm.h*.94f-arm.py;
            float handX=arm.px+vx*(float)Math.Cos(r)-vy*(float)Math.Sin(r),handY=arm.py+vx*(float)Math.Sin(r)+vy*(float)Math.Cos(r);
            grips &= Math.Abs(handX-weapon.px)<2 && Math.Abs(handY-weapon.py)<2;
            if(!melee){float mx,my;PuppetGeometry.Muzzle(p,out mx,out my);r=weapon.angle*Math.PI/180;vx=weapon.x+weapon.w*.98f-weapon.px;vy=weapon.y+weapon.h*.24f-weapon.py;
                muzzles &= Math.Abs(mx-(weapon.px+vx*(float)Math.Cos(r)-vy*(float)Math.Sin(r)))<.01f && Math.Abs(my-(weapon.py+vx*(float)Math.Sin(r)+vy*(float)Math.Cos(r)))<.01f;}
        }
        Check(grips,"Weapon stays inside moving hand through both complete attack sequences");
        Check(muzzles,"Muzzle effect follows weapon after parent torso rotation");
        var model=new GameModel(null,153);var battle=DummyCombat(model);int attacks=0,hits=0;
        battle.OnBasicAttack=()=>attacks++;battle.OnHit=()=>hits++;
        battle.attackTimer=0;float before=battle.enemyHealth;battle.Tick(.05f);
        Check(attacks==1 && battle.attackNumber==0 && battle.enemyHealth==before,"Attack starts without dealing damage before windup");
        battle.Tick(.3f);Check(battle.attackNumber==1 && battle.enemyHealth<before,"Impact resolves after raising and projectile flight");
        Simulate(battle,6);
        Check(attacks>=battle.attackNumber && attacks<=battle.attackNumber+1 && battle.droneHits>0,"Only basic attack starts drive puppet animation");
        Check(hits>0,"Enemy damage drives hurt animation");
        battle.attackTimer=0;battle.Tick(.05f);battle.ResetRegion();int prior=battle.attackNumber;battle.Tick(.2f);
        Check(battle.attackNumber==prior,"Region reset cancels pending strike");
    }

    private static void StorageChecks()
    {
        // Every run gets its own directory; existing user saves are never opened or removed.
        string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SaveChecks-"+Guid.NewGuid().ToString("N"));
        var saves=new SaveService(directory,Deserialize,Serialize);
        Check(saves.Load()==null && saves.Writable,"Missing save starts a writable new game");
        var model=new GameModel(null,17);
        Item drop=model.Create("b3",Quality.Epic,2); model.state.items.Add(drop); model.Unlock(drop.baseId); model.SetAppearance(drop.baseId); model.Equip(drop.uid);
        model.state.sinceLegendary=49;
        model.SetRecoveryTarget("b4"); model.state.targetBossMisses=3;
        model.state.glowMode=1;
        Check(saves.Save(model.state),"New save writes successfully");
        GameState read=saves.Load();
        Check(read.items.Last().uid==drop.uid && read.items.Last().affixes[0].value==drop.affixes[0].value,"Save roundtrip preserves random instance data");
        Check(read.appearance[3]=="b3" && read.equipped[3]==drop.uid && read.sinceLegendary==49,"Save roundtrip preserves appearance, loadout, pity");
        Check(read.recoveryTarget=="b4" && read.targetBossMisses==3,"Save roundtrip preserves tracked target and progress");
        Check(read.glowMode==1,"Save roundtrip preserves glow intensity preference");
        model.state.scrap=777; Check(saves.Save(model.state) && File.Exists(saves.path+".bak"),"Atomic replacement keeps previous backup");
        File.WriteAllText(saves.path,"{broken}");
        var recovery=new SaveService(directory,Deserialize,Serialize);
        GameState recovered=recovery.Load();
        Check(recovered!=null && recovery.Message.Contains("恢复"),"Corrupt main save recovers backup");
        Check(Directory.GetFiles(directory,"*.corrupt-*").Length==1,"Corrupt main preserved for diagnosis");
        File.WriteAllText(saves.path,"{broken}"); File.WriteAllText(saves.path+".bak","{broken}");
        var damaged=new SaveService(directory,Deserialize,Serialize);
        Check(damaged.Load()==null && !damaged.Writable && !damaged.Save(model.state),"Both corrupt files protected from overwrite");
        var future=new GameState{version=99}; File.WriteAllText(saves.path,Serialize(future));
        var newer=new SaveService(directory,Deserialize,Serialize);
        Check(newer.Load()==null && !newer.Writable,"Future save version protected from downgrade");
        Check(File.ReadAllText(saves.path).Contains("99"),"Protected future save unchanged");
        string notDirectory=Path.Combine(directory,"file-not-directory"); File.WriteAllText(notDirectory,"keep");
        var failure=new SaveService(notDirectory,Deserialize,Serialize);
        Check(!failure.Save(model.state) && failure.Message.Contains("保存失败"),"Write failure reported without crashing");
        Console.WriteLine("Storage adapter: .NET DataContract JSON; Unity JsonUtility still requires engine smoke test.");
    }
}
