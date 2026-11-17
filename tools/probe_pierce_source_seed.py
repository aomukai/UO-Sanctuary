#!/usr/bin/env python3
"""Bind the real older Pierce seed/init/roll bodies and input-index writers."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import ROOT,STAGE,function
OUT=ROOT/'.local/class-skills/pierce-source-seed'
def sha(b):return hashlib.sha256(b).hexdigest()
def record(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    paths={};bodies={}
    def source(path):
        b=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==next(t['sha']for t in tree['tree']if t['path']==path);paths[path]=sha(b);return b.decode()
    missile=source('source/D2Game/src/MISSILES/Missiles.cpp');start=missile.index('            const int32_t nPierceValue = ');block=missile[start:missile.index('\n        }',start)]
    initial=function(source('source/D2Common/src/D2Seed.cpp'),'SEED_InitLowSeed')
    roll=function(source('source/D2Common/include/D2Seed.h'),'SEED_RollRandomNumber')
    item=function(source('source/D2Game/src/ITEMS/Items.cpp'),'ITEMS_RollRandomNumber')
    player=source('source/D2Game/src/PLAYER/PlrMsg.cpp')
    import re
    names=re.findall(r'^int32_t __fastcall (D2GAME_PACKETCALLBACK_\w+)\([^\n]+\)\n\{',player,re.M)
    writers={name:function(player,name)for name in names if 'STAT_PIERCE_IDX' in function(player,name)}
    assert len(writers)==12
    for name,body in writers.items():assert 'STATLIST_GetUnitBaseStat(pUnit, STAT_PIERCE_IDX, 0) + 1' in body,name
    unit=function(source('source/D2Game/src/UNIT/SUnitMsg.cpp'),'D2GAME_SUNITMSG_FirstFn_6FCC5520');assert unit.index('case UNIT_MONSTER:')<unit.index('STAT_PIERCE_IDX')
    for name,body in [('SEED_InitLowSeed',initial),('SEED_RollRandomNumber',roll),('ITEMS_RollRandomNumber',item),*writers.items(),('D2GAME_SUNITMSG_FirstFn_6FCC5520',unit)]:bodies[name]=dict(sha256=sha(body.encode()))
    cpp='''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};
'''+initial+'\n'+roll.replace('0x6AC690C5i64','0x6AC690C5ULL')+'''
int calls=0,baseReads=0;uint64_t after=0;
uint64_t observed(D2SeedStrc* p){calls++;auto result=SEED_RollRandomNumber(p);after=p->lSeed;return result;}
#define SEED_RollRandomNumber observed
'''+item+'''
#undef SEED_RollRandomNumber
enum{STAT_ITEM_PIERCE,STAT_SKILL_PIERCE,STAT_PIERCE_IDX};
struct Unit{int32_t item,skill,index,budget;};struct Params{Unit* pOwner;};
int32_t STATLIST_UnitGetItemStatOrSkillStatValue(Unit*p,int,int){return p->item;}
int32_t STATLIST_UnitGetStatValue(Unit*p,int,int){return p->skill;}
int32_t STATLIST_GetUnitBaseStat(Unit*p,int,int){baseReads++;return p->index;}
void STATLIST_SetUnitStat(Unit*p,int,int32_t value,int){p->budget=value;}
void run(int32_t skill,int32_t item,int32_t low,bool&first){calls=baseReads=0;after=0;Unit owner{item,skill,low,0},missile{};Params params{&owner};auto*missileParams=&params;auto*pMissile=&missile;
'''+block+'''
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,",skill,item,low,missile.budget,calls);if(baseReads)std::printf("\\\"%llu\\\"",(unsigned long long)after);else std::printf("null");std::printf(",%d]",baseReads);}
int main(){bool first=true;std::printf("[");for(int32_t low:{0,1,2,3,4,5,7,9,17,31,63,99,127,255,256,257,1023,65535,65536,2147483647,-1,-2,(-2147483647-1),305419896}){for(int skill:{0,1,23,34,85,87,100})for(int item:{0,1,33,50,100})run(skill,item,low,first);for(int chance:{99,100,101,2147483647})run(chance,0,low,first);}std::printf("]\\n");}
'''
    result=dict(referenceVersion='3.3.93847',sourceRole='Supplementary older engine;modern D2R packet/index producer remains unverified.',legacyCommit=tree['sha'],sourceFiles=paths,functions=bodies,creationBlockSha256=sha(block.encode()),sourceChanges=['MSVC i64 integer suffix translated to standard ULL;calling-convention macro blank. Seed union layout declared with fixed-width fields.','The unchanged item RNG wrapper calls a transparent observation wrapper around the unchanged seed-roll body;only call count/final seed are observed.','Owner/stat/native allocation services are declared;full input callback/global factory execution is source-reviewed,not compiled.'],indexWriters=sorted(writers),established=['Factory uses a new temporary seed initialized from owner base STAT_PIERCE_IDX,not owner combat RNG. Each missile with the same supplied index/chance gets the same budget.','Chance0 does not read the owner index or draw;nonnegative chance>=100 still draws four times. A failure draw counts.','Twelve older player skill-input callbacks increment the owner base index before entering skill dispatch;the coordinate-sum initializer in FirstFn is specifically in UNIT_MONSTER,not the player branch.','The existing Random.Shared/LaunchRandom budget adapter does not implement this owner-index supplier or its same-index fan correlation.'],limits=['No current3.3.93847 executable certificate. New-protocol input/hold/release mapping,initial player index and saved owner-index policy remain to be implemented and explicitly resolved.','Only nonnegative total pierce is supported;negative/overflow stat contexts are not silently assigned a rule. Existing online consumers remain unchanged.'])
    encoded=json.dumps(result,indent=2)+'\n';p=ROOT/'tools/reference/pierce-source-seed-review.json'
    if check:
        assert p.read_text()==encoded;assert (OUT/'source-probe.cpp').read_text()==cpp
        data=json.loads((OUT/'source-cases.json').read_text())
    else:
        (OUT/'source-probe.cpp').write_text(cpp);subprocess.run(['g++','-std=c++20','-O2',str(OUT/'source-probe.cpp'),'-o',str(OUT/'source-probe')],check=True)
        data=json.loads(subprocess.check_output([str(OUT/'source-probe')]));(OUT/'source-cases.json').write_text(json.dumps(data,separators=(',',':'))+'\n');p.write_text(encoded)
    assert len(data)==936
    for skill,item,index,budget,calls,after,reads in data:
        chance=skill+item;seed=(666<<32)|(index&0xffffffff);expected=n=0
        if chance:
            while expected<4:
                seed=0x6AC690C5*(seed&0xffffffff)+(seed>>32);n+=1
                if (seed&0xffffffff)%100>=chance:break
                expected+=1
        assert (budget,calls,after,reads)==(expected,n,str(seed)if chance else None,int(bool(chance)))
    assert set(row[3]for row in data)==set(range(5))
    print('Pierce source seed:936real RNG/factory vectors,12input writers and monster-only coordinate initializer bound.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
