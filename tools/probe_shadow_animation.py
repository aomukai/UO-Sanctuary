#!/usr/bin/env python3
"""Owned Shadow S2 rows and complete transformed NPC rate/event controls."""
import hashlib,itertools,json,re,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-animation-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);inputs={}
 def pin(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_bytes()
 parent=json.loads(pin(ROOT/'tools/reference/valkyrie-animation-source-evidence.json'))
 template=BASE/'valkyrie-animation-source/source.cpp';assert sha(template)==parent['artifacts'][str(template.relative_to(ROOT))]
 cpp=pin(template).decode().split('int main(){')[0]
 replacements={
  'dwAnimSpeed=256':'dwAnimSpeed=128','dwClassId=357,dwAnimMode=4':'dwClassId=417,dwAnimMode=9','dwFrameCountPrecise=18*256':'dwFrameCountPrecise=8*256',
  'wGfxClass=0':'wGfxClass=6','transformStates[]={93}':'transformStates[]={119}','nStatesTxtRecordCount=94':'nStatesTxtRecordCount=120','assert(id==93);return true':'assert(id==119);return true',
  'int ias=0,attack=100,move=100,fasterMove=0;':'D2UnitStrc rightWeapon,leftWeapon;int rightRate=0,leftRate=0,merges=0;int ias=0,attack=100,move=100,fasterMove=0;',
  'int STATLIST_UnitGetStatValue(D2UnitStrc*,int id,int){assert(id==67||id==68);return id==67?move:attack;}':'int STATLIST_UnitGetStatValue(D2UnitStrc*u,int id,int){assert(id==67||id==68);return u==&rightWeapon?rightRate:u==&leftWeapon?leftRate:id==67?move:attack;}',
  'void UNITS_MergeDualWieldWeaponStatLists(D2UnitStrc*,int one){assert(one==1);}':'void UNITS_MergeDualWieldWeaponStatLists(D2UnitStrc*,int one){assert(one==1);merges++;}',
  'bool UNITS_CanDualWield(D2UnitStrc*){return false;}':'bool UNITS_CanDualWield(D2UnitStrc*){return true;}',
  'D2UnitStrc*INVENTORY_GetItemFromBodyLoc(void*,int){assert(false);return nullptr;}':'D2UnitStrc*INVENTORY_GetItemFromBodyLoc(void*,int body){return body==4?&rightWeapon:body==5?&leftWeapon:nullptr;}',
  'bool ITEMS_CanBeEquipped(D2UnitStrc*){assert(false);return false;}bool ITEMS_CheckItemTypeId(D2UnitStrc*,int){assert(false);return false;}':'bool ITEMS_CanBeEquipped(D2UnitStrc*u){return u!=nullptr;}bool ITEMS_CheckItemTypeId(D2UnitStrc*u,int type){assert(type==45);return u!=nullptr;}',
 }
 for old,new in replacements.items():assert cpp.count(old)==1,old;cpp=cpp.replace(old,new)
 tree=json.loads(pin(BASE/'d2moo-source-tree.json'));bodies={}
 for rel,names in [('source/D2Common/src/D2Skills.cpp',['D2COMMON_11013_ConvertMode']),('source/D2Common/src/Units/Units.cpp',['UNITS_GetFrameBonus','UNITS_UpdateAttackAnimRateAndVelocity']),('source/D2Game/src/UNIT/SUnit.cpp',['sub_6FCBCE70'])]:
  p=BASE/'d2moo-pinned-source'/rel;raw=pin(p);assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel)
  for name in names:bodies[name]=function(raw.decode(),name);assert function(cpp,name)==bodies[name]
 ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads(pin(ref/'sources.json'))
 def table(name):
  p=ref/'json'/(name+'.json');assert sha(p)==manifest['files']['json/'+name+'.json']['sha256'];return json.loads(pin(p))
 states=table('states');skills=table('skills');assert {k:states['shadowwarrior'][k]for k in ['*ID','transform','gfxtype','gfxclass']}=={'*ID':119,'transform':1,'gfxtype':2,'gfxclass':6}
 assert skills['268']['aurastate']==skills['279']['aurastate']=='shadowwarrior'and skills['251']['monanim']=='S2'and skills['251']['UseAttackRate']==1
 meta=json.loads(pin(ROOT/'data/Sanctuary/Balance/npc-skill-metadata.json'));assert meta[251]['Mode']==9
 local=json.loads(pin(ROOT/'tools/reference/d2r-local-animation-evidence.json'));rawpath=ROOT/local['localRawPath'];assert sha(rawpath)==local['sha256'];raw=pin(rawpath);cursor=0;rows=[]
 for _ in range(256):
  count=int.from_bytes(raw[cursor:cursor+4],'little');cursor+=4
  for _ in range(count):
   r=raw[cursor:cursor+160];cursor+=160;code=r[:8].split(b'\0')[0].decode('ascii')
   if code in ['AIS2HT2','AIS2HT1','AIS2HTH']:rows.append(dict(Code=code,Frames=int.from_bytes(r[8:12],'little'),Speed=int.from_bytes(r[12:16],'little'),Events=[[i,v]for i,v in enumerate(r[16:])if v]))
 assert cursor==len(raw)and len(rows)==3 and all(r['Frames']==8 and r['Speed']==128 and r['Events']==[[4,2]]for r in rows)
 data=ROOT/'data/Sanctuary/Balance/shadow-animation.json';data.write_text(json.dumps(dict(ReferenceVersion='3.3.93847',State=119,GraphicsType=2,GraphicsClass=6,ActorMode=9,GraphicsMode=14,Rows=sorted(rows,key=lambda r:r['Code'])),indent=2)+'\n');pin(data)
 cpp+=r'''
int main(){D2StatesTxt states[120];tables.pStatesTxt=states;int count;assert(scanf("%d",&count)==1);printf("[");
for(int i=0;i<count;i++){D2UnitStrc actor;D2GameStrc game;assert(scanf("%d%d%d%d%d%d",&actor.dwClassId,&ias,&attack,&rightRate,&leftRate,&game.dwGameFrame)==6);int kind=1,cls=actor.dwClassId,mode=9;D2COMMON_11013_ConvertMode(&actor,&kind,&cls,&mode,__FILE__,__LINE__);anim={};anim.pFrameFlags[4]=2;merges=0;events.clear();int bonus=UNITS_GetFrameBonus(&actor);UNITS_UpdateAttackAnimRateAndVelocity(&actor);sub_6FCBCE70(&game,&actor);
printf("%s[%d,%d,%d,%d,%d,%d,[",i?",":"",kind,cls,mode,bonus,actor.wAnimSpeed,merges);bool sep=false;for(auto e:events){printf("%s[%d,%d,%d,%d]",sep?",":"",e.type,e.frame,e.kind,e.param);sep=true;}printf("]]");}printf("]\n");}
'''
 cases=[dict(Actor=c,Rates=[ias,attack,r,l,frame])for c,ias,attack,r,l,frame in itertools.product([417,418],[0,10,40,120,300],[15,30,100,150,200],[-30,0,20],[-30,0,20],[0,37])]
 path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe'
 compile=subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-omit-frame-pointer','-no-pie',str(path),'-o',str(binary)],capture_output=True,text=True);(STAGE/'compiler.log').write_text(compile.stdout+compile.stderr);assert compile.returncode==0,(compile.stdout+compile.stderr)[-3000:]
 request=' '.join(map(str,[len(cases)]+[v for c in cases for v in [c['Actor'],*c['Rates']]]))+'\n';(STAGE/'input.txt').write_text(request)
 run=subprocess.run([str(binary)],input=request,capture_output=True,text=True);(STAGE/'sanitizer.log').write_text(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr
 output=json.loads(run.stdout);assert len(output)==len(cases)
 for c,result in zip(cases,output):assert result[:4]==[0,6,14,0]and result[5]==1;c['Expected']=result
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n');pin(Path(__file__).resolve())
 evidence=dict(referenceVersion='3.3.93847',contexts=len(cases),completeRoutineHashes={n:hashlib.sha256(s.encode()).hexdigest()for n,s in bodies.items()},inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,binary,fixture]},scope='Owned state119/Assassin6 transforms both species;monster S2mode9 becomes playerS2mode14,frame bonus0. Owned8frame/speed128/flag2at4 rows;complete older conversion,bonus,dual-wield rate and event routines. Numeric input getters already include selected-hand merge;actual graph merge is a native consumer check. Modern full executable parity is not claimed.')
 (ROOT/'tools/reference/shadow-animation-source-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n');print(f'Shadow S2:{len(cases)} complete source contexts;four routines;owned transform/rows;ASan/UBSan clean.')
if __name__=='__main__':main()
