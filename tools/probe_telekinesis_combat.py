#!/usr/bin/env python3
"""Compose complete older Telekinesis rolls, event health and NPC return bodies."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/telekinesis-combat-source'
LEGACY=Path('/home/aomukai/.codex/worktrees/death-sentry-damage/Sanctuary/.local/class-skills/monster-return/source.cpp')


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def probe(check=False):
    STAGE.mkdir(exist_ok=True)
    retained=ROOT/'tools/reference/monster-return-source-evidence.json';proof=json.loads(retained.read_text())
    old=STAGE/'reused-monster-return.cpp'
    expected=proof['artifacts']['.local/class-skills/monster-return/source.cpp']
    if not old.exists():
        assert sha(LEGACY)==expected;shutil.copy2(LEGACY,old)
    assert sha(old)==expected
    cpp=old.read_text().split('int main()',1)[0]
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==proof['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes={};blocks={}
    def source(rel):
        p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        hashes[rel]=sha(p);return raw.decode()
    damage=source('source/D2Game/src/UNIT/SUnitDmg.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp');sorc=source('source/D2Game/src/SKILLS/SkillSor.cpp')
    common=('SUNITDMG_ApplyResistancesAndAbsorb','SUNITDMG_CalculateTotalDamage','SUNITDMG_ExecuteEvents','SUNITDMG_ExecuteMissileDamage','sub_6FCC1870','SUNITDMG_GetHitClass')
    for name in common:
        body=function(damage,name);assert function(cpp,name)==body,name;blocks[name]=body
    def replace(a,b):
        nonlocal cpp
        assert cpp.count(a)==1,a[:100];cpp=cpp.replace(a,b,1)
    replace('struct D2SkillsTxt{int wStSound=0;};','struct D2SkillsTxt{int wStSound=0,nEType=2,wResultFlags=5,dwHitFlags=2,dwHitClass=109,dwParam[8]={25,35};}tkRow;')
    replace('D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){assert(false);return nullptr;}','D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==43);return &tkRow;}')
    replace('void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){callbackCount++;}', 'std::vector<int> tkEvents;void SUNITEVENT_Trigger(D2GameStrc*,int event,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){callbackCount++;tkEvents.push_back(event);}')
    for name in ('SUNITDMG_ExecuteEvents','SUNITDMG_ExecuteMissileDamage'):
        body=blocks[name]
        if name.endswith('ExecuteEvents'):
            wrapper='void SUNITDMG_ExecuteEvents(D2GameStrc*g,D2UnitStrc*a,D2UnitStrc*d,int m,D2DamageStrc*p){tkRaw=*p;TkEvents::SUNITDMG_ExecuteEvents(g,a,d,m,p);tkAfterEvents=*p;}'
            replace(body,'namespace TkEvents{\n'+body+'\n}\nD2DamageStrc tkRaw{},tkAfterEvents{};\n'+wrapper)
        else:
            wrapper='void SUNITDMG_ExecuteMissileDamage(D2GameStrc*g,D2UnitStrc*a,D2UnitStrc*d,D2DamageStrc*p){TkReturn::SUNITDMG_ExecuteMissileDamage(g,a,d,p);tkAfterReturn=*p;}'
            replace(body,'namespace TkReturn{\n'+body+'\n}\nD2DamageStrc tkAfterReturn{};\n'+wrapper)
    enums=source('source/D2Common/include/D2Combat.h');cpp+='\n'+re.search(r'enum D2C_ElemTypes\s*\{.*?\};',enums,re.S).group()+';\n'
    cpp+=r'''
constexpr int UNITFLAG_SKSRVDOFUNC=0x40,UNITFLAG_SENDREFRESHMSG=0x1000,IMODE_ONGROUND=3;
constexpr int ITEMTYPE_SCROLL=0,ITEMTYPE_GOLD=1,ITEMTYPE_MISSILE_POTION=2,ITEMTYPE_MISSILE=3,ITEMTYPE_POTION=4,ITEMTYPE_KEY=5;
int tkRank=1;
int SKILLS_GetMinPhysDamage(D2UnitStrc*,int,int,int){return 0;}int SKILLS_GetMaxPhysDamage(D2UnitStrc*,int,int,int){return 0;}
int SKILLS_GetMinElemDamage(D2UnitStrc*,int,int rank,int){return rank*256;}int SKILLS_GetMaxElemDamage(D2UnitStrc*,int,int rank,int){return (rank+1)*256;}
int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int){return 0;}
bool PLAYER_IsBusy(D2UnitStrc*){return false;}D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return &target;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*,int){assert(false);return false;}
void D2GAME_PickupItem_6FC43340(D2GameStrc*,D2UnitStrc*,int,int*){assert(false);}
void DUNGEON_AllocDrlgDelete(D2ActiveRoomStrc*,int,int){assert(false);}void ITEMS_SetInvPage(D2UnitStrc*,unsigned){assert(false);}
void UNITS_ChangeAnimMode(D2UnitStrc*,int){assert(false);}void OBJECTS_OperateHandler(D2GameStrc*,D2UnitStrc*,int,int,int*){assert(false);}
'''
    for name,body in ((n,function(skills,n)) for n in ('sub_6FD11E40','D2GAME_RollPhysicalDamage_6FD14EC0','D2GAME_RollElementalDamage_6FD14DD0')):
        blocks[name]=body;cpp+='\n'+body
    name='SKILLS_SrvDo021_Telekinesis';blocks[name]=function(sorc,name);cpp+='\n'+blocks[name]
    cpp+=r'''
int main(){for(int n=0;n<32;n++)gdwBitMasks[n]=1u<<n;printf("[");bool firstCase=true;
for(int rank:{1,2,3,6,10,20,40,255})for(uint32_t seed:{0u,1u,123u,0xffffffffu})for(int resistance:{-100,0,50,100,150})for(int maximum:{25600,12800000})for(int hp:{256,25600})for(int freeze:{0,1})for(int stun:{0,1})for(int mask:{0,1<<MONMODE_GETHIT,(1<<MONMODE_GETHIT)|(1<<MONMODE_KNOCKBACK)}){
actor={};target={};game={};row={};mon2={};actor.pGame=target.pGame=&game;actor.dwUnitType=UNIT_PLAYER;actor.dwUnitId=77;actor.group=1;target.dwUnitType=UNIT_MONSTER;target.dwClassId=413;target.dwUnitId=99;target.group=2;target.dwAnimMode=1;target.pMonsterData=nullptr;
actor.pSeed.nLowSeed=seed;actor.pSeed.nHighSeed=666;target.pSeed.nLowSeed=seed+1;target.pSeed.nHighSeed=666;
target.stats[STAT_HITPOINTS]=hp;target.stats[STAT_LIGHTRESIST]=resistance;target.stats[STAT_LAST_SENT_HP_PCT]=128;
maxLife=maximum;hasRoom=hasRow=hasModes=true;inTown=uninterruptible=false;frozen=freeze;stunned=stun;baseId=0;row.dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_KILLABLE];mon2.dwModeFlags=mask;tkRank=rank;
trace.clear();tkEvents.clear();requested=-1;kills=modes=recoveryCalls=callbackCount=0;tkRaw={};tkAfterEvents={};tkAfterReturn={};
int ret=SKILLS_SrvDo021_Telekinesis(&game,&actor,43,rank);
printf("%s[[%d,%u,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%u,%u,%u,%d,%d,%d,%llu,%llu,%d,%d],[",firstCase?"":",",rank,seed,resistance,maximum,hp,freeze,stun,mask,ret,tkRaw.dwLtngDamage,tkAfterEvents.dwDmgTotal,tkRaw.wResultFlags,tkAfterEvents.wResultFlags,tkAfterReturn.wResultFlags,target.stats[STAT_HITPOINTS],requested,kills,(unsigned long long)actor.pSeed.lSeed,(unsigned long long)target.pSeed.lSeed,tkAfterReturn.dwHitClass,int(tkAfterReturn.dwHitFlags));
for(size_t n=0;n<tkEvents.size();n++)printf("%s%d",n?",":"",tkEvents[n]);printf("]]");firstCase=false;
}printf("]\n");}
'''
    path=STAGE/'probe.cpp';path.write_text(cpp);binary=STAGE/'probe'
    subprocess.run(['g++','-std=c++20','-O2','-DD2_VERSION_HAS_UBERS',str(path),'-o',str(binary)],check=True)
    data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==7680
    def roll(seed):
        seed=0x6AC690C5*(seed&0xffffffff)+(seed>>32);return seed,seed&0xffffffff
    modes={}
    for inputs,outputs,events in data:
        rank,seed,resistance,maximum,hp,freeze,stun,mask=inputs
        stream,damage_draw=roll((666<<32)|seed);raw=rank*256+damage_draw%256
        stream,chance=roll(stream);flags=5|(8 if chance%100<35 else 0)
        amount=raw*max(0,100-resistance)//100;after=hp-amount;after=0 if after<256 else after
        event_flags=flags|(2 if after==0 else 0);final_flags=event_flags
        if flags&8 and not mask&(1<<13):final_flags=(final_flags&~8)|4
        defender=(666<<32)|((seed+1)&0xffffffff)
        mode=-1
        if after==0:mode=-1
        elif final_flags&8:mode=13
        elif stun:mode=3
        elif not freeze and amount>=256 and amount>=maximum//16:
            suppressed=False
            if amount<maximum//8:defender,draw=roll(defender);suppressed=not(draw&1)
            if not suppressed and amount<maximum//4:defender,draw=roll(defender);suppressed=not(draw&3)
            if not suppressed and mask&(1<<3):mode=3
        assert outputs==[1,raw,amount,flags,event_flags,final_flags,after,mode,int(after==0),stream,defender,109,2],(inputs,outputs)
        # Only absorb-damage and optional kill notifications: hit2 does not
        # enable domissiledamage/damagedbymissile, and never melee/Thorns.
        assert len(events)==(3 if after==0 else 1),(inputs,events)
        modes[mode]=modes.get(mode,0)+1
    san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-DD2_VERSION_HAS_UBERS','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(san)],check=True)
    log=STAGE/'sanitizer.log'
    with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==data
    assert not log.read_bytes();cases=STAGE/'cases.json';cases.write_text(json.dumps(data,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=7680,sanitizers='ASan/UBSan clean',modes=modes,
                sourceHashes=hashes,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},
                reusedSource=str(retained.relative_to(ROOT)),reusedSourceHash=sha(retained),reusedCppHash=expected,
                established=['Complete do21 plus physical/elemental roll wrappers and typed mapping compose with complete mitigation, event health commitment and NPC return. Physical0/0 consumes no RNG; elemental FP roll then chance%100 share one owner seed.',
                             'Flags5/hit2/class109 remain through source events. Lightning immunity does not suppress a successful knockback request. No stun duration is added. Low-damage recovery uses shared recipient thresholds/defender RNG, existing stun and freeze/mode policy.',
                             'Event2-style hit flags do not enable missile-damage callbacks; only absorb-damage and optional killed/kill callbacks occur. No melee attempt, Life Tap, Iron Maiden or Thorns callback is inserted.'],
                declaredServices=['Endpoints are supplied rank..rank+1 FP values; the complete formula/mastery interpreter is not executed. Non-town hostile player-to-ordinary-monster, zero physical/leech/other elements, empty event subscribers and stat storage are declared.',
                                  'Mode/kill/room/state/registration services are adapters. Source NPC return requests are executed, not the full animation/path/death engine. Native world/recipient/item/PvP/Sunder/current executable and global actor-seed allocation require separate acceptance.'],
                artifacts={str(p.relative_to(ROOT)):sha(p) for p in (Path(__file__).resolve(),old,path,binary,san,log,cases)})
    output=ROOT/'tools/reference/telekinesis-combat-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Telekinesis combat:7680complete older roll/mitigation/event-health/NPC-return compositions;ASan/UBSan clean;full native/current executable services remain separate.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');probe(p.parse_args().check)
