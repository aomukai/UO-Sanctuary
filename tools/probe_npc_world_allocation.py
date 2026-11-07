#!/usr/bin/env python3
"""Extend the frozen item allocator with complete missile init and seed entropy helpers."""
import hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-world-allocation-source'
PIN='0c0bbd1e6bef9181a3da1c046523af622a925416'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def routine(text,name):
 m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[m.start():end]
def main():
 STAGE.mkdir(exist_ok=True);parent=json.loads((ROOT/'tools/reference/npc-item-allocation-source-evidence.json').read_text())
 original=BASE/'npc-item-allocation-source/probe.cpp';assert sha(original)==parent['artifacts'][str(original.relative_to(ROOT))]
 prefix=original.read_text().split('int main(){',1)[0]
 for name,digest in parent['completeRoutineHashes'].items():assert hashlib.sha256(routine(prefix,name).encode()).hexdigest()==digest,name
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==PIN;hashes=dict(parent['sourceHashes'])
 def source(path):
  raw=(BASE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path)
  hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 entropy=routine(source('source/D2Common/src/D2Seed.cpp'),'SEED_GetRandomValue')
 missile=routine(source('source/D2Game/src/MISSILES/Missiles.cpp'),'MISSILES_Initialize')
 prefix=prefix.replace('int SEED_GetRandomValue(int){std::abort();}', 'long clockSeconds;uint32_t clockTicks;long time(void*){return clockSeconds;}uint32_t GetTickCount(){return clockTicks;}\n'+entropy)
 prefix=prefix.replace('void MISSILES_Initialize(D2GameStrc*,D2UnitStrc*,uint32_t){std::abort();}', 'int dataCalls=0,modeCalls=0;constexpr int EVENTTYPE_MODECHANGE=0;\nvoid MISSILE_AllocMissileData(D2UnitStrc*){++dataCalls;}\nvoid sub_6FC35570(D2GameStrc*,D2UnitStrc*,int,int,int){++modeCalls;}\n'+missile)
 cpp=prefix+r'''
int main(){bool first=true;MISSILES_Initialize(nullptr,nullptr,0);assert(dataCalls==0&&modeCalls==0);std::printf("{\"allocations\":[");
for(uint64_t initial:{0ULL,1ULL,666ULL,0xffffffffULL,0x100000000ULL,0x29a00000000ULL,0x29a00000001ULL,0x29affffffffULL,0xffffffffffffffffULL,0xdeadbeef12345678ULL})
for(int sequence:{0,1,2,3})for(int expansion:{0,1})for(int add:{0,1})for(uint32_t previous:{0u,0xfffffffeu,0xffffffffu})for(int fail:{0,1}){
D2GameStrc game{};game.bExpansion=expansion;game.pGameSeed.lSeed=initial;game.dwLastUsedUnitGUID[3]=game.dwLastUsedUnitGUID[4]=previous;failAllocation=fail;dataCalls=modeCalls=0;
int kinds[4][5]={{3,0,0,0,0},{3,3,3,0,0},{4,3,4,3,0},{3,4,3,4,3}};int count[]={1,3,4,5};
if(!first)std::printf(",");first=false;std::printf("[\"%llu\",%d,%d,%d,%u,%d,[",(unsigned long long)initial,sequence,expansion,add,previous,fail);
for(int i=0;i<count[sequence];i++){
int kind=kinds[sequence][i];auto*u=SUNIT_AllocUnitData(kind,0,0,0,&game,nullptr,add,4,0);if(i)std::printf(",");
if(!u)std::printf("null");else std::printf("[%d,%u,%u,\"%llu\",%u,\"%llu\",%u]",kind,u->dwUnitId,u->dwInitSeed,(unsigned long long)u->pSeed.lSeed,kind==4?u->pItemData->dwInitSeed:0,kind==4?(unsigned long long)u->pItemData->pSeed.lSeed:0,kind==4?u->pItemData->flags:u->dwFlags);
}std::printf("],\"%llu\",%d,%d,%u,%u]",(unsigned long long)game.pGameSeed.lSeed,dataCalls,modeCalls,game.dwLastUsedUnitGUID[3],game.dwLastUsedUnitGUID[4]);
}std::printf("],\"entropy\":[");first=true;
for(long seconds:{0L,1L,0x7fffffffL,0xffffffffL})for(uint32_t ticks:{0u,1u,0x7fffffffu,0xffffffffu})for(int value:{INT32_MIN,-1,0,1,INT32_MAX}){
clockSeconds=seconds;clockTicks=ticks;if(!first)std::printf(",");first=false;std::printf("[%ld,%u,%d,%d]",seconds,ticks,value,SEED_GetRandomValue(value));
}std::printf("]}\n");}
'''
 cppfile=STAGE/'probe.cpp';cppfile.write_text(cpp);binary=STAGE/'probe'
 subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(cppfile),'-o',str(binary)],check=True)
 with (STAGE/'sanitizers.log').open('w')as err:run=subprocess.run([str(binary)],stdout=subprocess.PIPE,stderr=err,check=True)
 assert not(STAGE/'sanitizers.log').read_text();result=json.loads(run.stdout);cases=result['allocations'];assert len(cases)==960 and len(result['entropy'])==80
 def roll(s):s=(s&0xffffffff)*0x6ac690c5+(s>>32);return s,s&0xffffffff
 kinds=[[3],[3,3,3],[4,3,4,3],[3,4,3,4,3]];successes=refusals=missiles=items=0
 for initial,sequence,expansion,add,previous,fail,rows,after,data,mode,guid3,guid4 in cases:
  game=int(initial);guids={3:previous,4:previous};count=0
  for kind,row in zip(kinds[sequence],rows,strict=True):
   if fail:assert row is None;refusals+=1;continue
   game,u=roll(game);guids[kind]=1 if guids[kind]==0xfffffffe else(guids[kind]+1)&0xffffffff;successes+=1
   if kind==4:
    game,item=roll(game);expect=[kind,guids[kind],u,str((666<<32)|u),item,str((666<<32)|item),16];items+=1
   else:expect=[kind,guids[kind],u,str((666<<32)|u),0,'0',4];count+=1;missiles+=1
   assert row==expect,(row,expect)
  assert str(game)==after and data==mode==count and guid3==guids[3] and guid4==guids[4]
 for seconds,ticks,value,out in result['entropy']:assert out==((0x2f490a95*(seconds+ticks+value)-0x2e330917)&0x7fffffff)
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(result,separators=(',',':'))+'\n')
 proof=dict(referenceVersion='3.3.93847',legacyCommit=PIN,currentExecutableParity=False,parent='tools/reference/npc-item-allocation-source-evidence.json',parentSha256=sha(ROOT/'tools/reference/npc-item-allocation-source-evidence.json'),contexts=len(cases),entropyContexts=80,successfulAllocations=successes,failedAllocations=refusals,missiles=missiles,items=items,sourceHashes=hashes,completeRoutineHashes=parent['completeRoutineHashes']|{name:hashlib.sha256(body.encode()).hexdigest()for name,body in [('SEED_GetRandomValue',entropy),('MISSILES_Initialize',missile)]},inputs={str(Path(__file__).resolve().relative_to(ROOT)):sha(Path(__file__))},artifacts={str(p.relative_to(ROOT)):sha(p)for p in [cppfile,binary,fixture,STAGE/'sanitizers.log']},scope=['Complete unchanged older allocator,seed,item helpers and missile initializer run in mixed interleaved contexts. Each successful missile consumes one supplied game draw;items consume two. Refused native allocation consumes neither RNG nor GUID. Missile data/event services are explicit counters;initial flags are named synthetic services,not a binary ABI.', 'Independent Python checks unit/property seed high666,per-kind GUID wrap,shared game continuation,and full legacy entropy helper with explicit wall/tick services. Null missile initialization invokes no services.'],limits=['No current executable allocation chronology or seed bootstrap is established. The game-creation function is not extracted/executed;entropy helper checks do not certify its full call order. Real missile-data storage,room/event registration,memory failure and cross-world initialization remain host services. No learned summon or default world RNG is enabled by this source probe.'])
 (ROOT/'tools/reference/npc-world-allocation-source-evidence.json').write_text(json.dumps(proof,indent=2)+'\n')
 print(f'World allocation source:960mixed contexts,{missiles}missiles/{items}items,{refusals}native refusals;80entropy contexts;complete12routines,ASan/UBSan clean.')
if __name__=='__main__':main()
