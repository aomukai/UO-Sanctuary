#!/usr/bin/env python3
"""Execute complete source item-proc callbacks20/21/30 with observed cast services."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'item-proc-callbacks-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};bodies={}
 for rel,names in {'source/D2Game/src/SKILLS/SkillItem.cpp':['SKILLITEM_EventFunc20_SkillOnAttackHitKill','SKILLITEM_EventFunc21_SkillOnGetHit','SKILLITEM_EventFunc30_SkillOnDeathLevelup'],'source/D2Common/include/D2Seed.h':['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber']}.items():
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p)
  for n in names:bodies[n]=function(raw.decode(),n)
 header=BASE/'d2moo-pinned-source/source/D2Common/include/DataTbls/SkillsTbls.h';text=header.read_text();assert text.index('SKILLSFLAGINDEX_ITEMTGTDO')>text.index('SKILLSFLAGINDEX_DECQUANT = 0');assert text[text.index('SKILLSFLAGINDEX_DECQUANT = 0'):text.index('SKILLSFLAGINDEX_ITEMTGTDO')].count(',')==29;inputs[str(header.relative_to(ROOT))]=sha(header)
 cpp=r'''
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr unsigned long long operator""i64(unsigned long long value){return value;}
constexpr int SKILLSFLAGINDEX_ITEMTGTDO=29;
uint32_t gdwBitMasks[32];
union D2SeedStrc{uint64_t lSeed;struct{uint32_t nLowSeed,nHighSeed;};};
struct D2DynamicPathStrc{int x=17,y=-3;};
struct D2UnitStrc{D2SeedStrc pSeed;int tag=0;D2DynamicPathStrc*pDynamicPath=nullptr;};
struct D2GameStrc{};
struct D2DamageStrc{uint32_t dwHitFlags=0;uint16_t wResultFlags=0;};
struct D2SkillsTxt{uint32_t dwFlags[2]={};}record;
struct Tables{int nStuff=6,nShiftedStuff=63;}tables;auto*sgptDataTables=&tables;
int chance=0,castResult=0;bool exists=false;std::vector<std::vector<int64_t>>trace;
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int stat,uint16_t layer){trace.push_back({0,stat,layer});return chance;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc*p){return p->x;}int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc*p){return p->y;}
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){trace.push_back({2,id});return exists?&record:nullptr;}
int SKILLITEM_CastSkillOnTarget(D2UnitStrc*caster,int skill,int level,D2UnitStrc*target,int flag){trace.push_back({3,caster->tag,skill,level,target->tag,flag});return castResult;}
int SKILLITEM_CastSkillOnPosition(D2UnitStrc*caster,int skill,int level,int x,int y,int flag){trace.push_back({4,caster->tag,skill,level,x,y,flag});return castResult;}
'''+bodies['SEED_RollRandomNumber']+'\n'+bodies['SEED_RollLimitedRandomNumber']+r'''
uint32_t ITEMS_RollRandomNumber(D2SeedStrc*p){uint32_t n=SEED_RollRandomNumber(p);trace.push_back({1,n});return n;}
'''+ '\n'.join(bodies[n] for n in list(bodies)[:3])+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool comma=false;printf("[");
int packets[][3]={{0,0,0},{1,0,0},{1,32,0},{1,0,4},{1,32,4}};
for(int fn:{20,21,30})for(int source:{0,1})for(int target:{0,1})for(auto p:packets)for(int pct:{-1,0,1,5,100,101})for(uint64_t seed:{0ULL,2860448284845ULL,18446744073709551615ULL})for(int valid:{0,1})for(int targetDo:{0,1})for(int result:{0,1})for(int bits:{1,6,8}){
D2DynamicPathStrc path;D2UnitStrc caster,other;caster.tag=1;caster.pDynamicPath=&path;other.tag=2;caster.pSeed.lSeed=seed;D2DamageStrc damage{(uint32_t)p[1],(uint16_t)p[2]};D2GameStrc game;
chance=pct;exists=valid;castResult=result;record.dwFlags[0]=targetDo?gdwBitMasks[29]:0;tables.nStuff=bits;tables.nShiftedStuff=(1<<bits)-1;trace.clear();
uint32_t packed=((fn==21?201:fn==30?197:198)<<16)|(36<<6)|5;
int value=fn==20?SKILLITEM_EventFunc20_SkillOnAttackHitKill(&game,0,source?&caster:nullptr,target?&other:nullptr,p[0]?&damage:nullptr,(int)packed,0):fn==21?SKILLITEM_EventFunc21_SkillOnGetHit(&game,0,source?&caster:nullptr,target?&other:nullptr,p[0]?&damage:nullptr,(int)packed,0):SKILLITEM_EventFunc30_SkillOnDeathLevelup(&game,0,source?&caster:nullptr,target?&other:nullptr,p[0]?&damage:nullptr,(int)packed,0);
if(comma)printf(",");comma=true;printf("[[%d,%d,%d,%d,%d,%d,%d,\"%llu\",%d,%d,%d,%d,%u],[%d,\"%llu\"],[",fn,source,target,p[0],p[1],p[2],pct,(unsigned long long)seed,valid,targetDo,result,bits,packed,value,(unsigned long long)caster.pSeed.lSeed);
for(size_t i=0;i<trace.size();i++){printf("%s[",i?",":"");for(size_t j=0;j<trace[i].size();j++)printf("%s%lld",j?",":"",(long long)trace[i][j]);printf("]");}printf("]]");
}printf("]\n");}
'''
 for n,b in bodies.items():assert function(cpp,n)==b,n
 source=STAGE/'source.cpp';source.write_text(cpp);binary=STAGE/'probe'
 with (STAGE/'compiler.log').open('w') as f:subprocess.run(['g++','-std=c++20','-O1','-g1','-Wno-literal-suffix','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(source),'-o',str(binary)],stdout=f,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
 cases=json.loads(run.stdout);assert len(cases)==25920,len(cases);output=STAGE/'cases.json';output.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,cases=25920,inputs=inputs,completeRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},
  artifacts={str(p.relative_to(ROOT)):sha(p) for p in (source,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',output,Path(__file__).resolve())},
  established=['Three complete source callbacks20/21/30and two complete seed routines execute over absent sources/targets/packets,hit/result flags,chance bounds,three seed states,skill lookup outcomes,target-do flags,cast return failures and1/6/8bit contexts. Return status,final seed and ordered chance/RNG/lookup/cast traces are emitted.',
               'Outgoing callback20requires hitflag32only when a packet exists. Get-hit21requires packet/resultflag4. Death/levelup30has no damage gate. RNG precedes skill lookup and cast attempts;failed nested cast does not undo callback1or the draw.',
               'Callback20target-do changes caster to the target with flag0;without a target it returns1without casting. Other outgoing casts use flag1;get-hit/death/levelup use0. No-target position uses the source first path point.'],
  services=['Stat lookup,source/target/packet,immutable bit context/skill flag records and target/position casts are declared services. Cast bodies,world admission/mana/damage/spell execution are not mocked into completeness. Pure path getters are observed through resulting cast coordinates,not host argument-evaluation order.',
            'Callback metadata flag index29is checked against the pinned header. Source uint16stat layer conversion is represented by the stat lookup signature. Source LCG supplies every roll.'],
  remaining=['NPC event registration/ordering/full spell contexts,charge skill actions,full item initialization/fallback/NPC actor delivery and current executable/client acceptance remain open. No new learned talent enabled;Blood gate reuse must retain actual original-native checks.'])
 target=ROOT/'tools/reference/item-proc-callbacks-source-evidence.json';text=json.dumps(e,indent=2)+'\n'
 if check:assert target.read_text()==text,'Item proc callback source evidence stale'
 else:target.write_text(text)
 print('Item proc callbacks:25920complete source controls;20/21/30gate/decode/RNG/lookup/caster/target/position/return semantics;ASan+UBSan clean;world casts remain services.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
