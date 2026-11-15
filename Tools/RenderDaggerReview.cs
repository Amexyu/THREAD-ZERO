using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

public static class RenderDaggerReview
{
    static Color[] gifColors;
    static byte[] gifMap;
    static void SaveGif(Bitmap frame,string path)
    {
        var area=new Rectangle(0,0,frame.Width,frame.Height);
        var read=frame.LockBits(area,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        var bytes=new byte[read.Stride*frame.Height];Marshal.Copy(read.Scan0,bytes,0,bytes.Length);frame.UnlockBits(read);
        if(gifMap==null)
        {
            var counts=new int[32768];
            for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++){int i=y*read.Stride+x*4;int key=(bytes[i+2]>>3)*1024+(bytes[i+1]>>3)*32+(bytes[i]>>3);counts[key]++;}
            var chosen=Enumerable.Range(0,32768).OrderByDescending(k=>counts[k]).Take(256).ToArray();
            gifColors=chosen.Select(k=>Color.FromArgb((k/1024)*8+4,(k/32%32)*8+4,(k%32)*8+4)).ToArray();
            gifMap=new byte[32768];
            for(int k=0;k<32768;k++){int r=k/1024*8+4,g=k/32%32*8+4,b=k%32*8+4,best=0,distance=int.MaxValue;for(int n=0;n<256;n++){Color c=gifColors[n];int dr=r-c.R,dg=g-c.G,db=b-c.B,d=dr*dr+dg*dg+db*db;if(d<distance){distance=d;best=n;}}gifMap[k]=(byte)best;}
        }
        using(var indexed=new Bitmap(frame.Width,frame.Height,PixelFormat.Format8bppIndexed))
        {
            var palette=indexed.Palette;for(int n=0;n<256;n++)palette.Entries[n]=gifColors[n];indexed.Palette=palette;
            var write=indexed.LockBits(area,ImageLockMode.WriteOnly,PixelFormat.Format8bppIndexed);var indices=new byte[write.Stride*frame.Height];
            for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++){int i=y*read.Stride+x*4;int key=(bytes[i+2]>>3)*1024+(bytes[i+1]>>3)*32+(bytes[i]>>3);indices[y*write.Stride+x]=gifMap[key];}
            Marshal.Copy(indices,0,write.Scan0,indices.Length);indexed.UnlockBits(write);indexed.Save(path,ImageFormat.Gif);
        }
    }
    static Rectangle Bounds(Bitmap im,Rectangle region){int l=region.Right,t=region.Bottom,r=region.Left,b=region.Top;for(int y=region.Top;y<region.Bottom;y++)for(int x=region.Left;x<region.Right;x++)if(im.GetPixel(x,y).A>32){l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y);}return Rectangle.FromLTRB(l,t,r+1,b+1);}
    static void Text(Graphics g,string s,float x,float y,int size,Color c){using(var font=new Font("Microsoft YaHei",size,FontStyle.Bold))using(var b=new SolidBrush(c))g.DrawString(s,font,b,x,y);}
    public static void Run(string root) { Render(root, false); }
    public static void RunV12(string root) { Render(root, true); }
    public static void RunStab12(string root) { RenderStab(root,false); }
    public static void RunStabClean(string root) { RenderStab(root,true); }
    public static void RunStabGuard(string root) { RenderStab(root,false,true); }
    static void RenderStab(string root,bool corrected,bool guard=false)
    {
        string review=Path.Combine(root,"Deliverables/ArtPack_v12/Review"),folder=Path.Combine(review,guard?"StabGuardFrames":corrected?"StabCleanFrames":"Stab12Frames");Directory.CreateDirectory(folder);
        using(var atlas=new Bitmap(Path.Combine(review,guard?"Dagger_Stab_guard.png":"Dagger_Stab12.png")))
        {
            int cw=atlas.Width/4,ch=atlas.Height/3;
            int[] sequence=corrected?new[]{0,1,2,3,5,6,7,8,3,2,1,0}:Enumerable.Range(0,12).ToArray();
            for(int f=0;f<12;f++)using(var frame=new Bitmap(650,500,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(frame))
            {
                int sourceFrame=sequence[f];
                g.Clear(Color.FromArgb(243,243,239));g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                Text(g,guard?"THREAD ZERO  /  普通短刺 · 左手护位修订":corrected?"THREAD ZERO  /  普通短刺 · 错手与速度修订":"THREAD ZERO  /  普通短刺 · 连续姿势修订",20,16,16,Color.FromArgb(28,31,34));
                Text(g,"无刀光、无震屏；检查肩肘、重心与收刀",20,47,10,Color.FromArgb(90,103,101));
                using(var p=new Pen(Color.FromArgb(193,201,195),1))g.DrawLine(p,0,447,650,447);
                using(var b=new SolidBrush(Color.FromArgb(20,40,48,48)))g.FillEllipse(b,249,441,149,8);
                // Same scale and sole registration for every cel. No external lunge or spin.
                float scale=.99f;
                g.DrawImage(atlas,new RectangleF(325-cw*.5f*scale,447-334*scale,cw*scale,ch*scale),new Rectangle(sourceFrame%4*cw,sourceFrame/4*ch,cw,ch),GraphicsUnit.Pixel);
                string stage=corrected?(f<3?"蓄力":f<5?"快速出刀":f==5?"命中":f<11?"收刀":"准备"):(f<3?"收肩蓄力":f<7?"重心前移 · 肩肘前送":f<10?"屈肘收刀":"恢复准备");
                Text(g,stage,20,469,12,Color.FromArgb(54,111,115));
                SaveGif(frame,Path.Combine(folder,f.ToString("D3")+".gif"));
                if(f==6)frame.Save(Path.Combine(review,guard?"StabGuard_Contact.png":"Stab12_Contact.png"),ImageFormat.Png);
            }
        }
    }
    static float Ease(float t){t=Math.Max(0,Math.Min(1,t));return 1-(1-t)*(1-t)*(1-t);}
    public static void RunPower(string root)
    {
        string review=Path.Combine(root,"Deliverables/ArtPack_v12/Review"),folder=Path.Combine(review,"PowerFrames");Directory.CreateDirectory(folder);
        using(var atlas=new Bitmap(Path.Combine(review,"Scavenger_DaggerKeys.png")))
        {
            int cw=atlas.Width/3,ch=atlas.Height/2;float scale=360f/ch;
            for(int f=0;f<200;f++)using(var frame=new Bitmap(700,510,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(frame))
            {
                bool combo=f>=100;int t=f%100,key=0,hit=0;float dx=0,rot=0,sy=1;string stage="准备";
                if(!combo)
                {
                    if(t>=18&&t<28){key=1;float q=(t-18)/10f;dx=-9*Ease(q);rot=-3*q;sy=1-.018f*q;stage="收肩蓄力";}
                    else if(t>=28&&t<31){key=4;dx=-9+39*Ease((t-28)/3f);stage="蹬地 · 短刺";}
                    else if(t>=31&&t<35){key=4;dx=30;hit=1;stage="命中停顿";}
                    else if(t>=35&&t<46){key=5;dx=30*(1-Ease((t-35)/11f));stage="迅速收刀";}
                }
                else
                {
                    if(t>=15&&t<26){key=1;float q=(t-15)/11f;dx=-9*Ease(q);rot=-3*q;sy=1-.02f*q;stage="蓄力";}
                    else if(t>=26&&t<29){key=2;dx=-9+74*Ease((t-26)/3f);rot=2;stage="斜切";}
                    else if(t>=29&&t<32){key=2;dx=65;rot=2;hit=1;stage="第一刀 · 顿";}
                    else if(t>=32&&t<36){key=3;dx=65-15*Ease((t-32)/4f);stage="回切";}
                    else if(t>=36&&t<39){key=3;dx=50;hit=2;stage="第二刀 · 顿";}
                    else if(t>=39&&t<46){key=1;dx=50-30*Ease((t-39)/7f);rot=-2;stage="收肩再发力";}
                    else if(t>=46&&t<49){key=4;dx=20+20*Ease((t-46)/3f);stage="终结前刺";}
                    else if(t>=49&&t<54){key=4;dx=40;hit=3;stage="第三刺 · 重命中";}
                    else if(t>=54&&t<68){key=5;dx=40*(1-Ease((t-54)/14f));stage="收刀复位";}
                }
                g.Clear(Color.FromArgb(8,10,11));g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                Text(g,"THREAD ZERO  /  匕首力量感修订",20,14,16,Color.FromArgb(230,234,231));
                Text(g,combo?"装备技能：迅切连击":"普通攻击：短刺",20,51,20,Color.FromArgb(230,234,231));
                Text(g,stage,20,87,12,Color.FromArgb(90,198,201));
                // One broad practice plate makes contact and recoil readable without masking the arm.
                float kick=hit>0?(hit==3?12:7):0;
                using(var b=new SolidBrush(Color.FromArgb(48,54,55)))g.FillRectangle(b,449,325,12,124);
                using(var b=new SolidBrush(Color.FromArgb(68,77,78)))g.FillRectangle(b,416,442,79,9);
                var plate=g.Save();g.TranslateTransform(458+kick,275);g.RotateTransform(hit>0?hit*2:0);
                using(var b=new SolidBrush(hit>0?Color.FromArgb(126,139,132):Color.FromArgb(66,76,77)))g.FillRectangle(b,-28,-69,57,131);
                using(var pen=new Pen(Color.FromArgb(135,150,145),2))g.DrawRectangle(pen,-28,-69,57,131);
                using(var pen=new Pen(Color.FromArgb(199,139,76),2)){g.DrawEllipse(pen,-16,-39,32,32);g.DrawLine(pen,-20,-23,20,-23);}
                g.Restore(plate);
                using(var b=new SolidBrush(Color.FromArgb(45,40,48,48)))g.FillEllipse(b,214+dx,447,128,9);
                var body=g.Save();g.TranslateTransform(280+dx,475);g.RotateTransform(rot);g.ScaleTransform(1,sy);
                g.DrawImage(atlas,new RectangleF(-cw*scale/2,-ch*scale,cw*scale,ch*scale),new Rectangle(key%3*cw,key/3*ch,cw,ch),GraphicsUnit.Pixel);g.Restore(body);
                if(hit>0)
                {
                    float yy=key==2?324:key==3?252:262;
                    using(var pen=new Pen(Color.FromArgb(225,181,103),2))for(int n=0;n<4;n++){double a=(-65+n*36)*Math.PI/180;float xx=433+kick;g.DrawLine(pen,xx,yy,xx+(float)Math.Cos(a)*(10+hit*4),yy+(float)Math.Sin(a)*(10+hit*4));}
                }
                using(var pen=new Pen(Color.FromArgb(92,109,107),2))g.DrawLine(pen,0,453,700,453);
                Text(g,"蓄力 → 快出手 → 命中停顿 → 收刀",20,478,11,Color.FromArgb(145,160,160));
                SaveGif(frame,Path.Combine(folder,f.ToString("D3")+".gif"));
                if(f==31||f==149)frame.Save(Path.Combine(review,"PowerCheck_"+f+".png"),ImageFormat.Png);
            }
        }
    }
    static void Render(string root, bool v12)
    {
        string review=Path.Combine(root,v12?"Deliverables/ArtPack_v12/Review":"Deliverables/ArtPack_v11/Review");
        string folder=Path.Combine(review,"DaggerFramesV4");Directory.CreateDirectory(folder);
        using(var atlas=new Bitmap(Path.Combine(review,v12?"Scavenger_DaggerKeys.png":"DaggerKeys_v4_clean.png")))
        {
            int cw=atlas.Width/3,ch=atlas.Height/2;var rects=new Rectangle[6];
            for(int i=0;i<6;i++)rects[i]=Bounds(atlas,new Rectangle(i%3*cw,i/3*ch,cw,ch));
            float scale=360f/ch;
            for(int f=0;f<84;f++)using(var frame=new Bitmap(900,510,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(frame))
            {
                g.Clear(Color.FromArgb(8,10,11));g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                Text(g,v12?"THREAD ZERO  /  维生回收员 · 匕首动作初稿":"THREAD ZERO  /  匕首动作样稿 v4",22,14,17,Color.FromArgb(230,234,231));
                Text(g,v12?"单手持械 · 短刺 / 斜切 / 回切 / 前刺":"单手持械 · 六个关键姿势 · 约四倍慢放检查",22,46,11,Color.FromArgb(150,160,163));
                using(var pen=new Pen(Color.FromArgb(205,210,207),1))g.DrawLine(pen,450,76,450,478);
                using(var pen=new Pen(Color.FromArgb(28,31,34),3))g.DrawLine(pen,0,452,900,452);
                Text(g,"普通攻击：短刺",24,76,18,Color.FromArgb(230,234,231));Text(g,"装备技能：迅切连击",474,76,18,Color.FromArgb(230,234,231));
                double p=f%42/42.0;
                for(int panel=0;panel<2;panel++)
                {
                    int key;string label;
                    if(panel==0){key=p<.32?0:p<.55?4:p<.78?5:0;label=key==4?"前刺 · 一次命中":key==5?"屈肘收回":"持刀准备";}
                    else{key=p<.10?0:p<.22?1:p<.34?2:p<.46?3:p<.64?4:p<.85?5:0;label=key==1?"拉开肩肘 · 准备":key==2?"第一段：斜切":key==3?"第二段：回切":key==4?"第三段：前刺":key==5?"收刀复位":"持刀准备";}
                    float x=panel==0?225:675;Rectangle src=rects[key];
                    float dx=key==4?8:key==2?4:0;
                    using(var b=new SolidBrush(Color.FromArgb(32,28,31,34)))g.FillEllipse(b,x-65,445,130,10);
                    float left=(src.X-key%3*cw-cw/2)*scale;
                    g.DrawImage(atlas,new RectangleF(x+left+dx,451-src.Height*scale,src.Width*scale,src.Height*scale),src,GraphicsUnit.Pixel);
                    Text(g,label,panel*450+24,465,12,Color.FromArgb(90,198,201));
                    if(panel==1){int hits=key==2?1:key==3?2:key==4?3:0;Text(g,hits>0?"命中 "+hits+" / 3":"",panel*450+310,128,14,Color.FromArgb(219,102,42));}
                }
                    SaveGif(frame,Path.Combine(folder,f.ToString("D3")+".gif"));
                if(f==12)frame.Save(Path.Combine(folder,"../DaggerReview_v4.png"),ImageFormat.Png);
            }
        }
    }
}
