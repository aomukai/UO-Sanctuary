#!/usr/bin/env python3
"""Own ordinary SC clock/rewind; isolate legacy signed critical ambiguity."""
import ast,hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-fury-action-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());inputs={};routines={}
 def source(rel,name):
  p=BASE/'d2moo-pinned-source'/rel;b=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);s=function(b.decode(),name);routines[name]=hashlib.sha256(s.encode()).hexdigest();return s
 template=ROOT/'tools/probe_blade_fury_timing_reference.py';inputs[str(template.relative_to(ROOT))]=sha(template)
 nodes=ast.walk(ast.parse(template.read_text()));cpp=next(n.value.value for n in nodes if isinstance(n,ast.Assign)and any(isinstance(t,ast.Name)and t.id=='cpp'for t in n.targets)and isinstance(n.value,ast.Constant))
 seqpath=BASE/'d2moo-pinned-source/source/D2Common/src/DataTbls/SequenceTbls.cpp';seq=seqpath.read_text();inputs[str(seqpath.relative_to(ROOT))]=sha(seqpath);a=seq.index('D2AnimSeqTxt gPlayerSequenceBladeFury[19]');cpp+=seq[a:seq.index('};',a)+2]+'\n'
 for rel,name in [('source/D2Common/src/DataTbls/SequenceTbls.cpp','DATATBLS_GetSequenceEvent'),('source/D2Common/src/Units/Units.cpp','UNITS_GetFrameBonus'),('source/D2Game/src/UNIT/SUnit.cpp','sub_6FCBCE70'),('source/D2Game/src/UNIT/SUnit.cpp','sub_6FCBD120')]:cpp+=source(rel,name)+'\n'
 # Existing SC owned-data/rate proof is authenticated,not regenerated.
 sc=ROOT/'tools/reference/shadow-buff-animation-source-evidence.json';d=json.loads(sc.read_text());inputs[str(sc.relative_to(ROOT))]=sha(sc)
 for p,h in (d['inputs']|d['artifacts']).items():assert sha(ROOT/p)==h,p
 cpp=cpp.replace('int dwUnitType=0,dwClassId=6,dwAnimMode=18','int dwUnitType=1,dwClassId=417,dwAnimMode=7')
 critical=source('source/D2Common/src/Units/Missile.cpp','MISSILE_HasBonusStats')
 # Same complete routine with only cast removal/name changed is a concrete
 # proposed adaptation;it is explicitly not labelled unchanged source.
 proposal=critical.replace('MISSILE_HasBonusStats(','PROPOSED_UnsignedCritical(').replace('(int)SEED_RollRandomNumber','SEED_RollRandomNumber')
 cpp+='\n#define BOOL int\n#define TRUE 1\n#define FALSE 0\nconstexpr int STAT_PASSIVE_CRITICAL_STRIKE=1,STAT_ITEM_DEADLYSTRIKE=2;int passive=0,deadly=0,mastery=0,draws=0;uint32_t randoms[3];\nint STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return passive;}\nint STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int,int){return deadly;}\nint SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*,void*,int kind){assert(kind==2);return mastery;}\nuint32_t SEED_RollRandomNumber(uint64_t*){assert(draws<3);return randoms[draws++];}\n'
 # Harness unit seed lane is an ordinary uint64;the critical routine only
 # passes its address to the declared random service.
 cpp=cpp.replace('struct D2UnitStrc{int dwUnitType','struct D2UnitStrc{uint64_t pSeed=0;int dwUnitType')
 cpp+=critical+'\n'+proposal+r'''
int main(){printf("{\"initial\":[");bool first=true;
for(int speed:{64,128,170,256,341,448}){owner={};owner.wAnimSpeed=speed;owner.dwFrameCountPrecise=17*256;owner.pAnimSeq=nullptr;anim={};anim.pFrameFlags[9]=1;events.clear();game.dwGameFrame=1000;sub_6FCBCE70(&game,&owner);printf("%s[%d,%d,[",first?"":",",speed,owner.nSeqCurrentFramePrecise);first=false;bool sep=false;for(auto e:events){printf("%s[%d,%d,%d]",sep?",":"",e.type,e.frame,e.event);sep=true;}printf("]]");}
printf("],\"rewind\":[");first=true;
for(int speed:{64,128,170,256,341,448})for(int elapsed:{8,9,10,20}){owner={};owner.wAnimSpeed=speed;owner.dwFrameCountPrecise=17*256;owner.nSeqCurrentFramePrecise=(1000-elapsed)*256;anim={};anim.pFrameFlags[9]=1;events={{1,1001,1,0},{2,1017,0,0},{3,1021,0,0}};game.dwGameFrame=1000;sub_6FCBD120(&game,&owner,1);printf("%s[%d,%d,%d,[",first?"":",",speed,elapsed,owner.nSeqCurrentFramePrecise);first=false;bool sep=false;for(auto e:events){printf("%s[%d,%d,%d]",sep?",":"",e.type,e.frame,e.event);sep=true;}printf("]]");}
printf("],\"critical\":[");first=true;
for(int context=0;context<4;context++)for(uint32_t r:{0u,99u,0x80000000u,0xFFFFFFFFu}){passive=context==3?25:0;deadly=context>=2?25:0;mastery=context>=1?25:0;randoms[0]=r;randoms[1]=99;randoms[2]=0;draws=0;int legacy=MISSILE_HasBonusStats(&owner,&owner),ld=draws;draws=0;int proposed=PROPOSED_UnsignedCritical(&owner,&owner),pd=draws;printf("%s[%d,%d,%d,%u,%u,%u,%d,%d,%d,%d]",first?"":",",passive,deadly,mastery,randoms[0],randoms[1],randoms[2],legacy,ld,proposed,pd);first=false;}printf("]}\n");}
'''
 generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 r=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr.decode();cases=json.loads(r.stdout);assert [len(cases[k])for k in ['initial','rewind','critical']]==[6,24,16]
 assert any(c[6]!=c[8]for c in cases['critical'])
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],contexts=46,contextsByKind={k:len(v)for k,v in cases.items()},completeRoutines=routines,inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [generated,binary,fixture,STAGE/'compiler.log',STAGE/'sanitizer.log',Path(__file__).resolve()]},scope='Complete initial/ordinary rewind on owned SC17/event9 geometry,6speeds/24rewinds.16critical comparisons compile full unchanged legacy MISSILE_HasBonusStats and separately named explicitly changed unsigned proposal. Target/chance/stat/random/event insert/delete services supplied;no automatic critical policy qualified. Existing SC conversion/rates/resource and NPCcallback0 proof reused,no prior matrices rerun. FIFO scheduled-state-before-renewed-action ordering reviewed in Event.cpp;modern executable remains unverified.')
 (ROOT/'tools/reference/shadow-fury-action-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print('Own SC Fury:30complete clock/rewind controls;16legacy-critical comparisons isolate proposed unsigned behavior;ASan/UBSan clean.')
if __name__=='__main__':main()
