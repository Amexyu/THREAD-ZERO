using System;
using System.Collections.Generic;
namespace ThreadZero {
 public struct PuppetSprite { public int atlas,index;public float x,y,w,h,angle,px,py,u0,u1; }
 // Shared by Unity and the offline renderer, including sleeve and hand transforms.
 public static class PuppetGeometry {
 public static void Muzzle(DollPose p,out float x,out float y){
 Muzzle(p,0,out x,out y);
 }
 public static void Muzzle(DollPose p,int weapon,out float x,out float y){
 float a=p.rightArm*(float)Math.PI/180,b=(p.rightArm+80)*(float)Math.PI/180;
 float hx=163-(float)Math.Sin(a)*82,hy=127+(float)Math.Cos(a)*82;
 float vx=(weapon==2?100:76)*(.98f-.24f),vy=(weapon==2?49:58)*(.24f-.73f);
 x=hx+vx*(float)Math.Cos(b)-vy*(float)Math.Sin(b);y=hy+vx*(float)Math.Sin(b)+vy*(float)Math.Cos(b);
 Rotate(ref x,ref y,p.torsoAngle);x+=p.rootX;y+=p.rootY;
 }
 static void Rotate(ref float x,ref float y,float angle){
 float r=angle*(float)Math.PI/180,c=(float)Math.Cos(r),s=(float)Math.Sin(r),dx=x-130,dy=y-211;
 x=130+dx*c-dy*s;y=211+dx*s+dy*c;
 }
 public static List<PuppetSprite> Build(int weapon,int head,int coat,int back,DollPose p){
 var list=new List<PuppetSprite>();float dx=p.rootX,dy=p.rootY;
 Action<int,int,float,float,float,float,float,float,float,float,float> add=(a,i,x,y,w,h,angle,fx,fy,u0,u1)=>{
 float px=x+w*fx,py=y+h*fy;
 if(a!=1||i<4){Rotate(ref px,ref py,p.torsoAngle);angle+=p.torsoAngle;}
 list.Add(new PuppetSprite{atlas=a,index=i,x=px-w*fx+dx,y=py-h*fy+dy,w=w,h=h,angle=angle,px=px+dx,py=py+dy,u0=u0,u1=u1});
 };
 float t=0;
 add(0,15+back,48,110,67,93,t,.9f,1,0,1);
 add(1,4,96,204,34,104,p.leftLeg,.4f,0,0,1);add(1,5,133,204,36,104,p.rightLeg,.4f,0,0,1);
 add(1,2,81,122,22,93,p.leftArm,.5f,.05f,0,1);add(0,10+coat,77,119,29,76,p.leftArm,.58f,.1f,0,.24f);
 add(1,1,91,108,77,112,t,.5f,.95f,0,1);
 add(0,10+coat,88,106,83,coat==1||coat==4?153:115,t,.5f,.75f,.23f,.78f);
 add(1,0,77,12,109,104,p.headAngle,.5f,.95f,0,1);
 if(head==0)add(0,5,69,3,123,132,p.headAngle,.5f,.95f,0,1);
 if(head==1)add(0,6,103,75,75,50,p.headAngle,.5f,.6f,0,1);
 if(head==2)add(0,7,90,61,92,49,p.headAngle,.5f,.6f,0,1);
 if(head==3)add(0,8,76,8,116,125,p.headAngle,.5f,.95f,0,1);
 if(head==4)add(0,9,75,0,117,118,p.headAngle,.5f,.95f,0,1);
 float arm=p.rightArm,rad=arm*(float)Math.PI/180;
 float hx=163-(float)Math.Sin(rad)*82,hy=127+(float)Math.Cos(rad)*82;
 float ww=weapon==1?89:weapon==4?94:weapon==2?100:76,wh=weapon==1?133:weapon==4?107:weapon==2?49:58;
 float fx=weapon==1||weapon==4?.22f:.24f,fy=weapon==1||weapon==4?.84f:.73f;
 add(0,weapon,hx-ww*fx,hy-wh*fy,ww,wh,arm+80,fx,fy,0,1);
 add(1,3,152,122,22,93,arm,.5f,.05f,0,1);
 add(0,10+coat,150,119,31,76,arm,.42f,.1f,.76f,1);
 return list;
 }
 }
}
