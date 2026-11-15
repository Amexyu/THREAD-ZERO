using System;
using System.Drawing;
using System.Drawing.Imaging;

public static class ReviewSkeletalV08
{
    struct V { public double x,y,u,v; public V(double x,double y,double u,double v){this.x=x;this.y=y;this.u=u;this.v=v;} }
    static readonly double[] X={256,269,211,191,291,313,232,222,281,289,220,300};
    static readonly double[] Y={258,185,225,286,224,287,338,400,338,400,448,448};
    static readonly int[] P={-1,0,0,2,0,4,0,6,0,8,7,9};
    static double Clamp(double v){return Math.Max(0,Math.Min(1,v));}
    static double Smooth(double v){v=Clamp(v);return v*v*(3-2*v);}
    static void Weight(double x,double y,out int a,out int b,out double blend)
    {
        a=b=0;blend=0;
        if(y<205&&x>175&&x<350){a=1;b=0;blend=Smooth((y-170)/45);}
        else if(y>325&&x>155&&x<350){bool rear=x<255;a=rear?6:8;
            b=y>440?(rear?10:11):y>395?(rear?7:9):0;
            if(y>440){a=rear?7:9;blend=Smooth((y-425)/35);}
            else blend=y>395?Smooth((y-380)/40):1-Smooth((y-325)/35);}
        else if(y>205&&y<355&&(x<227||x>295)){bool rear=x<227;a=rear?2:4;b=y>275?(rear?3:5):0;
            blend=y>275?Smooth((y-260)/45):1-Smooth((y-205)/40);}
    }
    static V Point(double x,double y,double[] angles,double[] px,double[] py,double[] worldAngle)
    {
        int a,b;double blend;Weight(x,y,out a,out b,out blend);
        double ax,ay,bx,by;Warp(x,y,a,px,py,worldAngle,out ax,out ay);Warp(x,y,b,px,py,worldAngle,out bx,out by);
        return new V(ax*(1-blend)+bx*blend,ay*(1-blend)+by*blend,x,y);
    }
    static void Warp(double x,double y,int i,double[] px,double[] py,double[] angle,out double ox,out double oy)
    {
        double dx=x-X[i],dy=y-Y[i],c=Math.Cos(angle[i]),s=Math.Sin(angle[i]);
        ox=px[i]+dx*c+dy*s;oy=py[i]-dx*s+dy*c;
    }
    static void Triangle(V a,V b,V c,Bitmap source,Bitmap output,int offsetX,int offsetY)
    {
        double den=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Math.Abs(den)<.0001)return;
        int left=Math.Max(0,(int)Math.Floor(Math.Min(a.x,Math.Min(b.x,c.x))));
        int right=Math.Min(511,(int)Math.Ceiling(Math.Max(a.x,Math.Max(b.x,c.x))));
        int top=Math.Max(0,(int)Math.Floor(Math.Min(a.y,Math.Min(b.y,c.y))));
        int bottom=Math.Min(511,(int)Math.Ceiling(Math.Max(a.y,Math.Max(b.y,c.y))));
        for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++)
        {
            double p=((b.y-c.y)*(x-c.x)+(c.x-b.x)*(y-c.y))/den;
            double q=((c.y-a.y)*(x-c.x)+(a.x-c.x)*(y-c.y))/den;
            double r=1-p-q;if(p<-.001||q<-.001||r<-.001)continue;
            int sx=Math.Max(0,Math.Min(511,(int)Math.Round(a.u*p+b.u*q+c.u*r)));
            int sy=Math.Max(0,Math.Min(511,(int)Math.Round(a.v*p+b.v*q+c.v*r)));
            Color color=source.GetPixel(sx,sy);if(color.A<=8)continue;
            Color dst=output.GetPixel(offsetX+x,offsetY+y);double alpha=color.A/255.0;
            output.SetPixel(offsetX+x,offsetY+y,Color.FromArgb(255,
                (int)Math.Round(color.R*alpha+dst.R*(1-alpha)),
                (int)Math.Round(color.G*alpha+dst.G*(1-alpha)),
                (int)Math.Round(color.B*alpha+dst.B*(1-alpha))));
        }
    }
    public static void Run(string sourcePath,string headPath,string outputPath)
    {
        using(var source=new Bitmap(sourcePath))using(var head=new Bitmap(headPath))
        using(var output=new Bitmap(2048,1024,PixelFormat.Format32bppArgb))
        {
            using(var g=Graphics.FromImage(output))g.Clear(Color.FromArgb(255,224,227,224));
            for(int layer=0;layer<2;layer++)for(int frame=0;frame<8;frame++)
            {
                double phase=frame*Math.PI/4,swing=Math.Sin(phase),lift=Math.Max(0,Math.Cos(phase));
                double torso=swing*1.5;
                double[] angles={torso,-swing*2,-swing*12,-lift*5,swing*12,-(1-lift)*5,
                    swing*16,-Math.Max(0,swing)*14,-swing*16,-Math.Max(0,-swing)*14,
                    -(torso+swing*16-Math.Max(0,swing)*14),
                    -(torso-swing*16-Math.Max(0,-swing)*14)};
                var px=new double[12];var py=new double[12];var worldAngle=new double[12];
                for(int i=0;i<12;i++)
                {
                    if(P[i]<0){px[i]=X[i];py[i]=Y[i];worldAngle[i]=angles[i]*Math.PI/180;}
                    else{Warp(X[i],Y[i],P[i],px,py,worldAngle,out px[i],out py[i]);worldAngle[i]=worldAngle[P[i]]+angles[i]*Math.PI/180;}
                }
                for(int row=0;row<32;row++)for(int col=0;col<32;col++)
                {
                    if(layer==0&&(col<9||col>=22||row<1||row>=31))continue;
                    if(layer==1&&(col<10||col>=22||row>=14))continue;
                    V a=Point(col*16,row*16,angles,px,py,worldAngle);
                    V b=Point((col+1)*16,row*16,angles,px,py,worldAngle);
                    V c=Point(col*16,(row+1)*16,angles,px,py,worldAngle);
                    V d=Point((col+1)*16,(row+1)*16,angles,px,py,worldAngle);
                    int ox=frame%4*512,oy=frame/4*512;
                    Triangle(a,c,b,layer==0?source:head,output,ox,oy);
                    Triangle(b,c,d,layer==0?source:head,output,ox,oy);
                }
            }
            output.Save(outputPath,ImageFormat.Png);
        }
    }
}
