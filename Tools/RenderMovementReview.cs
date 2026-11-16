using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using ThreadZero;

public static class RenderMovementReview
{
    static float S(float x){return (x-128)*2/3f;}
    static float Y(float y){return (y-104)*2/3f;}
    public static void Main(string[] args)
    {
        string root=args[0],frames=Path.Combine(root,"Artifacts/MovementFrames");Directory.CreateDirectory(frames);
        var m=new GameModel(null,1616);var w=m.Create("w1",Quality.Common,1);m.state.items.Add(w);m.Equip(w.uid);
        var c=new CombatSession(m);c.EnableMovement();
        using(var body=new Bitmap(Path.Combine(root,"ThreadZero/Assets/Resources/Art/V12/StarterDagger/Body.png")))
        using(var weapon=new Bitmap(Path.Combine(root,"ThreadZero/Assets/Resources/Art/V12/StarterDagger/Weapon.png")))
        using(var enemy=new Bitmap(Path.Combine(root,"ThreadZero/Assets/Resources/Art/V07/Enemies/patrol.png")))
        using(var font=new Font("Arial",12))
        {
            int contactFrames=0,movingFrames=0,kills=0;float corpseX=-10000;bool contactSaved=false;
            for(int n=0;n<250;n++)
            {
                int version=c.Movement.TargetVersion;float oldEnemy=c.Movement.EnemyX;c.Tick(.04f);
                if(version!=c.Movement.TargetVersion){corpseX=oldEnemy;kills++;}
                if(c.Movement.HeroMoving && c.Movement.EnemyMoving)movingFrames++;
                if(c.HeroInRange && c.EnemyInRange)contactFrames++;
                using(var image=new Bitmap(960,400))using(var g=Graphics.FromImage(image))
                {
                    g.Clear(Color.FromArgb(243,243,239));g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    g.FillRectangle(Brushes.Gainsboro,0,Y(576),960,100);
                    for(int j=0;j<12;j++){float x=(j*160-c.Movement.CameraX)%1800;g.FillRectangle(Brushes.Silver,S(x),Y(620),42,3);}
                    g.DrawLine(Pens.DimGray,0,Y(572),960,Y(572));
                    if(corpseX-c.Movement.CameraX>-100){var p=g.Save();g.TranslateTransform(S(corpseX-c.Movement.CameraX),Y(572));g.RotateTransform(-9);g.DrawImage(enemy,-65,-26,130,28);g.Restore(p);}
                    float hx=c.Movement.HeroScreenX,ex=c.Movement.EnemyScreenX;
                    float phase=c.Movement.HeroTravel/24f,eh=c.Movement.EnemyTravel/21f;
                    float hop=c.Movement.HeroMoving?(float)Math.Abs(Math.Sin(phase))*7:0;
                    g.FillEllipse(Brushes.LightGray,S(hx)-33,Y(570),66,4);
                    int frame=!c.Movement.HeroMoving && c.DaggerActive?DaggerSkill.Frame(c.DaggerAge,c.DaggerCombo):0;
                    var state=g.Save();g.TranslateTransform(S(hx),Y(574-hop));
                    if(c.Movement.HeroMoving){g.RotateTransform(2.5f+(float)Math.Sin(phase)*.6f);float squash=(float)Math.Cos(phase*2)*.018f;g.ScaleTransform(1+squash,1-squash);}
                    var rect=new RectangleF(-128*260/256f*2/3,-238*260/256f*2/3,260*2/3f,260*2/3f);
                    foreach(var texture in new[]{body,weapon})g.DrawImage(texture,rect,new RectangleF((frame%6)*256,(frame/6)*256,256,256),GraphicsUnit.Pixel);
                    g.Restore(state);
                    for(int i=c.reserve.Count;i>=0;i--)
                    {
                        float x=ex+i*136;var save=g.Save();float lift=c.Movement.EnemyMoving?(float)Math.Abs(Math.Sin(eh+i*.65f))*4:0;
                        g.TranslateTransform(S(x),Y(574-lift));if(c.Movement.EnemyMoving)g.RotateTransform(-2.5f+(float)Math.Sin(eh)*.6f);
                        float size=c.Boss?300:c.Elite?260:220;
                        g.DrawImage(enemy,-size/3,-470*size/512f*2/3,size*2/3,size*2/3);g.Restore(save);
                    }
                    g.FillRectangle(Brushes.Black,S(hx)-45,Y(320),90,5);g.FillRectangle(Brushes.SeaGreen,S(hx)-45,Y(320),90*Math.Max(0,c.health)/m.Total().health,5);
                    g.FillRectangle(Brushes.Black,S(ex)-45,Y(350),90,5);g.FillRectangle(Brushes.OrangeRed,S(ex)-45,Y(350),90*Math.Max(0,c.enemyHealth)/c.enemyMaxHealth,5);
                    string label=c.Movement.HeroMoving || c.Movement.EnemyMoving?"Both sides approach":"Continuous close combat";
                    g.DrawString("Movement simulation preview | actual runtime layers | not a Unity capture",font,Brushes.DimGray,18,12);
                    g.DrawString(label+"   Wave "+c.wave+"   "+(n*.04f).ToString("F2")+"s",font,Brushes.Black,18,36);
                    if(c.hitFlash>0)g.DrawString(c.lastPlayerDamage.ToString("F0"),font,Brushes.OrangeRed,S(ex),Y(320));
                    image.Save(Path.Combine(frames,n.ToString("D3")+".gif"),ImageFormat.Gif);
                    if(n==0 || n==20 || n==42 || n==85)image.Save(Path.Combine(frames,n.ToString("D3")+".png"),ImageFormat.Png);
                    if(!contactSaved && c.HeroInRange && c.EnemyInRange && !c.Movement.HeroMoving && !c.Movement.EnemyMoving)
                    {image.Save(Path.Combine(frames,"Contact.png"),ImageFormat.Png);contactSaved=true;}
                }
            }
            Console.WriteLine("Review: 250 frames, both moving="+movingFrames+", close combat="+contactFrames+", targets defeated="+kills);
        }
    }
}
