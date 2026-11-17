using UnityEngine;

namespace ThreadZero
{
    // Deliberately procedural placeholder art. Stable layers are the replacement boundary for final sprites.
    public static class CharacterView
    {
        private static Texture2D disk;
        public static readonly Color[] Palette = {
            new Color(.2f,.8f,.83f), new Color(.72f,.93f,.3f), new Color(.61f,.47f,.98f),
            new Color(.98f,.42f,.3f), new Color(.84f,.89f,.94f)
        };
        public static void Box(float x,float y,float w,float h,Color color)
        { Color old=GUI.color; GUI.color=color; GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture); GUI.color=old; }
        public static void Circle(float x,float y,float radius,Color color)
        {
            if (disk == null)
            {
                disk = new Texture2D(64,64,TextureFormat.RGBA32,false);
                var px = new Color[4096];
                for(int j=0;j<64;j++) for(int i=0;i<64;i++)
                { float d=Vector2.Distance(new Vector2(i+.5f,j+.5f),new Vector2(32,32)); px[j*64+i]=new Color(1,1,1,Mathf.Clamp01(32-d)); }
                disk.SetPixels(px); disk.Apply();
            }
            Color old=GUI.color; GUI.color=color; GUI.DrawTexture(new Rect(x-radius,y-radius,radius*2,radius*2),disk); GUI.color=old;
        }
        public static void Line(Vector2 a,Vector2 b,float width,Color color)
        {
            Matrix4x4 old=GUI.matrix; GUI.matrix=old*Ui.RotateLocal(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);
            Box(a.x,a.y-width/2,Vector2.Distance(a,b),width,color); GUI.matrix=old;
        }
        public static void Draw(GameModel model,Rect area,float animation,bool attacking=false,float hurt=0,bool defeated=false,float dash=0,DollPose? pose=null)
        {
            if(DaggerActorView.Portrait(model,area,animation,defeated)) return;
            if(PaperDollView.Draw(model,area,animation,attacking,hurt,defeated,dash,pose)) return;
            GUI.BeginGroup(area);
            float scale=Mathf.Min(area.width/210,area.height/275);
            Matrix4x4 old=GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(scale,scale),Vector2.zero);
            float x=area.width/scale/2, y=area.height/scale/2-10 + Mathf.Sin(animation*2.3f)*2;
            EquipmentBase coat=model.Appearance(Slot.Coat), head=model.Appearance(Slot.Head), back=model.Appearance(Slot.Back), weapon=model.Appearance(Slot.Weapon);
            Color dark=new Color(.055f,.07f,.10f), cloth=new Color(.15f,.18f,.23f);
            Color accent=Palette[coat.style];
            // Back module, then legs and coat, then head and weapon.
            Color bp=Palette[back.style];
            Box(x-56,y-28,27,76,dark); Box(x-54,y-23,21,57,new Color(.23f,.27f,.33f));
            Box(x-54,y-15,5,28,bp);
            if(back.style==1 || back.style==2) { Line(new Vector2(x-43,y-20),new Vector2(x-53,y-75),4,bp); Circle(x-53,y-75,5,bp); }
            if(back.style==3) { Line(new Vector2(x-48,y-18),new Vector2(x-80,y+22),12,bp); Line(new Vector2(x-80,y+22),new Vector2(x-62,y+48),9,bp); }
            if(back.style==4) { Box(x-64,y-10,10,45,bp); Box(x-63,y+38,8,10,new Color(1,.5f,.15f)); }
            Box(x-28,y+48,25,60,dark); Box(x+4,y+48,25,60,dark);
            Box(x-31,y+97,30,15,new Color(.24f,.28f,.33f)); Box(x+3,y+97,33,15,new Color(.24f,.28f,.33f));
            Box(x-28,y+65,24,11,accent*.8f); Box(x+4,y+67,24,5,accent);
            int length=coat.style==1 || coat.style==4 ? 85 : 65;
            Box(x-39,y-30,80,length+6,dark);
            Box(x-34,y-26,70,length,coat.style==4 ? new Color(.75f,.78f,.8f) : cloth);
            Box(x-36,y-27,14,length,accent*.6f); Box(x-2,y-27,4,length,dark);
            Box(x-23,y+3,21,18,dark); Box(x+11,y+12,17,20,dark);
            Box(x-20,y+6,15,3,accent); Box(x+13,y+15,12,3,accent);
            Line(new Vector2(x-27,y-23),new Vector2(x+26,y+34),7,dark);
            Box(x-31,y+length-30,64,7,dark); Box(x+6,y+length-31,14,10,bp);
            if(coat.style==2) { Box(x+3,y-19,28,25,accent*.5f); Box(x+7,y-15,20,3,accent); }
            if(coat.style==3) { Box(x-14,y+36,4,40,accent); Box(x+15,y+32,4,48,accent); }
            Circle(x,y-60,35,dark); Circle(x,y-60,29,new Color(.65f,.68f,.69f));
            Color hc=Palette[head.style];
            if(head.style==0) { Box(x-34,y-87,68,24,cloth); Box(x-36,y-73,13,40,cloth); Box(x+23,y-73,13,40,cloth); }
            if(head.style==1) { Box(x-31,y-62,62,30,dark); Box(x-20,y-49,40,3,hc); }
            if(head.style==2) { Box(x-33,y-73,66,20,dark); Box(x-27,y-69,54,10,hc); }
            if(head.style==3) { Circle(x,y-62,35,cloth); Box(x-29,y-71,58,16,hc); Box(x-20,y-45,40,7,dark); }
            if(head.style==4) { Box(x-29,y-100,17,26,dark); Box(x+12,y-100,17,26,dark); Box(x-25,y-96,9,13,hc); Box(x+16,y-96,9,13,hc); }
            if(head.style!=3) { Box(x-16,y-65,10,4,new Color(.04f,.06f,.09f)); Box(x+8,y-65,10,4,new Color(.04f,.06f,.09f)); }
            Box(x-24,y-33,48,12,dark); Box(x-9,y-30,18,4,accent);
            float kick=attacking ? 5 : 0;
            Line(new Vector2(x+28,y-9),new Vector2(x+48+kick,y+22),18,cloth);
            Box(x+39+kick,y+14,18,16,dark);
            Color wc=Palette[weapon.style];
            if(weapon.style==1) { Line(new Vector2(x+50,y+23),new Vector2(x+90+kick,y-49),9,dark); Line(new Vector2(x+53,y+17),new Vector2(x+87+kick,y-47),4,wc); }
            else { Box(x+40+kick,y+5,weapon.style==4 ? 65 : 49,18,dark); Box(x+45+kick,y+8,36,7,wc); Box(x+77+kick,y+10,20,7,new Color(.4f,.45f,.49f));
                if(weapon.style==2) { Circle(x+68+kick,y+13,12,dark); Circle(x+68+kick,y+13,7,wc); }
                if(attacking) Circle(x+103+kick,y+13,10,new Color(1,.8f,.32f)); }
            // Emission follows each ACTUAL equipped item's rarity, anchored on the selected appearance.
            // The texture/garment design is not recolored wholesale by rarity.
            int mode=model.state.glowMode;
            QualityVisual.EquipmentLine(model.Equipped(Slot.Back),new Vector2(x-52,y-14),new Vector2(x-52,y+14),animation,mode);
            QualityVisual.EquipmentLine(model.Equipped(Slot.Coat),new Vector2(x-21,y+7),new Vector2(x-7,y+7),animation,mode);
            QualityVisual.EquipmentLine(model.Equipped(Slot.Coat),new Vector2(x+14,y+16),new Vector2(x+26,y+16),animation,mode);
            QualityVisual.EquipmentLine(model.Equipped(Slot.Head),new Vector2(x-23,y+(head.style==0?-82:-67)),new Vector2(x+23,y+(head.style==0?-82:-67)),animation,mode);
            if(weapon.style==1)
                QualityVisual.EquipmentLine(model.Equipped(Slot.Weapon),new Vector2(x+57,y+9),new Vector2(x+87+kick,y-47),animation,mode);
            else QualityVisual.EquipmentLine(model.Equipped(Slot.Weapon),new Vector2(x+47+kick,y+11),new Vector2(x+77+kick,y+11),animation,mode);
            GUI.matrix=old; GUI.EndGroup();
        }
    }
}
