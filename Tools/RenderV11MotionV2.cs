using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class RenderV11MotionV2
{
    static void Text(Graphics g,string s,float x,float y,int size,Color c){using(var font=new Font("Microsoft YaHei",size,FontStyle.Bold))using(var b=new SolidBrush(c))g.DrawString(s,font,b,x,y);}
    static Rectangle Bounds(Bitmap im,Rectangle region){int l=region.Right,t=region.Bottom,r=region.Left,b=region.Top;for(int y=region.Top;y<region.Bottom;y++)for(int x=region.Left;x<region.Right;x++)if(im.GetPixel(x,y).A>32){l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y);}return Rectangle.FromLTRB(l,t,r+1,b+1);}
    public static void Run(string root) { Render(root, false); }
    public static void RunV3(string root) { Render(root, true); }
    static void Render(string root, bool v3)
    {
        string review=Path.Combine(root,"Deliverables/ArtPack_v11/Review");string folder=Path.Combine(review,v3?"MotionFramesV3":"MotionFramesV2");Directory.CreateDirectory(folder);
        using(var idle=new Bitmap(Path.Combine(root,"Artifacts/QDoll/v11sample.png")))
        using(var keys=new Bitmap(Path.Combine(review,"AttackKeys_v2.png")))
        using(var melee=new Bitmap(Path.Combine(review,v3?"MeleeKeys_v3.png":"AttackKeys_v2.png")))
        {
            var pose=new Rectangle[5];pose[0]=Bounds(idle,new Rectangle(0,0,idle.Width,idle.Height));
            for(int i=0;i<4;i++)pose[i+1]=Bounds(keys,new Rectangle(i%2*keys.Width/2,i/2*keys.Height/2,keys.Width/2,keys.Height/2));
            var meleePose=new Rectangle[4];
            for(int i=0;i<4;i++)meleePose[i]=Bounds(melee,new Rectangle(i%2*melee.Width/2,i/2*melee.Height/2,melee.Width/2,melee.Height/2));
            string[] modes={"待机","移动","射击","近战","受击"};
            const int frames=40;
            for(int f=0;f<frames*5;f++)
            {
                int mode=f/frames,k=f%frames,which=0,mi=-1;double t=k/(double)frames,p=t*2%1;
                float dx=0,dy=0,rot=0,sx=1,sy=1,flash=0,slash=0,hit=0;
                string stage="呼吸待机";
                if(mode==0){sy=1+(float)Math.Sin(t*Math.PI*2)*.006f;rot=(float)Math.Sin(t*Math.PI*2)*.4f;}
                if(mode==1){double a=t*4%1;float hop=(float)Math.Sin(a*Math.PI);dy=-hop*8;dx=(float)Math.Sin(a*Math.PI*2)*3;rot=2.5f+(float)Math.Sin(a*Math.PI*2)*1.7f;sx=1-.015f*hop;sy=1+.02f*hop;if(a<.16){float land=1-(float)(a/.16);sx+=.035f*land;sy-=.045f*land;}stage="前倾 · 小跳 · 落地回弹";}
                if(mode==2){if(p<.15){which=0;rot=-2*(float)(p/.15);stage="抬臂准备";}else if(p<.35){which=1;stage="抬手瞄准";}else if(p<.48){which=2;float q=(float)((p-.35)/.13);flash=1-q;dx=-11*(1-q);rot=-2*(1-q);stage="开火后坐";}else if(p<.75){which=1;stage="复位瞄准";}else{which=0;stage="收手";}}
                if(mode==3){if(p<.15){which=0;stage="准备";}else if(p<.38){which=3;float q=(float)((p-.15)/.23);dx=-10*q;rot=-3*q;stage="抬臂举刀蓄力";}else if(p<.60){which=4;float q=(float)((p-.38)/.22);dx=18+25*(float)Math.Sin(q*Math.PI);rot=4;slash=(float)Math.Sin(q*Math.PI);stage="挥砍前冲";}else if(p<.82){which=4;float q=(float)((p-.60)/.22);dx=18*(1-q);rot=4*(1-q);stage="收招";}else{which=0;stage="回到待机";}}
                if(mode==4){hit=(float)Math.Exp(-p*9);dx=-hit*12+(float)Math.Sin(p*55)*hit*3;rot=-hit*4;sy=1-hit*.025f;sx=1+hit*.02f;stage="受击 · 后震 · 恢复";}
                if(mode==3&&v3){dx=dy=rot=0;slash=0;if(p<.16){mi=0;stage="正手握刀准备";}else if(p<.42){mi=1;stage="肩带上臂 · 屈肘举刀";}else if(p<.54){mi=2;stage="肩前摆 · 肘伸展";}else if(p<.82){mi=3;stage="刀尖落向前下方";}else{mi=0;stage="收回准备位";}}
                using(var frame=new Bitmap(700,500,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(frame))
                {
                    g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.Clear(Color.FromArgb(243,243,239));
                    using(var b=new SolidBrush(Color.FromArgb(231,233,228)))for(int i=0;i<8;i++)g.FillRectangle(b,i*120-(mode==1?(float)(t*280)%120:0),235+i%3*30,86,216);
                    using(var pen=new Pen(Color.FromArgb(28,31,34),3))g.DrawLine(pen,0,450,700,450);
                    for(int i=0;i<12;i++)using(var b=new SolidBrush(Color.FromArgb(255,99,34)))g.FillRectangle(b,i*64,452,31,5);
                    Text(g,"THREAD ZERO  /  动作修订 "+(v3?"v3":"v2"),20,14,15,Color.FromArgb(28,31,34));Text(g,modes[mode],22,56,25,Color.FromArgb(28,31,34));Text(g,stage,22,98,10,Color.FromArgb(85,95,98));
                    using(var b=new SolidBrush(Color.FromArgb(30,28,31,34)))g.FillEllipse(b,300+dx,441,160,12);
                    if(mode==1)for(int i=0;i<4;i++){float dust=(float)((t*4+i*.23)%1);using(var b=new SolidBrush(Color.FromArgb((int)((1-dust)*65),128,135,134)))g.FillEllipse(b,318-dust*70,443-dust*10,4+dust*5,3+dust*2);}
                    Rectangle src=pose[which];Bitmap art=which==0?idle:keys;
                    float height=which==3?425:390,width=src.Width*height/src.Height;
                    float left=-width/2;
                    if(mi>=0){src=meleePose[mi];art=melee;height=src.Height*.64f;width=src.Width*.64f;left=(src.X-mi%2*melee.Width/2-melee.Width/4)*.64f;}
                    var state=g.Save();g.TranslateTransform(378+dx,449+dy);g.RotateTransform(rot);g.ScaleTransform(sx,sy);
                    g.DrawImage(art,new RectangleF(left,-height,width,height),src,GraphicsUnit.Pixel);
                    if(hit>.08){using(var attrs=new ImageAttributes()){attrs.SetColorMatrix(new ColorMatrix(new float[][]{new float[]{1,0,0,0,0},new float[]{0,.5f,0,0,0},new float[]{0,0,.5f,0,0},new float[]{0,0,0,hit*.5f,0},new float[]{.25f,0,0,0,1}}));g.DrawImage(art,new Rectangle((int)(-width/2),(int)-height,(int)width,(int)height),src.X,src.Y,src.Width,src.Height,GraphicsUnit.Pixel,attrs);}}
                    if(flash>0){float mx=-width/2+(1105-src.X)*height/src.Height,my=-height+(149-src.Y)*height/src.Height;var fs=g.Save();g.TranslateTransform(mx,my);g.RotateTransform(-17);using(var b=new SolidBrush(Color.FromArgb(255,201,74)))g.FillPolygon(b,new PointF[]{new PointF(0,-4),new PointF(20+flash*30,-14),new PointF(24,0),new PointF(20+flash*25,12),new PointF(0,4)});using(var pen=new Pen(Color.FromArgb(255,181,53),2))g.DrawLine(pen,45,0,120,0);g.Restore(fs);}
                    g.Restore(state);
                    if(slash>0)using(var pen=new Pen(Color.FromArgb((int)(slash*215),46,201,203),7))g.DrawArc(pen,403+dx,197,184,186,-65,145);
                    Text(g,"待机 / 移动 / 射击 / 近战 / 受击",20,473,11,Color.FromArgb(90,99,101));
                    frame.Save(Path.Combine(folder,f.ToString("D3")+".gif"),ImageFormat.Gif);if(k==18)frame.Save(Path.Combine(review,(v3?"V3_Mode_":"V2_Mode_")+mode+".png"),ImageFormat.Png);
                }
            }
        }
    }
}
