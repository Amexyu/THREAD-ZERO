"""Copy the current project into the existing local repo, without moving or publishing."""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess

root=Path(__file__).resolve().parent.parent
repo=root/'THREAD-ZERO'
if not (repo/'.git').exists():raise SystemExit('Expected existing THREAD-ZERO repository')
status=subprocess.check_output(['git','-C',str(repo),'status','--porcelain'],text=True)
if status.strip():raise SystemExit('Repository changed since inspection; preserve files and review before copying')

ignore='''
# THREAD ZERO project folder (Unity project is nested one level down)
/ThreadZero/[Ll]ibrary/
/ThreadZero/[Tt]emp/
/ThreadZero/[Oo]bj/
/ThreadZero/[Ll]ogs/
/ThreadZero/[Uu]ser[Ss]ettings/
/ThreadZero/[Mm]emoryCaptures/
/ThreadZero/[Rr]ecordings/
/ThreadZero/.vs/
/Artifacts/
/Backups/
/Builds/
/.codex/
/Tools/LibraryComboTransfer/
__pycache__/
*.pyc
*.b64
'''
ignore_path=repo/'.gitignore'
current=ignore_path.read_bytes().decode('utf-8-sig')
if '# THREAD ZERO project folder' not in current:ignore_path.write_bytes((current.rstrip()+'\n'+ignore).encode('utf-8'))

copied=[]
def copy_file(src,dst):
 dst.parent.mkdir(parents=True,exist_ok=True)
 if dst.exists() and src.read_bytes()!=dst.read_bytes():raise RuntimeError('Refuse to overwrite changed destination: '+str(dst))
 shutil.copy2(src,dst);copied.append((src,dst))

for relative in ['ThreadZero/Assets','ThreadZero/Packages','ThreadZero/ProjectSettings','Docs','Tests','Tools','Deliverables/ArtPack_v12/Runtime']:
 for src in sorted((root/relative).rglob('*')):
  if not src.is_file():continue
  parts=src.relative_to(root).parts
  if 'LibraryComboTransfer' in parts or '__pycache__' in parts or src.suffix in {'.pyc','.b64'}:continue
  copy_file(src,repo/src.relative_to(root))
for name in ['README.md','CLAUDE_ART_HANDOFF.md','THREAD_ZERO_完整企划书.md','ReviewStarterDagger.cmd']:
 copy_file(root/name,repo/name)
for name in ['Dagger_Combo18_v2.png','Dagger_Combo18_Alpha.png','V12_Dagger_Combo18_v2_fast.gif','V12_Dagger_Combo18_v2_slow.gif','RuntimeLayers_QA.png','HANDOFF.md']:
 src=root/'Deliverables/ArtPack_v12/Review'/name
 copy_file(src,repo/src.relative_to(root))

for src,dst in copied:
 if hashlib.sha256(src.read_bytes()).digest()!=hashlib.sha256(dst.read_bytes()).digest():raise RuntimeError('Copy mismatch: '+str(dst))

for path in ['ThreadZero/Library/example','ThreadZero/Temp/example','ThreadZero/Logs/example','Artifacts/example','Backups/example','Builds/example']:
 result=subprocess.run(['git','-C',str(repo),'check-ignore',path],capture_output=True,text=True)
 if result.returncode!=0:raise RuntimeError('Cache not ignored: '+path)
tracked_candidates=subprocess.check_output(['git','-C',str(repo),'ls-files','--others','--exclude-standard'],encoding='utf-8').splitlines()
bad=[x for x in tracked_candidates if any(t in x.replace('\\','/').split('/') for t in ['Library','Temp','Backups','Artifacts','.codex'])]
if bad:raise RuntimeError('Unwanted generated files: '+repr(bad))
oversized=[str(p.relative_to(repo)) for p in repo.rglob('*') if p.is_file() and '.git' not in p.parts and p.stat().st_size>=100*1024*1024]
if oversized:raise RuntimeError('Oversized files: '+repr(oversized))
report={'repository':str(repo),'unityProject':str(repo/'ThreadZero'),'copiedFiles':len(copied),'copiedBytes':sum(dst.stat().st_size for _,dst in copied),'hashVerified':True,'generatedFoldersIgnored':True,'gitCommitted':False,'gitPushed':False}
(root/'Artifacts/RepoCopyReport.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
