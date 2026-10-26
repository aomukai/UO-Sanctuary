#!/usr/bin/env python3
"""Compose Boulder damage14 with full flags, mitigation, health and mode dispatch."""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable,str(ROOT/'tools/probe_sentry_dead_damage_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
    parent=json.loads((ROOT/'tools/reference/sentry-dead-damage-source-evidence.json').read_text())
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
    def routine(relative,name):
        raw=(STAGE/'d2moo-pinned-source'/relative).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=hashlib.sha256(raw).hexdigest();text=raw.decode()
        match=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert match,name
        end=text.index('{',match.start())+1;depth=1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        body=text[match.start():end];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
    base=(STAGE/'sentry-dead-damage-source-probe.cpp').read_text().split('int main()',1)[0]
    def replace(before,after):
        nonlocal base
        assert base.count(before)==1,before;base=base.replace(before,after,1)
    seed='struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};\n'
    replace('struct D2MonStatsTxt;struct D2MonsterDataStrc',seed+'struct D2MonStatsTxt;struct D2MonsterDataStrc')
    replace('int pSeed=0,dwLastHitClass=0;', 'D2SeedStrc pSeed{};int dwLastHitClass=0;')
    replace('struct D2MonStats2Txt{unsigned dwModeFlags=0;}mon2;', 'struct D2MonStats2Txt{unsigned dwFlags=0,dwModeFlags=0;}mon2;')
    replace('int nKnockBack=0;}missileRow;', 'int nKnockBack=0,dwDmgParam[2]={1,50};}missileRow;')
    replace('bool STATES_CheckState(D2UnitStrc*,int){return false;}', 'bool uninterruptible=false;bool STATES_CheckState(D2UnitStrc* p,int state){return p==&target&&state==STATE_UNINTERRUPTABLE&&uninterruptible;}')
    replace('&&physical==0', '&&(physical==0||physical==1)')
    replace('int ITEMS_RollLimitedRandomNumber(int*,int){assert(false);return 0;}unsigned ITEMS_RollRandomNumber(int*){assert(false);return 0;}', '')
    replace('MONMODE_BLOCK=22,MONMODE_GETHIT=23,MONMODE_KNOCKBACK=24', 'MONMODE_BLOCK=6,MONMODE_GETHIT=3,MONMODE_KNOCKBACK=13')
    replace('struct D2ModeChangeStrc{', 'int requested=-1;struct D2ModeChangeStrc{int mode=-1;')
    replace('void MONSTERMODE_GetModeChangeInfo(D2UnitStrc* p,int mode,D2ModeChangeStrc* out){assert(mode==0);out->pUnit=p;}', 'void MONSTERMODE_GetModeChangeInfo(D2UnitStrc* p,int mode,D2ModeChangeStrc* out){out->mode=mode;out->pUnit=p;}')
    replace('void D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc* mode,int one){assert(one==1&&mode->pUnit==&actor);modes++;actor.dwAnimMode=0;}', 'void D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc* mode,int one){assert(one==1&&mode->pUnit==&target);requested=mode->mode;modes++;}')
    # Function declarations precede full callers in the inherited source base.
    replace('int kills=0,', 'uint32_t ITEMS_RollRandomNumber(D2SeedStrc*);uint32_t ITEMS_RollLimitedRandomNumber(D2SeedStrc*,int);\nint kills=0,')
    base='#include <cstring>\n'+base
    base+='\nconstexpr int MONSTATS2FLAGINDEX_SMALL=28,MONSTATS2FLAGINDEX_LARGE=29;\n'
    for name in ['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber']:
        base+=routine('source/D2Common/include/D2Seed.h',name).replace('0x6AC690C5i64','0x6AC690C5ULL')
    for name in ['ITEMS_RollRandomNumber','ITEMS_RollLimitedRandomNumber']:
        base+=routine('source/D2Game/src/ITEMS/Items.cpp',name)
    base+=routine('source/D2Game/src/MISSILES/MissMode.cpp','MISSMODE_SrvDmg14_MoltenBoulder')
    base+=routine('source/D2Game/src/SKILLS/Skills.cpp','sub_6FD10140')
    base+='struct D2AuraCallbackStrc{D2GameStrc* pGame;D2UnitStrc* pOwner;void* pArgs;};\n'
    base+=routine('source/D2Game/src/SKILLS/SkillAss.cpp','sub_6FCF5DE0')
    base+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int kind:{0,1,2})for(int species:{0,1,2})for(int kb:{0,1})for(int guard:{0,1})for(int defense:{0,1,2,4,8,16})for(int resistance:{0,100})for(uint32_t low:{0u,1u,19u,89u,123u,410u,0xFFFFFFFFu}){
 game={};actor={};target={};missile={};row={};mon2={};missileRow={};actor.dwUnitType=UNIT_PLAYER;actor.dwUnitId=77;actor.group=1;target.group=2;target.dwUnitId=99;actor.pGame=target.pGame=missile.pGame=&game;
 game.pUnitList[0][77]=&actor;missile.dwUnitType=UNIT_MISSILE;missile.dwFlagEx=UNITFLAGEX_STOREOWNERINFO;missile.dwOwnerType=0;missile.dwOwnerGUID=77;missile.pSeed.nLowSeed=low;missile.pSeed.nHighSeed=666;
 row.dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_KILLABLE];target.stats[STAT_HITPOINTS]=25600;target.stats[STAT_DAMAGERESIST]=target.stats[STAT_FIRERESIST]=resistance;
 mon2.dwFlags=species==1?gdwBitMasks[MONSTATS2FLAGINDEX_SMALL]:species==2?gdwBitMasks[MONSTATS2FLAGINDEX_LARGE]:0;mon2.dwModeFlags=gdwBitMasks[MONMODE_BLOCK]|(kb?gdwBitMasks[MONMODE_KNOCKBACK]:0);
 missileRow.dwMissileFlags=kind==0?gdwBitMasks[MISSILESFLAGINDEX_GETHIT]:0;missileRow.nKnockBack=kind==0?1:0;missileRow.dwDmgParam[0]=1;missileRow.dwDmgParam[1]=50;
 blocked=defense;uninterruptible=guard;callbackCount=kills=modes=0;lastAttacker=nullptr;requested=-1;
 D2DamageStrc damage{};damage.dwPhysDamage=kind==1?0:1536;damage.dwFireDamage=1536;damage.wResultFlags=kind==2?5:0;D2DamageStrc original=damage;
 if(kind==2){D2AuraCallbackStrc aura{&game,&actor,&damage};sub_6FCF5DE0(&aura,&target);assert(damage.wResultFlags==original.wResultFlags&&damage.dwPhysDamage==original.dwPhysDamage&&damage.dwFireDamage==original.dwFireDamage);}
 else{if(kind==0)MISSMODE_SrvDmg14_MoltenBoulder(&game,&missile,&target,&damage);MISSMODE_SetDamageFlags(&game,&missile,&target,&damage);}
 printf("%s[%d,%d,%d,%d,%d,%d,%u,%d,%d,%llu,%d,%d]",first?"":",",kind,species,kb,guard,defense,resistance,low,target.stats[STAT_HITPOINTS],requested,(unsigned long long)missile.pSeed.lSeed,callbackCount,damage.wResultFlags);first=false;
}printf("]\n");}
'''
    path=STAGE/'molten-boulder-impact-source-probe.cpp';path.write_text(base);binary=path.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'molten-boulder-impact-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    data=json.loads(run.stdout);assert len(data)==3024
    # Central composed behavior; production comparisons check the complete grid.
    for kind,species,kb,guard,defense,resist,low,life,mode,seed,events,flags in data:
        assert life==25600 if defense or resist==100 else life==(25600-(1536 if kind==1 else 3072))
        if guard:assert mode==-1
        if mode==13:assert kind==0 and kb and not guard
        if kind!=0:assert seed==(666<<32)|low
    fixture=STAGE/'molten-boulder-impact-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',handlerEnabled=False,installed=False,legacyCommit=tree['sha'],sourceHashes=hashes,routineHashes=blocks,
        reusedSourceEvidence='tools/reference/sentry-dead-damage-source-evidence.json',reusedRoutineHashes=parent['compiledRoutineHashes'],
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data)),
        established=['Complete damage14 and SetDamageFlags compose with unchanged mitigation, ExecuteEvents health and ExecuteMissileDamage mode dispatch for player-owned rolling, unskilled fire and copied-area packets.',
            'The separate row KnockBack1 draw still occurs after damage14 unless uninterruptible. Uninterruptible permits event health loss but suppresses mode changes.',
            'A blocked direct hit can still request knockback when the target has that mode. Missing knockback mode replaces its flag with get-hit before block/mode processing. Area wrapper supplies block-check0 and has no damage14 or missile RNG draw.',
            'Copied-area wrapper retains the original packet unchanged per victim; owner/event/health/mode paths are executed, rather than recorded as a damage-commit sentinel.'],
        limits=['Declared block/dodge masks, species/mode records, resistance/owner stats, native mode callback and empty event subscriber services reuse the parent fixture; no live path movement or item subscriber is simulated.',
            'Monster mode numeric constants GH3/BL6/KB13 follow the pinned source header; SMALL/LARGE remain named logical fixture flags. Current executable and all live native actor/state/proc contexts remain separate.'],
        sanitizers='ASan/UBSan exit0 and empty runtime stderr required')
    out=ROOT/'tools/reference/molten-boulder-impact-source-evidence.json'
    if check:assert json.loads(out.read_text())==evidence
    else:out.write_text(json.dumps(evidence,indent=2)+'\n')
    print('Molten Boulder:3024complete impact/health/mode/RNG contexts pass with clean sanitizers.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
