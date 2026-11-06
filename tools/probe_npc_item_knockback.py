#!/usr/bin/env python3
"""Compile the complete item knockback callback with explicit stat/RNG services."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
from probe_npc_damage_fill import enum
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-knockback-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={}
 def read(name):
  p=BASE/'d2moo-pinned-source'/name;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==name);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 body=function(read('source/D2Game/src/SKILLS/SkillItem.cpp'),'SKILLITEM_EventFunc07_Knockback')
 flags=enum(read('source/D2Common/include/DataTbls/MonsterTbls.h'),'D2C_MonStats2TxtFlagIndices');large=flags['MONSTATS2FLAGINDEX_LARGE'];small=flags['MONSTATS2FLAGINDEX_SMALL']
 cpp='''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_MONSTER=1;
struct D2GameStrc{};struct D2UnitStrc{int dwUnitType=1,dwClassId=0;uint32_t pSeed=0;};struct D2DamageStrc{uint16_t wResultFlags=0;};struct D2MonStats2Txt{uint32_t dwFlags=0;};
uint32_t gdwBitMasks[32];D2MonStats2Txt mon;bool metadata=true;int value=1,draws=0;uint32_t raw=0;
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,uint32_t,int){return value;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return metadata?&mon:nullptr;}
uint32_t ITEMS_RollRandomNumber(uint32_t*){draws++;return raw;}
'''+f'constexpr int MONSTATS2FLAGINDEX_LARGE={large},MONSTATS2FLAGINDEX_SMALL={small};\n'+body+r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;D2GameStrc game;D2UnitStrc a,t;D2DamageStrc d;
for(int src:{0,1})for(int target:{0,1})for(int packet:{0,1})for(int v:{-1,0,1,2})for(int kind:{0,1,4})for(int meta:{0,1})for(int sizes:{0,1,2,3})for(int before:{0,1,16,65527})for(uint32_t roll:{0u,31u,32u,63u,64u,127u,128u,255u}){
 value=v;metadata=meta;raw=roll;draws=0;t.dwUnitType=kind;d.wResultFlags=before;mon.dwFlags=(sizes&1?1u<<MONSTATS2FLAGINDEX_SMALL:0)|(sizes&2?1u<<MONSTATS2FLAGINDEX_LARGE:0);
 int result=SKILLITEM_EventFunc07_Knockback(&game,5,src?&a:nullptr,target?&t:nullptr,packet?&d:nullptr,81<<16,0);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%u,%d]",first?"":",",src,target,packet,v,kind,meta,sizes,before,roll,result,d.wResultFlags,draws);first=false;}printf("]\n");}
'''
 assert function(cpp,'SKILLITEM_EventFunc07_Knockback')==body
 p=STAGE/'probe.cpp';p.write_text(cpp)
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(p),'-o',str(STAGE/'probe')],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(STAGE/'probe')],capture_output=True,timeout=60);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr
 cases=json.loads(run.stdout);assert len(cases)==24576
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n');inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__))
 result=dict(referenceVersion='3.3.93847',currentExecutableParity=False,contexts=len(cases),completeRoutineHash=hashlib.sha256(body.encode()).hexdigest(),sizeFlagIndices=dict(small=small,large=large),inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in (STAGE/'probe.cpp',STAGE/'probe',STAGE/'compiler.log',STAGE/'sanitizer.log',out)},scope='Complete unchanged callback07;supplied accrued stat,metadata lookup and one raw RNG service. Positive stat is a presence gate,not a percentage. Large precedence32/128,small128/128,otherwise64/128;OR8 preserves prior result flags. Movement belongs to the later return phase.',limits=['Supplementary older callback does not certify current dispatch,global order or native movement/client parity. No elite exclusion or missile penalty is added.'])
 path=ROOT/'tools/reference/npc-item-knockback-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 print('NPC item knockback:24576complete callback contexts;ASan/UBSan pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
