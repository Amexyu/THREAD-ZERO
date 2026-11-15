using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using ThreadZero;

public static class DollPreviewRenderer
{
    private static Bitmap atlas;
    private static void Part(Graphics g,int index,float offset=0)
    {
        DollPart p=PaperDollLayout.Parts[index];
        g.DrawImage(atlas,new RectangleF(p.left+offset,p.top,p.drawWidth,p.drawHeight),new RectangleF(p.x,p.y,p.width,p.height),GraphicsUnit.Pixel);
    }
    public static void DrawDoll(Graphics graphics,Bitmap texture)
    {
        Bitmap previous=atlas;atlas=texture;
        try { Part(graphics,3);Part(graphics,0);Part(graphics,1);Part(graphics,2);Part(graphics,6);Part(graphics,4);Part(graphics,7); }
        finally { atlas=previous; }
    }
    public static void Main(string[] args)
    {
        if(args.Length!=2) throw new ArgumentException("atlas and preview path required");
        using(atlas=new Bitmap(args[0]))
        using(var result=new Bitmap(1440,660))
        using(var g=Graphics.FromImage(result))
        using(var title=new Font("Microsoft YaHei",21,FontStyle.Bold))
        using(var label=new Font("Microsoft YaHei",12,FontStyle.Bold))
        using(var note=new Font("Microsoft YaHei",10))
        using(var white=new SolidBrush(Color.FromArgb(222,232,239)))
        using(var muted=new SolidBrush(Color.FromArgb(150,170,185)))
        using(var cyan=new SolidBrush(Color.FromArgb(97,216,217)))
        {
            g.Clear(Color.FromArgb(16,22,30));g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.DrawString("THREAD ZERO / 分层与动作离屏检查",title,white,24,20);
            g.DrawString("真实图集 + 源码布局 / 软件离屏渲染 / 非Unity实机截图 / 不改素材原图",note,muted,26,67);
            string[] names={"素体","穿戴叠合","攻击姿态","受击姿态","维修姿态"};
            string[] hints={"素体头、手、裤腿位置","背包 → 素体 → 外套 → 兜帽 → 武器","绿色十字是武器握持枢轴","位置扰动；染色留待Unity验证","压低重心；结束后恢复原姿势"};
            for(int state=0;state<5;state++)
            {
                float left=24+state*280;
                using(var card=new SolidBrush(Color.FromArgb(25,35,47)))g.FillRectangle(card,left,112,264,450);
                g.DrawString(names[state],label,cyan,left+12,126);
                for(int yy=0;yy<340;yy+=20)for(int xx=0;xx<256;xx+=20)
                    using(var square=new SolidBrush((xx/20+yy/20)%2==0?Color.FromArgb(38,50,61):Color.FromArgb(43,55,67)))
                        g.FillRectangle(square,left+4+xx,162+yy,Math.Min(20,256-xx),20);
                GraphicsState saved=g.Save();g.TranslateTransform(left+4,172);
                if(state==4){g.TranslateTransform(128,310);g.ScaleTransform(1,.75f);g.TranslateTransform(-128,-310);}
                if(state==3)g.TranslateTransform(-2,0);
                if(state!=0)Part(g,3);Part(g,0);
                if(state!=0)
                {
                    Part(g,1);Part(g,2);Part(g,6);
                    GraphicsState weapon=g.Save();
                    if(state==2){g.TranslateTransform(PaperDollLayout.GripX,PaperDollLayout.GripY);g.RotateTransform(-9);g.TranslateTransform(-PaperDollLayout.GripX,-PaperDollLayout.GripY);}
                    Part(g,4);g.Restore(weapon);Part(g,7);
                }
                g.Restore(saved);
                using(var line=new Pen(Color.FromArgb(120,150,160)))g.DrawLine(line,left+4,482,left+260,482);
                if(state==1||state==2)using(var grip=new Pen(Color.FromArgb(195,235,110)))
                {float x=left+4+PaperDollLayout.GripX,y=172+PaperDollLayout.GripY;g.DrawLine(grip,x-4,y,x+4,y);g.DrawLine(grip,x,y-4,x,y+4);}
                g.DrawString(hints[state],note,muted,new RectangleF(left+10,515,244,45));
            }
            g.DrawString("检查：袖口与手 / 兜帽露出脸 / 枪握把贴手 / 背包遮挡 / 动作边界",note,white,26,582);
            g.DrawString("此图验证静态叠合和变换范围，不代替Unity中的交互、时间同步与最终动画验收。",note,muted,26,611);
            result.Save(args[1],ImageFormat.Png);
            Console.WriteLine("OFFSCREEN_PREVIEW_OK "+args[1]);
        }
    }
}
