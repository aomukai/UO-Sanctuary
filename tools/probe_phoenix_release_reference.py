#!/usr/bin/env python3
"""Compile complete Phoenix tier dispatch and release producers with explicit factory services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 parent=json.loads((ROOT/'tools/reference/martial-lightning-source-evidence.json').read_text());cpp0=STAGE/'martial-lightning-source-probe.cpp';assert hashlib.sha256(cpp0.read_bytes()).hexdigest()==parent['fixture']['cppSha256'];base=cpp0.read_text().split('std::array<D2UnitStrc,80> units;',1)[0]
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());blocks={};hashes={}
 def routine(rel,name):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();s=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;d=1
  while d:d+=(s[i]=='{')-(s[i]=='}');i+=1
  body=s[m.start():i];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
 def replace(a,b):
  nonlocal base
  assert base.count(a)==1,a;base=base.replace(a,b)
 replace('struct{int nX=0;}pTargetCoords','struct{int nX=0,nY=0;}pTargetCoords')
 replace('void sub_6FCF6D70(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int,int){std::abort();}', 'void sub_6FCF6D70(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int,int);')
 replace('formula==100||formula==101?4:formula==102?0:3*rank','formula==100?10:formula==101?16:formula==102?0:3*rank')
 base+='\nconstexpr int MISSILE_ROYALSTRIKECHAINLIGHTNING=568;\nvoid sub_6FCF7390(D2UnitStrc*,int){} // Explicit initializer service; not a full path proof.\nuint32_t SEED_GetLowSeed(D2SeedStrc* p){return p->nLowSeed;}\n'
 base+=routine('source/D2Common/src/Units/Missile.cpp','MISSILE_SetTargetY')+'\n'
 base+='void D2GAME_CreateMissile_6FD115E0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int);\n'
 for name in ['sub_6FCF6D70','SKILLS_SrvDo040_RoyalStrike_ProgressiveFn1','SKILLS_SrvDo041_RoyalStrike_ProgressiveFn3']:base+=routine('source/D2Game/src/SKILLS/SkillAss.cpp',name)+'\n'
 base+=r'''std::array<D2UnitStrc,64> units;std::array<D2MissileDataStrc,64> data;std::vector<std::array<int,11>> emitted;int allocationKind=0;
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){int i=emitted.size();bool ok=!allocationKind||i%2==0;emitted.push_back({p->nX,p->nY,p->nMissile,p->nSkill,p->nSkillLevel,p->dwFlags,p->nTargetX,p->nTargetY,p->pInitArgs,int(p->pInitFunc!=nullptr),int(ok)});if(!ok)return nullptr;units[i]={};data[i]={};units[i].dwUnitType=UNIT_MISSILE;units[i].pMissileData=&data[i];return &units[i];}
void D2GAME_CreateMissile_6FD115E0(D2GameStrc* g,D2UnitStrc* u,int skill,int rank,int missile,int x,int y){D2MissileStrc p;p.pOwner=u;p.nX=x;p.nY=y;p.nMissile=missile;p.nSkill=skill;p.nSkillLevel=rank;MISSILES_CreateMissileFromParams(g,&p);}
int main(){gpSkillSrvDoFnTable_6FD40A20[40]=SKILLS_SrvDo040_RoyalStrike_ProgressiveFn1;gpSkillSrvDoFnTable_6FD40A20[143]=SKILLS_SrvDo143_FistsOfFire_RoyalStrike_ProgressiveFn;gpSkillSrvDoFnTable_6FD40A20[41]=SKILLS_SrvDo041_RoyalStrike_ProgressiveFn3;bool comma=false;printf("[");for(int count:{1,2,3})for(int rank:{0,1,20})for(int gates=0;gates<64;gates++)for(int low:{0,7,-1})for(int alloc:{0,1}){
 royal={};royal.dwFlags[0]=1;royal.wSrvPrgFunc[0]=40;royal.wSrvPrgFunc[1]=143;royal.wSrvPrgFunc[2]=41;royal.wSrvMissileA=564;royal.wSrvMissileB=568;royal.wSrvMissileC=569;royal.dwPrgCalc[0]=102;royal.dwPrgCalc[1]=100;royal.dwPrgCalc[2]=101;royal.dwParam[1]=6;row=royal;list={count,count*25,rank,280,321};currentRank=rank;saved={};saved.wResultFlags=gates&1;selected=gates&2;inRange=gates&4;enabled=gates&8;exists=gates&16;coordinates=gates&32;record=true;freed=0;SEED_InitLowSeed(&owner.pSeed,low);emitted.clear();allocationKind=alloc;sub_6FCF77E0(&game,&owner,&saved);
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%u,%u,%d,[",count,rank,gates,low,alloc,owner.pSeed.nLowSeed,owner.pSeed.nHighSeed,freed);for(size_t i=0;i<emitted.size();i++){if(i)printf(",");printf("[");for(int j=0;j<11;j++)printf("%s%d",j?",":"",emitted[i][j]);printf(",%d,%d]",emitted[i][10]?data[i].pTargetCoords.nX:0,emitted[i][10]?data[i].pTargetCoords.nY:0);}printf("]]");}printf("]\n");}
'''
 cpp=STAGE/'phoenix-release-source-probe.cpp';cpp.write_text(base);binary=cpp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(cpp),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==3456
 def roll(lo,hi=666):v=hi+0x6AC690C5*(lo&0xffffffff);return v&0xffffffff,v>>32
 def signed(n):return n if n<2**31 else n-2**32
 source=(STAGE/'d2moo-pinned-source/source/D2Game/src/SKILLS/SkillAss.cpp').read_text();start=source.index('void __fastcall sub_6FCF6D70(');body=source[start:source.index('//D2Game.0x6FCF7390',start)]
 offsets=[[int(x) for x in re.search(axis+r'Offsets\[64\]\s*=\s*\{([^}]+)',body,re.S)[1].split(',') if x.strip()] for axis in ['x','y']]
 for count,rank,gates,low,alloc,endLow,endHigh,freed,calls in rows:
  release=(gates&31)==31;active=release and rank>0;coords=bool(gates&32);owner=roll(low) if active and(count==3 or count==2 and coords) else (low&0xffffffff,666);assert(endLow,endHigh,freed)==(*owner,int(release));expected=[]
  if active and coords:
   if count==1:expected=[[10,20,564,280,rank,0,0,0,0,0]]
   elif count==2:
    lo,hi=owner[0],666
    for i in range(0,64,10):lo,hi=roll(lo,hi);expected.append([10,20,568,280,rank,3,offsets[0][i],offsets[1][i],signed(lo),1])
   else:
    lo,hi=owner[0],666
    for i in range(16):lo,hi=roll(lo,hi);x=lo%40-20;lo,hi=roll(lo,hi);y=lo%40-20;expected.append([10,20,569,280,rank,3,x or (20 if y==0 else 0),y,0,0,signed(lo)])
  assert len(calls)==len(expected),(count,rank,gates,len(calls),len(expected))
  for i,(got,want) in enumerate(zip(calls,expected)):
   ok=not alloc or i%2==0;assert got[:10]==want[:10] and got[10]==int(ok),(got,want)
   if count==1:tail=[0,0]
   elif count==2:tail=[7,0] if ok else [0,0]
   else:tail=[want[10],signed((want[6]&65535)|((want[7]&65535)<<16))] if ok else [0,0]
   assert got[11:]==tail,(got,tail)
 fixture=STAGE/'phoenix-release-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n');e=dict(referenceVersion='3.3.93847',talentId='assassin.phoenixstrike',installed=False,handlerEnabled=False,reusedCompleteComposition='tools/reference/martial-lightning-source-evidence.json',sourceHashes=hashes,compiledAdditionalRoutines=blocks,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(cpp.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Full progressive dispatcher with current prgstack0calls only the selected40/143/41tier,with rank/target/success/range/state gates. Old list removal remains a separate control.','Tier1requests one564meteor center. Tier2uses one owner draw only after valid coordinates and7step10directions,one private LCG seed per child,flags3and init callback,then sets each successful chain targetX7.','Tier3uses one owner draw before coordinate validation,then32private draws for16vector pairs;zero/zero becomes20/0. Failed allocations do not rewind RNG. Successful569children receive the current private low seed in targetX and packed uint16vector in targetY.'],services=['Complete full source callbacks,progressive getters,dispatcher,LCG and target setters execute with current supplied280row/constants.','Factory records explicit requests,alternating allocation failure,and saved target fields. Falling-meteor factory,damage,clock,collision and event services are not compiled.','Charged initializer is an explicit service pointer and is not executed; current full chain path and chaos-ice movement/damage2 remain independent gates.','Stat lists/formulas/target/range services inherit the bounded parent harness. This is not a current executable,modern consume-one or native skill certificate.'],next='Compose complete selected missile creator/damage/movement/factory/control routes before enabling Phoenix. Effects/icons remain separate.')
 out=ROOT/'tools/reference/phoenix-release-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Phoenix release evidence stale'
 else:out.write_text(encoded)
 print('Phoenix release:3456 complete tier/producer/LCG/target-setter contexts pass;factory/initializer/damage/native gates remain.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
