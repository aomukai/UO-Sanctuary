#!/usr/bin/env python3
"""Execute the unchanged lob launch and complete shared missile factory for Fire Blast."""
import argparse,hashlib,json,re,struct,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run([sys.executable,str(ROOT/'tools/probe_guided_arrow_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 shared=json.loads((ROOT/'tools/reference/guided-arrow-engine-evidence.json').read_text());tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 rel='source/D2Game/src/SKILLS/Skills.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);text=raw.decode();name='sub_6FD11710';m=re.search(r'^D2UnitStrc\* __fastcall '+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m;i=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
 body=text[m.start():i]
 base=(STAGE/'guided-arrow-source-probe.cpp').read_text().split('int main()',1)[0].replace('MISSILES_CreateMissileFromParams(','FullFactory(')
 base+=r'''
int quantityCalls=0,quantityResult=1,factoryCalls=0;
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc*){return aimX;}
int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc*){return aimY;}
int sub_6FD118C0(D2GameStrc*,D2UnitStrc*){quantityCalls++;return quantityResult;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc* g,D2MissileStrc* p){factoryCalls++;captured=*p;return FullFactory(g,p);}
'''+body+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int aim:{0,1,2,3})for(int dx:{0,1,2,8,35,99,100})for(int dy:{0,1,7})for(int offset:{0,1})for(int rank:{1,20,255})for(int type:{0,1})for(int skip:{0,1})for(int quantity:{0,1}){
 reset();owner.dwUnitType=type;row={};row.nVel=row.nMaxVel=12;row.wRange=50;row.nCollideType=6;row.dwMissileFlags=0;quantityCalls=factoryCalls=0;quantityResult=quantity;
 aimX=owner.x+dx;aimY=owner.y+dy;target.x=aimX;target.y=aimY;selected=aim==1?&target:nullptr;if(aim==3)aimX=0;
 int explicitX=aim==0?owner.x+dx:0,explicitY=aim==0?owner.y+dy:0;
 auto* result=sub_6FD11710(&game,385,&owner,251,rank,offset,offset,explicitX,explicitY,skip);
 if(!first)printf(",");first=false;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",aim,dx,dy,offset,rank,type,skip,quantity,result!=nullptr,factoryCalls,quantityCalls,captured.dwFlags,captured.nX,captured.nY,captured.nTargetX,captured.nTargetY,created.remaining,created.total,created.path.velocity,created.skill,created.rank,created.x,created.y);
}printf("]\n");}
'''
 cpp=STAGE/'fire-blast-lob-source-probe.cpp';cpp.write_text(base);binary=cpp.with_suffix('');subprocess.run(['g++','-std=c++20','-O2',str(cpp),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==4032
 for aim,dx,dy,offset,rank,type_,skip,quantity,ok,factory,quantity_calls,flags,x,y,tx,ty,current,total,velocity,skill,level,cx,cy in rows:
  valid=aim!=3;called=valid and (not skip or type_!=0 or quantity>0)
  assert factory==int(called) and quantity_calls==int(valid and skip and type_==0)
  if not called:assert not ok;continue
  assert (flags,x,y,tx,ty)==(0x420,40+offset,70+offset,40+dx,70+dy)
  ax,ay=abs(dx),abs(dy);expected_ok=ax<100 and ay<100
  assert bool(ok)==expected_ok
  if not ok:continue
  assert (cx,cy)==(40,70)
  if ax==ay==0:ax=ay=1
  distance=max(ax,ay)+min(ax,ay)//2
  assert (current,total,velocity,skill,level)==(max(1,distance)*16//9,50,2304,251,rank)
 out=STAGE/'fire-blast-lob-source-probe.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 anim=(ROOT/'.local/research/AnimData.d2').read_bytes();animhash=hashlib.sha256(anim).hexdigest();assert animhash=='e3a54b9ab71df7ccdf3655bbba5868c9d53cd8716b0aad25e8c3d5621579cecb';pos=0;animations=[]
 for group in range(256):
  count=struct.unpack_from('<I',anim,pos)[0];pos+=4
  for _ in range(count):
   row=anim[pos:pos+160];pos+=160;code=row[:8].split(b'\0')[0].decode()
   if code.startswith('AIS2'):
    frames,speed=struct.unpack_from('<ii',row,8);events=[(j,x) for j,x in enumerate(row[16:]) if x];assert(frames,speed,events)==(8,128,[(4,2)]);animations.append(dict(code=code,frames=frames,speed=speed,event=4,eventKind=2))
 assert len(animations)==10 and pos==len(anim)
 e=dict(referenceVersion='3.3.93847',talentId='assassin.fireblast',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest(),'source/D2Game/src/MISSILES/Missiles.cpp':shared['sourceHashes']['source/D2Game/src/MISSILES/Missiles.cpp'],'source/D2Common/src/Units/Units.cpp':shared['sourceHashes']['source/D2Common/src/Units/Units.cpp']},compiledUnchangedRoutines={name:hashlib.sha256(body.encode()).hexdigest(),**{k:shared['blockHashes'][k] for k in ['MISSILES_CreateMissileFromParams','UNITS_GetDistanceToCoordinates']}},fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=len(rows)),cachedAnimation=dict(path='.local/research/AnimData.d2',sha256=animhash,provenanceComplete=False,records=animations),established=['Full lob helper emits flags0x420 with recorded owner-relative launch offsets, resolved explicit/selected/fallback coordinates, captured skill251/rank and no tracked-unit pTarget. The factory ignores those offsets because flag1 is absent and uses pOrigin coordinates; native ordinary Fire Blast passes zero offsets. Fire Blast bSkipCheck0 never calls quantity consumption. Zero unresolved coordinate refuses the helper.', 'Full factory keeps total50frames, computes velocity2304 from Vel12 times256 times75percent, and assigns current lifetime floor(max(1,source distance)*16/9). Source distance is max(abs(dx),abs(dy))+floor(min/2). Same-cell moving aim is nudged diagonally before computing that duration. Axis differences>=100 refuse creation.', 'All10 cached Assassin S2 composites have8frames, speed128 and event-kind2 atframe4. These are supplementary metadata with incomplete acquisition provenance, not current3.3 executable timing proof.'],services=['Complete shared factory executes with declared room/allocation/path/stat/owner/event services. Damage collection/writing and source seed consumers remain sentinels inherited from the Guided Arrow factory harness; no combined native damage certificate is claimed.','The full source distance helper is compiled; trajectory advancement is a declared path service here. This proof establishes launch/current-frame inputs, not server movement, landing, full area/defense/event delivery or current geometry.','Unrelated Guided Arrow routines remain compiled for shared service reuse but are not called. Quantity is a declared stub exercised by controls; Fire Blast passes bSkipCheck0 and has no quantity request.'],required=['Compile complete landing hit36/hit3/hit44, current do/update and shared area/filter/defense routing before enabling native delivery.','Resolve S2 IAS/WSM/dual-claw stat merge and scheduler/frame bonus with complete source and current acceptance.','Verify child damage creation and snapshot/live-stat context, actual learned input, resources, natural travel/landing, lifecycle and two reloads.'])
 p=ROOT/'tools/reference/fire-blast-lob-engine-evidence.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Fire Blast:4032 complete lob/factory cases pass; ten cached S2 records identified; no handler enabled.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
