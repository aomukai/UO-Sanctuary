#!/usr/bin/env python3
"""Compile complete historical layered weapon-block and block/dodge dispatch.

Equipment composition, stat supply and RNG remain declared services; this
preparation does not enable the learned native skill.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, blocks = {}, {}

    def source(path):
        raw = (STAGE / 'd2moo-pinned-source' / path).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == path)
        hashes[path] = hashlib.sha256(raw).hexdigest()
        return raw.decode()

    def routine(text, name):
        match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end = text.index('{', match.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        blocks[name] = text[match.start():end]
        return blocks[name] + '\n'

    damage = source('source/D2Game/src/UNIT/SUnitDmg.cpp')
    header = source('source/D2Game/include/UNIT/SUnitDmg.h')
    flags = {name: int(value, 16) for name, value in re.findall(r'^\s*(BLOCKFLAG_\w+)\s*=\s*(0x[0-9A-Fa-f]+)', header, re.M)}
    code = r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <iterator>
#include <algorithm>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,PLRMODE_WALK=2,PLRMODE_RUN=3,MONMODE_WALK=2,MONMODE_RUN=15;
constexpr int BODYLOC_RARM=0,BODYLOC_LARM=1,STAT_PASSIVE_WEAPONBLOCK=0,STAT_PASSIVE_EVADE=1,STAT_PASSIVE_DODGE=2,STAT_PASSIVE_AVOID=3;
struct D2UnitStrc{int dwUnitType=0,dwAnimMode=1,pSeed=0;void* pInventory=nullptr;int itemType=0;} attacker,defender,items[2];
struct D2GameStrc{bool bExpansion=true;} game;
struct D2StatStrc{int nLayer=0,nValue=0;};
D2StatStrc layers[32];int count=0,chances[4]={},shield=0,draw=0,draws=0,weaponClass=0;
int STATLIST_CopyStats(D2UnitStrc*,int,D2StatStrc* out,size_t cap){int n=std::min(count,(int)cap);for(int i=0;i<n;i++)out[i]=layers[i];return n;}
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(void*,int loc){return items[loc].itemType?&items[loc]:nullptr;}
bool ITEMS_CheckItemTypeId(D2UnitStrc* item,int type){return item && (item->itemType==type || item->itemType==3 && type==1);}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){return chances[stat];}
int ITEMS_RollRandomNumber(int*){draws++;return draw;}
void COMPOSIT_GetWeaponClassId(D2UnitStrc*,void*,int* value,int,int){*value=weaponClass;}
int UNITS_GetBlockRate(D2UnitStrc*,bool){return shield;}
bool UNITS_IsInMovingMode(D2UnitStrc* unit){return unit->dwAnimMode==PLRMODE_WALK || unit->dwAnimMode==PLRMODE_RUN;}
'''
    code += 'constexpr uint32_t ' + ','.join(name + '=' + str(value) for name, value in flags.items()) + ';\n'
    for name in ['SUNITDMG_GetWeaponBlock', 'SUNITDMG_ApplyDodge', 'SUNITDMG_ApplyBlockOrDodge']:
        code += routine(damage, name)
    code += r'''int main(){bool first=true;std::printf("[");
for(int scenario=0;scenario<8;scenario++)for(int left=0;left<4;left++)for(int right=0;right<4;right++)for(int inventory:{0,1})for(int mode:{0,1,2})for(int composite:{0,13})for(int avoid:{0,1})for(int value:{0,25,75})for(int block:{0,1})for(int roll:{0,19,20,25,26,39,40,59,60,99}){
 count=0;if(scenario==1){layers[count++]={1,26};}if(scenario==2){layers[count++]={3,59};}
 if(scenario==3){layers[count++]={1,40};layers[count++]={1,60};}
 if(scenario==4){layers[count++]={0,60};layers[count++]={1,20};}
 if(scenario==5){layers[count++]={1,60};layers[count++]={0,20};}
 if(scenario==6){layers[count++]={2,59};layers[count++]={1,26};}
 if(scenario==7){layers[count++]={1,60};layers[count++]={-1,-2};}
 items[0].itemType=right;items[1].itemType=left;defender.pInventory=inventory?&game:nullptr;
 defender.dwAnimMode=mode==1?PLRMODE_WALK:mode==2?PLRMODE_RUN:1;
 for(int& chance:chances)chance=0;draw=roll;draws=0;shield=value;weaponClass=composite;
 int chance=SUNITDMG_GetWeaponBlock(&defender);
 int result=SUNITDMG_ApplyBlockOrDodge(&game,&attacker,&defender,avoid,block);
 if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",scenario,left,right,inventory,mode,composite,avoid,value,block,roll,chance,result,draws);
}std::printf("]\n");}
'''
    path = STAGE / 'weapon-block-source-probe.cpp'
    path.write_text(code)
    binary = STAGE / 'weapon-block-source-probe'
    subprocess.run(['g++', '-std=c++20', '-O2', str(path), '-o', str(binary)], check=True)
    rows = json.loads(subprocess.check_output([str(binary)]))
    assert len(rows) == 8 * 4 * 4 * 2 * 3 * 2 * 2 * 3 * 2 * 10
    scenarios = [[], [(1,26)], [(3,59)], [(1,40),(1,60)], [(0,60),(1,20)], [(1,60),(0,20)], [(2,59),(1,26)], [(1,60),(-1,-2)]]
    for scenario,left,right,inventory,mode,composite,avoid,shield,block,roll,chance,result,draws in rows:
        maximum = 0
        for layer,value in scenarios[scenario]:
            matches = inventory and any(item == layer or item == 3 and layer == 1 for item in [left,right] if item)
            if layer <= 0 or matches and value > maximum:
                maximum = value
        assert chance == maximum
        shield_chance = shield // 3 if mode == 2 else shield
        shield_try = block and shield_chance > 0
        shield_hit = shield_try and roll < shield_chance
        weapon_try = not shield_hit and not mode and chance > 0 and composite == 13
        expected = flags['BLOCKFLAG_BLOCK'] if shield_hit else flags['BLOCKFLAG_WEAPONBLOCK'] if weapon_try and roll < chance else flags['BLOCKFLAG_NONE']
        assert (result,draws) == (expected,int(shield_try)+int(weapon_try))
    fixture = STAGE / 'weapon-block-source-probe.json'
    fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    evidence = dict(referenceVersion='3.3.93847',talentId='assassin.weaponblock',proofEnablesHandler=False,legacyCommit=tree['sha'],sourceHashes=hashes,
        compiledUnchangedRoutines={name:hashlib.sha256(body.encode()).hexdigest() for name,body in blocks.items()},
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(rows)),
        established=['Complete layered stat getter uses either-hand item-type eligibility, positive-layer maximum and the original nonpositive-layer assignment precedence. Parent h2h2-to-h2h type matching is a declared nested service.',
            'Complete dispatch tries eligible shield first, then stationary composite13 Weapon Block, then Avoid/Dodge. Walking/running returns through Evade before weapon-block stat collection. Equality fails each roll; only positive eligible chances consume a draw.'],
        services=['Declared inventory/stat supplies, item-type inheritance, composite class0/13, zero Amazon passives, block rate and repeated supplied RNG draw. The composite class is not derived from real equipment in this probe. Full composite/equip transaction evidence remains required.',
            'Eight synthetic stat-layer inventories include mixed and nonpositive layers to expose source precedence; these are not a claim that learned Weapon Block supplies such foreign layers. Only player dispatch is exercised.'],
        limits=['Historical source preparation only. No modern executable or native skill delivery is established. Full usable dual-claw equipment, current dm12 curve, native attack/missile defense stages, mode/recovery/throttle/event/RNG consumers, persistence and item/foreign grants remain separate.',
            'Source running shield reduction is retained in this comparison; Sanctuary approved accepted-running-step avoidance disable takes precedence in native combat.'])
    target=ROOT/'tools/reference/weapon-block-engine-evidence.json'
    rendered=json.dumps(evidence,indent=2)+'\n'
    if check:
        assert target.read_text()==rendered,'Weapon Block evidence changed'
    else:
        target.write_text(rendered)
    print(f'Weapon Block:{len(rows)} complete layered getter/block dispatch source cases pass; native/equipment gate remains closed.')


if __name__ == '__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
