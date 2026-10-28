#!/usr/bin/env python3
"""Run complete shared NPC reaction/typed-class/recovery bodies offline."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'monster-return'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(exist_ok=True);treepath=BASE/'d2moo-source-tree.json';tree=json.loads(treepath.read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={str(treepath.relative_to(ROOT)):sha(treepath)}
 def source(name):
  p=BASE/'d2moo-pinned-source'/name;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==name);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 routines={}
 def full(name,body):
  text=function(body,name);routines[name]=hashlib.sha256(text.encode()).hexdigest();return text
 proofpath=ROOT/'tools/reference/molten-boulder-impact-source-evidence.json';proof=json.loads(proofpath.read_text());inputs[str(proofpath.relative_to(ROOT))]=sha(proofpath)
 original=BASE/'molten-boulder-impact-source-probe.cpp';inputs[str(original.relative_to(ROOT))]=sha(original);cpp=original.read_text().split('int main()',1)[0]
 originals={**proof['reusedRoutineHashes'],**proof['routineHashes']}
 def retained_hash(name,digest):
  actual=hashlib.sha256(function(cpp,name).encode()).hexdigest()
  if name.startswith('SEED_'):
   raw=function(source('source/D2Common/include/D2Seed.h'),name);assert hashlib.sha256(raw.encode()).hexdigest()==digest
   assert actual==hashlib.sha256(raw.replace('0x6AC690C5i64','0x6AC690C5ULL').encode()).hexdigest(),name
  else:assert actual==digest,name
  return actual
 for name,digest in originals.items():retained_hash(name,digest)
 damage=source('source/D2Game/src/UNIT/SUnitDmg.cpp');monster=source('source/D2Game/src/MONSTER/MonsterMode.cpp');source('source/D2Game/include/UNIT/SUnitDmg.h')
 hit=re.search(r'enum D2C_HitClassIds\s*\{.*?\};',source('source/D2Common/include/D2DataTbls.h'),re.S).group()
 monids=source('source/D2Common/include/DataTbls/MonsterIds.h');monbody=re.sub(r'^\s*#.*$','',re.sub(r'//[^\n]*','',monids.split('{',1)[1].split('}',1)[0]),flags=re.M);ids={};value=-1
 for entry in monbody.split(','):
  entry=entry.strip()
  if not entry:continue
  if '=' in entry:
   name,n=entry.split('=',1);n=n.strip();value=ids[n] if n in ids else int(n,0)
  else:name=entry;value+=1
  ids[name.strip()]=value
 modes=source('source/D2Common/include/D2Monsters.h');assert all(re.search(name+r',\s*//0x'+n,modes) for name,n in [('MONMODE_GETHIT','03'),('MONMODE_BLOCK','06'),('MONMODE_DEAD','0C'),('MONMODE_KNOCKBACK','0D')])
 unit=source('source/D2Common/include/Units/Units.h');assert re.search(r'UNITFLAG_UPGRLIFENHITCLASS\s*=\s*0x00008000',unit)
 def replace(a,b):
  nonlocal cpp
  assert cpp.count(a)==1,a[:120];cpp=cpp.replace(a,b,1)
 cpp='#include <vector>\n#include <array>\n#define __stdcall\n'+cpp
 replace('int requested=-1;', 'std::vector<std::array<int,3>> trace;bool hasRoom=true,inTown=false,hasRow=true,hasModes=true,frozen=false,stunned=false,delay=false;int baseId=0,maxLife=25600,currentPercent=0,classCalls=0,recoveryCalls=0;int requested=-1;')
 for name in ['MONSTER_DIABLO','MONSTER_DIABLOCLONE','MONSTER_UBERDIABLO']:
  cpp,n=re.subn(r'\b'+name+r'=\d+',name+'='+str(ids[name]),cpp,count=1);assert n==1
 replace('UNITFLAG_UPGRLIFENHITCLASS=83','UNITFLAG_UPGRLIFENHITCLASS=0x8000')
 replace('D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return &row;}', 'D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int cls){trace.push_back({3,cls,hasRow});return hasRow?&row:nullptr;}')
 replace('void SUNITDMG_KillMonster(D2GameStrc*,D2UnitStrc* p,D2UnitStrc* attacker,int petKill){assert(p==&target&&attacker==&actor&&petKill==1);kills++;p->dwAnimMode=0;}', 'void SUNITDMG_KillMonster(D2GameStrc*,D2UnitStrc* p,D2UnitStrc* attacker,int petKill){assert(p==&target&&attacker==&actor&&petKill==1);trace.push_back({11,0,0});kills++;p->dwAnimMode=0;}')
 replace('void MONSTERMODE_GetModeChangeInfo(D2UnitStrc* p,int mode,D2ModeChangeStrc* out){out->mode=mode;out->pUnit=p;}', 'void MONSTERMODE_GetModeChangeInfo(D2UnitStrc* p,int mode,D2ModeChangeStrc* out){assert(p==&target);trace.push_back({9,mode,0});out->mode=mode;out->pUnit=p;}')
 replace('requested=mode->mode;modes++;}', 'trace.push_back({10,mode->mode,mode->pTargetUnit==&actor});requested=mode->mode;modes++;}')
 replace('int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*){return 25600;}', 'int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*){trace.push_back({18,maxLife,0});return maxLife;}')
 replace('D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return false;}', 'D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){trace.push_back({1,hasRoom,0});return hasRoom?&room:nullptr;}bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){trace.push_back({2,inTown,0});return inTown;}')
 replace('bool uninterruptible=false;bool STATES_CheckState(D2UnitStrc* p,int state){return p==&target&&state==STATE_UNINTERRUPTABLE&&uninterruptible;}', 'constexpr int STATE_FREEZE=1001;constexpr BOOL TRUE=1,FALSE=0;bool uninterruptible=false;bool STATES_CheckState(D2UnitStrc* p,int state){assert(p==&target);bool answer=state==STATE_UNINTERRUPTABLE?uninterruptible:state==STATE_FREEZE?frozen:false;trace.push_back({5,state,answer});return answer;}')
 replace('void sub_6FC62DF0(D2UnitStrc* p,D2DamageStrc*){assert(p==&target);}', 'int D2GAME_GetMonsterBaseId_6FC64B10(D2UnitStrc* p){assert(p==&target);trace.push_back({7,baseId,0});return baseId;}\nconstexpr int MONSTER_SANDLEAPER1='+str(ids['MONSTER_SANDLEAPER1'])+';\n'+full('sub_6FC62DF0',monster))
 replace('void UNITROOM_RefreshUnit(D2UnitStrc*){}void MONSTER_SetAiState(D2UnitStrc*,unsigned){}void sub_6FC6E8A0(D2GameStrc*,D2UnitStrc*){}', 'void UNITROOM_RefreshUnit(D2UnitStrc*){trace.push_back({12,0,0});}void MONSTER_SetAiState(D2UnitStrc*,unsigned n){trace.push_back({13,int(n),0});}void sub_6FC6E8A0(D2GameStrc*,D2UnitStrc*){trace.push_back({14,0,0});}')
 replace('int SUNITDMG_GetHitClass(D2DamageStrc*,int n){return n;}', 'struct D2DamageHitClassMappingStrc{size_t nDamageStrcOffset;uint32_t nHitClass;};\nnamespace OriginalHitClass{\n'+full('SUNITDMG_GetHitClass',damage)+'\n}\nint SUNITDMG_GetHitClass(D2DamageStrc* p,uint32_t n){trace.push_back({4,classCalls,int(n)});classCalls++;return OriginalHitClass::SUNITDMG_GetHitClass(p,n);}')
 replace('bool sub_6FCC1870(D2UnitStrc*,D2DamageStrc*,int){return true;}', hit+'\nD2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int);\nnamespace OriginalRecovery{\n'+full('sub_6FCC1870',damage)+'\n}\nbool sub_6FCC1870(D2UnitStrc* p,D2DamageStrc* d,int cls){recoveryCalls++;trace.push_back({16,cls,d->dwDmgTotal});return OriginalRecovery::sub_6FCC1870(p,d,cls);}')
 replace('int sub_6FC62F50(D2UnitStrc*){return 0;}', full('sub_6FC62F50',monster))
 replace('D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return &mon2;}', 'D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int cls){trace.push_back({8,cls,hasModes});return hasModes?&mon2:nullptr;}')
 replace('D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return nullptr;}', 'D2StatListStrc stunList;D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* p,int state){assert(p==&target&&state==STATE_STUNNED);trace.push_back({15,state,stunned});return stunned?&stunList:nullptr;}')
 replace('UNUSED_VOID(STATES_ToggleState)', 'void STATES_ToggleState(D2UnitStrc* p,int state,int on){assert(p==&target&&state==STATE_DEATH_DELAY&&on==1);delay=true;trace.push_back({6,state,on});}')
 ret=full('SUNITDMG_ExecuteMissileDamage',damage);assert function(cpp,'SUNITDMG_ExecuteMissileDamage')==ret
 retained={}
 for name,digest in originals.items():retained[name]=retained_hash(name,digest)
 # Link namespaces only instrument calls around unchanged bodies. Their local
 # process-wide static counter and actual source seed routines are retained.
 cpp+=r'''
bool first=true;
void Emit(int tag,int kind,int roomKind,int rowPresent,int townFlag,int guard,int mode,int flags,int cls,int sand,int modeMask,int stun,int freeze,int hit,int active,int typed,int amount,int poisonOnly,int maximum,int hp,int sent,uint32_t seed){
actor={};target={};game={};row={};mon2={};actor.pGame=target.pGame=&game;actor.dwUnitType=kind==6?UNIT_MONSTER:kind;target.dwUnitType=UNIT_MONSTER;target.dwUnitId=99;target.dwClassId=cls;target.dwAnimMode=mode;target.dwLastHitClass=777;target.dwFlags=0x10000000;
target.pSeed.nLowSeed=seed;target.pSeed.nHighSeed=666;target.stats[STAT_HITPOINTS]=hp;target.stats[STAT_LAST_SENT_HP_PCT]=sent;
hasRoom=roomKind>=0;inTown=roomKind==1;hasRow=rowPresent;row.dwMonStatsFlags=townFlag?gdwBitMasks[MONSTATSFLAGINDEX_INTOWN]:0;uninterruptible=guard;hasModes=modeMask>=0;mon2.dwModeFlags=modeMask<0?0:modeMask;stunned=stun;frozen=freeze;baseId=sand?MONSTER_SANDLEAPER1:0;maxLife=maximum;delay=false;kills=modes=recoveryCalls=0;requested=-1;trace.clear();int before=classCalls;
D2DamageStrc d{};d.wResultFlags=flags;d.dwHitClass=hit;d.nHitClassActiveSet=active;d.dwDmgTotal=amount;d.dwColdDamage=typed&1?512:0;d.dwFireDamage=typed&2?512:0;d.dwLtngDamage=typed&4?512:0;d.dwPoisDamage=typed&8?512:0;if(poisonOnly)d.dwPoisDamage=amount;
SUNITDMG_ExecuteMissileDamage(&game,kind==6?nullptr:&actor,&target,&d);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u],[%u,%u,%d,%u,%d,%d,%d,%d,%llu,%d,%d,%d,%d,%d],[",first?"":",",tag,kind,roomKind,rowPresent,townFlag,guard,mode,flags,cls,sand,modeMask,stun,freeze,hit,active,typed,amount,poisonOnly,maximum,hp,sent,seed,d.wResultFlags,d.dwHitClass,target.dwLastHitClass,target.dwFlags,requested,delay,kills,modes,(unsigned long long)target.pSeed.lSeed,before,classCalls,recoveryCalls,target.dwAnimMode,target.stats[STAT_HITPOINTS]);
for(size_t i=0;i<trace.size();i++)printf("%s[%d,%d,%d]",i?",":"",trace[i][0],trace[i][1],trace[i][2]);printf("]]");first=false;
}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");
int all=(1<<MONMODE_BLOCK)|(1<<MONMODE_GETHIT)|(1<<MONMODE_KNOCKBACK);
for(int kind:{0,1,2,6})for(int room:{-1,0,1})for(int row:{0,1})for(int town:{0,1})for(int guard:{0,1})for(int mode:{0,1,8,12,14})for(int flag:{0,1,2,3,4,5,16,16385})Emit(0,kind,room,row,town,guard,mode,flag,413,0,all,0,0,3,0,0,512,0,25600,25600,128,123);
for(int flag:{0,1,2,3,4,5,8,9,10,12,16,17,18,20,24,16384,16385,16388,16392,16400,16403})for(int cls:std::array<int,4>{413,MONSTER_DIABLO,MONSTER_DIABLOCLONE,MONSTER_UBERDIABLO})for(int sand:{0,1})for(int mask:{-1,0,1<<MONMODE_BLOCK,1<<MONMODE_GETHIT,1<<MONMODE_KNOCKBACK,all})for(int stun:{0,1})for(int freeze:{0,1})Emit(1,1,0,1,0,0,1,flag,cls,sand,mask,stun,freeze,3,0,0,512,0,25600,25600,128,123);
for(int hit:{0,1,2,3,4,5,6,7,8,10,11,13,0x60,0x22})for(int stun:{0,1})for(int freeze:{0,1})for(int poison:{0,1})for(int maximum:{25600,100003}){
 int divisor=hit==2||hit==6||hit==10||hit==11?8:hit==5?64:hit==4||hit==8?32:16;
 std::vector<int> amounts={0,255,256,maximum/divisor-1,maximum/divisor,maximum/(divisor/2)-1,maximum/(divisor/2),maximum/(divisor/4)-1,maximum/(divisor/4),maximum};std::sort(amounts.begin(),amounts.end());amounts.erase(std::unique(amounts.begin(),amounts.end()),amounts.end());
 for(int amount:amounts)for(uint32_t seed:{0u,1u,123u})for(int mask:{-1,0,all})Emit(2,1,0,1,0,0,1,5,413,0,mask,stun,freeze,hit,1,0,amount,poison,maximum,maximum,128,seed);
}
for(int hit:{0,1,2,3,4,5,6,7,8,10,11,13,0x60,0x22})for(int active:{0,1})for(int typed=0;typed<16;typed++)for(int flag:{0,1,8193,3})Emit(3,1,0,1,0,1,1,flag,413,0,all,0,0,hit,active,typed,512,0,25600,25600,128,123);
for(int sent:{0,4,5,64,128})for(int hp:{0,255,256,12800,25599,25600,51200})for(int amount:{0,1,255,256})for(int flag:{0,1,16385})Emit(4,1,0,1,0,0,1,flag,413,0,all,0,0,3,1,0,amount,0,25600,hp,sent,123);
printf("]\n");}
'''
 path=STAGE/'source.cpp';path.write_text(cpp);outputs=[];artifacts=[path,*list((STAGE/'diagnostics').iterdir())]
 for ubers in [False,True]:
  suffix='ubers' if ubers else 'classic';binary=STAGE/suffix;compiler=STAGE/(suffix+'-compiler.log');san=STAGE/(suffix+'-sanitizer.log')
  with compiler.open('w') as f:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',*(['-DD2_VERSION_HAS_UBERS'] if ubers else []),str(path),'-o',str(binary)],stdout=f,stderr=subprocess.STDOUT,check=True)
  run=subprocess.run([str(binary)],capture_output=True);san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:];cases=json.loads(run.stdout);assert len(cases)==30244 and Counter(p[0][0] for p in cases)==Counter({0:3840,1:4032,2:20160,3:1792,4:420})
  out=STAGE/(suffix+'.json');encoded=json.dumps(cases,separators=(',',':'))+'\n'
  if check:assert out.read_text()==encoded
  else:out.write_text(encoded)
  artifacts += [compiler,san,out];outputs.append(cases)
 # Invariants use the complete output,including service order and RNG state.
 totals=[]
 for ubers,cases in zip([False,True],outputs):
  stats=dict(cases=len(cases),deferred=0,kills=0,stunGetHit=0,sandMissingKnockback=0,recoveryDraws=[0,0,0],classCalls=0,fractionalFloorDraws=0)
  for p,o,t in cases:
   tag,kind,room,row,town,guard,mode,flags,cls,sand,mask,stun,freeze,hit,active,typed,amount,poison,maximum,hp,sent,seed=p
   f,h,last,unit,requested,delay,kills,modes,final,before,after,recovery,animation,life=o
   assert life==hp
   rejectedTown=room==1 and (kind!=1 or not row or not town)
   if rejectedTown:assert o[:8]==[flags,hit,777,0x10000000,-1,0,0,0] and before==after and recovery==0
   else:
    assert delay==int(bool(guard and flags&2))
    if guard:assert kills==modes==0
   if not rejectedTown and not guard and kind!=6 and mode not in [0,12]:
    assert kills==int(bool(f&2))
    if f&2:assert requested==-1
    if requested==MON_GH and stun:stats['stunGetHit']+=1
    if requested==13 and sand and not freeze and (mask<0 or not(mask&(1<<13))):stats['sandMissingKnockback']+=1
   s=(666<<32)|seed;possible=[]
   for n in range(3):
    if s==final:possible.append(n)
    s=(s&0xffffffff)*0x6ac690c5+(s>>32)
   assert len(possible)==1,(p,o);stats['recoveryDraws'][possible[0]]+=1
   stats['deferred']+=delay;stats['kills']+=kills;stats['classCalls']+=after-before
   divisor=8 if h in [2,6,10,11] else 64 if h==5 else 32 if h in [4,8] else 16
   if tag==2 and amount>=256 and amount*divisor<maximum and possible[0]>0:stats['fractionalFloorDraws']+=1
  assert all(stats[k]>0 for k in ['deferred','kills','stunGetHit','sandMissingKnockback','classCalls','fractionalFloorDraws']) and all(stats['recoveryDraws'])
  totals.append(stats)
 assert len(outputs[0])==len(outputs[1]);differences=sum(a!=b for a,b in zip(*outputs));assert differences>0
 inputs[str(Path(__file__).relative_to(ROOT))]=sha(Path(__file__))
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],component='Shared complete NPC damage return,hit class and recovery decision source',processMemoryRead=False,currentExecutableParity=False,nativeConsumerAccepted=False,handlerEnabled=False,compiledUnchangedRoutineHashes=routines,retainedRoutineHashes=retained,retainedOriginalRoutineHashes=originals,inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},sourceMonsterIds={k:ids[k] for k in ['MONSTER_DIABLO','MONSTER_DIABLOCLONE','MONSTER_UBERDIABLO','MONSTER_SANDLEAPER1']},variants=dict(classic=totals[0],ubers=totals[1],differentCases=differences),
 established=['Complete return,typed-hit-class,recovery,sand-leaper adjustment and HP-percentage routines run unchanged under ASan/UBSan. Existing complete missile/health source is reused without regenerating its accepted stage. Link namespaces only instrument calls around unchanged class/recovery bodies;the inherited seed literal i64-to-ULL platform shim is checked against the original pinned function.', 'Town/row/source and missing-room ordering,hit-class-before-uninterruptible/null-source/dead-mode guards,cached death before reaction,block species exceptions,Stun bypass of ordinary recovery suppression,and source two-stage RNG/integer thresholds are recorded. Sand-leaper adjustment runs after the initial knockback-mode fallback. Both compile-time Uber branches are retained.', 'Nondivisible FP maximum-life thresholds use integer division before comparison;the matrix records RNG draws where an aggregated multiply comparison would suppress recovery. This is a separate exact-source consumer requirement;the existing native generic recovery is not changed here.', 'Typed hit class has a process-wide rotating four-element scan with a counter that advances only when its helper is called;magic is absent from that scan. The complete recovery helper uses the full final hit class,FP total,integer maximum-life thresholds and zero,one or two actual source RNG draws.'],
 services=['NPC targets only;all player-only branches retain guarded services. Source monster base-ID,row/mode/state presence,room/town,stat storage,mode requests,kill,deferred-state application,network refresh and AI notifications are explicit fixture services. No real death/animation/path scheduling is supplied. Source stat/state indices and ancillary layout remain declared;monster mode/base-hit-class IDs and life/hit-class flag follow pinned headers.', 'Return does not commit HP here;health is supplied as an already-committed field. The full class regression package is unchanged. Native reaction consumers,modern executable comparison,persistence/input/actor integration and final effects/icons remain unfinished.'])
 receipt=ROOT/'tools/reference/monster-return-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert receipt.read_text()==encoded
 else:receipt.write_text(encoded)
 print('NPC return:'+str(totals[0]['cases'])+'contexts per classic/Uber variant;'+str(differences)+'variant differences;source only,native consumer pending.')
MON_GH=3
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
