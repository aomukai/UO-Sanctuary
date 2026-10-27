#!/usr/bin/env python3
"""Execute complete composite weapon/range routing with current form metadata."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}

    def source(name):
        path = BASE / 'd2moo-pinned-source' / name
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == name)
        hashes[name] = sha(path)
        return raw.decode()

    def extract(name, symbol):
        body = function(source(name), symbol)
        routines[symbol] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    def enum(name, symbol):
        match = re.search(r'enum ' + symbol + r'\s*\{.*?\};', source(name), re.S)
        assert match, symbol
        return match.group() + '\n'

    preflight_path = ROOT / 'tools/reference/druid-forms-preflight.json'
    preflight = json.loads(preflight_path.read_text())
    for name in ['wolf', 'bear']:
        assert preflight['currentRows']['monstats2'][name]['BaseW'] == 'hth'
        assert preflight['currentRows']['states'][name]['meleeonly'] == '1'
    cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __stdcall
#define D2_ASSERT assert
using BOOL=int;
'''
    for path, name in [
        ('D2Composit.h', 'D2WeaponClasses'), ('D2Composit.h', 'D2Composits'),
        ('Units/Player.h', 'D2C_PlayerModes'), ('Units/Player.h', 'D2C_PlayerClasses'),
        ('Units/Units.h', 'D2C_UnitTypes'), ('Units/Units.h', 'D2C_UnitFlagsEx'),
        ('D2Monsters.h', 'D2C_MonModes'), ('DataTbls/MonsterTbls.h', 'D2C_MonStats2TxtFlagIndices'),
        ('D2States.h', 'D2C_StateMasks')]:
        cpp += enum('source/D2Common/include/' + path, name)
    cpp += r'''
// Inventory services are guards: no player equipment branch is executed.
constexpr int BODYLOC_RARM=4,BODYLOC_LARM=5,ITEMTYPE_WEAPON=0;
struct D2InventoryStrc{};
struct D2MonStats2Txt{unsigned dwFlags=0;int nBaseW=' hth';}mon;
struct D2CharStatsTxt{int dwBaseWClass=' hth';}character;
struct D2UnitStrc{int dwUnitType=UNIT_PLAYER,dwClassId=PCLASS_DRUID,dwAnimMode=PLRMODE_ATTACK1;
 unsigned dwFlagEx=0;D2InventoryStrc* pInventory=nullptr;}owner;
struct D2SkillsTxt{int nRange=3;}row;
struct D2SkillStrc{D2SkillsTxt* pSkillsTxt=&row;}skill;
unsigned gdwBitMasks[32];int form=0;bool melee=false,metadata=true;
int UNITS_GetAnimOrSeqMode(D2UnitStrc* p){return p->dwAnimMode;}
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int* type,int* cls,int* mode,const char*,int)
{assert(form==139||form==140);*type=UNIT_MONSTER;*cls=form==139?430:431;*mode=MONMODE_ATTACK1;}
int COMPOSIT_GetWeaponClassIdFromCode(int code){switch(code){case ' hth':return WEAPONCLASS_HTH;case ' wob':return WEAPONCLASS_BOW;case ' wbx':return WEAPONCLASS_XBW;default:assert(false);return -1;}}
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(D2InventoryStrc*,int){assert(false);return nullptr;}
bool ITEMS_CanBeEquipped(D2UnitStrc*){assert(false);return false;}
uint8_t ITEMS_GetComponent(D2UnitStrc*){assert(false);return 0;}
D2CharStatsTxt* UNITS_GetCharStatsTxtRecord(int cls){assert(cls==PCLASS_DRUID);return &character;}
bool ITEMS_CheckItemTypeId(D2UnitStrc*,int){assert(false);return false;}
D2UnitStrc* INVENTORY_GetLeftHandWeapon(D2InventoryStrc*){assert(false);return nullptr;}
void INVENTORY_UpdateWeaponGUIDOnInsert(D2InventoryStrc*,D2UnitStrc*){assert(false);}
int ITEMS_GetWeaponClassId(D2UnitStrc*){assert(false);return 0;}
int INVENTORY_GetWieldType(D2UnitStrc*,D2InventoryStrc*){assert(false);return 0;}
int ITEMS_Get2HandWeaponClassCode(D2UnitStrc*){assert(false);return 0;}
int ITEMS_GetWeaponClassCode(D2UnitStrc*){assert(false);return 0;}
D2MonStats2Txt* UNITS_GetMonStats2TxtRecordFromMonsterId(int id){assert(id==430||id==431);return metadata?&mon:nullptr;}
bool STATES_CheckStateMaskOnUnit(D2UnitStrc*,int mask){assert(mask==STATEMASK_MELEEONLY);return melee;}
'''
    for name in ['COMPOSIT_GetWeaponClassCode', 'COMPOSIT_IsWeaponBowOrXBow']:
        cpp += extract('source/D2Common/src/D2Composit.cpp', name)
    cpp += extract('source/D2Common/src/D2Skills.cpp', 'D2Common_11016')
    cpp += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;
 printf("[");bool first=true;
 for(int shape:{0,139,140})for(int base:{0,1,2})for(int range:{0,1,2,3})for(int mask:{0,1}){
  form=shape;owner.dwFlagEx=shape?UNITFLAGEX_ISSHAPESHIFTED:0;
  character.dwBaseWClass=base==0?' hth':base==1?' wob':' wbx';row.nRange=range;melee=mask;metadata=true;
  int bow=COMPOSIT_IsWeaponBowOrXBow(&owner),resolved=D2Common_11016(&owner,&skill);
  printf("%s[%d,%d,%d,%d,%d,%d]",first?"":",",shape,base,range,mask,bow,resolved);first=false;
 }
 printf("]\n");
 // Missing skill/row and missing transformed model remain separate paths.
 assert(D2Common_11016(&owner,nullptr)==0);skill.pSkillsTxt=nullptr;assert(D2Common_11016(&owner,&skill)==0);
 form=139;metadata=false;assert(COMPOSIT_IsWeaponBowOrXBow(&owner)==0);
}
'''
    generated = BASE / 'druid-form-weapon-routing-source-probe.cpp'
    generated.write_text(cpp)
    compiler = BASE / 'druid-form-weapon-routing-compiler.log'
    with compiler.open('wb') as output:
        # Original four-character source literals must remain unchanged.
        subprocess.run(['g++', '-std=c++20', '-Wall', '-Wextra', '-Werror', '-Wno-multichar', '-O1', '-g',
            '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], stdout=output, stderr=subprocess.STDOUT, check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    sanitizer = BASE / 'druid-form-weapon-routing-sanitizer.log';sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-2000:]
    cases = json.loads(process.stdout)
    assert len(cases) == 72
    for form, base, range_, mask, bow, resolved in cases:
        expected_bow = int(form == 0 and base in [1, 2])
        expected_range = (1 if mask else expected_bow + 1) if range_ == 3 else range_
        assert bow == expected_bow and resolved == expected_range
    fixture = BASE / 'druid-form-weapon-routing-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourcePreparation=True, nativeAccepted=False, currentExecutableParity=False,
        currentPreflightSha256=sha(preflight_path), sourceHashes=hashes, completeRoutineHashes=routines, contexts=72, guards=3,
        generatedSourceSha256=sha(generated), compilerLogSha256=sha(compiler), sanitizerLogSha256=sha(sanitizer),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)),
        established=[
            'Complete composite-class selector,bow detector and dynamic skill-range functions execute unchanged. Current wolf430/bear431BaseW=hthand meleeonly1metadata are pinned in the licensed/current corroborated preflight. Original player identity remains separate from converted animation identity.',
            'Both forms select their HTHcomposite weapon class independently of the declared human base class. Common Attack range=both resolves to melee1. Human HTH/BOW/XBWmetadata controls preserve dynamic range and the separate player-only melee mask rule. Missing skill,row and model controls retain their own paths.',
        ], remaining=[
            'Conversion,table lookup,weapon-code linker and state-mask lookup are declared services. Human equipped-inventory,dual-hand and exceptional model branches are guarded unexecuted; no equipment-selection acceptance is claimed.',
            'These results prepare native effective-attack classification,range,ammunition/projectile suppression and property consumers. Exact source distance/footprint,full transformed Attack start/do damage/AR/SrcDam,modern executable/client and learned casting remain separate. No native form attack is enabled.',
        ])
    path = ROOT / 'tools/reference/druid-form-weapon-routing-engine-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Druid form weapon routing:72complete source class/bow/range contexts+3guards,ASan/UBSan clean; no native attack enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
