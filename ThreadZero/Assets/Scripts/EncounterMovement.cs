using System;

namespace ThreadZero
{
    // Distances are logical stage pixels. The renderer and combat share these positions.
    public sealed class EncounterMovement
    {
        public const float InitialHero=428, InitialEnemy=1060, PackSpacing=136;
        public float HeroX {get;private set;}=InitialHero;
        public float EnemyX {get;private set;}=InitialEnemy;
        public float CameraX {get;private set;}
        public float HeroTravel {get;private set;}
        public float EnemyTravel {get;private set;}
        public float HeroVelocity {get;private set;}
        public float EnemyVelocity {get;private set;}
        public float ContactGap {get;private set;}=176;
        public float HeroRange {get;private set;}=176;
        public int TargetVersion {get;private set;}
        public float HeroScreenX {get{return HeroX-CameraX;}}
        public float EnemyScreenX {get{return EnemyX-CameraX;}}
        public float Gap {get{return EnemyX-HeroX;}}
        public bool HeroInRange {get{return Gap<=HeroRange+.1f;}}
        public bool EnemyInRange {get{return Gap<=ContactGap+.1f;}}
        public bool HeroMoving {get{return HeroVelocity>.01f;}}
        public bool EnemyMoving {get{return EnemyVelocity>.01f;}}

        public void ResetAtCamera(float camera=0)
        {
            CameraX=Math.Max(0,camera);HeroX=CameraX+InitialHero;EnemyX=CameraX+InitialEnemy;
            HeroVelocity=EnemyVelocity=HeroTravel=EnemyTravel=0;TargetVersion++;
        }
        public void BeginEncounter()
        {
            // Continue down the street: never warp the hero back after each kill.
            EnemyX=Math.Max(EnemyX,HeroX+760);HeroVelocity=EnemyVelocity=0;TargetVersion++;
        }
        public void AdvanceTarget()
        {EnemyX+=PackSpacing;HeroVelocity=EnemyVelocity=0;TargetVersion++;}
        public void Stop(){HeroVelocity=EnemyVelocity=0;}
        private static float Approach(float value,float target,float max)
        {return value<target?Math.Min(target,value+max):Math.Max(target,value-max);}
        public void Tick(float dt,bool melee,bool boss,bool elite,bool heroBusy)
        {
            if(dt<=0 || float.IsNaN(dt))return;
            ContactGap=boss?200:elite?188:176;
            HeroRange=melee?ContactGap:ContactGap+150;
            float heroNeed=Math.Max(0,Gap-HeroRange), enemyNeed=Math.Max(0,Gap-ContactGap);
            float hv=Approach(HeroVelocity,heroNeed>.1f && !heroBusy?220:0,900*dt);
            float ev=Approach(EnemyVelocity,enemyNeed>.1f?160:0,700*dt);
            float hd=heroBusy?0:Math.Min(heroNeed,hv*dt),ed=Math.Min(enemyNeed,ev*dt);
            float allowed=Math.Max(0,Gap-ContactGap),sum=hd+ed;
            if(sum>allowed && sum>0){float ratio=allowed/sum;hd*=ratio;ed*=ratio;}
            HeroX+=hd;EnemyX-=ed;HeroTravel+=hd;EnemyTravel+=ed;
            HeroVelocity=hd/dt;EnemyVelocity=ed/dt;
            float cameraTarget=Math.Max(CameraX,HeroX-640);
            CameraX+=(cameraTarget-CameraX)*(1-(float)Math.Exp(-6*dt));
        }
    }

    public sealed partial class CombatSession
    {
        public readonly EncounterMovement Movement=new EncounterMovement();
        public bool MovementEnabled {get;private set;}
        public void EnableMovement(){MovementEnabled=true;Movement.ResetAtCamera();}
        public bool HeroInRange {get{return !MovementEnabled || Movement.HeroInRange;}}
        public bool EnemyInRange {get{return !MovementEnabled || Movement.EnemyInRange;}}
    }
}
