#!/usr/bin/env python3
"""Bounded immutable legacy Conversion AI gates and expiry callback, not D2R parity."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
COMMIT = '0c0bbd1e6bef9181a3da1c046523af622a925416'
tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
assert tree['sha'] == COMMIT and not tree['truncated']
entries = {r['path']: r['sha'] for r in tree['tree']}
paths = {key: STAGE / 'd2moo-pinned-source' / path for key, path in {
    'pal': 'source/D2Game/src/SKILLS/SkillPal.cpp',
    'unit': 'source/D2Common/src/Units/Units.cpp',
    'think': 'source/D2Game/src/AI/AiThink.cpp',
    'util': 'source/D2Game/src/AI/AiUtil.cpp',
}.items()}
for path in paths.values():
    blob = path.read_bytes()
    relative = path.relative_to(STAGE / 'd2moo-pinned-source').as_posix()
    assert hashlib.sha1(b'blob '+str(len(blob)).encode()+b'\0'+blob).hexdigest() == entries[relative]


def routine(key, name):
    text = paths[key].read_text()
    at = text.index('__fastcall '+name+'(')
    start = text.rfind('\n', 0, at)+1
    end = text.index('{', at)+1
    depth = 1
    while depth:
        depth += (text[end] == '{')-(text[end] == '}')
        end += 1
    return text[start:end]


cpp = r'''#include <cstdint>
#include <cstdio>
#define __fastcall
using BOOL=int; constexpr int FALSE=0,TRUE=1,UNIT_MONSTER=1;
using D2C_AiSpecialState=int;
constexpr int AISPECIALSTATE_INVALID=5,AISPECIALSTATE_DIMVISION=1,AISPECIALSTATE_TERROR=2,AISPECIALSTATE_TAUNT=3;
constexpr int STATE_UNINTERRUPTABLE=1,STATE_CONVERSION_SAVE=2;
constexpr int UNITFLAG_CANBEATTACKED=1,MONTYPEFLAG_UNIQUE=1,MONTYPEFLAG_SUPERUNIQUE=2;
constexpr int MONSTATS2MODEFLAG_WL=1,MONSTATSFLAG_BOSS=1,MONSTATSFLAG_SWITCHAI=2;
constexpr int STAT_CONVERSION_LEVEL=0,STAT_CONVERSION_MAXHP=1,STAT_LEVEL=2,STAT_HITPOINTS=3,STAT_MAXHP=4;
struct D2GameStrc{} game;
struct D2UnitStrc{int dwUnitType=1,dwClassId=1,dwFlags=1,level=30,hp=75*256,maxhp=100*256,monType=0;bool uninterruptible=false,dead=false;};
struct D2StatListStrc{int level=80,maxhp=400;};
struct D2MonStatsTxt{int wMonStatsEx=1,dwMonStatsFlags=2;};
struct D2MonStats2Txt{int dwModeFlags=1;};
D2MonStatsTxt monster;D2MonStats2Txt modes;D2StatListStrc saved;
bool hasRow=true,hasModes=true,hasSaved=true;
int alignment=-1,targets=0,freed=0,removed=0,toggled=0;
D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int id){return hasRow&&id>=0?&monster:nullptr;}
D2MonStats2Txt* UNITS_GetMonStats2TxtRecord(int){return hasModes?&modes:nullptr;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int id){return DATATBLS_GetMonStatsTxtRecord(id);}
int STATES_CheckState(D2UnitStrc* p,int){return p->uninterruptible;}
int SUNIT_IsDead(D2UnitStrc* p){return p->dead;}
int MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc* p,int flags){return (p->monType&flags)!=0;}
void STATES_ToggleState(D2UnitStrc*,int,int){toggled++;}
void sub_6FCBDD30(D2UnitStrc*,uint8_t value,int){alignment=value;}
D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc*){return &game;}
void D2GAME_TARGETS_Last_6FC40380(D2GameStrc*,D2UnitStrc*){targets++;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return hasSaved?&saved:nullptr;}
int STATLIST_GetStatValue(D2StatListStrc* s,int stat,int){return stat==STAT_CONVERSION_LEVEL?s->level:s->maxhp;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc* p){return p->maxhp;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_HITPOINTS?p->hp:stat==STAT_MAXHP?p->maxhp:p->level;}
int MONSTERUNIQUE_CalculatePercentage(int value,int percent,int divisor){return int(int64_t(value)*percent/divisor);}
void STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){if(stat==STAT_HITPOINTS)p->hp=value;else if(stat==STAT_MAXHP)p->maxhp=value;else p->level=value;}
void D2Common_10474(D2UnitStrc*,D2StatListStrc*){removed++;}
void STATLIST_FreeStatList(D2StatListStrc*){freed++;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndFlag(D2UnitStrc*,int){return nullptr;} // No auxiliary lists: ordering/contents not certified.
'''
cpp += routine('unit', 'UNITS_CanSwitchAI')+'\n'
cpp += routine('think', 'AITHINK_CanUnitSwitchAi')+'\n'
cpp += routine('util', 'AIUTIL_CanUnitSwitchAi')+'\n'
cpp += routine('pal', 'SKILLS_StatRemoveCallback_Conversion')+'\n'
cpp += r'''int main(){
printf("{\"eligibilityCases\":[");bool first=true;
for(int flags=0;flags<4;flags++)for(int walk=0;walk<2;walk++)for(int unique=0;unique<4;unique++)for(int locked=0;locked<2;locked++){
 D2UnitStrc actor;actor.monType=unique;actor.uninterruptible=locked;
 monster.dwMonStatsFlags=flags;modes.dwModeFlags=walk;
 int result=AIUTIL_CanUnitSwitchAi(&actor,AISPECIALSTATE_TERROR);
 printf("%s[%d,%d,%d,%d,%d]",first?"":",",flags,walk,unique,locked,result);first=false;
}
printf("],\"expiryCases\":[");first=true;
for(int present=0;present<2;present++)for(int maxhp: {0,100})for(int hp: {0,1,50,100}){
 D2UnitStrc actor;actor.maxhp=maxhp*256;actor.hp=hp*256;
 hasSaved=present;alignment=-1;targets=freed=removed=toggled=0;
 SKILLS_StatRemoveCallback_Conversion(&actor,5,nullptr);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",present,maxhp,hp,actor.level,actor.hp,actor.maxhp,alignment,targets,freed);first=false;
}
printf("]}\n");}
'''
cpp = '#include <initializer_list>\n'+cpp
source = STAGE / 'conversion-source-probe.cpp'
binary = STAGE / 'conversion-source-probe'
source.write_text(cpp)
subprocess.run(['c++', '-std=c++20', '-O2', str(source), '-o', str(binary)], check=True)
result = json.loads(subprocess.check_output([str(binary)], text=True))
for flags, walk, unique, locked, actual in result['eligibilityCases']:
    assert actual == int(bool(flags & 2) and not(flags & 1) and walk and not unique and not locked)
for present, maxhp, hp, level, output_hp, output_max, align, target_calls, freed in result['expiryCases']:
    assert align == 0 and target_calls == 1
    if present:
        assert level == 80 and output_hp == (400*hp//maxhp if maxhp else 1)*256
        assert output_max == 400 and freed == 1  # Literal legacy whole/FP256 mismatch, not a modern policy.
    else:
        assert (level, output_hp, output_max, freed) == (30, hp*256, maxhp*256, 0)
evidence = dict(referenceVersion='3.3.93847', sourceCommit=COMMIT,
    sourceHashes={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths.values()},
    sourceTreesVerifiedAgainstGitBlobs=True, probe=result,
    limits=[
        'Unmodified legacy AI gates/expiry callback with declared stat/table/alignment/target stubs; not live Conversion or modern executable certification.',
        'Expiry restores saved whole maximum life without shifting by8, while hitpoints are shifted. Do79 saves whole maximum life; this discrepancy requires modern evidence, not a silently invented correction.',
        'Auxiliary stat lists are absent in this probe. Alignment room counts, summon target tree, species abilities, owner/death/kill credit, aura leases and native save ownership need separate integration.',
        'Do79 checks a prepared combat record, not successful-hit flag; shared St32 also uses calc1 as enhanced damage. Modern event ordering and hidden damage/miss behavior require comparison.',
    ])
(ROOT / 'tools/reference/conversion-engine-evidence.json').write_text(json.dumps(evidence, indent=2)+'\n')
(STAGE / 'conversion-source-probe.json').write_text(json.dumps(result, indent=2)+'\n')
print(f"Conversion: {len(result['eligibilityCases'])} AI eligibility and {len(result['expiryCases'])} expiry cases passed; legacy life-unit discrepancy retained.")
