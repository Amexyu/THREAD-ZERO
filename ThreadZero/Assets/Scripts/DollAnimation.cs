using System;
namespace ThreadZero {
 public struct DollPose {
 public float rootX,rootY,scaleY,weaponAngle,damageTint,torsoAngle,headAngle,leftArm,rightArm,leftLeg,rightLeg,muzzle,slash,projectile,attackProgress;
 public bool melee;
 }
 public sealed class DollAnimation {
 float elapsed,age=10,hurtRemaining;bool melee;
 public static float Windup(bool melee){return melee?.22f:.16f;}
 public void TriggerAttack(bool isMelee){melee=isMelee;age=0;}
 public void TriggerHit(){hurtRemaining=.2f;}
 public void Clear(){age=10;hurtRemaining=0;}
 public void Tick(float dt){dt=Math.Max(0,Math.Min(dt,2));elapsed+=dt;age+=dt;hurtRemaining=Math.Max(0,hurtRemaining-dt);}
 static float Smooth(float a,float b,float x){x=Math.Max(0,Math.Min(1,x));return a+(b-a)*x*x*(3-2*x);}
 public DollPose Pose(bool repair){
 if(repair)return new DollPose{scaleY=.75f,damageTint=.35f,leftArm=12,rightArm=12};
 float breath=(float)Math.Sin(elapsed*2.2f),duration=melee?.68f:.56f;
 var p=new DollPose{scaleY=1,rootY=breath*.6f,leftArm=3+breath,rightArm=-5-breath,leftLeg=3,rightLeg=-3,damageTint=hurtRemaining/.2f*.35f,melee=melee};
 if(age<duration){
 float wind=Windup(melee),release=age-wind;p.attackProgress=age/duration;
 if(melee){
 p.rightArm=age<wind?Smooth(-5,-152,age/wind):release<.11f?Smooth(-152,-18,release/.11f):Smooth(-18,-5,(release-.11f)/.35f);
 p.leftArm=p.rightArm*.45f;p.torsoAngle=age<wind?Smooth(0,-12,age/wind):Smooth(14,0,release/.46f);
 p.rootX=age<wind?Smooth(0,-13,age/wind):release<.12f?Smooth(-13,28,release/.12f):Smooth(28,0,(release-.12f)/.34f);
 p.leftLeg=3+p.rootX*.3f;p.rightLeg=-3-p.rootX*.2f;
 p.slash=release>=0&&release<.19f?(float)Math.Sin(Math.PI*release/.19f):0;
 }else{
 p.rightArm=age<wind?Smooth(-5,-80,age/wind):release<.06f?Smooth(-80,-96,release/.06f):Smooth(-96,-5,(release-.06f)/.34f);
 p.leftArm=p.rightArm*.72f;p.torsoAngle=age<wind?Smooth(0,4,age/wind):Smooth(-7,0,release/.4f);
 p.rootX=release>=0?Smooth(-9,0,release/.4f):0;
 p.muzzle=release>=0&&release<.065f?1-release/.065f:0;
 p.projectile=release>=0&&release<.12f?release/.12f:0;
 }
 p.weaponAngle=p.rightArm+80;p.headAngle=-p.torsoAngle*.45f;
 }
 p.rootX+=(float)Math.Sin(elapsed*70)*1.5f*hurtRemaining/.2f;return p;
 }
 }
}
