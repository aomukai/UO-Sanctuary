#!/usr/bin/env python3
"""Compose complete older damage/contact/Life Tap/ATD/Thorns with declared world services."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
CACHE=Path('/home/aomukai/.codex/worktrees/death-sentry-damage/Sanctuary/.local/class-skills')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def build(check=False):
    proof_path=ROOT/'tools/reference/goatman-npc-release-source-evidence.json'
    proof=json.loads(proof_path.read_text())
    parent=CACHE/'goatman-npc-release/source.cpp'
    assert sha(parent)==proof['artifacts']['.local/class-skills/goatman-npc-release/source.cpp']
    cpp=parent.read_text().split('bool first=true;')[0]
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes={};bodies={}
    for relative,names in [('source/D2Game/src/UNIT/SUnitDmg.cpp',['SUNITDMG_ExecuteEvents','SUNITDMG_DrainItemDurability']),
        ('source/D2Game/src/SKILLS/SkillNec.cpp',['D2GAME_EventFunc05_6FD0EDE0']),
        ('source/D2Game/src/SKILLS/SkillItem.cpp',['SKILLITEM_EventFunc06_AttackerTakesDamage']),
        ('source/D2Game/src/SKILLS/SkillPal.cpp',['SKILLS_ApplyThornsDamage'])]:
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=sha(path)
        for name in names:bodies[name]=function(raw.decode(),name)
    for name in ['SUNITDMG_ExecuteEvents','SUNITDMG_DrainItemDurability']:assert bodies[name] in cpp
    def replace(a,b):
        nonlocal cpp
        assert cpp.count(a)==1,a[:120]
        cpp=cpp.replace(a,b,1)
    replace('struct D2SkillsTxt{int dwCalc[4]', 'struct D2SkillsTxt{int wPrgOverlay=0;int dwCalc[4]')
    replace('bool STATES_CheckState(D2UnitStrc*,int){return false;}',
        'bool tapOn=false,deathDelay=false;constexpr int LIFE_TAP_STATE=42,STAT_THORNS_PERCENT=1200;int beforeHealthActor=-1,afterHealthTarget=-1,beforeModeTarget=-1;\nbool STATES_CheckState(D2UnitStrc*u,int state){return u==&defender&&((state==LIFE_TAP_STATE&&tapOn)||(state==STATE_DEATH_DELAY&&deathDelay));}')
    replace('D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){return hasRow&&id==currentId?&skill:nullptr;}',
        'D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){return hasRow&&(id==currentId||id==82)?&skill:nullptr;}')
    replace('int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int id,int rank){if(id==416)return 0;',
        'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int id,int rank){if(id==82)return 50;if(id==416)return 0;')
    replace('void SUNITEVENT_Trigger(D2GameStrc*,int event,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*d){events++;trace.push_back({20,event,d->dwPhysDamage});}',
        'void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*);')
    replace('int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*){return 10000000;}',
        'int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*){return 100*256;}')
    replace('void sub_6FC6E890(D2GameStrc*,D2UnitStrc*){trace.push_back({4});}',
        'void sub_6FC6E890(D2GameStrc*,D2UnitStrc*u){if(u==&attacker)beforeHealthActor=u->stats[STAT_HITPOINTS];trace.push_back({4});}')
    replace('void SUNITDMG_ApplyColdState(D2UnitStrc*,D2UnitStrc*,int n){trace.push_back({6,n});}',
        'void SUNITDMG_ApplyColdState(D2UnitStrc*,D2UnitStrc*u,int n){if(u==&defender)afterHealthTarget=u->stats[STAT_HITPOINTS];trace.push_back({6,n});}')
    replace('void SKILLS_ApplyThornsDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){thornsCalls++;trace.push_back({7});}',
        'void SKILLS_ApplyThornsDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*);')
    replace('void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*d){modeCalls++;dispatched=*d;trace.push_back({8,d->wResultFlags,d->dwHitClass});}',
        'void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*u,D2DamageStrc*d){modeCalls++;dispatched=*d;if(u==&defender)beforeModeTarget=u->stats[STAT_HITPOINTS];if(d->wResultFlags&2)u->dwAnimMode=0;trace.push_back({8,d->wResultFlags,d->dwHitClass});}')
    replace('wantedState=state;if(u==&attacker',
        'wantedState=state;if(u==&defender&&state==LIFE_TAP_STATE&&tapOn)return &stateList;if(u==&attacker')
    cpp+='\nconstexpr int UNITFLAG_CANBEATTACKED=1;\nint STATLIST_GetOwnerType(D2StatListStrc*){return 1;}\nint STATLIST_GetOwnerGUID(D2StatListStrc*){return 17;}\nD2UnitStrc*SUNIT_GetServerUnit(D2GameStrc*,int,int){return &attacker;}\n'
    for name in ['D2GAME_EventFunc05_6FD0EDE0','SKILLITEM_EventFunc06_AttackerTakesDamage','SKILLS_ApplyThornsDamage']:cpp+=bodies[name]+'\n'
    cpp+=r'''
void SUNITEVENT_Trigger(D2GameStrc*g,int event,D2UnitStrc*a,D2UnitStrc*b,D2DamageStrc*d){
events++;trace.push_back({20,event,d->dwPhysDamage});
if(event==UNITEVENT_DAMAGEDINMELEE&&a==&defender&&tapOn)D2GAME_EventFunc05_6FD0EDE0(g,event,a,b,d,82,1);
if(event==UNITEVENT_ATTACKEDINMELEE&&a==&defender)SKILLITEM_EventFunc06_AttackerTakesDamage(g,event,a,b,d,78<<16,0);
}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int tap:{0,1})for(int flags:{0,1,33,16})for(int hp:{256,5*256,30*256,50*256})for(int ahp:{256,10*256,50*256})for(int absorb:{0,10*256,100*256})for(int delayed:{0,1})for(int flat:{0,4,50})for(int percent:{0,100,250}){
Reset(416,1);mon={};victimHire=victimBoss=victimUnique=false;victimStunPresent=false;tapOn=tap;deathDelay=delayed;beforeHealthActor=afterHealthTarget=beforeModeTarget=-1;
attacker.dwAnimMode=4;defender.dwAnimMode=1;attacker.dwFlags=defender.dwFlags=UNITFLAG_CANBEATTACKED;
attacker.stats[STAT_HITPOINTS]=ahp;defender.stats[STAT_HITPOINTS]=hp;defender.stats[78]=flat;defender.stats[STAT_THORNS_PERCENT]=percent;skill.wAuraTargetState=LIFE_TAP_STATE;
allocated={};allocated.dwAttackerType=allocated.dwDefenderType=1;allocated.dwAttackerId=17;allocated.dwDefenderId=29;allocated.pGame=&game;
allocated.tDamage.dwPhysDamage=20*256;allocated.tDamage.dwDmgTotal=20*256;allocated.tDamage.wResultFlags=flags;allocated.tDamage.dwAbsLife=absorb;attacker.pCombat=&allocated;
SUNITDMG_DrainItemDurability(&game,&attacker,&defender,0);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",tap,flags,hp,ahp,absorb,delayed,flat,percent,beforeHealthActor,afterHealthTarget,beforeModeTarget,attacker.stats[STAT_HITPOINTS],defender.stats[STAT_HITPOINTS],attacker.dwAnimMode==0,defender.dwAnimMode==0,dispatched.wResultFlags,dispatched.dwPhysDamage);first=false;
}printf("]\n");}
'''
    for name,body in bodies.items():assert body in cpp,name
    generated=BASE/'monster-weapon-phases-source-probe.cpp';generated.write_text(cpp);binary=generated.with_suffix('')
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(binary)],capture_output=True)
    (BASE/'monster-weapon-phases-compiler.log').write_bytes(run.stdout+run.stderr)
    assert run.returncode==0,run.stderr.decode()[-4000:]
    run=subprocess.run([str(binary)],capture_output=True);(BASE/'monster-weapon-phases-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    rows=json.loads(run.stdout);assert len(rows)==5184,len(rows)
    fixture=BASE/'monster-weapon-phases-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(rows),sourceHashes=hashes,
        unchangedRoutineHashes={name:hashlib.sha256(body.encode()).hexdigest() for name,body in bodies.items()},
        parentFixture=dict(path=str(parent),sha256=sha(parent),receipt=str(proof_path.relative_to(ROOT)),receiptSha256=sha(proof_path)),
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture)),generatedSourceSha256=sha(generated),sanitizers='ASan+UBSan exit0;empty runtime stderr',
        scope='Complete unchanged incoming ExecuteEvents/contact,Life Tap,flat ATD and percentage Thorns compose with declared stats,world eligibility/event registration and reaction/death-mode services. Incoming packet is supplied already mitigated. Return mode service marks death on cached flag;this is not full ExecuteMissileDamage/current executable or actual source event-registry execution.',
        remaining=['Native actual actors/state consumers must verify the corresponding phases without replaying HP or immediate incoming native death. Actual event registry priority/duplication,complete leech/poison/burn/allocation/return adapters and client acceptance remain separate.'])
    target=ROOT/'tools/reference/monster-weapon-phases-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==text
    else:target.write_text(text)
    print('Monster weapon phases:5184complete-source Life Tap/absorb/HP/flat/percentage compositions pass ASan+UBSan;mode/stat/event services explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
