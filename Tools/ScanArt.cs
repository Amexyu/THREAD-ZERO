using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
class ScanArt
{
 static void Main(string[] args)
 {
  var code=new StringBuilder("// Generated from alpha bounds by Tools/ScanArt.cs.\nnamespace ThreadZero { public static class SpriteAtlasLayout {\n");
  for(int a=0;a<2;a++)using(var bmp=new Bitmap(Path.Combine(args[0],a==0?"EquipmentAtlas.png":"BodyRigAtlas.png")))
  {
   int cols=a==0?5:3,rows=a==0?4:2;
   Console.WriteLine((a==0?"Equipment":"Rig")+" "+bmp.Width+"x"+bmp.Height+" corner alpha="+bmp.GetPixel(0,0).A);
   code.Append("public const int "+(a==0?"Equipment":"Rig")+"Width="+bmp.Width+", "+(a==0?"Equipment":"Rig")+"Height="+bmp.Height+";\npublic static readonly int[,] "+(a==0?"Equipment":"Rig")+" = {\n");
   for(int row=0;row<rows;row++)for(int col=0;col<cols;col++)
   {
    int x0=col*bmp.Width/cols,y0=row*bmp.Height/rows,x1=(col+1)*bmp.Width/cols,y1=(row+1)*bmp.Height/rows;
    int l=x1,t=y1,r=x0,b=y0,count=0,cw=x1-x0,ch=y1-y0;
    var solid=new bool[cw*ch];var seen=new bool[solid.Length];var queue=new int[solid.Length];
    for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)solid[(y-y0)*cw+x-x0]=bmp.GetPixel(x,y).A>180;
    var pieces=new System.Collections.Generic.List<int[]>();int largest=0;
    for(int q=0;q<solid.Length;q++)if(solid[q]&&!seen[q])
    {
     int tail=1,head=0,pl=x1,pt=y1,pr=x0,pb=y0;queue[0]=q;seen[q]=true;
     while(head<tail){int n=queue[head++],xx=n%cw,yy=n/cw;pl=Math.Min(pl,x0+xx);pt=Math.Min(pt,y0+yy);pr=Math.Max(pr,x0+xx);pb=Math.Max(pb,y0+yy);
      for(int ny=Math.Max(0,yy-1);ny<=Math.Min(ch-1,yy+1);ny++)for(int nx=Math.Max(0,xx-1);nx<=Math.Min(cw-1,xx+1);nx++){int next=ny*cw+nx;if(solid[next]&&!seen[next]){seen[next]=true;queue[tail++]=next;}}
     }
     largest=Math.Max(largest,tail);pieces.Add(new[]{tail,pl,pt,pr,pb});
    }
    // Sprite UV bounds exclude isolated generation debris, retaining detached major parts.
    foreach(var piece in pieces)if(piece[0]>=largest*.025f){count+=piece[0];l=Math.Min(l,piece[1]);t=Math.Min(t,piece[2]);r=Math.Max(r,piece[3]);b=Math.Max(b,piece[4]);}
    if(count<100)throw new Exception("Missing sprite "+row+","+col);
    code.Append("{"+l+","+t+","+(r-l+1)+","+(b-t+1)+"},\n");
   }
   code.Append("};\n");
   using(var preview=new Bitmap(bmp.Width,bmp.Height))using(var g=Graphics.FromImage(preview))
   {g.Clear(Color.FromArgb(237,240,238));g.DrawImageUnscaled(bmp,0,0);preview.Save(Path.Combine(args[1],a==0?"EquipmentAtlasReview.png":"BodyRigReview.png"),ImageFormat.Png);}
  }
  code.Append("} }\n");File.WriteAllText(args[2],code.ToString());
 }
}
