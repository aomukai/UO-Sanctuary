#!/usr/bin/env python3
"""Compile immutable summon equipment selection; item affix generation is a declared service."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
REF = ROOT / 'tools/reference/d2r-named-equipment'


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
        found = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert found, name
        end = text.index('{', found.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        blocks[name] = text[found.start():end]
        return blocks[name] + '\n'

    equip = routine(source('source/D2Game/src/SKILLS/SkillAss.cpp'), 'sub_6FCF9580')
    create = routine(source('source/D2Game/src/MONSTER/MonsterAI.cpp'), 'sub_6FC61190')
    summon = routine(source('source/D2Game/src/SKILLS/SkillAma.cpp'), 'SKILLS_SrvDo016_Valkyrie')
    nec = source('source/D2Game/src/SKILLS/SkillNec.cpp')
    routine(nec, 'D2GAME_SetSummonPassiveStats_6FD0C530')
    routine(nec, 'D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10')
    manifest = json.loads((REF / 'sources.json').read_text())['files']
    tables, pins = {}, {}
    for name in ('monequip', 'skills', 'monstats', 'monstats2', 'pettype', 'weapons', 'armor', 'misc'):
        path = REF / 'json' / (name + '.json')
        pins[name] = hashlib.sha256(path.read_bytes()).hexdigest()
        assert pins[name] == manifest['json/' + name + '.json']['sha256']
        tables[name] = json.loads(path.read_text())
    rows = list(tables['monequip'].values())
    valk = [x for x in rows if x.get('monster') == 'valkyrie']
    assert len(valk) == 19 and [x['level'] for x in valk] == [27,25,23,21,19,17,16,14,13,12,11,10,8,7,5,4,2,0,0]
    locations = {'head':1,'neck':2,'tors':3,'rarm':4,'larm':5,'rrin':6,'lrin':7,'belt':8,'feet':9,'glov':10}
    # D2 item codes are four-byte little-endian values; omitted copy-from-owner
    # slots compile to four spaces rather than an invented generic item.
    code = lambda value: int.from_bytes(value.ljust(4).encode(), 'little')
    monster = {'bloodraven':267,'flyingscimitar':234,'valkyrie':357,'shadowwarrior':418,'shadowmaster':419}
    cpp = r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <vector>
#define __fastcall
using DWORD=uint32_t;
constexpr int UNIT_MONSTER=1,BODYLOC_NONE=0,IFLAG_NOSELL=1,AISPECIALSTATE_NONE=0,UNITFLAG_SKSRVDOFUNC=1,EVENTTYPE_AITHINK=1,STATE_VALKYRIE=2;
struct D2UnitStrc;struct Inventory{D2UnitStrc* items[11]={};};
struct D2UnitStrc{int dwUnitType=UNIT_MONSTER,dwClassId=357,dwFlags=0,dwNodeIndex=23,dwUnitId=6,code=0,loc=0,dur=0,flags=0;Inventory* pInventory=nullptr;} owner,pet;
Inventory ownerInventory,petInventory;D2UnitStrc generated[11],ownerItems[11];
struct D2GameStrc{int dwGameFrame=700;} game;
struct Unk{int nItemCode=0;uint8_t nBodyLoc=0;int unk0x05=0;};
struct D2MonStatsTxt{int nMonEquipTxtRecordId=2;} mon;
struct D2MonEquipTxt{int nMonster=0,nOnInit=0,wLevel=0;uint8_t nLoc[3]={};int nMod[3]={},dwItem[3]={};};
struct Tables{D2MonEquipTxt pMonEquipTxt[45];int nPetTypeTxtRecordCount=20;}tables;auto* sgptDataTables=&tables;
struct D2SkillsTxt{int nPetType=2,dwPetMax=0,dwCalc[2]={1,2};} skill;
struct D2SummonArgStrc{int nAiSpecialState=0,nPetType=0,nHcIdx=0,nMonMode=0,nPetMax=0;D2UnitStrc* pOwner=nullptr;} captured;
struct Creation{int loc,code,quality,level;};std::vector<Creation> made;std::vector<int> draws;
bool hasMon=true,hasRecord=true,allowSpawn=true,allowCreate=true,allowEquip=true,validCode=true;int choice=0,rank=1,baseRank=0,passiveRank=0,itemLevel=0,aiAt=0,state=0,stored=0,aiNode=0,removed=0;
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return hasMon?&mon:nullptr;}
uint32_t ITEMS_RollLimitedRandomNumber(void*,int maximum){draws.push_back(maximum);return maximum?choice%maximum:0;}
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(Inventory* inventory,int loc){return inventory && loc>=0 && loc<11?inventory->items[loc]:nullptr;}
int ITEMS_GetBaseCode(D2UnitStrc* item){return item->code;}
void INVENTORY_AllocInventory(void*,D2UnitStrc* unit){unit->pInventory=&petInventory;}
bool DATATBLS_GetItemRecordFromItemCode(int value,int* id){*id=value;return validCode;}
D2UnitStrc* D2GAME_CreateItemUnit_6FC501A0(D2UnitStrc*,int id,D2GameStrc*,unsigned,int quality,unsigned,unsigned,int level,int,int,int){if(!allowCreate){return nullptr;}auto& item=generated[made.size()];item={};item.code=id;made.push_back({0,id,quality,level});return &item;}
void ITEMS_SetItemFlag(D2UnitStrc* item,int flag,int){item->flags|=flag;}
void ITEMS_SetBodyLocation(D2UnitStrc* item,int loc){item->loc=loc;made.back().loc=loc;}
int ITEMS_GetBodyLocation(D2UnitStrc* item){return item->loc;}
bool sub_6FC45060(D2GameStrc*,D2UnitStrc* unit,int,int loc,int,int*){if(!allowEquip){return false;}unit->pInventory->items[loc]=&generated[made.size()-1];return true;}
int STATLIST_GetMaxDurabilityFromUnit(D2UnitStrc*){return 37;}
constexpr int STAT_DURABILITY=1;
void STATLIST_SetUnitStat(D2UnitStrc* item,int,int value,int){item->dur=value;}
void SUNIT_RemoveUnit(D2GameStrc*,D2UnitStrc*){removed++;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRecord?&skill:nullptr;}
int D2GAME_GetSummonIdFromSkill_6FD15580(D2UnitStrc*,int,int,int,int* mode,int,int){*mode=1;return 357;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int level){return formula==2?25+3*(level-1):1;}
D2UnitStrc* D2GAME_SummonPet_6FD14430(D2GameStrc*,D2SummonArgStrc* args){captured=*args;return allowSpawn?&pet:nullptr;}
void D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int level){baseRank=level;}
void D2GAME_SetSummonPassiveStats_6FD0C530(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int level,int ilvl){passiveRank=level;itemLevel=ilvl;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int at,int,int){aiAt=at;}
void STATES_ToggleState(D2UnitStrc*,int value,int){state=value;}
void UNITS_StoreOwner(D2UnitStrc*,D2UnitStrc*){stored++;}
void D2GAME_UpdateSummonAI_6FC401F0(D2GameStrc*,D2UnitStrc*,int,int value){aiNode=value;}
'''
    # Source pSeed's fields are not inspected by selection; the declared bounded
    # draw service records every requested bound, including singleton choices.
    cpp = cpp.replace('code=0,loc=0,dur=0,flags=0;', 'code=0,loc=0,dur=0,flags=0,pSeed=0;')
    cpp += create + equip + summon
    cpp += r'''void reset(){owner={};pet={};ownerInventory={};petInventory={};owner.pInventory=&ownerInventory;pet.pInventory=&petInventory;mon={};made.clear();draws.clear();hasMon=hasRecord=allowSpawn=allowCreate=allowEquip=validCode=true;choice=baseRank=passiveRank=itemLevel=aiAt=state=stored=aiNode=removed=0;}
void print(int level,int existing){std::printf("[%d,%d,[",level,existing);bool first=true;for(auto v:made){if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d]",v.loc,v.code,v.quality,v.level);}std::printf("],[");first=true;for(int v:draws){if(!first)std::printf(",");first=false;std::printf("%d",v);}std::printf("]]");}
int main(){
'''
    for i, row in enumerate(rows):
        cpp += 'tables.pMonEquipTxt[' + str(i) + ']={' + ','.join(map(str,[monster.get(row.get('monster'),-1),row.get('oninit',0),row.get('level',0)]))
        for field, encode in [('loc',lambda x:locations.get(x,0)),('mod',lambda x:x or 0),('item',lambda x:code(x or ''))]:
            cpp += ',{' + ','.join(str(encode(row.get(field+str(n)))) for n in range(1,4)) + '}'
        cpp += '};\n'
    cpp += r'''std::printf("{\"equipment\":[");bool first=true;
for(rank=1;rank<=40;rank++)for(int occupied:{0,1}){reset();if(occupied){petInventory.items[4]=&ownerItems[4];}sub_6FCF9580(&game,&owner,&pet,32,rank,25+3*(rank-1),0);if(!first)std::printf(",");first=false;print(rank,occupied);for(int loc=1;loc<11;loc++){auto* item=petInventory.items[loc];if(item && (!occupied || loc!=4) && (item->dur!=37 || !(item->flags&IFLAG_NOSELL))){return 2;}}}
std::printf("],\"guards\":[");first=true;for(int guard=0;guard<8;guard++){reset();if(guard==0){pet.dwUnitType=0;}if(guard==1){hasMon=false;}if(guard==2){mon.nMonEquipTxtRecordId=-1;}if(guard==3){pet.pInventory=nullptr;}if(guard==4){allowCreate=false;}if(guard==5){validCode=false;}if(guard==6){allowEquip=false;}sub_6FCF9580(&game,&owner,&pet,32,1,25,guard==7);if(!first)std::printf(",");first=false;std::printf("[%d,%zu,%d]",guard,made.size(),removed);}
std::printf("],\"dispatch\":[");first=true;for(int r:{1,8,17,27,40})for(int guard:{0,1,2}){reset();hasRecord=guard!=1;allowSpawn=guard!=2;int ok=SKILLS_SrvDo016_Valkyrie(&game,&owner,32,r);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",r,guard,ok,baseRank,passiveRank,itemLevel,aiAt,state,stored,aiNode);}
std::printf("]}\n");}
'''
    path, binary = STAGE / 'valkyrie-equipment-source-probe.cpp', STAGE / 'valkyrie-equipment-source-probe'
    path.write_text(cpp)
    subprocess.run(['g++','-std=c++20','-Wno-multichar','-O2',str(path),'-o',str(binary)], check=True)
    data = json.loads(subprocess.check_output([str(binary)]))
    for rank, occupied, made, draws in data['equipment']:
        used = {4} if occupied else set()
        expected, expected_draws = [], []
        for row in valk:
            if row['level'] > rank:
                continue
            expected_draws.append(1)
            loc = locations[row['loc1']]
            if loc not in used:
                expected.append([loc,code(row['item1']),row['mod1'],25+3*(rank-1)])
                used.add(loc)
        assert (made,draws) == (expected,expected_draws), (rank,occupied,made,expected)
    assert data['guards'] == [[0,0,0],[1,0,0],[2,0,0],[3,0,0],[4,0,0],[5,0,0],[6,2,2],[7,0,0]]
    for rank, guard, ok, base_rank, passive_rank, ilvl, at, state, stored, node in data['dispatch']:
        assert [ok,base_rank,passive_rank,ilvl,at,state,stored,node] == ([1,rank,rank,25+3*(rank-1),720,2,1,23] if guard==0 else [0]*8)
    fixture = STAGE / 'valkyrie-equipment-source-probe.json'
    fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence = dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,sourceHashes=hashes,pinnedTableHashes=pins,
        compiledRoutines=['sub_6FCF9580','sub_6FC61190','SKILLS_SrvDo016_Valkyrie'],
        blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},
        probe=dict(caseCounts={k:len(v) for k,v in data.items()},fixture=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),
        equipmentRows=valk,
        established=[
            'All19 pinned Valkyrie equipment rows retain descending threshold and original slot/code/quality. Rank1 creates rare spr/ful; rank8 replaces weapon with rare9p9; rank17 uses rare7p7. Later rows fill only empty slots, so lower thresholds never overwrite higher-rank equipment.',
            'Selection consumes one bounded pet-seed draw per visited row, including singleton choices and already-filled slots. The supplied bounded draw returns0; this proves requested bounds/order, not the modern RNG algorithm or real affix rolls.',
            'The unchanged item wrapper passes requested quality and item level into CreateItemUnit, marks NOSell, equips at the requested body location and restores max durability on successful equip. Failed equip removes the created item. These are recording services, not actual native gear creation.',
            'do16 dispatch passes effective rank to base/passive helpers and pinned calc2=25+3*(rank-1) as equipment item level, schedules first AI think20frames later, enables Valkyrie state, stores owner and updates owner AI node. Full helper bodies are reviewed/hash-pinned, not compiled in this dispatch harness.'],
        services=[
            'Declared table/code/inventory lookup, item allocation/quality/affix generation, equip/stat/durability services and bounded RNG. Neither D2 item generation nor Sanctuary native items/combat execute here.',
            'do16 uses recording summon allocation, skill/formula, base/passive stats, event/state/owner/AI services. It does not execute life initialization, random HP, summon passives, spell input/payment, movement or combat.'],
        remaining=[
            'Implement the source-quality, item-level and type-specific magic/rare affix generator/consumer for NPC summon equipment. Existing player GearDrops chooses its own quality/source path and ClassGear activates player items; neither is a drop-in Valkyrie equipment implementation. Do not substitute plain or arbitrary fixed gear.',
            'Pinned current row has seven passive stat slots while the older helper iterates five. Preserve current immunity-piercing fields and resolve modern dispatch rather than silently dropping the last fields.',
            'Life roll/regeneration/base level and AC/AR, Strength/Dexterity/defense/HP/Decoy synergies and inherited hard-point Dodge/Avoid/Evade/Critical Strike need native summon consumers. Existing player-only avoidance/mastery routes cannot be claimed to cover the pet.',
            'Placement fallback shares the recorded stub gap; current town allowance,15-frame local delay,mana25+rank-1,owner/replacement/follower/save/client behavior remain full-handler work.',
            'Continue unaffected Amazon bow work while these implementation dependencies remain. No new talent handler, class certificate or live item availability is enabled by this probe.'],
        assets=dict(effect='Final Valkyrie body/summon/attack/contact/audio pending in skill-effects.json; no native summon placeholder instantiated yet.',icon='Talent and right-hotkey icon remain separately pending in skill-icons.json.'))
    target = ROOT / 'tools/reference/valkyrie-equipment-engine-evidence.json'
    rendered = json.dumps(evidence,indent=2)+'\n'
    if check:
        assert target.read_text() == rendered, 'Valkyrie equipment evidence changed'
    else:
        target.write_text(rendered)
    print('Valkyrie:80 equipment/8 guard/15 dispatch source cases pass; native gear and full skill remain pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check',action='store_true')
    build(parser.parse_args().check)
