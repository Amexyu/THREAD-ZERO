namespace ThreadZero
{
    public sealed class DollPart
    {
        public readonly string id;
        public readonly int x,y,width,height;
        public readonly float left,top,drawWidth,drawHeight;
        public DollPart(string name,int px,int py,int w,int h,float dx,float dy,float dw,float dh)
        { id=name;x=px;y=py;width=w;height=h;left=dx;top=dy;drawWidth=dw;drawHeight=dh; }
    }
    public static class PaperDollLayout
    {
        public const int AtlasWidth=1536,AtlasHeight=1024;
        public const float FrameWidth=256,FrameHeight=320;
        public const float GripX=170,GripY=206;
        // Source coordinates are top-left pixels. Initial destination calibration, pending engine visual QA.
        public static readonly DollPart[] Parts={
            new DollPart("body",176,24,200,480,65.5f,10,125,300),
            new DollPart("coat",632,192,280,236,60,105,136,115),
            new DollPart("hood",1168,116,228,212,54,0,148,138),
            new DollPart("backpack",136,652,236,224,45,112,71,77),
            new DollPart("pistol",720,724,212,152,160,171,67,48),
            new DollPart("drone",1128,700,316,128,0,0,64,26),
            // Reuse hand pixels from the unmodified base body in a foreground layer.
            new DollPart("leftHand",180,316,48,44,68,192.5f,30,27.5f),
            new DollPart("rightHand",330,316,44,44,161.75f,192.5f,27.5f,27.5f)
        };
    }
}
