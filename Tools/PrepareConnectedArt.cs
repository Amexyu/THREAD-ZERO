using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using ThreadZero;

// Asset import/export only: whole frames, no limb cuts, pixel cleanup, or joint transforms.
class PrepareConnectedArt
{
 static readonly string[] poses={"idle","ranged_aim","ranged_fire","melee_ready","melee_windup","melee_strike"};
 static readonly string[] enemies={"patrol","salvager","jammer","patrol_commander","furnace_foreman","blackout_broadcaster"};
 static readonly Color light=Color.FromArgb(237,241,238),dark=Color.FromArgb(35,43,51);
 static int borderFailures;
 static Rectangle Bounds(Bitmap bmp,Rectangle region,out PointF feet)
 {
  int l=region.Right,t=region.Bottom,r=region.X,b=region.Y,count=0;
  for(int y=region.Y;y<region.Bottom;y++)for(int x=region.X;x<region.Right;x++)if(bmp.GetPixel(x,y).A>=180){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);b=Math.Max(b,y);count++;}
  if(count<500)throw new Exception("Empty frame");
  int fl=region.Right,fr=region.X;
  for(int y=Math.Max(t,b-30);y<=b;y++)for(int x=l;x<=r;x++)if(bmp.GetPixel(x,y).A>=180){fl=Math.Min(fl,x);fr=Math.Max(fr,x);}
  feet=new PointF((fl+fr)/2f,b+1);return Rectangle.FromLTRB(l,t,r+1,b+1);
 }
 static void Checker(Graphics g,Rectangle r,bool isDark=false)
 {
  Color a=isDark?dark:light,b=isDark?Color.FromArgb(43,52,61):Color.FromArgb(224,230,226);
  for(int y=r.Y;y<r.Bottom;y+=20)for(int x=r.X;x<r.Right;x+=20)using(var brush=new SolidBrush(((x-r.X)/20+(y-r.Y)/20)%2==0?a:b))g.FillRectangle(brush,x,y,20,20);
 }
 static void Text(Graphics g,string s,float x,float y,float size=16)
 {using(var f=new Font("Microsoft YaHei",size,FontStyle.Regular,GraphicsUnit.Pixel))using(var b=new SolidBrush(Color.FromArgb(26,42,51)))g.DrawString(s,f,b,x,y);}
 static Bitmap Export(Bitmap src,Rectangle cell,string target,float scale,PointF anchor,out Rectangle bounds,out PointF feet)
 {
  bounds=Bounds(src,cell,out feet);var result=new Bitmap(512,512,PixelFormat.Format32bppArgb);
  using(var g=Graphics.FromImage(result))
  {
   g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.CompositingMode=CompositingMode.SourceCopy;
   g.DrawImage(src,new RectangleF(anchor.X-(feet.X-cell.X)*scale,anchor.Y-(feet.Y-cell.Y)*scale,cell.Width*scale,cell.Height*scale),cell,GraphicsUnit.Pixel);
  }
  Directory.CreateDirectory(Path.GetDirectoryName(target));result.Save(target,ImageFormat.Png);
  for(int i=0;i<512;i++)if(result.GetPixel(i,0).A>180||result.GetPixel(i,511).A>180||result.GetPixel(0,i).A>180||result.GetPixel(511,i).A>180){borderFailures++;break;}
  return result;
 }
 static void Main(string[] args)
 {
  string pack=args[0],source=Path.Combine(pack,"SourceAtlases"),output=Path.Combine(pack,"Runtime"),review=Path.Combine(pack,"Review");
  Directory.CreateDirectory(review);var records=new StringBuilder("kind\tid\tpose\tpath\tsourceX\tsourceY\tsourceW\tsourceH\tfeetSourceX\tfeetSourceY\tscale\n");
  var frames=new Bitmap[5,6];
  for(int look=0;look<5;look++)using(var src=new Bitmap(Path.Combine(source,"Player_c"+look+".png")))
  {
   if(src.Width!=1536||src.Height!=1024)throw new Exception("Unexpected player atlas dimensions");
   using(var atlas=new Bitmap(1536,1024,PixelFormat.Format32bppArgb))using(var ag=Graphics.FromImage(atlas))
   {
    ag.Clear(Color.Transparent);
    for(int p=0;p<6;p++)
    {
     int row=p/3,col=p%3;
     // Lunge feet extend left of the nominal third cell in several source variants.
     // Sample whole characters through the actual empty gap, never sever the foot at x=1024.
     int sx=row==1&&col==2?960:col*512,sw=row==1&&col==1?448:row==1&&col==2?576:512;
     Rectangle cell=new Rectangle(sx,row==0?0:492,sw,row==0?492:532);Rectangle bounds;PointF feet;
     string relative="Players/c"+look+"/"+poses[p]+".png";
     frames[look,p]=Export(src,cell,Path.Combine(output,relative),.88f,new PointF(256,470),out bounds,out feet);
     ag.DrawImageUnscaled(frames[look,p],col*512,row*512);
     records.Append("player\tc"+look+"\t"+poses[p]+"\t"+relative+"\t"+bounds.X+"\t"+bounds.Y+"\t"+bounds.Width+"\t"+bounds.Height+"\t"+feet.X.ToString(CultureInfo.InvariantCulture)+"\t"+feet.Y+"\t0.88\n");
    }
    Directory.CreateDirectory(Path.Combine(output,"Atlases"));atlas.Save(Path.Combine(output,"Atlases/Player_c"+look+".png"),ImageFormat.Png);
   }
  }
  var enemyFrames=new Bitmap[6];
  using(var src=new Bitmap(Path.Combine(source,"Enemies.png")))
  {
   Rectangle[] cells={new Rectangle(0,0,480,452),new Rectangle(512,0,512,452),new Rectangle(1024,0,512,452),new Rectangle(0,452,434,572),new Rectangle(434,452,590,572),new Rectangle(1024,452,512,572)};
   for(int i=0;i<6;i++)
   {
    PointF feet;Rectangle bounds=Bounds(src,cells[i],out feet);float scale=Math.Min(412f/bounds.Width,416f/bounds.Height);
    string relative="Enemies/"+enemies[i]+".png";
    enemyFrames[i]=Export(src,cells[i],Path.Combine(output,relative),scale,new PointF(256,470),out bounds,out feet);
    records.Append("enemy\t"+enemies[i]+"\tidle\t"+relative+"\t"+bounds.X+"\t"+bounds.Y+"\t"+bounds.Width+"\t"+bounds.Height+"\t"+feet.X.ToString(CultureInfo.InvariantCulture)+"\t"+feet.Y+"\t"+scale.ToString(CultureInfo.InvariantCulture)+"\n");
   }
  }
  using(var icons=new Bitmap("ThreadZero/Assets/Resources/Art/EquipmentAtlas.png"))
  {
   string dir=Path.Combine(output,"Icons");Directory.CreateDirectory(dir);
   for(int i=0;i<20;i++)using(var icon=new Bitmap(128,128,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(icon))
   {
    var l=SpriteAtlasLayout.Equipment;float s=Math.Min(112f/l[i,2],112f/l[i,3]);g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;
    g.DrawImage(icons,new RectangleF(64-l[i,2]*s/2,64-l[i,3]*s/2,l[i,2]*s,l[i,3]*s),new Rectangle(l[i,0],l[i,1],l[i,2],l[i,3]),GraphicsUnit.Pixel);
    string id=new[]{"w","h","c","b"}[i/5]+i%5;icon.Save(Path.Combine(dir,id+".png"),ImageFormat.Png);
   }
  }
  File.WriteAllText(Path.Combine(pack,"measurements.tsv"),records.ToString(),new UTF8Encoding(false));
  using(var b=new Bitmap(1536,1420))using(var g=Graphics.FromImage(b))
  {
   g.Clear(light);Text(g,"五套完整角色帧 / 仅整帧切换，不旋转肢体或切衣服",20,8,24);
   for(int look=0;look<5;look++)for(int p=0;p<6;p++){int x=p*256,y=54+look*268;Checker(g,new Rectangle(x,y,256,256));g.DrawImage(frames[look,p],x,y,256,256);Text(g,"c"+look+" / "+poses[p],x+6,y+248,12);}
   b.Save(Path.Combine(review,"Players_AllFrames.png"),ImageFormat.Png);
  }
  using(var b=new Bitmap(1440,1050))using(var g=Graphics.FromImage(b))
  {
   g.Clear(light);Text(g,"敌人贴图 / 三种普通单位 + 三种首领 / 默认朝左",20,10,24);
   for(int i=0;i<6;i++){int x=i%3*480,y=50+i/3*492;Checker(g,new Rectangle(x,y,480,480));g.DrawImage(enemyFrames[i],x,y,480,480);Text(g,enemies[i],x+24,y+455,18);}
   b.Save(Path.Combine(review,"Enemies_All.png"),ImageFormat.Png);
  }
  using(var b=new Bitmap(1280,750))using(var g=Graphics.FromImage(b))
  {
   g.Clear(light);Text(g,"透明边缘检查 / 左浅底 · 右深底",20,8,22);
   Checker(g,new Rectangle(0,48,640,640));Checker(g,new Rectangle(640,48,640,640),true);
   for(int i=0;i<2;i++){g.DrawImage(frames[0,2],i*640,52,380,380);g.DrawImage(enemyFrames[4],i*640+235,280,380,380);}
   b.Save(Path.Combine(review,"Alpha_LightDark.png"),ImageFormat.Png);
  }
  int[] sequence={0,0,0,0,1,1,2,1,1,0,0,0,3,3,4,4,4,5,5,3,3,0,0,0};
  string gifFrames=Path.Combine(review,"gif_frames");Directory.CreateDirectory(gifFrames);
  for(int f=0;f<sequence.Length;f++)using(var b=new Bitmap(1280,330))using(var g=Graphics.FromImage(b))
  {
   g.Clear(light);for(int look=0;look<5;look++){g.DrawImage(frames[look,sequence[f]],look*256,20,256,256);Text(g,"c"+look,look*256+112,277,18);}
   Text(g,"整帧切换预览 / 非 Unity 运行录屏",20,307,13);b.Save(Path.Combine(gifFrames,f.ToString("D3")+".gif"),ImageFormat.Gif);
  }
  foreach(var f in frames)f.Dispose();foreach(var e in enemyFrames)e.Dispose();
  if(borderFailures>0)throw new Exception("Opaque frame edge clipping: "+borderFailures);
  Console.WriteLine("Exported 30 connected player frames + 6 enemy sprites + 20 icons; normalized feet=(256,470); opaque edge clipping=0");
 }
}
