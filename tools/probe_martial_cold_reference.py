#!/usr/bin/env python3
"""Compose complete Ice inline/stacked area/scatter and cube damage10 bodies."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_martial_lightning_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/MISSILES/MissMode.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);source=raw.decode();name='MISSMODE_SrvDmg10_BladesOfIceCubes';m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',source,re.M);i=source.index('{',m.start())+1;depth=1
 while depth:depth+=(source[i]=='{')-(source[i]=='}');i+=1
 body=source[m.start():i];base=(STAGE/'martial-lightning-source-probe.cpp').read_text().split('int main(){',1)[0]
 extraSources={};extraRoutines={}
 def inherited(rel,name):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);extraSources[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  result=text[m.start():i];extraRoutines[name]=hashlib.sha256(result.encode()).hexdigest();return result
 def replace(old,new):
  nonlocal base
  assert base.count(old)==1,old;base=base.replace(old,new)
 replace('int ITEMS_RollLimitedRandomNumber(D2SeedStrc* p,int limit){++localDraws;return limit>0?int((p->nLowSeed+5*p->index++)%unsigned(limit)):0;}',inherited('source/D2Common/include/D2Seed.h','SEED_RollLimitedRandomNumber')+'\n'+inherited('source/D2Game/src/ITEMS/Items.cpp','ITEMS_RollLimitedRandomNumber'))
 replace('int x=10,y=20,total=0,current=0;', 'int dwClassId=561,x=10,y=20,total=0,current=0;')
 replace('formula==100||formula==101?4:formula==102?0:3*rank;', 'formula==100?6:formula==101?3:formula==102||formula==0?0:3*rank;')
 replace('else d->dwColdDamage+=rank*100;', 'else{d->dwColdDamage+=rank*100;d->dwColdLen+=100+10*(rank-1);}')
 replace('hits.push_back({d->dwPhysDamage,d->dwFireDamage,d->wResultFlags,d->dwHitFlags});d->dwFireDamage+=1;', 'hits.push_back({d->dwPhysDamage,d->dwColdDamage,d->dwColdLen,d->dwFrzLen,d->wResultFlags,d->dwHitFlags});d->dwColdDamage+=1;')
 replace('u.total=u.current=p->nMissile==558?98:13;', 'u.total=u.current=p->nMissile==561?30:p->nMissile==558?98:13;')
 base+='\nstruct D2MissilesTxt{} cubeRow;bool cubeRecord=true;D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return cubeRecord?&cubeRow:nullptr;}\n'+body+r'''
int main(){gpSkillSrvDoFnTable_6FD40A20[38]=SKILLS_SrvDo038_FistsOfFire_BladesOfIce_ProgressiveFn2;gpSkillSrvDoFnTable_6FD40A20[39]=SKILLS_SrvDo039_FistsOfFire_BladesOfIce_ProgressiveFn3;printf("[");bool comma=false;
for(int count:{1,2,3})for(int rank:{0,1,20})for(int gates=0;gates<64;++gates)for(int low:{0,7,-1})for(int alloc:{0,1}){
row={};row.nEType=ELEMTYPE_COLD;row.dwCalc[0]=0;row.wSrvPrgFunc[0]=0;row.wSrvPrgFunc[1]=38;row.wSrvPrgFunc[2]=39;row.dwPrgCalc[0]=102;row.dwPrgCalc[1]=100;row.dwPrgCalc[2]=101;row.wSrvMissileC=561;row.dwAuraFilter=0x870b;
list={count,count*50,rank,274,321};currentRank=rank;saved={};saved.wResultFlags=gates&1;saved.dwPhysDamage=25600;selected=gates&2;inRange=gates&4;enabled=gates&8;exists=gates&16;coordinates=gates&32;record=true;allocationKind=alloc;SEED_InitLowSeed(&owner.pSeed,low);freed=drains=areaCalls=areaRadius=0;emitted.clear();hits.clear();sub_6FCF5870(&owner,&saved);auto inlineDamage=saved;sub_6FCF77E0(&game,&owner,&saved);
if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%u,%d,%d,%d,[",count,rank,gates,low,alloc,inlineDamage.dwPhysDamage,inlineDamage.dwColdDamage,inlineDamage.dwColdLen,inlineDamage.dwFrzLen,inlineDamage.wResultFlags,freed,owner.pSeed.nLowSeed,owner.pSeed.nHighSeed,drains,areaCalls,areaRadius);bool sep=false;for(auto h:hits){if(sep)printf(",");sep=true;printf("[%d,%d,%d,%d,%d,%d]",h[0],h[1],h[2],h[3],h[4],h[5]);}printf("],[");sep=false;for(auto x:emitted){if(sep)printf(",");sep=true;printf("[%d,%d,%d,%d,%d,%d,%d]",x[0],x[1],x[4],x[5],x[6],x[7],x[11]);}printf("],[");sep=false;for(int present:{0,1})for(int valid:{0,1}){D2DamageStrc d;d.dwColdLen=100;d.dwFrzLen=7;cubeRecord=valid;MISSMODE_SrvDmg10_BladesOfIceCubes(&game,present?&target:nullptr,&owner,&d);if(sep)printf(",");sep=true;printf("[%d,%d,%d,%d]",present,valid,d.dwColdLen,d.dwFrzLen);}printf("]]");
}printf("]\n");}
'''
 cpp=STAGE/'martial-cold-source-probe.cpp';cpp.write_text(base);binary=cpp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(cpp),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==3456
 def roll(low,hi=666):v=hi+0x6ac690c5*(low&0xffffffff);return v&0xffffffff,v>>32
 for count,rank,gates,low,alloc,physical,cold,length,freeze,flags,freed,endLow,endHigh,drains,areas,radius,hits,children,callbacks in rows:
  inline=(gates&25)==25 and rank>0;release=(gates&31)==31;active=release and rank>0;area=active and count>=2;scatter=active and count==3;coord=bool(gates&32);coldLen=100+10*(rank-1)
  assert(physical,cold,length,freeze,flags)==(25600,rank*100 if inline else 0,coldLen if inline else 0,coldLen if inline and count==3 else 0,(gates&1)|(0x4000 if inline else 0))
  assert(freed,drains,areas,radius)==(int(release),int(area),int(area and coord),6 if area and coord else 0)
  assert hits==([[0,rank*100,coldLen,0,1,1]]*2 if area and coord else [])
  owner=roll(low) if scatter else (low&0xffffffff,666);assert(endLow,endHigh)==owner
  expected=[]
  if scatter and coord:
   localLow,localHi=owner[0],666
   for _ in range(9):
    localLow,localHi=roll(localLow,localHi);x=3-localLow%6;localLow,localHi=roll(localLow,localHi);y=3-localLow%6
    if x*x+y*y<=9:expected.append([10+x,20+y,561,274,rank,1,int(not alloc or len(expected)%2==0)])
  assert children==expected,(count,rank,gates,low,children,expected)
  assert callbacks==[[p,v,100,100 if p and v else 7] for p in [0,1] for v in [0,1]]
 fixture=STAGE/'martial-cold-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n');e=dict(referenceVersion='3.3.93847',talentId='assassin.bladesofice',sourceId=274,legacyCommit=tree['sha'],reusedCompleteComposition=['tools/reference/fists-fire-source-evidence.json','tools/reference/martial-fire-release-source-evidence.json','tools/reference/martial-lightning-source-evidence.json'],sourceHashes={rel:hashlib.sha256(raw).hexdigest(),**extraSources},additionalFunctionHashes=extraRoutines,cubeDamage10FunctionHash=hashlib.sha256(body.encode()).hexdigest(),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(cpp.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Complete prgdam4consumer adds cold and SOFTHIT without physical conversion. At count3,full inline helper also adds freeze length equal to cold length under current Param5=1.','Complete progressive stack selects count2for callback38before restoring count3for callback39. Full area callback copies cold damage/length per accepting receiver; its copied packet has freeze0. Receiver mutation does not contaminate the next packet.','Full callback39draws one owner seed even when coordinates fail; successful coordinates consume18private LCG draws for9radius3attempts. Outside-circle positions are rejected,duplicates remain separate561/rank/flags1children,and failed factory allocations do not rewind RNG.','Full cube damage10copies cold length to freeze length only with a present missile and record. It preserves cold length; a missing missile/record preserves the prior freeze field.'],services=['Elemental damage/length are declared rank*100and100+10*(rank-1)providers,not compiled current skill endpoint/length getters. Only complete consumer/control ordering is established.','Full min/max/mitigation/freeze application,difficulty/cold-effect/boss/immune/chill fallback,filter0x870B,target enumeration and actual cube factory/collision/lifetime remain separate.','Area service supplies two receivers; full callback copies its packet for each. Underlying health service only records and mutates a copy.','Missile factory records source561arguments and supplies30frame lifetime; full damage10record-existence service is declared. Actual cube damage packet and freeze target state are not executed here.'],limits=['Source preparation only; no native Ice handler or freeze field is enabled.','Old full-list removal remains an oracle control; current ordinary consume-one state is separate.','Exact current executable/stat/seed/geometry/proc/PvP/NPC/client and final effects/icons remain pending.']);out=ROOT/'tools/reference/martial-cold-source-evidence.json';s=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==s,'Martial cold evidence stale'
 else:out.write_text(s)
 print('Ice:3456 complete inline/area/scatter/LCG/damage10compositions pass; current native freeze delivery remains separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
