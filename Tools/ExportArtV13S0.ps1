$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class ArtV13S0
{
    public static Rectangle Bounds(Bitmap im)
    {
        int l=im.Width,t=im.Height,r=-1,b=-1;
        for(int y=0;y<im.Height;y++)for(int x=0;x<im.Width;x++)if(im.GetPixel(x,y).A>8)
        {l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);b=Math.Max(b,y);}
        if(r<l)throw new Exception("Empty alpha silhouette");
        return Rectangle.FromLTRB(l,t,r+1,b+1);
    }
    public static Bitmap Sprite(Bitmap source,int height)
    {
        Rectangle bounds=Bounds(source);int width=(int)Math.Round(bounds.Width*height/(double)bounds.Height);
        var result=new Bitmap(width,height,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(result))
        {g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(source,new Rectangle(0,0,width,height),bounds,GraphicsUnit.Pixel);}
        Rectangle actual=Bounds(result);
        if(actual.Height!=height)
        {
            // Preserve exact visible pixel height for the scale chart after antialiasing.
            var exact=new Bitmap(width,height,PixelFormat.Format32bppArgb);
            for(int y=0;y<height;y++)
            {
                int sy=actual.Top+Math.Min(actual.Height-1,(int)(y*actual.Height/(double)height));
                for(int x=0;x<width;x++)exact.SetPixel(x,y,result.GetPixel(x,sy));
            }
            result.Dispose();result=exact;
        }
        return result;
    }
    public static string Build(string root)
    {
        string review=Path.Combine(root,"Deliverables/ArtPack_v13/Review");Directory.CreateDirectory(review);
        string stats;
        using(var source=new Bitmap(Path.Combine(review,"s0_character_source.png")))
        using(var ui=new Bitmap(Path.Combine(review,"s0_action_ui_source.png")))
        using(var hero=Sprite(source,500))
        {
            Rectangle sb=Bounds(source);Rectangle hb=Bounds(hero);
            using(var output=new Bitmap(640,640,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(output))
            {g.Clear(Color.Transparent);g.DrawImageUnscaled(hero,(640-hero.Width)/2,608-hero.Height);output.Save(Path.Combine(review,"s0_character.png"),ImageFormat.Png);}
            using(var output=new Bitmap(1600,900,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(output))
            {g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(ui,0,0,1600,900);output.Save(Path.Combine(review,"s0_action_ui.png"),ImageFormat.Png);}

            var paths=new[]{Path.Combine(review,"s0_character_source.png"),Path.Combine(root,"ThreadZero/Assets/Resources/Art/V07/Enemies/patrol.png"),Path.Combine(root,"ThreadZero/Assets/Resources/Art/V07/Enemies/salvager.png"),Path.Combine(root,"ThreadZero/Assets/Resources/Art/V07/Enemies/furnace_foreman.png")};
            int[] heights={210,180,215,245};int[] centers={180,485,790,1095};
            string[] labels={"PLAYER / S0 CANDIDATE","NORMAL / V07 PLACEHOLDER","ELITE / V07 PLACEHOLDER","BOSS / V07 PLACEHOLDER"};
            using(var chart=new Bitmap(1280,680))using(var g=Graphics.FromImage(chart))
            using(var title=new Font("Arial",23,FontStyle.Bold))using(var font=new Font("Arial",13))using(var small=new Font("Arial",10))
            using(var teal=new SolidBrush(Color.FromArgb(65,139,125)))using(var light=new SolidBrush(Color.FromArgb(232,233,228)))
            using(var dark=new SolidBrush(Color.FromArgb(28,31,34)))
            {
                g.Clear(Color.FromArgb(246,246,242));g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.DrawString("S0  /  BATTLE SIZE REFERENCE",title,Brushes.Black,28,22);
                g.DrawString("Native pixel sizes. New character; enemy artwork is OLD V07 placeholder, not the S3 style proposal.",font,Brushes.DimGray,30,65);
                g.DrawLine(Pens.Gray,25,380,1255,380);
                g.FillRectangle(dark,20,462,1240,160);g.DrawString("64 px SILHOUETTE CHECK / DARK BACKGROUND",font,light,36,475);
                string heightStats="";
                for(int i=0;i<4;i++)using(var image=new Bitmap(paths[i]))using(var sprite=Sprite(image,heights[i]))using(var tiny=Sprite(image,64))
                {
                    Rectangle actual=Bounds(sprite);
                    g.DrawImageUnscaled(sprite,centers[i]-sprite.Width/2,380-sprite.Height);
                    g.DrawString(labels[i],small,i==0?teal:Brushes.DimGray,centers[i]-110,398);
                    g.DrawString(heights[i]+" px",font,Brushes.Black,centers[i]-28,421);
                    g.DrawImageUnscaled(tiny,centers[i]-tiny.Width/2,593-tiny.Height);
                    if(i>0)heightStats+=",";
                    heightStats+="{\"targetHeight\":"+heights[i]+",\"measuredAlphaHeight\":"+actual.Height+",\"rasterWidth\":"+sprite.Width+"}";
                }
                g.DrawString("Style approval only. Not a rig, animation test, final enemy set or in-game capture.",font,Brushes.DimGray,30,639);
                chart.Save(Path.Combine(review,"s0_battle_scale.png"),ImageFormat.Png);
                stats="{\"source\":{\"width\":"+source.Width+",\"height\":"+source.Height+",\"alphaBounds\":["+sb.X+","+sb.Y+","+sb.Width+","+sb.Height+"]},\"character\":{\"canvas\":[640,640],\"targetAlphaHeight\":500,\"measuredAlphaHeight\":"+hb.Height+"},\"ui\":{\"canvas\":[1600,900],\"status\":\"raster skin concept; actors are illustrative placeholders\"},\"scaleChart\":["+heightStats+"]}";
            }
            using(var qa=new Bitmap(1280,640))using(var g=Graphics.FromImage(qa))
            using(var dark=new SolidBrush(Color.FromArgb(28,31,34)))
            {
                g.Clear(Color.FromArgb(232,233,228));g.FillRectangle(dark,640,0,640,640);
                g.DrawImageUnscaled(hero,(640-hero.Width)/2,608-hero.Height);g.DrawImageUnscaled(hero,640+(640-hero.Width)/2,608-hero.Height);
                qa.Save(Path.Combine(review,"s0_character_alpha_qa.png"),ImageFormat.Png);
            }
        }
        File.WriteAllText(Path.Combine(review,"s0_measurements.json"),stats);
        return stats;
    }
}
'@
$root=Split-Path -Parent $PSScriptRoot
[ArtV13S0]::Build($root)
