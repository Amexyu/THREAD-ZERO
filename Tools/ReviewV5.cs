using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using ThreadZero;
class ReviewV5
{
 static Bitmap equipment,rig;
 static Color bg=Color.FromArgb(241,241,235),ink=Color.FromArgb(26,43,51),blue=Color.FromArgb(41,79,217),orange=Color.FromArgb(224,71,36);
 static void Box(Graphics g,float x,float y,float w,float h,Color c){using(var b=new SolidBrush(c))g.FillRectangle(b,x,y,w,h);}
 static void Text(Graphics g,string s,float x,float y,float size,Color c){using(var f=new Font("Microsoft YaHei",size,FontStyle.Regular,GraphicsUnit.Pixel))using(var b=new SolidBrush(c))g.DrawString(s,f,b,x,y);}
 public static void Doll(Graphics g,float x,float y,float scale,DollPose pose,int style=0)
 {
  var outer=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(scale,scale);
  foreach(var s in PuppetGeometry.Build(style,style,style,style,pose))
  {
   var t=g.Save();g.TranslateTransform(s.px,s.py);g.RotateTransform(s.angle);g.TranslateTransform(-s.px,-s.py);
   var l=s.atlas==0?SpriteAtlasLayout.Equipment:SpriteAtlasLayout.Rig;
   g.DrawImage(s.atlas==0?equipment:rig,new RectangleF(s.x,s.y,s.w,s.h),new RectangleF(l[s.index,0]+l[s.index,2]*s.u0,l[s.index,1],l[s.index,2]*(s.u1-s.u0),l[s.index,3]),GraphicsUnit.Pixel);g.Restore(t);
  }
  if(pose.slash>0)using(var pen=new Pen(Color.FromArgb((int)(pose.slash*220),41,120,240),5))g.DrawArc(pen,104,54,212,212,-80,143);
  if(pose.muzzle>0){float mx,my;PuppetGeometry.Muzzle(pose,out mx,out my);Box(g,mx,my-3,22,6,orange);}
  g.Restore(outer);
 }
 static void Main(string[] args)
 {
  equipment=new Bitmap(Path.Combine(args[0],"EquipmentAtlas.png"));rig=new Bitmap(Path.Combine(args[0],"BodyRigAtlas.png"));
  using(var bmp=new Bitmap(1600,900))using(var g=Graphics.FromImage(bmp))
  {
   g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(bg);
   Box(g,0,0,18,900,blue);Box(g,18,0,6,900,orange);
   Box(g,64,28,50,50,blue);Text(g,"TZ",72,32,27,Color.White);Text(g,"零织协议",132,23,29,ink);Text(g,"THREAD ZERO / 工坊终端",135,70,11,ink);
   Text(g,"行动 / 自动回收",500,48,15,ink);Text(g,"旧城外围   ▾",1180,46,16,ink);Text(g,"设置",1440,46,16,ink);
   Box(g,64,105,1472,1,Color.FromArgb(180,195,195));
   Text(g,"回收员",200,126,16,ink);Box(g,200,162,320,5,blue);Text(g,"186 / 186",200,175,13,ink);
   Text(g,"第 1 / 5 波",710,135,18,ink);Text(g,"失控巡逻机",1000,126,18,ink);Box(g,1000,162,340,5,orange);Text(g,"60 / 60",1000,175,13,ink);
   Box(g,100,240,1400,410,Color.FromArgb(232,236,230));
   for(int i=0;i<6;i++){int x=128+i*238;Box(g,x,270,208,310,Color.FromArgb(244,245,240));Box(g,x,270,208,3,Color.FromArgb(196,209,204));Box(g,x,270,3,310,Color.FromArgb(207,217,212));Box(g,x+18,294,36,6,i%2==0?blue:ink);}
   Text(g,"RECOVERY / 01",159,288,35,Color.FromArgb(165,183,178));Text(g,"RECLAIM · REFIT · REPEAT",162,337,12,ink);
   Box(g,100,651,1400,3,ink);Box(g,100,655,1400,18,Color.FromArgb(212,219,209));
   using(var p=new Pen(orange,5))for(int i=0;i<22;i++)g.DrawLine(p,116+i*64,655,132+i*64,672);
   var anim=new DollAnimation();anim.TriggerAttack(false);anim.Tick(.18f);Doll(g,176,218,1.375f,anim.Pose(false));
   Box(g,1097,460,136,102,ink);Box(g,1103,466,124,90,Color.FromArgb(67,82,94));Box(g,1114,486,102,16,orange);
   using(var b=new SolidBrush(blue))g.FillEllipse(b,1153,523,24,24);
   using(var p=new Pen(ink,18)){g.DrawLine(p,1140,548,1113,640);g.DrawLine(p,1190,548,1217,640);}
   Text(g,"01 / 现场回收",110,686,14,blue);Text(g,"正在自动回收",656,682,16,ink);
   Box(g,637,729,154,42,Color.FromArgb(223,230,228));Text(g,"暂停",690,741,16,ink);Box(g,809,729,154,42,Color.FromArgb(223,230,228));Text(g,"速度 1×",850,741,16,ink);
   Box(g,64,810,1472,1,Color.FromArgb(179,196,196));
   string[] names={"行动","整备","衣柜"};for(int i=0;i<3;i++){Box(g,509+i*198,830,186,48,i==0?Color.FromArgb(204,219,250):Color.FromArgb(223,230,228));Text(g,names[i],578+i*198,843,16,i==0?blue:ink);}
   Text(g,"TZ / RECOVERY DIVISION",64,844,12,ink);
   bmp.Save(Path.Combine(args[1],"ActionUiV5Review.png"),ImageFormat.Png);
  }
  using(var bmp=new Bitmap(1800,850))using(var g=Graphics.FromImage(bmp))
  {
   g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(bg);Text(g,"同一套贴图与骨骼参数 / 攻击动作关键帧",24,12,24,ink);
   float[] times={0,.12f,.22f,.28f,.38f,.65f};
   for(int row=0;row<2;row++)for(int i=0;i<6;i++)
   {
    var anim=new DollAnimation();anim.TriggerAttack(row==1);anim.Tick(times[i]);
    int x=i*300,y=70+row*380;Doll(g,x-12,y,1.05f,anim.Pose(false),row==1?1:0);
    Text(g,(row==0?"射击":"挥刃")+"  "+times[i].ToString("F2")+"s",x+40,y+338,16,ink);
   }
   bmp.Save(Path.Combine(args[1],"AttackFramesV5.png"),ImageFormat.Png);
  }
  using(var bmp=new Bitmap(1550,520))using(var g=Graphics.FromImage(bmp))
  {
   g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(bg);Text(g,"五套完整搭配 / 装备贴图映射检查",20,12,23,ink);
   for(int i=0;i<5;i++){Doll(g,i*310+15,70,1.2f,new DollPose{scaleY=1,rightArm=-5},i);Text(g,"STYLE / 0"+i,i*310+88,472,18,ink);}
   bmp.Save(Path.Combine(args[1],"OutfitsV5.png"),ImageFormat.Png);
  }
  string frames=Path.Combine(args[1],"AttackGifFrames");Directory.CreateDirectory(frames);
  for(int f=0;f<36;f++)using(var bmp=new Bitmap(880,440))using(var g=Graphics.FromImage(bmp))
  {
   g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(bg);
   Text(g,"射击 / 抬臂 · 后坐 · 收枪",28,12,19,ink);Text(g,"近战 / 蓄力 · 挥刃 · 收招",458,12,19,ink);
   for(int row=0;row<2;row++)
   {
    var a=new DollAnimation();a.TriggerAttack(row==1);a.Tick(f/24f);Doll(g,30+row*430,64,1.03f,a.Pose(false),row);
   }
   Box(g,20,393,840,2,blue);Text(g,"贴图与骨骼参数预览 / 非 Unity 运行录屏",24,410,12,ink);
   bmp.Save(Path.Combine(frames,f.ToString("D3")+".gif"),ImageFormat.Gif);
  }
  Console.WriteLine("Rendered UI, outfits, attack keyframes and 36 animation frames from shared puppet commands");
 }
}
