#!/usr/bin/env python3
"""Compile complete history setter/checker and mode3 candidate callback.

This isolates candidate selection, rather than mistaking a post-selection
router fixture for evidence about the preceding search.
"""
import argparse
import csv
import hashlib
import json
import re
import subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'


def build(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
    hashes,routines={},{}
    def routine(relative,name):
        raw=(STAGE/'d2moo-pinned-source'/relative).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=hashlib.sha256(raw).hexdigest()
        text=raw.decode();match=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert match,name
        end,depth=text.index('{',match.start())+1,1
        while depth:
            depth+=(text[end]=='{')-(text[end]=='}');end+=1
        body=text[match.start():end];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
    pin=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json'
    manifest=json.loads((pin.parent.parent/'sources.json').read_text());digest=hashlib.sha256(pin.read_bytes()).hexdigest()
    assert digest==manifest['files']['json/missiles.json']['sha256']
    rows=json.loads(pin.read_bytes());local=ROOT/'.local/research/d2r-3.3.93847/missiles.txt'
    actual={r['Missile']:r for r in csv.DictReader(local.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t') if r.get('Missile')}
    selected={}
    names=['poisonjav','poisonjavcloud','plaguejavelin','plaguejavcloud','lightningjavelin','lightningfury','furylightning','sentrylightningbolt','sentrylightningbolt2','bladefragment1']
    for name in names:
        row=rows[name];assert row['CollideType']==3 and not row.get('NextHit')
        facts={k:row.get(k,0) for k in ['LastCollide','NextHit','CollideType','Size','pSrvDoFunc','pSrvHitFunc','pSrvDmgFunc']}
        for key,value in facts.items():assert actual[name][key] in (str(value),'') if value==0 else actual[name][key]==str(value),(name,key)
        assert facts['LastCollide']==int(name!='bladefragment1')
        selected[name]=facts
    cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstddef>
#define __stdcall
#define __fastcall
using BOOL=int;constexpr int FALSE=0;
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3,STATE_JUSTHIT=1,MISSILESFLAGINDEX_LASTCOLLIDE=0,D2UnitInvalidGUID=-1;
constexpr unsigned UNITFLAG_ISVALIDTARGET=1,UNITFLAG_CANBEATTACKED=2;
unsigned gdwBitMasks[32];
struct D2MissilesTxt{unsigned dwMissileFlags=1;int nNextHit=0,nCollideFriend=0;};
struct D2MissileDataStrc{int nOwnerType=1,dwOwnerGUID=-1;};
struct D2UnitStrc{int dwUnitType=1,dwClassId=1,dwUnitId=10;unsigned dwFlags=3;D2MissileDataStrc* pMissileData=nullptr;bool justHit=false,hostile=true;};
struct D2GameStrc{};
struct D2MissileUnitFindArgStrc{D2GameStrc* pGame;D2UnitStrc* pMissile;D2MissilesTxt* pMissilesTxtRecord;D2UnitStrc* pOwner;};
D2MissilesTxt row;bool hasRecord=true;
D2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int){return hasRecord?&row:nullptr;}
bool STATES_CheckState(D2UnitStrc* p,int s){assert(s==STATE_JUSTHIT);return p->justHit;}
bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p){return p->hostile;}
'''
    missile='source/D2Common/src/Units/Missile.cpp';mode='source/D2Game/src/MISSILES/MissMode.cpp'
    cpp+=routine(missile,'MISSILE_SetOwner')+routine(missile,'MISSILE_CheckUnitIfOwner')+routine(mode,'MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster')
    cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int lastFlag:{0,1})for(int nextHit:{0,1})for(int last:{-1,10,20,77})for(int hit1:{0,1})for(int hit2:{0,1})for(int hostile1:{0,1})for(int flags1:{0,1,2,3})for(int kind1:{0,1,2,3}){
 row={};row.dwMissileFlags=lastFlag;row.nNextHit=nextHit;D2MissileDataStrc md;md.dwOwnerGUID=last;D2UnitStrc missile,owner,a,b;
 missile.dwUnitType=UNIT_MISSILE;missile.pMissileData=&md;a.dwUnitId=10;a.dwUnitType=kind1;a.justHit=hit1;a.hostile=hostile1;a.dwFlags=flags1;b.dwUnitId=20;b.justHit=hit2;
 D2GameStrc game;D2MissileUnitFindArgStrc args{&game,&missile,&row,&owner};D2UnitStrc* chosen=nullptr;
 for(auto* v:{&a,&b}){if(MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster(v,&args)){chosen=v;break;}}
 int selected=chosen?chosen->dwUnitId:-1;if(chosen)MISSILE_SetOwner(&missile,chosen);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",lastFlag,nextHit,last,hit1,hit2,hostile1,flags1,kind1,selected,md.nOwnerType,md.dwOwnerGUID);first=false;
}printf("]\n");}
'''
    cpp=cpp.replace('#include <cstddef>','#include <cstddef>\n#include <initializer_list>')
    path=STAGE/'missile-history-finder-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'missile-history-finder-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    data=json.loads(run.stdout);assert len(data)==2048
    for lastFlag,nh,last,h1,h2,hostile,flags,kind,selected,historyType,history in data:
        eligible1=kind in (0,1) and flags==3 and hostile and not(nh and h1) and not(lastFlag and kind==1 and last==10)
        eligible2=not(nh and h2) and not(lastFlag and last==20)
        expected=10 if eligible1 else 20 if eligible2 else -1
        assert selected==expected
        assert history==(selected if selected>=0 and lastFlag else last)
        assert historyType==(kind if selected==10 and lastFlag else 1)
    fixture=STAGE/'missile-history-finder-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledUnchangedRoutines=routines,
        pinnedMissilesSha256=digest,localMissiles=dict(path=str(local.relative_to(ROOT)),sha256=hashlib.sha256(local.read_bytes()).hexdigest()),selectedRows=selected,
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data)),
        established=[
            'Complete mode3 callback rejects the prior collider before the ordered search selects its recipient. An overlapping second eligible actor remains selectable. Complete LastCollide getter/setter preserve type+GUID history, including returning to a previously hit actor after a different contact.',
            'All nine listed history-enabled javelin/cloud/sentry rows have CollideType3 and NextHit0 in current local data; these history gates require no invented hit delay. Bladefragment1 is a LastCollide0 control and must not inherit the gate.',
            'Other candidate gates remain independent: valid/attackable flags, player/monster type, hostility and optional historical NextHit. A player and monster with equal numeric GUID do not share the same history identity.'],
        correctedEvidenceScope='Earlier router-only probes supplied a preselected actor and could not prove whether the preceding finder skipped that actor. This full finder composition supersedes post-selection-only assertions for these mode3 history rows.',
        services=['Ordered candidate list and native world geometry are explicit services; this does not establish modern room enumeration, collision footprints, first-contact frame or path equivalence.',
                  'Current rows do not use NextHit. Its historical controls prove callback ordering only and do not establish modern cast/caster hit-window identity.',
                  'Damage, AR/block, weapon/item effects, pierce, resource and lifetime routers are outside this focused candidate-selection proof and retain their separate evidence.'],
        sanitizers='ASan/UBSan: exit0 and empty runtime stderr required')
    out=ROOT/'tools/reference/missile-history-finder-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Missile history finder evidence stale'
    else:out.write_text(encoded)
    print('Missile history:2048 complete callback/setter/checker controls;10 current rows corroborated; pre-selection history and type/GUID identity proved within declared geometry services.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
