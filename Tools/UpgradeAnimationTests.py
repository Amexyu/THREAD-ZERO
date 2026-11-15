from pathlib import Path
root=Path(__file__).resolve().parent.parent
p=root/'Tests/ModelTests.cs'
s=p.read_text(encoding='utf-8')
s=s.replace('gc.attackTimer=0; gc.Tick(.05f);','gc.attackTimer=0; gc.Tick(.4f);')
s=s.replace('recovery.Tick(.05f);','recovery.Tick(.4f);')
start=s.index('    private static void AnimationChecks()')
end=s.index('    private static void StorageChecks()',start)
s=s[:start]+'''    private static void AnimationChecks()
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

''' +s[end:]
p.write_text(s,encoding='utf-8')
p=root/'Tools/TestModel.ps1';s=p.read_text(encoding='utf-8')
s=s.replace("(Join-Path $projectRoot 'Tests\\ModelTests.cs')", "(Join-Path $projectRoot 'ThreadZero\\Assets\\Scripts\\SpriteAtlasLayout.cs') (Join-Path $projectRoot 'Tests\\ModelTests.cs')")
p.write_text(s,encoding='utf-8')
