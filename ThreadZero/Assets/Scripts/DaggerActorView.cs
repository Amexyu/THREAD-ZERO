using UnityEngine;

namespace ThreadZero
{
    // One validated outfit sample. Do not hide unrelated equipped head/coat/back appearances.
    public static class DaggerActorView
    {
        private static Texture2D body, weapon;
        private static bool loaded;
        private static bool missingReported;
        public static bool Ready(out string failure)
        {
            if(!loaded) {loaded=true;body=Resources.Load<Texture2D>("Art/V12/StarterDagger/Body");weapon=Resources.Load<Texture2D>("Art/V12/StarterDagger/Weapon");}
            failure=body==null?"Art/V12/StarterDagger/Body.png":weapon==null?"Art/V12/StarterDagger/Weapon.png":null;
            if(failure==null && (body.width!=1536 || body.height!=768 || weapon.width!=1536 || weapon.height!=768)) failure="StarterDagger atlas must be 1536x768; texture import size changed";
            if(failure!=null && !missingReported){missingReported=true;Debug.LogError("THREAD ZERO: V12 art unavailable: "+failure);}
            return failure==null;
        }
        public static bool CanDraw(GameModel model)
        {
            bool defaultHead=model.Equipped(Slot.Head)==null && string.IsNullOrEmpty(model.state.appearance[(int)Slot.Head]);
            return DaggerSkill.Matches(model) && model.Appearance(Slot.Weapon).id=="w1" && defaultHead
                && model.Appearance(Slot.Coat).id=="c0" && model.Appearance(Slot.Back).id=="b0";
        }
        public static bool Draw(GameModel model,float x,float y,float canvas,int frame,Color tint)
        {
            if(!CanDraw(model))return false;
            float scale=canvas/256;
            return Render(Ui.R(x-128*scale,y-238*scale,canvas,canvas),frame,tint);
        }
        private static bool Render(Rect rect,int frame,Color tint)
        {
            string failure;if(!Ready(out failure))return false;
            frame=Mathf.Clamp(frame,0,17);
            Rect uv=new Rect((frame%6)/6f,1-(frame/6+1)/3f,1/6f,1/3f);
            Color old=GUI.color;GUI.color=tint;
            GUI.DrawTextureWithTexCoords(rect,body,uv,true);
            GUI.DrawTextureWithTexCoords(rect,weapon,uv,true);
            GUI.color=old;return true;
        }
        public static bool Portrait(GameModel model,Rect area,float time,bool down)
        {
            if(!CanDraw(model))return false;
            float canvas=Mathf.Min(area.width,area.height*1.05f);
            return Render(new Rect(area.center.x-canvas*.5f,area.yMax-238/256f*canvas,canvas,canvas),0,down?new Color(.6f,.65f,.68f):Color.white);
        }
    }
}
