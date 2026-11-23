using UnityEngine;

namespace ThreadZero
{
    public static class QualityVisual
    {
        public static readonly Color[] Colors = MakeColors();
        private static Texture2D glowTexture;
        // Text on the pale workshop surface uses ink; luminous emission keeps the bright palette.
        public static Color Ink(Quality quality)
        {
            Color[] ink={new Color(.32f,.39f,.43f),new Color(.13f,.42f,.28f),new Color(.12f,.32f,.74f),new Color(.49f,.24f,.66f),new Color(.65f,.35f,.05f),new Color(.72f,.13f,.18f),new Color(.04f,.47f,.44f)};
            return ink[(int)quality];
        }
        private static Color[] MakeColors()
        {
            var colors = new Color[Balance.Qualities.Length];
            for(int i=0;i<colors.Length;i++)
                ColorUtility.TryParseHtmlString("#"+Balance.Qualities[i].colorHex, out colors[i]);
            return colors;
        }
        private static Texture2D GlowTexture
        {
            get
            {
                if(glowTexture != null) return glowTexture;
                glowTexture=new Texture2D(64,64,TextureFormat.RGBA32,false) { filterMode=FilterMode.Bilinear, wrapMode=TextureWrapMode.Clamp };
                var pixels=new Color[4096];
                for(int y=0;y<64;y++) for(int x=0;x<64;x++)
                {
                    float r=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32))/32;
                    float alpha=Mathf.Pow(Mathf.Clamp01(1-r),2);
                    pixels[y*64+x]=new Color(1,1,1,alpha);
                }
                glowTexture.SetPixels(pixels); glowTexture.Apply(); return glowTexture;
            }
        }
        public static void Halo(Vector2 center,float radius,Color color,float opacity)
        {
            if(opacity<=0) return;
            Color old=GUI.color; color.a=Mathf.Clamp01(opacity); GUI.color=color;
            GUI.DrawTexture(new Rect(center.x-radius,center.y-radius,radius*2,radius*2),GlowTexture);
            GUI.color=old;
        }
        public static void EquipmentLine(Item item,Vector2 a,Vector2 b,float time,int mode)
        {
            if(item==null || mode<=0) return;
            QualityTier q=Balance.Tier(item.quality);
            if(q.glow<=0) return;
            Color color=Colors[(int)item.quality];
            // Low mode: static dim lighting, no particles. Full pulses never flash or go fully dark.
            float pulse=mode==1 ? .45f : .85f+.15f*Mathf.Sin(time*2*Mathf.PI/q.pulseSeconds);
            Vector2 middle=(a+b)/2;
            float radius=Mathf.Clamp(Vector2.Distance(a,b)*.48f,11,24);
            Halo(middle,radius,color,q.glow*pulse);
            Color edge=color; edge.a=Mathf.Clamp01(q.glow*pulse+.22f);
            CharacterView.Line(a,b,2,edge);
            if(mode<2) return;
            for(int i=0;i<q.particles;i++)
            {
                float phase=time*.75f+i*2.39996f+(int)Catalog.Find(item.baseId).slot;
                Vector2 point=middle+new Vector2(Mathf.Cos(phase)*radius*.75f,Mathf.Sin(phase)*radius*.45f);
                Halo(point,4,color,.35f); CharacterView.Circle(point.x,point.y,1.2f,color);
            }
        }
        public static void ItemFrame(Rect rect,Quality quality,bool selected,int mode,float time)
        {
            Color color=Colors[(int)quality];
            CharacterView.Box(rect.x,rect.y,4,rect.height,color);
            float opacity=selected?.7f:.22f;
            Color edge=color; edge.a=opacity;
            CharacterView.Box(rect.x,rect.y,rect.width,1,edge);
            CharacterView.Box(rect.x,rect.y+rect.height-1,rect.width,1,edge);
            if(mode>0 && (int)quality>=3)
                Halo(new Vector2(rect.x+8,rect.y+rect.height/2),18,color,mode==1?.1f:.2f);
        }
        public static float DropDuration(Quality quality) { return new[]{1.2f,1.5f,2.2f,3.2f,4.2f,5f,5.6f}[(int)quality]; }
        public static void DropBeam(Vector2 point,Quality quality,float remaining,int mode)
        {
            Color color=Colors[(int)quality]; float fade=Mathf.Clamp01(remaining*2);
            if(mode>0) Halo(point,18+(int)quality*4,color,(mode==1?.18f:.4f)*fade);
            CharacterView.Box(point.x-6,point.y-12,12,17,color);
            CharacterView.Box(point.x-3,point.y-9,6,8,new Color(.05f,.08f,.11f));
            if(mode<=0 || (int)quality<2) return;
            float height=32+(int)quality*10;
            Color beam=color; beam.a=(mode==1?.12f:.22f)*fade;
            CharacterView.Box(point.x-4,point.y-height,8,height,beam);
            if(mode==2) Halo(new Vector2(point.x,point.y-height/2),height*.5f,color,.18f*fade);
        }
    }
}
