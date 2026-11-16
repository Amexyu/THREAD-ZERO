using System;
using ThreadZero;

public static class MovementTests
{
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS: "+name);}
    static GameModel Model(bool melee=true)
    {
        var m=new GameModel(null,1616);
        var w=m.Create(melee?"w1":"w0",Quality.Common,1);m.state.items.Add(w);m.Equip(w.uid);
        m.state.equipped[(int)Slot.Back]=null;return m;
    }
    static CombatSession Combat(bool melee=true)
    {var c=new CombatSession(Model(melee));c.EnableMovement();c.enemyHealth=c.enemyMaxHealth=100000;return c;}
    static void Advance(CombatSession c,float seconds)
    {for(float t=0;t<seconds-.00001f;t+=.01f)c.Tick(Math.Min(.01f,seconds-t));}
    public static void Main()
    {
        var c=Combat();float h=c.health,e=c.enemyHealth;int strikes=0;c.OnBasicAttack=()=>strikes++;
        c.Tick(.05f);Check(c.Movement.HeroX>428 && c.Movement.EnemyX<1060,"Both combatants approach simultaneously");
        Advance(c,.75f);Check(c.health==h && c.enemyHealth==e && strikes==0 && !c.DaggerActive,"No remote melee damage or attacks while approaching");
        Advance(c,.8f);Check(c.HeroInRange && c.EnemyInRange,"Close to physical contact range");
        Check(c.Movement.Gap>=c.Movement.ContactGap-.01f,"Bodies never overlap");
        float hx=c.Movement.HeroX,ex=c.Movement.EnemyX;Advance(c,.9f);
        Check(c.health<h && c.enemyHealth<e,"Both sides attack continuously at contact");
        Check(Math.Abs(c.Movement.HeroX-hx)<.01f && Math.Abs(c.Movement.EnemyX-ex)<.01f,"Attack cycles do not send either side back to starting positions");
        Check(!c.Movement.HeroMoving && !c.Movement.EnemyMoving,"Locomotion stops while fighting");
        float cam=c.Movement.CameraX;Advance(c,.8f);Check(c.Movement.CameraX>=cam && c.Movement.HeroScreenX>600,"Camera follows progression without resetting encounter positions");
        c.Tick(0);Check(c.Movement.HeroX==hx && c.Movement.EnemyX==ex,"Zero simulation time freezes positions");

        var ranged=Combat(false);bool separate=false;
        for(int i=0;i<300;i++){ranged.Tick(.01f);if(!ranged.Movement.HeroMoving && ranged.Movement.EnemyMoving && ranged.HeroInRange)separate=true;}
        Check(separate,"Ranged hero can stop and fire while enemy keeps approaching");
        Check(ranged.EnemyInRange,"Enemy continues into close range against ranged hero");

        var pack=Combat();Advance(pack,1.5f);pack.reserve.Clear();pack.reserve.Add(10000);pack.reserveMax=10000;
        pack.enemyHealth=1;float oldHero=pack.Movement.HeroX;int oldVersion=pack.Movement.TargetVersion;
        for(int i=0;i<150 && pack.Movement.TargetVersion==oldVersion;i++)pack.Tick(.01f);
        Check(pack.Movement.TargetVersion>oldVersion && pack.Movement.Gap>pack.Movement.ContactGap+100,"Next pack member remains at its real position and must be approached");
        Check(Math.Abs(pack.Movement.HeroX-oldHero)<.1f,"Killing front member does not teleport hero");
        float nextHealth=pack.enemyHealth;pack.Tick(.01f);Check(pack.enemyHealth==nextHealth,"Remaining combo cannot hit the next enemy across a gap");
        float oldGap=pack.Movement.Gap;Advance(pack,.5f);Check(pack.Movement.Gap<oldGap,"Survivors close the new gap instead of waiting for a scripted turn");

        var wave=Combat();Advance(wave,1.5f);wave.reserve.Clear();wave.enemyHealth=1;oldHero=wave.Movement.HeroX;oldVersion=wave.Movement.TargetVersion;
        for(int i=0;i<150 && wave.Movement.TargetVersion==oldVersion;i++)wave.Tick(.01f);
        Check(wave.wave==2 && Math.Abs(wave.Movement.HeroX-oldHero)<.1f && wave.Movement.EnemyX>oldHero+700,"Next wave spawns ahead without resetting hero down the street");

        var independent=Combat();Advance(independent,1.45f);independent.enemyTimer=.25f;float startHealth=independent.health;Advance(independent,.3f);
        Check(independent.DaggerActive && independent.health<startHealth,"Enemy strike can resolve during the hero combo; no turn ownership");

        var large=new EncounterMovement();large.Tick(10,true,true,false,false);
        Check(large.Gap>=large.ContactGap-.01f && large.HeroX<large.EnemyX,"Large movement step clamps closing distance");
        large.ResetAtCamera(1500);Check(large.HeroScreenX==428 && large.EnemyScreenX==1060,"Region transitions preserve the camera coordinate system");
        Console.WriteLine("MOVEMENT TESTS: "+count+" PASS");
    }
}
