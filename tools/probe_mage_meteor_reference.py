#!/usr/bin/env python3
"""Reuse complete Phoenix/Fire Blast orchestration with the normal Meteor row."""
import argparse, hashlib, json, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_phoenix_meteor_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 prior=json.loads((ROOT/'tools/reference/phoenix-meteor-source-evidence.json').read_text())
 missiles=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/missiles.json').read_text())
 center,fire=missiles['meteorcenter'],missiles['meteorfire']
 assert [center[k] for k in ['*ID','pSrvDoFunc','pSrvHitFunc','Range','sHitPar1','sHitPar2','HitSubMissile1']]==[101,1,14,60,0,1,'meteorfire']
 assert [fire[k] for k in ['*ID','pSrvDoFunc','pSrvDmgFunc','Size','DamageRate','dParam1']]==[240,5,3,2,41,19]
 definition=next(x for x in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text()) if x['talentId']=='sorceress.meteor')['sourceFields']
 assert [definition[k] for k in ['Param1','Param2','Param3','Param4','localdelay']]==[6,0,30,15,30]
 base=(STAGE/'phoenix-meteor-source-probe.cpp').read_text().split('int main(){',1)[0]
 assert 'assert(id==280);' in base and '(emitted.size()-2)%18' in base
 base=base.replace('assert(id==280);','assert(id==56);').replace('(emitted.size()-2)%18','(emitted.size()-1)%18')
 base+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool comma=false;printf("[");for(int frame:{1,2,59,60})for(int rank:{0,1,10,20,255})for(int own:{0,1})for(int known:{0,1})for(int fail:{0,2})for(int kind:{0,1}){
owner={};owner.dwUnitType=kind;parent={};parent.dwClassId=101;parent.skill=56;parent.rank=rank;parent.frame=frame;parent.path.velocity=0;parent.dwMissileMode=0;parent.pDynamicPath=&parent.path;parent.pMissileData=&parent.md;row={};row.wResultFlags=5;row.dwHitFlags=2;row.wSrvHitFunc=14;row.nAlwaysExplode=1;row.nCollideKill=0;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE];row.dwHitPar[0]=0;row.dwHitPar[1]=1;row.wHitSubMissile[0]=240;skill={};skill.dwAuraRangeCalc=6;hasOwner=own;hasRecord=true;hasSkill=known;hasRoom=true;failure=fail;creates=fills=flags=areas=events=commits=defenses=ar=0;areaRadius=filter=0;noAura=-1;emitted.clear();victims={&target,&other};resultFlags.clear();fireValues.clear();target={};other={};target.dwUnitType=other.dwUnitType=1;
int result=MISSMODE_SrvDo01_BasicMissile(&game,&parent);if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",frame,rank,own,known,fail,kind,result,parent.frame,areas,areaRadius,filter,noAura);for(size_t i=0;i<emitted.size();i++){if(i)printf(",");printf("[");for(size_t j=0;j<emitted[i].size();j++)printf("%s%d",j?",":"",emitted[i][j]);printf("]");}printf("]]");}printf("]\n");}'''
 cpp=STAGE/'mage-meteor-source-probe.cpp';cpp.write_text(base);binary=cpp.with_suffix('')
 subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(cpp),'-o',str(binary)],check=True)
 rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==320
 xs=[2,-2,0,0,-3,0,3,-1,1,-1,2,-4,-3,-1,0,1,3,4];ys=[-2,-2,2,5,3,3,3,2,1,-1,-1,-2,-2,-3,-4,-3,-3,-2]
 for frame,rank,own,known,fail,kind,result,remaining,areas,radius,filt,no_aura,calls in rows:
  active=frame==1 and own and known;assert remaining==frame-1
  assert (areas,radius,filt,no_aura)==((1,6,0x8583,0) if active else (0,0,0,-1))
  length=30+(rank-1)*15 if rank else 0
  assert calls==([[240,40+x,70+y,0x8001 if length else 1,length,56,rank,0,1,int(fail!=2 or i%2==0)] for i,(x,y) in enumerate(zip(xs,ys))] if active else [])
 fixture=STAGE/'mage-meteor-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 out=ROOT/'tools/reference/mage-meteor-source-evidence.json'
 result=dict(referenceVersion='3.3.93847',installed=False,reusedComposition='tools/reference/phoenix-meteor-source-evidence.json',sourceHash=prior['sourceHash'],compiledRoutines=prior['compiledAdditionalRoutines'],fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(cpp.read_bytes()).hexdigest(),cases=320,sanitizers='ASan+UBSan'),established=['Normal Meteor center101 uses complete do1/expiry/router/hit14/area/field-producer composition directly,without Phoenix hit4 child creation. Range60 expires after60updates;the boundary compares remaining1/2/59/60.','Pinned range0override resolves skill radius6/filter0x8583/checkAura0;18fields240 inherit56/rank/owner and30+15*(rank-1)override. Missing owner/skill suppresses impact/fields;individual allocation failure does not suppress later fields.','Player/NPC owner kinds share this bounded supplied composition. Numeric packet preparation/field03/do5 animation,world geometry,actual factory allocation/global seed/event order and current executable remain separate.'])
 rendered=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==rendered
 else:out.write_text(rendered)
 print('Mage Meteor:320 complete normal-center expiry/hit14/area/field source contexts pass;world and packet consumers remain scoped.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
