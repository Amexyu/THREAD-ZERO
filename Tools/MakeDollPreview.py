"""Render only through browser code; never modifies the source artwork."""
import json
import pathlib
import re

root = pathlib.Path(__file__).resolve().parent.parent
source = (root / 'ThreadZero/Assets/Scripts/PaperDollLayout.cs').read_text(encoding='utf-8')
parts = []
for name, values in re.findall(r'new DollPart\("([^"]+)",([^\)]+)\)', source):
    numbers = [float(value.strip().rstrip('f')) for value in values.split(',')]
    if len(numbers) != 8:
        raise ValueError(name)
    parts.append(dict(id=name, source=numbers[:4], target=numbers[4:]))
if len(parts) != 8:
    raise ValueError('Expected six source parts and two foreground hand samples')

html = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8">
<title>THREAD ZERO · 分层离屏检查</title>
<style>
*{box-sizing:border-box}body{margin:0;padding:26px;background:#10161e;color:#dbe6ed;font:15px "Microsoft YaHei",sans-serif}h1{margin:0 0 10px;font-size:27px}header p{color:#91a4b3;margin:5px 0}.grid{display:grid;grid-template-columns:repeat(5,1fr);gap:12px;margin-top:22px}.card{background:#19232f;border:1px solid #33414d;padding:14px 6px;text-align:center}.card h2{font-size:17px;color:#68d5d7;margin:3px 0 8px}canvas{width:100%;max-width:256px;height:auto;background:repeating-conic-gradient(#26323d 0 25%,#2b3743 0 50%) 0/20px 20px}.caption{color:#9aaebe;font-size:12px;min-height:35px;margin-top:10px}footer{padding:17px 0;color:#91a4b3}.status{color:#c0e780}.details{font:12px monospace;color:#a5b5c2;line-height:1.8}
</style><header><h1>THREAD ZERO / 分层与动作离屏检查</h1><p>真实图集＋源码裁切坐标 · 浏览器离屏渲染 · 非 Unity 实机截图</p><p>用于检查拆件比例、透明开口、握持锚点和变换范围。图片文件保持原样。</p></header>
<div class="grid" id="grid"></div><footer><span class="status" id="status">正在加载本地图集…</span><div class="details" id="details"></div></footer>
<script>
const parts=__PARTS__;
const image=new Image();
const states=[['素体','base','素体头、手、裤腿位置'],['穿戴叠合','idle','背包 → 素体 → 外套 → 兜帽 → 武器'],['攻击姿态','attack','手持部件绕握持点旋转'],['受击姿态','hurt','轻微扰动与短促染色'],['维修姿态','repair','压低重心、降低亮度']];
function part(ctx,index,dx=0){let p=parts[index],s=p.source,t=p.target;ctx.drawImage(image,...s,t[0]+dx,t[1],t[2],t[3]);}
function line(ctx,a,b,color){ctx.strokeStyle=color;ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(...a);ctx.lineTo(...b);ctx.stroke();}
function render(){
 const grid=document.getElementById('grid'); grid.innerHTML='';
 for(const [title,state,caption] of states){
  const card=document.createElement('div');card.className='card';
  const h=document.createElement('h2');h.textContent=title;card.append(h);
  const canvas=document.createElement('canvas');canvas.width=256;canvas.height=340;card.append(canvas);
  const ctx=canvas.getContext('2d');ctx.imageSmoothingEnabled=true;ctx.save();ctx.translate(0,10);
  if(state==='repair'){ctx.translate(128,310);ctx.scale(1,.75);ctx.translate(-128,-310);ctx.globalAlpha=.65;}
  if(state==='hurt')ctx.translate(-2,0);
  if(state!=='base')part(ctx,3);part(ctx,0);
  if(state!=='base'){part(ctx,1);part(ctx,2);part(ctx,6);ctx.save();if(state==='attack'){ctx.translate(170,206);ctx.rotate(-9*Math.PI/180);ctx.translate(-170,-206);}part(ctx,4);ctx.restore();part(ctx,7);}
  ctx.restore();line(ctx,[0,320],[256,320],'#617585');
  if(state==='idle'||state==='attack'){ctx.strokeStyle='#c6eb73';ctx.lineWidth=1;ctx.beginPath();ctx.arc(170,216,4,0,Math.PI*2);ctx.stroke();}
  const p=document.createElement('div');p.className='caption';p.textContent=caption;card.append(p);grid.append(card);
 }
 document.getElementById('status').textContent='图集已加载：'+image.naturalWidth+' × '+image.naturalHeight+'，6部件。';
 document.getElementById('details').textContent=parts.map(p=>p.id+': '+p.target.join(',')).join('  /  ');
 document.documentElement.dataset.ready='true';
}
image.onload=render;image.onerror=()=>{document.getElementById('status').textContent='图集加载失败';};
image.src='../ThreadZero/Assets/Resources/Art/RainRunnerAtlas.png';
</script></html>'''
output = root / 'Tools/DollPreview.html'
output.write_text(html.replace('__PARTS__', json.dumps(parts)), encoding='utf-8')
print(output)
