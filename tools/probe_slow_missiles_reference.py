#!/usr/bin/env python3
"""Compile the unchanged legacy Slow Missiles velocity block; no modern damage proof."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 path='source/D2Game/src/MISSILES/Missiles.cpp';raw=(STAGE/'d2moo-pinned-source'/path).read_bytes()
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
 text=raw.decode();start=text.index('    int32_t nVelocity = 0;');end=text.index('    D2UnitStrc* pTarget = missileParams->pTarget;',start);block=text[start:end]
 assert 'STAT_SKILL_HANDOFATHENA' in block and '75 * nVelocity / 100' in block
 bossPath='source/D2Common/src/Monsters/Monsters.cpp';bossRaw=(STAGE/'d2moo-pinned-source'/bossPath).read_bytes()
 assert hashlib.sha1(b'blob '+str(len(bossRaw)).encode()+b'\0'+bossRaw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==bossPath)
 bossText=bossRaw.decode();m=re.search(r'^BOOL __stdcall MONSTERS_IsBoss\([^;\n]*\)\s*\n\{',bossText,re.M);assert m;end=bossText.index('{',m.start())+1;depth=1
 while depth:depth+=(bossText[end]=='{')-(bossText[end]=='}');end+=1
 boss=bossText[m.start():end]
 cpp=r'''#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
constexpr int MISSILESFLAGINDEX_CANSLOW=0,STATE_SLOWMISSILES=1,STAT_SKILL_HANDOFATHENA=2;
#define __stdcall
using BOOL=int;constexpr int UNIT_MONSTER=1,MONSTATSFLAGINDEX_BOSS=1;
constexpr bool FALSE=false;constexpr uint32_t gdwBitMasks[]={1,2};
struct D2UnitStrc{int dwUnitType=1,dwClassId=0;} owner;
struct D2MonStatsTxt{uint32_t dwMonStatsFlags=0;} monster;bool lookupValid=true;
D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int){return lookupValid?&monster:nullptr;}
struct D2MissileStrc{uint32_t dwFlags=0;int nSkillLevel=1,nVelocity=0;D2UnitStrc* pOwner=&owner;} params;
struct D2MissilesTxt{int nVel=0,nVelLev=0;uint32_t dwMissileFlags=0;} row;
struct D2StatListStrc{} list;
bool active=false,hasList=false;int percent=100,stateCalls=0,listCalls=0,statCalls=0;
bool STATES_CheckState(D2UnitStrc* p,int state){if(p!=&owner || state!=STATE_SLOWMISSILES)std::abort();stateCalls++;return active;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* p,int state){if(p!=&owner || state!=STATE_SLOWMISSILES)std::abort();listCalls++;return hasList?&list:nullptr;}
int STATLIST_GetStatValue(D2StatListStrc* p,int stat,int layer){if(p!=&list || stat!=STAT_SKILL_HANDOFATHENA || layer!=0)std::abort();statCalls++;return percent;}
int velocity(){auto* missileParams=&params;auto* pMissilesTxtRecord=&row;
'''+block+r'''
return nVelocity;}
'''+boss+r'''
int main(){bool first=true;std::printf("{\"velocity\":[");
// Fixed inputs cover zero, fractional level increments, byte velocity maximum,
// and both supplied integer and already-FP8 creation flag branches.
for(int flags:{0,4,20})for(int input=0;input<8;input++)for(int slow:{0,1})for(int state:{0,1})for(int exists:{0,1})for(int pct:{0,1,32,50,67,100}){
 const int bases[]={0,1,18,255,7,9,10,18},levels[]={1,3,20,1,3,20,1,20},increments[]={0,1,7,8,1,7,8,0},supplied[]={0,1,257,1024,3,255,511,4608};
 params.dwFlags=flags;params.nSkillLevel=levels[input];params.nVelocity=supplied[input];row.nVel=bases[input];row.nVelLev=increments[input];row.dwMissileFlags=slow;active=state;hasList=exists;percent=pct;stateCalls=listCalls=statCalls=0;
 int result=velocity();if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",flags,row.nVel,row.nVelLev,params.nSkillLevel,params.nVelocity,slow,state,exists,pct,result,stateCalls,listCalls,statCalls,input);
}std::printf("],\"boss\":[");first=true;
for(int supplied:{0,1})for(int present:{0,1})for(int type:{0,1})for(int valid:{0,1})for(int flag:{0,1}){
 D2UnitStrc target;target.dwUnitType=type;lookupValid=valid;monster.dwMonStatsFlags=flag?2:0;
 int result=MONSTERS_IsBoss(supplied?&monster:nullptr,present?&target:nullptr)!=0;
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",supplied,present,type,valid,flag,result);
}std::printf("]}\n");}
'''
 p=STAGE/'slow-missiles-source-probe.cpp';p.write_text(cpp);binary=STAGE/'slow-missiles-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert len(data['velocity'])==1152
 for flags,base,increment,level,supplied,slow,state,exists,pct,result,stateCalls,listCalls,statCalls,_ in data['velocity']:
  value=(base+level*increment//8)*256 if not flags&4 else supplied if flags&16 else supplied*256
  if slow and state and exists:value=value*pct//100
  if value:value=value*75//100
  assert (result,stateCalls,listCalls,statCalls)==(value,slow,int(bool(slow and state)),int(bool(slow and state and exists)))
 assert len(data['boss'])==32
 for supplied,present,kind,valid,flag,result in data['boss']:assert result==int(bool(present and kind==1 and (supplied or valid) and flag))
 fixture=STAGE/'slow-missiles-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes={path:hashlib.sha256(raw).hexdigest(),bossPath:hashlib.sha256(bossRaw).hexdigest()},compiledBossHelper=dict(name='MONSTERS_IsBoss',sha256=hashlib.sha256(boss.encode()).hexdigest(),unchanged=True),compiledBlock=dict(parent='MISSILES_CreateMissileFromParams',start='int32_t nVelocity = 0;',endBefore='D2UnitStrc* pTarget = missileParams->pTarget;',sha256=hashlib.sha256(block.encode()).hexdigest(),unchanged=True),probe=dict(velocityCases=len(data['velocity']),bossCases=len(data['boss']),fixture=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=[
 'Legacy factory chooses table base plus integer level increment, shiftedFP8, unless flags4 supplies velocity. Supplied velocity is shiftedFP8 unless flags16 already marksFP8.',
 'Only canSlow requests the owner state. Only an active state requests its list, and only a present list reads skill_handofathena. Eligible velocity is scaled by that percent with integer division before the independent75-percent factory conversion. Zero and subinteger rounding are retained.',
 'Full unchanged MONSTERS_IsBoss returns only the source species boss flag after monster/row guards. Champion, unique or superunique grade is not inspected; never use a generic elite predicate as this source filter.',
 'Factory creation reads owner state at creation. This bounded block establishes no behavior for a missile already in flight, later state expiry, children, path/lifetime/range or a modern damage-scale stat.'
 ],services=[
 'Declared owner state/list/stat and canSlow bit services record call counts. Positive unsigned table/supplied input ranges are bounded to avoid signed-shift/overflow assertions. No real source stat graph, room, full factory or current executable is compiled.',
 'This compiles an unchanged contiguous velocity block, not the complete factory function. Full source and block hashes are validated against pinned Git blobs. Python expectations use these bounded positive arithmetic cases only.'
 ],limits=[
 'No native handler, modern damage reduction or class/current executable certificate. Current skill_missile_damage_scale and second-stat do6 handling are absent from this legacy source. Do not silently apply first-stat-only behavior as complete current Slow Missiles.',
 'Current missile eligibility/owner/child snapshots, damage order, hostile mapped sources, actual travel/expiration/save/client behavior remain separate work. See tools/reference/slow-missiles-preflight.json.'
 ])
 p=ROOT/'tools/reference/slow-missiles-engine-evidence.json';serialized=json.dumps(out,indent=2)+'\n'
 if check:assert p.read_text()==serialized
 else:p.write_text(serialized)
 print('Slow Missiles:1152 unchanged legacy velocity-block and32 full boss-helper cases pass; modern damage consumer/native integration remain separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
