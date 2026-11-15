using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class UiReviewRenderer
{
    private static Graphics g;
    private static readonly Color Bg=Color.FromArgb(10,13,19),Surface=Color.FromArgb(17,22,29),Raised=Color.FromArgb(24,31,40),
        Text=Color.FromArgb(224,235,242),Muted=Color.FromArgb(122,148,168),Cyan=Color.FromArgb(77,214,214),Danger=Color.FromArgb(242,125,102);
    private static void Box(float x,float y,float w,float h,Color color){using(var b=new SolidBrush(color))g.FillRectangle(b,x,y,w,h);}
    private static void Label(float x,float y,float w,float h,string text,float size,Color color)
    {using(var font=new Font("Microsoft YaHei",size,FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(color))g.DrawString(text,font,brush,new RectangleF(x,y,w,h));}
    private static void Button(float x,float y,float w,float h,string text,bool active=false)
    {
        Box(x,y,w,h,active?Color.FromArgb(33,61,71):Raised);if(active)Box(x,y+h-2,w,2,Cyan);
        using(var font=new Font("Microsoft YaHei",16,FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(active?Cyan:Text))
        using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString(text,font,brush,new RectangleF(x,y,w,h),format);
    }
    public static void Main(string[] args)
    {
        using(var texture=new Bitmap(args[0]))using(var canvas=new Bitmap(1600,900))using(g=Graphics.FromImage(canvas))
        {
            g.Clear(Bg);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            Label(64,28,360,40,"零织协议",27,Text);Label(66,72,550,25,"行动 / 自动回收",14,Muted);
            Button(1134,34,246,42,"雨巷边界  ▾");Button(1400,34,136,42,"设置");
            Label(200,126,300,26,"回收员",16,Muted);Box(200,162,320,5,Cyan);Label(200,174,320,24,"186 / 186",13,Muted);
            Label(1000,126,340,28,"失控巡逻机",18,Muted);Box(1000,162,340,5,Danger);Label(1000,174,340,24,"60 / 60",13,Muted);
            Label(710,135,230,30,"第 1 / 5 波",18,Muted);
            for(int i=0;i<15;i++){float height=75+i*47%190;Box(64+i*100,610-height,76,height,Color.FromArgb(14,20,26));if(i%3==0)Box(76+i*100,631-height,4,13,Color.FromArgb(28,51,56));}
            Box(100,651,1400,2,Color.FromArgb(38,59,69));
            var pose=g.Save();float scale=350f/256;g.TranslateTransform(175,218+(440-320*scale));g.ScaleTransform(scale,scale);DollPreviewRenderer.DrawDoll(g,texture);g.Restore(pose);
            var drone=ThreadZero.PaperDollLayout.Parts[5];g.DrawImage(texture,new RectangleF(585,322,86,36),new RectangleF(drone.x,drone.y,drone.width,drone.height),GraphicsUnit.Pixel);
            // The same placeholder enemy silhouette as the live ActionPage.
            float x=1165,y=528,s=68;
            using(var pen=new Pen(Raised,18)){g.DrawLine(pen,x-25,y+20,x-52,y+112);g.DrawLine(pen,x+25,y+20,x+52,y+112);}
            Box(x-s,y-s,s*2,s*1.5f,Color.FromArgb(6,10,14));Box(x-s+6,y-s+6,s*2-12,s*1.5f-12,Color.FromArgb(46,61,74));
            Box(x-s+17,y-s+26,s*2-34,16,Danger);using(var dark=new SolidBrush(Color.FromArgb(9,15,20)))g.FillEllipse(dark,x-21,y+7-21,42,42);using(var light=new SolidBrush(Cyan))g.FillEllipse(light,x-10,y+7-10,20,20);
            Label(656,682,320,28,"正在自动回收",16,Muted);Button(637,729,154,42,"暂停");Button(809,729,154,42,"速度 1×");
            Box(64,810,1472,1,Color.FromArgb(33,46,56));Button(509,830,186,48,"行动",true);Button(707,830,186,48,"整备");Button(905,830,186,48,"衣柜");
            Label(66,863,400,25,"离屏布局预览 · 非Unity实机截图",11,Muted);
            canvas.Save(args[1],ImageFormat.Png);Console.WriteLine("UI_REVIEW_OK "+args[1]);
        }
    }
}
