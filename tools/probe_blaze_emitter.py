#!/usr/bin/env python3
"""Compose the complete older Blaze emitter with the retained missile factory."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/blaze-emitter-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);prior_path=ROOT/'tools/reference/chilling-armor-source-evidence.json';prior=json.loads(prior_path.read_text())
 original=ROOT/'.local/class-skills/chilling-armor-source/probe.cpp';retained=STAGE/'reused-chilling.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 blocks={};sources={}
 for rel,name in [('source/D2Game/src/SKILLS/SkillSor.cpp','SKILLS_CreateBlazeMissile'),('source/D2Game/src/MISSILES/Missiles.cpp','MISSILES_CreateMissileFromParams')]:
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
  sources[rel]=sha(p);blocks[name]=function(raw.decode(),name)
 cpp=retained.read_text().split('int main()',1)[0];assert function(cpp,'MISSILES_CreateMissileFromParams')==blocks['MISSILES_CreateMissileFromParams']
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a;cpp=cpp.replace(a,b,1)
 replace('D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return &stat;}', 'bool blazeState=true;int blazeRank=1;constexpr int STATE_BLAZE=2,STAT_MODIFIERLIST_SKILL=90,STAT_MODIFIERLIST_LEVEL=91;D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int id){return id==STATE_BLAZE&&!blazeState?nullptr:&stat;}')
 replace('int STATLIST_GetStatValue(D2StatListStrc*,int,int){return 50;}', 'int STATLIST_GetStatValue(D2StatListStrc*,int id,int){return id==STAT_MODIFIERLIST_SKILL?46:id==STAT_MODIFIERLIST_LEVEL?blazeRank:50;}')
 cpp+='\nbool D2Common_10234(D2DynamicPathStrc* p){return (p->flags&1)!=0;}\n'+blocks['SKILLS_CreateBlazeMissile']
 cpp+=r'''
int main(){for(int n=0;n<32;n++)gdwBitMasks[n]=1u<<n;printf("[");bool first=true;
for(int guard=0;guard<8;guard++)for(int world=0;world<4;world++)for(int rank:{-1,0,1,20,255})for(int position=0;position<3;position++)for(int slowBit:{0,1}){
owner={};created={};captured={};row={};skill={};owner.dwUnitType=0;owner.dwUnitId=77;owner.x=40+(position==1);owner.y=70+(position==2);owner.pDynamicPath=guard==1?nullptr:&owner.path;owner.path.flags=guard==2?0:1;
blazeState=guard!=3;hasSkill=guard!=4;skill.wSrvMissileA=guard==5?-1:guard==6?1000:67;town=guard==7;blazeRank=rank;
hasRoom=world!=1;hasRecord=world!=2;allocated=world!=3;slow=slowBit;
// Current Range is an evaluated formula service, not the old literal column.
row.wRange=90+25*(rank<0?1:rank);row.nVel=row.nMaxVel=0;row.nCollideType=3;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_CANSLOW];
creates=draws=factoryCalls=0;SKILLS_CreateBlazeMissile(&game,&owner);
printf("%s[[%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",guard,world,rank,position,slowBit,factoryCalls,creates,captured.dwFlags,captured.nX,captured.nY,captured.nSkill,captured.nSkillLevel,created.path.velocity,created.total,created.rank);first=false;
}printf("]\n");}
'''
 for name,body in blocks.items():assert function(cpp,name)==body
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
 cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==960
 for inputs,out in cases:
  guard,world,rank,position,slow=inputs;accepted=guard==0;made=accepted and world==0;effective=1 if rank<0 else rank
  expected=[int(accepted),int(accepted and world not in (1,2)),33 if accepted else 0,40+(position==1) if accepted else 0,70+(position==2) if accepted else 0,46 if accepted else 0,effective if accepted else 0,0,90+25*effective if made else 128,effective if made else 1]
  assert out==expected,(inputs,out,expected)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 table=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';manifest=json.loads((table.parent.parent/'sources.json').read_text());assert sha(table)==manifest['files']['json/missiles.json']['sha256']
 current=next(x for x in json.loads(table.read_text()).values() if x.get('Missile')=='blaze');assert current['*ID']==67 and current['Range']=='90 +  (lvl *  25)' and current['pSrvDoFunc']==5 and current['pSrvDmgFunc']==3
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},currentTable={str(table.relative_to(ROOT)):sha(table)},currentRow=current,cases=len(cases),sanitizers='ASan/UBSan clean',established=['Complete older emitter requires movement path/flag,state/list/row,valid missile and nontown room. Each qualified callback requests one flags33factory at current caster coordinates and retained skill/rank;negative rank normalizes1. Shared complete factory retains stationary velocity and supplied evaluated lifetime even with Slow Missiles;refused factory services do not create a second request.'],declaredServices=['Older engine,not modern executable proof. The current Range expression is supplied as evaluated90+25*rank into the old literal range slot;the modern expression compiler is not executed. State/stat/movement/room/allocation/path/damage-writing services remain supplied. Full player movement caller/cadence,two-subtile mapping,cast/passive/state and typed field impact/events are not proved.'],artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),prior_path,retained,p,binary,san,log,fixture)})
 out=ROOT/'tools/reference/blaze-emitter-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Blaze emitter:960complete older emitter/factory compositions;ASan/UBSan clean;evaluated current lifetime and outer movement/field services remain explicit.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
