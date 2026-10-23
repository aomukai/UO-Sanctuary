#!/usr/bin/env python3
"""Compile pinned inventory primary-identity routines with declared grid services."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
TREE = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
assert TREE['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
hashes, blocks = {}, {}


def source(path):
    raw = (STAGE / 'd2moo-pinned-source' / path).read_bytes()
    blob = next(v['sha'] for v in TREE['tree'] if v['path'] == path)
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == blob
    hashes[path] = hashlib.sha256(raw).hexdigest()
    return raw.decode()


def routine(text, name):
    match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
    assert match, name
    end = text.index('{', match.start()) + 1
    depth = 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    blocks[name] = text[match.start():end]
    return blocks[name] + '\n'


inventory = source('source/D2Common/src/D2Inventory.cpp')
items = source('source/D2Common/src/Items/Items.cpp')
cpp = r'''#include <cstdio>
#include <initializer_list>
#define __stdcall
using BOOL=int;
constexpr int FALSE=0,TRUE=1,UNIT_ITEM=4,ITEMTYPE_WEAPON=1,ITEMTYPE_MISSILE_POTION=2,
 BODYLOC_LARM=1,BODYLOC_RARM=2,INVGRID_BODYLOC=0,D2UnitInvalidGUID=-1,IFLAG_BROKEN=1,IFLAG_NOEQUIP=2;
struct D2ItemDataStrc{int dwItemFlags=0;};
struct D2UnitStrc{int dwUnitId=0,dwUnitType=UNIT_ITEM,body=0;bool weapon=true,potion=false;D2ItemDataStrc* pItemData=nullptr;};
struct D2InventoryGridStrc{D2UnitStrc* ppItems[3]={};};
struct D2InventoryStrc{bool valid=true,grid=true;int dwLeftItemGUID=-1;D2InventoryGridStrc body;};
int gBodyLocInventoryGridInfo=0;
bool INVENTORY_CheckSignature(D2InventoryStrc* p){return p&&p->valid;}
D2InventoryStrc* INVENTORY_GetPtrIfValid(D2InventoryStrc* p){return INVENTORY_CheckSignature(p)?p:nullptr;}
D2InventoryGridStrc* INVENTORY_GetGrid(D2InventoryStrc* p,int,int*){return p&&p->grid?&p->body:nullptr;}
bool INVENTORY_UnitIsItem(D2UnitStrc* p){return p&&p->dwUnitType==UNIT_ITEM;}
bool ITEMS_CheckItemTypeId(D2UnitStrc* p,int type){return p&&(type==ITEMTYPE_WEAPON?p->weapon:p->potion);}
int ITEMS_GetBodyLocation(D2UnitStrc* p){return p?p->body:0;}
'''
cpp += routine(items, 'ITEMS_CanBeEquipped')
for name in ('INVENTORY_GetLeftHandWeapon', 'INVENTORY_GetSecondaryWeapon',
             'INVENTORY_UpdateWeaponGUIDOnInsert', 'INVENTORY_UpdateWeaponGUIDOnRemoval'):
    cpp += routine(inventory, name)
cpp += r'''int id(D2UnitStrc* p){return p?p->dwUnitId:-1;}
int main(){bool first=true;std::printf("{\"identityCases\":[");
for(int gate=0;gate<32;gate++)for(int flags=0;flags<4;flags++)for(int body=0;body<3;body++)
for(int seed:{-1,10,20,30,99})for(int kind=0;kind<3;kind++){
 D2ItemDataStrc data{flags},otherData;
 D2UnitStrc left{10,UNIT_ITEM,BODYLOC_LARM,kind!=1,kind==2,&otherData};
 D2UnitStrc right{20,UNIT_ITEM,BODYLOC_RARM,kind!=1,kind==2,&otherData};
 D2UnitStrc candidate{30,UNIT_ITEM,body,bool(gate&8),false,&data};
 auto* item=gate&16?&candidate:nullptr;
 D2InventoryStrc state;state.valid=gate&2;state.grid=gate&4;state.dwLeftItemGUID=seed;
 state.body.ppItems[BODYLOC_LARM]=body==BODYLOC_LARM?item:&left;
 state.body.ppItems[BODYLOC_RARM]=body==BODYLOC_RARM?item:&right;
 auto* inv=gate&1?&state:nullptr;
 int primary=id(INVENTORY_GetLeftHandWeapon(inv)),secondary=id(INVENTORY_GetSecondaryWeapon(inv));
 INVENTORY_UpdateWeaponGUIDOnInsert(inv,item);int inserted=state.dwLeftItemGUID;
 state.dwLeftItemGUID=seed;INVENTORY_UpdateWeaponGUIDOnRemoval(inv,item);int removed=state.dwLeftItemGUID;
 if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d]",gate,flags,body,seed,kind,primary,secondary,inserted,removed);
}
std::printf("],\"equipCases\":[");first=true;
for(int present:{0,1})for(int type:{0,UNIT_ITEM})for(int dataPresent:{0,1})for(int flags=0;flags<4;flags++){
 D2ItemDataStrc data{flags};D2UnitStrc item;item.dwUnitType=type;item.pItemData=dataPresent?&data:nullptr;
 int result=ITEMS_CanBeEquipped(present?&item:nullptr);if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d]",present,type,dataPresent,flags,result);
}std::printf("]}\n");}
'''
path = STAGE / 'weapon-identity-source-probe.cpp'
binary = STAGE / 'weapon-identity-source-probe'
path.write_text(cpp)
subprocess.run(['g++', '-std=c++20', '-O2', str(path), '-o', str(binary)], check=True)
data = json.loads(subprocess.check_output([str(binary)]))
for gate, flags, body, seed, kind, primary, secondary, inserted, removed in data['identityCases']:
    # Model identities separately from native layer or item creation ordering.
    candidate = (30, bool(gate & 8), False) if gate & 16 else None
    left = candidate if body == 1 else (10, kind != 1, kind == 2)
    right = candidate if body == 2 else (20, kind != 1, kind == 2)
    available = [v if v and v[1] else None for v in (left, right)]
    match = next((v for v in available if v and v[0] == seed), None) if gate & 1 and gate & 2 and gate & 4 else None
    other = next((v for v in available if v and v != match), None) if match else None
    assert (primary, secondary) == (match[0] if match else -1, other[0] if other else -1)
    expected_insert, expected_remove = seed, seed
    if gate & 1 and candidate and candidate[1] and body in (1, 2):
        if flags == 0 and (not match or match[2]):
            if gate & 2:
                expected_insert = 30
        elif gate & 2 and seed == 30:
            expected_insert = -1
    if gate & 1 and gate & 2 and candidate and candidate[1]:
        if seed == 30:
            expected_remove = -1
        if gate & 4 and body in (1, 2):
            opposite = right if body == 1 else left
            if opposite:
                expected_remove = opposite[0]
    assert (inserted, removed) == (expected_insert, expected_remove), (gate, flags, body, seed, kind, inserted, removed)
for present, unit_type, data_present, flags, result in data['equipCases']:
    assert result == int(bool(present and (unit_type != 4 or not data_present or flags == 0)))
data_path = STAGE / 'weapon-identity-source-probe.json'
data_path.write_text(json.dumps(data, indent=2) + '\n')
out = dict(
    referenceVersion='3.3.93847', legacyCommit=TREE['sha'], sourceHashes=hashes,
    probe=dict(caseCounts={k: len(v) for k, v in data.items()}, totalCases=sum(map(len, data.values())),
               dataPath=str(data_path.relative_to(ROOT)),
               blockHashes={k: hashlib.sha256(v.encode()).hexdigest() for k, v in blocks.items()}),
    established=[
        'The inventory primary is an explicit item GUID found in either arm; secondary is the other weapon. Neither creation serial order nor presentation arm establishes primary.',
        'Successful insertion retains an existing ordinary primary; missing/nonweapon/missile-potion primary permits replacement. Removal clears the removed primary and may promote an equippable opposite-arm item.',
        'The source CanBeEquipped helper tests broken/noequip, not identification or class/stat requirements; the surrounding equip path has independent guards. The native supported pair deliberately adds converted/identified/noncrafted boundaries.'
    ],
    limits=[
        'Five unchanged legacy routines compiled with declared inventory validity/grid/type/body services. Guards and bounded invalid/stale identities are exercised; this is not a full engine or modern executable certificate.',
        'Native transaction tests establish supported ordinary/named first-equip, opposite layer and removal identity. Broader broken/requirements, swap, trade, death, class/history and full graphical client behavior remain acceptance work.',
        'Double Swing modern start68, SQ11 hand/events/target/payment and crafted/rune/material pairs remain separate; this probe enables no talent.'
    ])
(ROOT / 'tools/reference/weapon-identity-engine-evidence.json').write_text(json.dumps(out, indent=2) + '\n')
print('Primary weapon identity source cases:', out['probe']['totalCases'])
