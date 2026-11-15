using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class RenderV11MotionReview
{
    static readonly PointF[] GunOutline={new PointF(865,708),new PointF(935,708),new PointF(1030,815),new PointF(1030,920),new PointF(868,920),new PointF(868,824),new PointF(839,774)};
    static GraphicsPath GunMask(){var p=new GraphicsPath();p.AddPolygon(GunOutline);return p;}
    static void Label(Graphics g,string text,float x,float y,int size,Color color)
    {using(var font=new Font("Microsoft YaHei",size,FontStyle.Bold))using(var b=new SolidBrush(color))g.DrawString(text,font,b,x,y);}
    static void Line(Graphics g,Color c,float width,float x,float y,float xx,float yy)
    {using(var p=new Pen(c,width)){p.StartCap=p.EndCap=LineCap.Round;g.DrawLine(p,x,y,xx,yy);}}
    public static void Run(string root)
    {
        string folder=Path.Combine(root,"Deliverables/ArtPack_v11/Review/MotionFrames");Directory.CreateDirectory(folder);
        using(var source=new Bitmap(Path.Combine(root,"Artifacts/QDoll/v11sample.png")))
        using(var blade=new Bitmap(Path.Combine(root,"Deliverables/ArtPack_v08/Source/Weapon_w1.png")))
        using(var body=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb))
        using(var gun=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb))
        {
            int minx=blade.Width,miny=blade.Height,maxx=0,maxy=0;
            for(int yy=0;yy<blade.Height;yy++)for(int xx=0;xx<blade.Width;xx++)if(blade.GetPixel(xx,yy).A>16){minx=Math.Min(minx,xx);miny=Math.Min(miny,yy);maxx=Math.Max(maxx,xx);maxy=Math.Max(maxy,yy);}
            var bladeRect=new Rectangle(minx,miny,maxx-minx+1,maxy-miny+1);
            // Temporary extraction for approval video; the production pack will use proper layers.
            using(var path=GunMask())
            {
                using(var g=Graphics.FromImage(body)){g.DrawImageUnscaled(source,0,0);g.CompositingMode=CompositingMode.SourceCopy;using(var clear=new SolidBrush(Color.Transparent))g.FillPath(clear,path);}
                using(var g=Graphics.FromImage(gun)){g.SetClip(path);g.DrawImageUnscaled(source,0,0);}
            }
            string[] names={"待机","移动","射击","近战","受击"};
            const int per=30;
            for(int f=0;f<per*5;f++)
            {
                int mode=f/per,k=f%per;double t=k/(double)per;
                float dx=0,dy=0,angle=0,sx=1,sy=1,wa=0;
                float shot=0,slash=0,hurt=0;
                if(mode==0){sy=1+(float)Math.Sin(t*Math.PI*2)*.007f;}
                if(mode==1){double p=t*3%1;float hop=(float)Math.Sin(p*Math.PI);dy=-hop*15;sx=1-.025f*hop;sy=1+.035f*hop;if(p<.13){sx=1.035f;sy=.96f;}}
                if(mode==2){double p=t*2%1;if(p<.25)wa=(float)(-38*p/.25);else if(p<.45){wa=-38;shot=(float)(1-(p-.25)/.2);dx=-shot*10;angle=-shot*2;}else wa=(float)(-38*(1-(p-.45)/.55));}
                if(mode==3){double p=t*2%1;if(p<.3){dx=(float)(-12*p/.3);angle=(float)(-4*p/.3);wa=(float)(-70*p/.3);}else if(p<.55){float q=(float)((p-.3)/.25);dx=-12+q*67;angle=-4+q*11;wa=-70+q*125;slash=(float)Math.Sin(q*Math.PI);}else{float q=(float)((p-.55)/.45);dx=55*(1-q);angle=7*(1-q);wa=55*(1-q);}}
                if(mode==4){double p=t*2%1;hurt=(float)Math.Exp(-p*8);dx=-hurt*13+(float)Math.Sin(p*55)*hurt*4;angle=-hurt*5;sx=1+hurt*.025f;sy=1-hurt*.025f;}
                using(var frame=new Bitmap(700,500,PixelFormat.Format32bppArgb))
                using(var g=Graphics.FromImage(frame))
                {
                    g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    g.Clear(Color.FromArgb(243,243,239));
                    using(var bg=new SolidBrush(Color.FromArgb(231,233,228)))for(int i=0;i<7;i++){float x=i*130-(mode==1?(float)(t*180)%130:0);g.FillRectangle(bg,x,210+i%3*45,92,238);}
                    Line(g,Color.FromArgb(28,31,34),3,0,449,700,449);
                    for(int i=0;i<12;i++)using(var b=new SolidBrush(Color.FromArgb(255,99,34)))g.FillRectangle(b,i*64,451,31,5);
                    Label(g,"THREAD ZERO  /  v0.11 动效样稿",22,14,15,Color.FromArgb(28,31,34));
                    Label(g,names[mode],24,56,25,Color.FromArgb(28,31,34));
                    Label(g,"Q 版单站姿 · 整体动效",24,96,11,Color.FromArgb(99,108,111));
                    using(var shadow=new SolidBrush(Color.FromArgb(35,28,31,34)))g.FillEllipse(shadow,300+dx,439,165,12);
                    if(mode==1)for(int i=0;i<5;i++){float q=(float)((t*3+i*.18)%1);using(var b=new SolidBrush(Color.FromArgb((int)((1-q)*70),120,131,132)))g.FillEllipse(b,318-q*72,439-q*13,4+q*5,3+q*3);}
                    var save=g.Save();
                    g.TranslateTransform(376+dx,449+dy);g.RotateTransform(angle);g.ScaleTransform(.332f*sx,.332f*sy);g.TranslateTransform(-630,-1233);
                    g.DrawImageUnscaled(body,0,0);
                    if(hurt>.15f){using(var attrs=new ImageAttributes()){var m=new ColorMatrix(new float[][]{new float[]{1,0,0,0,0},new float[]{0,.45f,0,0,0},new float[]{0,0,.45f,0,0},new float[]{0,0,0,.45f*hurt,0},new float[]{.35f,0,0,0,1}});attrs.SetColorMatrix(m);g.DrawImage(body,new Rectangle(0,0,1254,1254),0,0,1254,1254,GraphicsUnit.Pixel,attrs);}}
                    var weapon=g.Save();g.TranslateTransform(853,789);g.RotateTransform(wa);
                    if(mode==3){g.DrawImage(blade,new Rectangle(-38,-52,380,145),bladeRect,GraphicsUnit.Pixel);}
                    else{g.TranslateTransform(-853,-789);g.DrawImageUnscaled(gun,0,0);if(shot>0){float mx=978,my=855;using(var b=new SolidBrush(Color.FromArgb(255,205,80)))g.FillPolygon(b,new PointF[]{new PointF(mx,my),new PointF(mx+shot*95,my-12),new PointF(mx+shot*56,my+20),new PointF(mx+shot*60,my+45),new PointF(mx+5,my+20)});}}
                    g.Restore(weapon);
                    // Keep the original fingers in front of the weapon's grip.
                    using(var handClip=new GraphicsPath()){handClip.AddPolygon(new PointF[]{new PointF(804,751),new PointF(849,750),new PointF(869,783),new PointF(862,815),new PointF(815,829),new PointF(795,797)});var hs=g.Save();g.SetClip(handClip,CombineMode.Intersect);g.DrawImageUnscaled(source,0,0);g.Restore(hs);}
                    if(slash>0){using(var p=new Pen(Color.FromArgb((int)(slash*210),46,201,203),14)){g.DrawArc(p,800,650,430,380,-65,135);}}
                    g.Restore(save);
                    if(shot>0)Line(g,Color.FromArgb(255,171,51),3,485+dx,307+dy,550+shot*75,300+dy);
                    Label(g,"待机  /  移动  /  射击  /  近战  /  受击",22,472,11,Color.FromArgb(90,99,101));
                    frame.Save(Path.Combine(folder,f.ToString("D3")+".gif"),ImageFormat.Gif);
                    if(k==10)frame.Save(Path.Combine(folder,"../Mode_"+mode+".png"),ImageFormat.Png);
                }
            }
        }
    }
}
