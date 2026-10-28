#!/usr/bin/env python3
"""Run complete Iron Golem target/cast and item metal/body-slot routines."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'iron-golem-items-source'


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(exist_ok=True); treepath = BASE / 'd2moo-source-tree.json'; tree = json.loads(treepath.read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs = {str(treepath.relative_to(ROOT)): sha(treepath)}; routines = {}
    def source(name):
        p = BASE / 'd2moo-pinned-source' / name; raw = p.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == name)
        inputs[str(p.relative_to(ROOT))] = sha(p); return raw.decode()
    nec = source('source/D2Game/src/SKILLS/SkillNec.cpp'); items = source('source/D2Common/src/Items/Items.cpp')
    inventory = source('source/D2Common/include/D2Inventory.h'); itemheader = source('source/D2Common/include/D2Items.h')
    assert re.search(r'IFLAG_IDENTIFIED\s*=\s*0x0*10', itemheader)
    equip = function(source('source/D2Game/src/ITEMS/ItemMode.cpp'), 'sub_6FC45060')
    save = source('source/D2Game/src/PLAYER/PlrSave2.cpp'); clients = source('source/D2Game/src/GAME/Clients.cpp')
    reviewed = {n: hashlib.sha256(function(save, n).encode()).hexdigest() for n in ['PLRSAVE2_WriteIronGolemSection', 'PLRSAVE2_ReadIronGolemSection']}
    reviewed['sub_6FC45060'] = hashlib.sha256(equip.encode()).hexdigest()
    assert 'INVENTORY_PlaceItemInBodyLoc' in equip and 'D2GAME_ITEMS_UpdateItemStatlist_6FC42310' in equip
    assert 'nIronGolemItemGUID' in clients and 'D2GAME_SKILLS_Handler_6FD12BA0' in clients
    cpp = r'''
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_ASSERT assert
constexpr int UNIT_ITEM=4,IMODE_ONGROUND=3,IMODE_ONCURSOR=4,IFLAG_IDENTIFIED=16,UNITFLAG_SKSRVDOFUNC=0x100,AISPECIALSTATE_NONE=0;
struct D2CoordStrc{int nX=0,nY=0;};struct D2ActiveRoomStrc{}room;struct D2GameStrc{}game;struct D2ClientStrc{}client;
struct D2UnitStrc{int dwUnitType=0,dwAnimMode=0,dwClassId=11,dwUnitId=17,dwNodeIndex=42,dwFlags=0;};D2UnitStrc owner,item,pet;
struct D2ItemsTxt{unsigned dwBitField1=3;int wType[2]={1,0};}itemrow;
struct D2ItemTypesTxt{uint8_t nBodyLoc1=4,nBodyLoc2=5;};D2ItemTypesTxt types[4]={{1,1},{4,5},{3,3},{9,9}};
struct Tables{int nItemTypesTxtRecordCount=4,nPetTypeTxtRecordCount=4;D2ItemTypesTxt*pItemTypesTxt=types;}tables;Tables*sgptDataTables=&tables;
struct D2SkillsTxt{uint8_t nPetType=3;int dwPetMax=0;}skill;
struct D2SummonArgStrc{int nAiSpecialState=0,nMonMode=0;D2UnitStrc*pOwner=nullptr;int nHcIdx=0,nPetType=0,dwFlags=0,nPetMax=0;D2CoordStrc pPosition;};
std::vector<std::array<int,4>>trace;D2SummonArgStrc request;bool hasTarget=true,identified=true,statOwner=false,hasSkill=true,allocation=true,equipSuccess=true,attached=false;int summonId=291,spawnMode=1,maximum=1;
void T(int id,int a=0,int b=0,int c=0){trace.push_back({id,a,b,c});}
D2UnitStrc*SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*p){assert(p==&owner);T(1);return hasTarget?&item:nullptr;}
D2ItemsTxt*DATATBLS_GetItemsTxtRecord(int id){assert(id==11);T(2);return &itemrow;}
int ITEMS_CheckItemFlag(D2UnitStrc*p,unsigned flag,int,const char*){assert(p==&item&&flag==16);T(3,identified);return identified;}
D2UnitStrc*STATLIST_GetOwner(D2UnitStrc*p,int zero){assert(p==&item&&zero==0);T(4,statOwner);return statOwner?&owner:nullptr;}
D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){assert(id==90);T(6,hasSkill,owner.dwFlags);return hasSkill?&skill:nullptr;}
int D2GAME_GetSummonIdFromSkill_6FD15580(D2UnitStrc*p,int zero,int id,int rank,int*mode,int,int){assert(p==&owner&&zero==0&&id==90&&rank==15);*mode=spawnMode;T(7,summonId,spawnMode);return summonId;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*p,int,int id,int rank){assert(p==&owner&&id==90&&rank==15);T(8,maximum);return maximum;}
int CLIENTS_GetUnitX(D2UnitStrc*p){assert(p==&item);return 210;}int CLIENTS_GetUnitY(D2UnitStrc*p){assert(p==&item);return 220;}
D2UnitStrc*D2GAME_SummonPet_6FD14430(D2GameStrc*,D2SummonArgStrc*p){request=*p;assert(p->pOwner==&owner);T(9,p->nHcIdx,p->nPetType,p->nPetMax);return allocation?&pet:nullptr;}
D2ActiveRoomStrc*UNITS_GetRoom(D2UnitStrc*p){assert(p==&item);return &room;}
void DUNGEON_AllocDrlgDelete(D2ActiveRoomStrc*r,int type,int guid){assert(r==&room&&type==4&&guid==29);T(10,type,guid);}
void UNITS_FreeCollisionPath(D2UnitStrc*p){assert(p==&item);T(11);}void UNITROOM_RemoveUnitFromRoom(D2UnitStrc*p){assert(p==&item);T(12);}
void UNITS_ChangeAnimMode(D2UnitStrc*p,int mode){assert(p==&item);p->dwAnimMode=mode;T(14,mode);}
int sub_6FC45060(D2GameStrc*,D2UnitStrc*p,int guid,uint8_t body,int skip,int zero){assert(p==&pet&&guid==29&&skip==1&&zero==0);T(15,guid,body,skip);if(equipSuccess){attached=true;item.dwAnimMode=1;}return equipSuccess;}
void D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10(D2GameStrc*,D2UnitStrc*p,D2UnitStrc*q,int zero,int rank){assert(p==&owner&&q==&pet&&zero==0&&rank==15);T(16);}
void D2GAME_SetSummonPassiveStats_6FD0C530(D2GameStrc*,D2UnitStrc*p,D2UnitStrc*q,int id,int rank,int zero){assert(p==&owner&&q==&pet&&id==90&&rank==15&&zero==0);T(17);}
void D2GAME_SetSummonResistance_6FD0C2E0(D2UnitStrc*p,D2UnitStrc*q){assert(p==&owner&&q==&pet);T(18);}
D2ClientStrc*SUNIT_GetClientFromPlayer(D2UnitStrc*p,const char*,int){assert(p==&owner);T(19);return &client;}
void sub_6FC3E200(D2ClientStrc*c,D2UnitStrc*p){assert(c==&client&&p==&pet);T(20);}
void D2GAME_UpdateSummonAI_6FC401F0(D2GameStrc*,D2UnitStrc*p,int zero,int index){assert(p==&pet&&zero==0&&index==42);T(21,index);}
'''
    for raw, name in [(items, 'ITEMS_IsMetalItem'), (items, 'ITEMS_GetAllowedBodyLocations'), (nec, 'SKILLS_SrvSt20_IronGolem'), (nec, 'SKILLS_SrvDo057_IronGolem')]:
        body = function(raw, name); cpp += body + '\n'; routines[name] = hashlib.sha256(body.encode()).hexdigest()
    cpp += r'''
void Reset(){owner={};item={};pet={};item.dwUnitType=4;item.dwUnitId=29;item.dwAnimMode=3;pet.dwUnitType=1;pet.dwUnitId=77;itemrow={};skill={};hasTarget=identified=hasSkill=allocation=equipSuccess=true;statOwner=attached=false;summonId=291;spawnMode=1;maximum=1;trace.clear();request={};tables.nPetTypeTxtRecordCount=4;}
void Trace(){printf("[");for(size_t j=0;j<trace.size();j++){auto a=trace[j];printf("%s[%d,%d,%d,%d]",j?",":"",a[0],a[1],a[2],a[3]);}printf("]");}
void Emit(int kind,int mode,int flags,int known,int bound,int row,int summon,int alloc,int equip,int type,int count,int cap,int itemtype,bool&first){
Reset();hasTarget=kind>=0;item.dwUnitType=kind;item.dwAnimMode=mode;itemrow.dwBitField1=flags;identified=known;statOwner=bound;hasSkill=row;summonId=summon;allocation=alloc;equipSuccess=equip;skill.nPetType=type;tables.nPetTypeTxtRecordCount=count;maximum=cap;itemrow.wType[0]=itemtype;
int start=SKILLS_SrvSt20_IronGolem(&game,&owner,90,15);auto initial=trace;trace.clear();int result=SKILLS_SrvDo057_IronGolem(&game,&owner,90,15);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],%d,[%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d],",first?"":",",kind,mode,flags,known,bound,row,summon,alloc,equip,type,count,cap,itemtype,start,result,owner.dwFlags,item.dwAnimMode,attached,request.nAiSpecialState,request.nMonMode,request.nHcIdx,request.nPetType,request.dwFlags,request.nPetMax,request.pPosition.nX,request.pPosition.nY,request.pOwner==&owner);first=false;
auto final=trace;trace=initial;Trace();printf(",");trace=final;Trace();printf("]");}
int main(){bool first=true;printf("{\"cast\":[");
for(int kind:{-1,0,1,4})for(int mode:{0,1,3})for(int flags=0;flags<8;flags++)for(int known:{0,1})for(int bound:{0,1})for(int row:{0,1})for(int summon:{-1,291})for(int alloc:{0,1})for(int equip:{0,1})Emit(kind,mode,flags,known,bound,row,summon,alloc,equip,3,4,1,1,first);
printf("],\"metadata\":[");first=true;
for(int type:{0,3,255})for(int count:{0,4,256})for(int cap:{0,1,4})for(int itemtype:{-1,0,1,3,4})Emit(4,3,3,1,0,1,291,1,1,type,count,cap,itemtype,first);
printf("]}\n");}
'''
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest
    p = STAGE / 'source.cpp'; p.write_text(cpp); compiler = STAGE / 'compiler.log'; sanitizer = STAGE / 'sanitizer.log'
    with compiler.open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(p), '-o', str(STAGE / 'probe')], stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True); sanitizer.write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-3000:]
    data = json.loads(run.stdout); assert len(data['cast']) == 6144 and len(data['metadata']) == 135
    enum = re.search(r'enum D2C_PlayerBodyLocs\s*\{(.*?)\}', inventory, re.S).group(1)
    names = re.findall(r'^\s*(BODYLOC_\w+)', enum, re.M)
    mapping = {'': 0, 'none': 0, 'head': 1, 'neck': 2, 'tors': 3, 'rarm': 4, 'larm': 5, 'rrin': 6, 'lrin': 7, 'belt': 8, 'feet': 9, 'glov': 10, 'swra': 11, 'swla': 12}
    assert [names.index('BODYLOC_' + n) for n in ['NONE', 'HEAD', 'NECK', 'TORSO', 'RARM', 'LARM', 'RRIN', 'LRIN', 'BELT', 'FEET', 'GLOVES', 'SWRARM', 'SWLARM']] == list(range(13))
    directory = ROOT / 'tools/reference/d2r-named-equipment'; manifest = json.loads((directory / 'sources.json').read_text()); tables2 = {}
    for name in ['weapons', 'armor', 'misc', 'itemtypes', 'skills']:
        p = directory / 'json' / (name + '.json'); assert sha(p) == manifest['files']['json/' + name + '.json']['sha256']; inputs[str(p.relative_to(ROOT))] = sha(p); tables2[name] = json.loads(p.read_text())
    profiles = []
    for table in ['weapons', 'armor', 'misc']:
        for r in tables2[table].values():
            t = tables2['itemtypes'][r['type']]
            profiles.append(dict(Code=r['code'], ItemId=r['classid'], Flags=r.get('bitfield1', 0), Body1=mapping[t.get('BodyLoc1', '')], Body2=mapping[t.get('BodyLoc2', '')], SourceTable=table))
    profiles.sort(key=lambda r: r['ItemId']); assert len(profiles) == len({p['Code'] for p in profiles}) == len({p['ItemId'] for p in profiles}) == 692
    assert sum(bool(p['Flags'] & 2) for p in profiles) == 316 and all(p['Body1'] > 0 for p in profiles if p['Flags'] & 2)
    skillrow = tables2['skills']['90']; assert skillrow['srvstfunc'] == 20 and skillrow['srvdofunc'] == 57 and skillrow['petmax'] == 1
    data['items'] = profiles
    out = BASE / 'iron-golem-items-source-probe.json'; encoded = json.dumps(data, separators=(',', ':')) + '\n'
    if check:
        assert out.read_text() == encoded
    else:
        out.write_text(encoded)
    profilepath = ROOT / 'data/Sanctuary/Balance/iron-golem-items.json'; encoded = json.dumps(profiles, indent=2) + '\n'
    if check:
        assert profilepath.read_text() == encoded
    else:
        profilepath.write_text(encoded)
    inputs[str(Path(__file__).relative_to(ROOT))] = sha(Path(__file__))
    ownedpath = ROOT / 'tools/reference/iron-golem-local-items-evidence.json'; owned = json.loads(ownedpath.read_text())
    assert owned['referenceVersion'] == '3.3.93847' and owned['verified']['itemBases'] == 692 and owned['verified']['unexplainedDifferences'] == 0
    inputs[str(ownedpath.relative_to(ROOT))] = sha(ownedpath)
    for group in ['inputs']:
        for n, digest in owned[group].items():
            assert sha(ROOT / n) == digest, n
            inputs[n] = digest
    for table in owned['tables']:
        assert sha(ROOT / table['path']) == table['sha256']
        inputs[table['path']] = table['sha256']
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], component='Iron Golem item eligibility and original-item transfer ordering', handlerEnabled=False, currentExecutableParity=False, nativeConsumerAccepted=False,
        inputs=inputs, compiledUnchangedRoutineHashes=routines, reviewedUncompiledRoutineHashes=reviewed, verified=dict(castContexts=6144, metadataContexts=135, itemBases=692, metalBases=316, currentSkillId=90),
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in [STAGE / 'source.cpp', compiler, sanitizer, out, profilepath, *sorted((STAGE / 'diagnostics').glob('*.log'))]},
        established=['Complete start20/do57and metal/body-location helpers retain lazy eligibility and all post-allocation service order. Actual item metal eligibility is bit2of the base row,not material appearance. Pet type truncation/clamping,raw pet maximum and primary body location are distinct. Failed pet allocation leaves the item untouched;successful allocation removes it from the ground and requests equip of the original GUID with requirement checks skipped.',
            'do57ignores the equip helper return and still applies base/passive/resistance/client/AI services. Its declared equip-failure controls retain cursor-mode item identity;no guessed rollback is attributed to the source. Reviewed full equip code places the original item in pet inventory and applies its statlist. Reviewed save code serializes the held item and recreates an Iron Golem from it on player load. Those inventory/save bodies are reviewed,not executed here.'],
        remaining=['Actual full equipment/stat/proc inheritance,pet actor/AI/resource/cap/owner/death/respec handling and persistent native item storage require integration. The source equip/save functions and allocation/room/stat/client services are not whole-engine acceptance. Current executable parity is unverified;no learned cast is enabled. Final body/effects/audio and separate talent/hotkey icons remain pending.'])
    p = ROOT / 'tools/reference/iron-golem-items-source-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert p.read_text() == encoded
    else:
        p.write_text(encoded)
    print('Iron Golem:6144cast/135metadata contexts,692base rows/316metal;original item is equipped,not deleted;full actor/effects remain gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
