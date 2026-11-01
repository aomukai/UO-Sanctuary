#!/usr/bin/env python3
"""Compose complete older Shiver event03, elemental roll, HP and NPC return."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/shiver-armor-source'


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def probe(check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    prior_path=ROOT/'tools/reference/telekinesis-combat-source-evidence.json';prior=json.loads(prior_path.read_text())
    original=ROOT/'.local/class-skills/telekinesis-combat-source/probe.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
    retained=STAGE/'reused-telekinesis.cpp'
    if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
    assert sha(retained)==expected
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    paths={'source/D2Game/src/UNIT/SUnitDmg.cpp':('SUNITDMG_ApplyResistancesAndAbsorb','SUNITDMG_CalculateTotalDamage','SUNITDMG_ExecuteEvents','SUNITDMG_ExecuteMissileDamage','sub_6FCC1870','SUNITDMG_GetHitClass'),
           'source/D2Game/src/SKILLS/Skills.cpp':('sub_6FD11E40','D2GAME_RollElementalDamage_6FD14DD0'),
           'source/D2Game/src/SKILLS/SkillSor.cpp':('SKILLS_EventFunc03_ShiverArmor',)}
    cpp=retained.read_text().split('int main()',1)[0];sources={};blocks={}
    for rel,names in paths.items():
        p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        sources[rel]=sha(p)
        for name in names:
            b=function(raw.decode(),name);blocks[name]=b
            if name!='SKILLS_EventFunc03_ShiverArmor':assert function(cpp,name)==b,name
    def replace(old,new):
        nonlocal cpp
        assert cpp.count(old)==1,old[:100];cpp=cpp.replace(old,new,1)
    replace('struct Tables{int nMonStatsTxtRecordCount=1000;}','struct Tables{int nMonStatsTxtRecordCount=1000,nOverlayTxtRecordCount=200;}')
    replace('int wStSound=0,nEType=2,','int wCltOverlayA=-1,wStSound=0,nEType=2,')
    replace('assert(id==43);return &tkRow;','assert(id==50);return &tkRow;')
    replace('int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int){return 0;}','int suppliedCold=0;int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int){return suppliedCold;}')
    replace('void SUNITDMG_ApplyColdState(D2UnitStrc*,D2UnitStrc*,int n){assert(n==0);}', 'int appliedCold=0;void SUNITDMG_ApplyColdState(D2UnitStrc*,D2UnitStrc*,int n){appliedCold=n;}')
    replace('UNUSED_VOID(UNITS_SetOverlay)','int overlays=0;void UNITS_SetOverlay(D2UnitStrc*,int,int){overlays++;}')
    cpp+='\n'+blocks['SKILLS_EventFunc03_ShiverArmor']
    cpp+=r'''
int main(){for(int n=0;n<32;n++)gdwBitMasks[n]=1u<<n;bool first=true;printf("[");
for(int rank:{1,3,20,255})for(uint32_t seed:{0u,1u,123u,0xffffffffu})for(int resistance:{-100,0,50,100,150})for(int hp:{256,25600})for(int defenses=0;defenses<4;defenses++)for(int cold:{0,1,101})for(int freeze:{0,1})for(int stun:{0,1}){
actor={};target={};game={};row={};mon2={};actor.pGame=target.pGame=&game;actor.dwUnitType=UNIT_PLAYER;actor.dwUnitId=77;actor.group=1;
target.dwUnitType=UNIT_MONSTER;target.dwClassId=413;target.dwUnitId=99;target.group=2;target.dwAnimMode=1;
actor.pSeed.nLowSeed=seed;actor.pSeed.nHighSeed=666;target.pSeed.nLowSeed=seed+1;target.pSeed.nHighSeed=666;
target.stats[STAT_HITPOINTS]=hp;target.stats[STAT_COLDRESIST]=resistance;target.stats[STAT_LAST_SENT_HP_PCT]=128;
target.stats[STAT_ITEM_CANNOTBEFROZEN]=defenses&1;target.stats[STAT_ITEM_HALFFREEZEDURATION]=(defenses&2)!=0;
maxLife=12800000;hasRoom=hasRow=hasModes=true;inTown=uninterruptible=false;frozen=freeze;stunned=stun;baseId=0;
row.dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_KILLABLE];mon2.dwModeFlags=(1<<MONMODE_GETHIT)|(1<<MONMODE_KNOCKBACK);
tkRow.nEType=ELEMTYPE_COLD;tkRow.wCltOverlayA=0;suppliedCold=cold;
trace.clear();tkEvents.clear();requested=-1;kills=modes=recoveryCalls=callbackCount=overlays=appliedCold=0;tkRaw={};tkAfterEvents={};tkAfterReturn={};
int ret=SKILLS_EventFunc03_ShiverArmor(&game,1,&actor,&target,nullptr,50,rank);int notifications=0,refreshes=0;for(auto& e:trace){notifications+=e[0]==14;refreshes+=e[0]==12;}
printf("%s[[%d,%u,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%u,%u,%d,%d,%d,%llu,%llu,%d,%d,%d,%d,%d,%d,%d,%d],[",first?"":",",rank,seed,resistance,hp,defenses,cold,freeze,stun,ret,tkRaw.dwColdDamage,tkAfterEvents.dwDmgTotal,tkRaw.wResultFlags,tkAfterReturn.wResultFlags,target.stats[STAT_HITPOINTS],requested,kills,(unsigned long long)actor.pSeed.lSeed,(unsigned long long)target.pSeed.lSeed,tkAfterReturn.dwHitClass,int(tkAfterReturn.dwHitFlags),tkAfterReturn.nHitClassActiveSet,appliedCold,recoveryCalls,overlays,notifications,refreshes);
for(size_t n=0;n<tkEvents.size();n++)printf("%s%d",n?",":"",tkEvents[n]);printf("]]");first=false;
}printf("]\n");}
'''
    p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe'
    subprocess.run(['g++','-std=c++20','-O2','-DD2_VERSION_HAS_UBERS',str(p),'-o',str(binary)],check=True)
    cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==7680
    for inputs,outputs,events in cases:
        rank,seed,resistance,hp,defenses,cold,freeze,stun=inputs
        stream=0x6AC690C5*seed+666;raw=rank*256+(stream&0xffffffff)%256
        amount=raw*max(0,100-resistance)//100;after=hp-amount;after=0 if after<256 else after
        length=0 if defenses&1 else ((cold//2 if defenses&2 else cold)*max(0,100-resistance)//100)
        expected=[1,raw,amount,16417,16417|int(after==0)*2,after,-1,int(after==0),stream,(666<<32)|((seed+1)&0xffffffff),61,0,1,length,0,1,int(after>0),int(after>0)]
        assert outputs==expected,(inputs,outputs,expected)
        assert len(events)==1,(inputs,events) # absorb only; no damage/kill events
    san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-DD2_VERSION_HAS_UBERS','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True)
    log=STAGE/'sanitizer.log'
    with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
    assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    table=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';manifest=json.loads((table.parent.parent/'sources.json').read_text());assert sha(table)==manifest['files']['json/skills.json']['sha256']
    current=next(r for r in json.loads(table.read_text()).values() if r.get('skill')=='Shiver Armor');assert (current['*Id'],current['srvdofunc'],current['auraeventfunc1'])==(50,18,3)
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},
                currentTable={str(table.relative_to(ROOT)):sha(table)},currentRow=current,cases=len(cases),sanitizers='ASan/UBSan clean',
                established=['Complete event03 and elemental roll compose with complete mitigation/event HP/NPC return. One elemental FP draw,flags16417/hit0/class61active (cold48 OR base13),post-HP cold and optional overlay are retained.',
                             'Flag32 suppresses damage and killed/kill callbacks,while SOFTHIT refreshes surviving recipients and sends recovery notification without requesting a get-hit mode or consuming defender recovery RNG. This remains true for cold immunity and existing frozen/stunned recipients.'],
                declaredServices=['Pinned older engine,not modern executable proof. Supplied rank..rank+1elemental endpoints,cold length,stat/room/mode/kill/empty-event services are bounded;full formula/mastery/native/world/PvP/Sunder/global actor seed remain separate.',
                                  'The exact retained Telekinesis composition is reused and complete shared bodies rechecked against the canonical tree. Outer FillDamageValues/DrainItemDurability notification sites and modern registration/duplicate policy are not established by this callback composition.'],
                artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),prior_path,retained,p,binary,san,log,fixture)})
    out=ROOT/'tools/reference/shiver-armor-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Shiver Armor:7680complete older callback/roll/mitigation/HP/cold/NPC-return compositions;ASan/UBSan clean;outer notification chronology remains separate.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');probe(p.parse_args().check)
