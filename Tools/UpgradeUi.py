from pathlib import Path
root=Path(__file__).resolve().parent.parent
p=root/'ThreadZero/Assets/Scripts/GameApp.cs'
s=p.read_text(encoding='utf-8')
start=s.index('        private static readonly Color Bg=')
end=s.index('\n\n        private void Awake',start)
s=s[:start]+'''        private static readonly Color Bg=new Color(.945f,.945f,.92f),Surface=new Color(.99f,.99f,.975f),
            Raised=new Color(.875f,.90f,.895f),Text=new Color(.10f,.17f,.20f),Muted=new Color(.35f,.43f,.45f),
            Cyan=new Color(.16f,.31f,.85f),Lime=new Color(.14f,.48f,.33f),Danger=new Color(.88f,.28f,.14f);
''' +s[end:]
s=s.replace('Color fill=primary?new Color(.19f,.43f,.44f):active?new Color(.13f,.24f,.28f):Raised;', 'Color fill=primary?Cyan:active?new Color(.80f,.86f,.98f):Raised;')
s=s.replace('fill*=1.18f','fill=Color.Lerp(fill,Color.white,.22f)')
s=s.replace('Box(x,y,w,h,fill);if(active)Box(x,y+h-2,w,2,Cyan);','Box(x,y,w,h,fill);Box(x,y,primary?4:2,h,primary?Danger:active?Cyan:new Color(.72f,.78f,.77f));if(active)Box(x,y+h-2,w,2,Cyan);')
s=s.replace('available?(active?Cyan:Text):Muted','available?(primary?Color.white:active?Cyan:Text):Muted')
s=s.replace('            Header();','            Backdrop();Header();')
at=s.index('        private void Header()')
s=s[:at]+'''        private void Backdrop()
        {
            Color grid=new Color(.16f,.31f,.40f,.045f);
            for(int x=64;x<1540;x+=40)Box(x,110,1,677,grid);
            for(int y=110;y<790;y+=40)Box(64,y,1472,1,grid);
            Box(0,0,18,900,Cyan);Box(18,0,6,900,Danger);
            Box(64,105,1472,1,new Color(.69f,.75f,.75f));
            Label(64,844,310,22,"TZ / RECOVERY DIVISION",12,Muted);
            Label(1350,844,190,22,"FIELD TERMINAL  /  01",11,Muted);
        }
''' +s[at:]
s=s.replace('Label(64,28,360,40,"零织协议",27);','Box(64,28,50,50,Cyan);Label(72,33,46,44,"TZ",27,Color.white);Label(132,24,360,43,"零织协议",29);Label(135,68,250,19,"THREAD ZERO / 工坊终端",11,Muted);')
s=s.replace('Label(66,72,550,25,subtitle,14,Muted);','Label(500,48,550,30,subtitle,15,Muted);')
s=s.replace('new Color(.13f,.18f,.22f)','new Color(.70f,.77f,.77f)')
start=s.index('            // The scene is the main content')
end=s.index('            float lunge=',start)
s=s[:start]+'''            // High-key industrial bay: structure, location marks and safety paint.
            Box(100,240,1400,410,new Color(.91f,.925f,.90f));
            for(int i=0;i<6;i++)
            {
                float x=128+i*238;Box(x,270,208,310,new Color(.955f,.96f,.94f));
                Box(x,270,208,3,new Color(.77f,.82f,.80f));Box(x,270,3,310,new Color(.81f,.85f,.83f));
                Box(x+18,294,36,6,i%2==0?Cyan:Muted);
            }
            Label(159,288,480,80,"RECOVERY / 0"+(model.state.region+1),35,new Color(.65f,.72f,.70f));
            Label(162,337,350,25,"RECLAIM · REFIT · REPEAT",12,Muted);
            Box(100,651,1400,3,Text);Box(100,655,1400,18,new Color(.83f,.86f,.82f));
            for(int i=0;i<22;i++)CharacterView.Line(new Vector2(116+i*64,655),new Vector2(132+i*64,672),5,i%3==0?Danger:new Color(.71f,.77f,.73f));
            Label(110,686,365,28,"01 / 现场回收",14,Cyan);
''' +s[end:]
s=s.replace('new Rect(175+lunge,218,350,440)','new Rect(130+lunge,218,465,440)')
s=s.replace('            Enemy(1165,528);','''            float approach=model.IsMelee&&pose.attackProgress>0?Mathf.Sin(pose.attackProgress*Mathf.PI)*520:0;
            // A second offset moves the complete puppet for melee contact, including all clothes.
            Enemy(1165,528);
            if(pose.projectile>0)CharacterView.Line(new Vector2(610+pose.projectile*500,422),new Vector2(647+pose.projectile*500,422),4,Danger);''')
# Move puppet as a complete unit during close combat; never leave arms or gear behind.
s=s.replace('CharacterView.Draw(model,new Rect(130+lunge,218,465,440)', 'float approach=model.IsMelee&&pose.attackProgress>0?Mathf.Sin(pose.attackProgress*Mathf.PI)*520:0;\n            CharacterView.Draw(model,new Rect(130+lunge+approach,218,465,440)')
s=s.replace('            float approach=model.IsMelee&&pose.attackProgress>0?Mathf.Sin(pose.attackProgress*Mathf.PI)*520:0;\n            // A second offset moves the complete puppet for melee contact, including all clothes.','            // Impact effects remain visible against the light workshop background.')
s=s.replace('combat.Boss?QualityVisual.Colors[4]:Muted','combat.Boss?QualityVisual.Ink(Quality.Legendary):Muted')
s=s.replace('QualityVisual.Colors[(int)item.quality]', 'QualityVisual.Ink(item.quality)')
s=s.replace('new Color(.09f,.15f,.18f)','new Color(.985f,.985f,.96f)')
s=s.replace('new Color(.015f,.025f,.04f,.84f)','new Color(.12f,.18f,.21f,.48f)')
s=s.replace('THREAD ZERO · v0.4','THREAD ZERO · v0.5')
p.write_text(s,encoding='utf-8')
