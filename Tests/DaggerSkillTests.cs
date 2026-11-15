using System;
using System.Linq;
using ThreadZero;

public static class DaggerSkillTests
{
    private static int checks;
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
    private static GameModel Model(Quality quality=Quality.Common)
    {
        var m=new GameModel(null,1827);var item=m.Create("w1",quality,1);m.state.items.Add(item);m.Equip(item.uid);
        m.state.equipped[(int)Slot.Back]=null;return m;
    }
    private static CombatSession Session(GameModel m)
    {return new CombatSession(m){enemyHealth=100000,enemyMaxHealth=100000,enemyTimer=1000,attackTimer=0};}
    private static void Advance(CombatSession c,float seconds)
    {for(float t=0;t<seconds-.00001f;t+=.01f)c.Tick(Math.Min(.01f,seconds-t));}
    public static void Main()
    {
        var m=Model();var c=Session(m);int hits=0;float[] ages=new float[3];
        c.OnDaggerHit=(index,damage)=>{ages[hits]=c.DaggerAge;hits++;};
        c.Tick(.01f);Check(c.DaggerCombo && c.DaggerActive,"Common w1 starts its equipment skill");
        float before=c.enemyHealth;Advance(c,.22f);Check(hits==0 && c.enemyHealth==before,"No damage before visible first cut");
        Advance(c,.32f);Check(hits==3,"Exactly three independent hits");
        Check(Math.Abs(ages[0]-.23f)<.011f && Math.Abs(ages[1]-.32f)<.011f && Math.Abs(ages[2]-.51f)<.011f,"Hits follow cut, return, thrust contact frames");
        float damage=GameModel.Damage(m.Total().attack*.8f,Balance.EnemyDefense(0,false))*3;
        Check(Math.Abs((before-c.enemyHealth)-damage)<.03f,"Skill has three 80% hits without basic-only execution/proc multiplication");
        Advance(c,.15f);Check(!c.DaggerActive,"Clip finishes with recovery");
        int attacks=0;c.OnBasicAttack=()=>attacks++;Advance(c,1.5f);Check(hits==3 && attacks>0,"Cooldown uses single-stab basic attacks");
        float age=c.DaggerAge,cd=c.DaggerCooldown;c.Tick(0);Check(c.DaggerAge==age && c.DaggerCooldown==cd,"Zero simulation time does not advance timeline");
        foreach(Quality q in new[]{Quality.Uncommon,Quality.Rare,Quality.Epic,Quality.Legendary,Quality.Prototype})
        {var high=Session(Model(q));int count=0;high.OnDaggerHit=(i,d)=>count++;Advance(high,1);Check(count==0 && !high.DaggerCombo,"Higher-quality w1 does not inherit starter skill: "+q);}
        var dead=Session(Model());int killedHits=0;dead.enemyHealth=1;dead.OnDaggerHit=(i,d)=>killedHits++;Advance(dead,.7f);
        Check(killedHits==1,"Target death cancels remaining hits instead of hitting next pack member");
        var swapModel=Model();var swap=Session(swapModel);int swapHits=0;swap.OnDaggerHit=(i,d)=>swapHits++;Advance(swap,.24f);
        var gun=swapModel.Create("w0",Quality.Common,1);swapModel.state.items.Add(gun);swapModel.Equip(gun.uid);Advance(swap,.5f);
        Check(swapHits==1 && !swap.DaggerActive,"Weapon swap cancels active combo");
        var fastModel=Model();fastModel.Equipped(Slot.Weapon).affixes.Add(new Affix{stat=3,value=2});var fast=Session(fastModel);int fastHits=0;fast.OnDaggerHit=(i,d)=>fastHits++;Advance(fast,.8f);
        Check(fastHits==3,"High attack speed does not overlap or duplicate skill events");
        Check(DaggerSkill.Frame(.23f,true)==4 && DaggerSkill.Frame(.32f,true)==8 && DaggerSkill.Frame(.51f,true)==14,"Rendering and battle share contact-frame timeline");
        Check(DaggerSkill.Frame(.14f,false)==14,"Single stab contact uses the same held weapon pose");
        Console.WriteLine("DAGGER TESTS: "+checks+" PASS");
    }
}
