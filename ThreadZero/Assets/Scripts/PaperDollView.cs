using System.Collections.Generic;
using UnityEngine;
namespace ThreadZero {
 public static class PaperDollView {
 static Texture2D equipment,rig,legacy;static bool attempted;
 static readonly Dictionary<string,Texture2D> iconCache=new Dictionary<string,Texture2D>();
 static bool Ready(){
 if(!attempted){attempted=true;equipment=Resources.Load<Texture2D>("Art/EquipmentAtlas");rig=Resources.Load<Texture2D>("Art/BodyRigAtlas");legacy=Resources.Load<Texture2D>("Art/RainRunnerAtlas");if(equipment==null||rig==null)Debug.LogError("THREAD ZERO: missing EquipmentAtlas / BodyRigAtlas in Resources/Art");}
 return equipment!=null&&rig!=null&&equipment.width==SpriteAtlasLayout.EquipmentWidth&&equipment.height==SpriteAtlasLayout.EquipmentHeight&&rig.width==SpriteAtlasLayout.RigWidth&&rig.height==SpriteAtlasLayout.RigHeight;
 }
 static void Sprite(Texture2D tex,int[,] l,int i,Rect rect,float u0=0,float u1=1){
 float x=l[i,0],y=l[i,1],w=l[i,2],h=l[i,3];
 GUI.DrawTextureWithTexCoords(rect,tex,new Rect((x+w*u0)/tex.width,1-(y+h)/tex.height,w*(u1-u0)/tex.width,h/tex.height),true);
 }
 public static bool Draw(GameModel model,Rect area,float time,bool attacking,float hurt,bool defeated,float dash,DollPose? animationPose=null){
 if(!Ready())return false;Matrix4x4 old=GUI.matrix;Color tint=GUI.color;
 float scale=Mathf.Min(area.width/330,area.height/320);
 GUI.matrix=old*Matrix4x4.TRS(new Vector3(area.center.x-128*scale,area.yMax-320*scale,0),Quaternion.identity,new Vector3(scale,scale,1));
 DollPose p=animationPose??new DollPose{scaleY=1,rootY=Mathf.Sin(time*2.2f)*.6f,leftArm=3,rightArm=-5,leftLeg=3,rightLeg=-3};
 GUIUtility.ScaleAroundPivot(new Vector2(1,p.scaleY),new Vector2(128,310));
 if(defeated)GUI.color=new Color(.6f,.65f,.68f);else if(p.damageTint>0)GUI.color=new Color(1,1-p.damageTint,1-p.damageTint);
 foreach(var s in PuppetGeometry.Build(model.Appearance(Slot.Weapon).style,model.Appearance(Slot.Head).style,model.Appearance(Slot.Coat).style,model.Appearance(Slot.Back).style,p)){
 Matrix4x4 before=GUI.matrix;GUIUtility.RotateAroundPivot(s.angle,new Vector2(s.px,s.py));
 Sprite(s.atlas==0?equipment:rig,s.atlas==0?SpriteAtlasLayout.Equipment:SpriteAtlasLayout.Rig,s.index,new Rect(s.x,s.y,s.w,s.h),s.u0,s.u1);
 if(s.atlas==0&&s.index<5)QualityVisual.EquipmentLine(model.Equipped(Slot.Weapon),new Vector2(s.x+s.w*.65f,s.y+s.h*.2f),new Vector2(s.x+s.w*.85f,s.y+s.h*.2f),time,model.state.glowMode);
 GUI.matrix=before;
 }
 GUI.color=tint;
 if(!defeated){
 QualityVisual.EquipmentLine(model.Equipped(Slot.Head),new Vector2(155+p.rootX,65),new Vector2(168+p.rootX,65),time,model.state.glowMode);
 QualityVisual.EquipmentLine(model.Equipped(Slot.Coat),new Vector2(136+p.rootX,153),new Vector2(144+p.rootX,153),time,model.state.glowMode);
 QualityVisual.EquipmentLine(model.Equipped(Slot.Back),new Vector2(61+p.rootX,137),new Vector2(61+p.rootX,159),time,model.state.glowMode);
 }
 if(p.muzzle>0){float mx,my;PuppetGeometry.Muzzle(p,model.Appearance(Slot.Weapon).style,out mx,out my);QualityVisual.Halo(new Vector2(mx,my),25,new Color(1,.61f,.2f),p.muzzle);CharacterView.Line(new Vector2(mx-4,my),new Vector2(mx+17,my),5,new Color(1,.66f,.22f,p.muzzle));}
 if(p.slash>0)for(int i=0;i<13;i++){float a=(-80+i*11)*Mathf.Deg2Rad,b=(-69+i*11)*Mathf.Deg2Rad;CharacterView.Line(new Vector2(210+Mathf.Cos(a)*106,160+Mathf.Sin(a)*106),new Vector2(210+Mathf.Cos(b)*106,160+Mathf.Sin(b)*106),3+p.slash*5,new Color(.12f,.55f,1,p.slash*(i+1)/13));}
 GUI.matrix=old;GUI.color=tint;return true;
 }
 public static bool DrawDrone(Rect area){
 if(!Ready()||legacy==null)return false;DollPart s=PaperDollLayout.Parts[5];GUI.DrawTextureWithTexCoords(area,legacy,new Rect((float)s.x/legacy.width,1f-(float)(s.y+s.height)/legacy.height,(float)s.width/legacy.width,(float)s.height/legacy.height),true);return true;
 }
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetCache(){iconCache.Clear();attempted=false;}
 // 装备图标：只用 V13 现行部件（Parts/<id>/ 或初始件 Starter/），旧 V08/EquipmentAtlas 图标不再使用。
 static Texture2D PartIcon(EquipmentBase item){
 Texture2D t;if(iconCache.TryGetValue(item.id,out t))return t;
 string file=item.slot==Slot.Head?"head":item.slot==Slot.Coat?"torso":item.slot==Slot.Back?"pack":item.slot==Slot.Legs?"leg_near":"weapon";
 t=Resources.Load<Texture2D>("Art/V13/Parts/"+item.id+"/icon");   // 单品图（例如头部装备不带人头），有就优先用
 if(t==null)t=Resources.Load<Texture2D>("Art/V13/Parts/"+item.id+"/"+file);
 if(t==null)t=Resources.Load<Texture2D>("Art/V13/Starter/"+file);
 iconCache[item.id]=t;return t;
 }
 static bool IsPartIcon(Texture2D t){return t!=null&&t.name=="icon";}
 public static bool DrawEquipmentIcon(EquipmentBase item,Rect area,bool dim=false){
 if(item==null)return false;Texture2D tex=PartIcon(item);if(tex==null)return false;
 Color previous=GUI.color;if(dim)GUI.color=new Color(.65f,.67f,.7f,.55f);
 if(item.slot==Slot.Weapon&&!IsPartIcon(tex)){
 // 武器部件是竖着画的（握把在上、枪口/刃尖朝下），图标里斜放成刃尖朝右上。
 float k=Mathf.Min(area.width,area.height)*.98f/((tex.width+tex.height)*.70711f),w=tex.width*k,h=tex.height*k;
 Matrix4x4 before=GUI.matrix;GUI.matrix=before*Ui.RotateLocal(-135,area.center);
 GUI.DrawTexture(new Rect(area.center.x-w/2,area.center.y-h/2,w,h),tex,ScaleMode.StretchToFill,true);
 GUI.matrix=before;
 }else GUI.DrawTexture(area,tex,ScaleMode.ScaleToFit,true);
 GUI.color=previous;return true;
 }
 }
}
