#!/usr/bin/env python3
"""Execute both complete common corpse predicates against current trap rows.

Corpse ineligibility is separate from registered owner/stat/GUID lifetime.
This proof does not authorize a new retirement timer or a damage proxy.
"""
import argparse,csv,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'


def build(check=False):
    model=json.loads((ROOT/'tools/reference/d2r-local-sentry-model-evidence.json').read_text());p=ROOT/model['files']['monstats2']['path'];raw=p.read_bytes();assert hashlib.sha256(raw).hexdigest()==model['files']['monstats2']['sha256']
    rows={r['Id']:r for r in csv.DictReader(raw.decode('utf-8-sig').splitlines(),delimiter='\t')}
    ids=['chargeboltsentry','wakeofdestruction','lightningsentry','infernosentry','deathsentry','bladecreeper','zombie1']
    modes=json.loads((ROOT/'tools/reference/d2r-local-monmode-evidence.json').read_text());assert modes['rows'][12]['code']=='DD'
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};routines={}
    def block(relative,name,enum=False):
        raw=(STAGE/'d2moo-pinned-source'/relative).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=hashlib.sha256(raw).hexdigest();text=raw.decode();pattern=r'^.*\b'+name+(r'\s*\n\{' if enum else r'\([^;\n]*\)\s*\n\{');m=re.search(pattern,text,re.M);assert m,name
        end,depth=text.index('{',m.start())+1,1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        body=text[m.start():end]+(';' if enum else '');routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
    cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstddef>
#include <initializer_list>
#define __stdcall
using BOOL=int;constexpr int TRUE=1,FALSE=0,UNIT_MONSTER=1,MONMODE_DEAD=12;
unsigned gdwBitMasks[32];struct D2UnitStrc{int dwUnitType=1,dwAnimMode=12,dwClassId=0;};
struct D2MonStats2Txt{unsigned dwFlags=0;}row;bool haveRow=true,undeadMask=false;
D2MonStats2Txt* UNITS_GetMonStats2TxtRecordFromMonsterId(int){return haveRow?&row:nullptr;}
BOOL STATES_CheckStateMaskUdeadOnUnit(D2UnitStrc*){return undeadMask;}
'''
    cpp+=block('source/D2Common/include/DataTbls/MonsterTbls.h','D2C_MonStats2TxtFlagIndices',True)
    cpp+=block('source/D2Common/src/D2Skills.cpp','SKILLS_CanUnitCorpseBeSelected')
    cpp+=block('source/D2Common/src/Units/Units.cpp','UNITS_IsCorpseUseable')
    bits=[(int(rows[key]['corpseSel'] or 0),int(rows[key]['deadCol'] or 0)) for key in ids]
    cpp+='int bits[][2]={'+','.join('{'+f'{a},{b}'+'}' for a,b in bits)+'};\n'
    cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int id=0;id<7;id++)for(int type:{0,1,3,4})for(int mode:{0,1,12,14})for(int mask:{0,1})for(int present:{0,1})for(int nullUnit:{0,1}){
 D2UnitStrc unit;unit.dwClassId=id;unit.dwUnitType=type;unit.dwAnimMode=mode;haveRow=present;undeadMask=mask;
 row.dwFlags=(bits[id][0]?gdwBitMasks[MONSTATS2FLAGINDEX_CORPSESEL]:0)|(bits[id][1]?gdwBitMasks[MONSTATS2FLAGINDEX_DEADCOL]:0);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",id,type,mode,mask,present,nullUnit,SKILLS_CanUnitCorpseBeSelected(nullUnit?nullptr:&unit),UNITS_IsCorpseUseable(nullUnit?nullptr:&unit));first=false;
}printf("]\n");}
'''
    path=STAGE/'sentry-corpse-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sentry-corpse-sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
    cases=json.loads(run.stdout);assert len(cases)==896;positive=0
    for ident,kind,mode,mask,present,null,a,b in cases:
        selected=int(not null and kind==1 and mode==12 and not mask and present and bits[ident][0]);usable=selected*int(not bits[ident][1])
        assert (a,b)==(selected,usable);positive+=a
        if ident<6:assert a==b==0
    assert positive==1
    fixture=STAGE/'sentry-corpse-source-probe.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutineHashes=routines,currentModelEvidence='tools/reference/d2r-local-sentry-model-evidence.json',modelFileSha256=model['files']['monstats2']['sha256'],rows={key:{field:rows[key][field] for field in ['corpseSel','deadCol','limitCorpses']} for key in ids},fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(cases),positiveZombieControls=positive),
        established=['Both complete common predicates reject all six current trap model rows, including registered dead mode12 units: corpseSel is absent/zero. Neither death mode nor registry retention makes a trap a selectable/usable corpse.',
                     'The directly extracted current zombie1 row is a positive control only in monster/dead/model-present/not-undead-masked/nonnull context. Null/missing row, other unit types/modes and undead masks reject.',
                     'Current trap rows independently have limitCorpses1. That field is not read by these two predicates and does not establish when a GUID is removed.'],
        limits=['Only common predicate bodies are compiled; used-state masks, row lookup, nonmetadata text fields and unit layout are declared services. This is not a modern executable trace or binary ABI certificate.',
                'Corpse ineligibility does not prove all skill/AI/event/minion/stat/room references are absent. Full retirement timing, corpse compression/unload, final GUID retention and native retired-owner damage policy remain separate. Do not infer a new timer, immortal proxy or player-substituted source.'],sanitizers='ASan/UBSan exit0 and empty runtime stderr required')
    out=ROOT/'tools/reference/sentry-corpse-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Sentry corpse evidence stale'
    else:out.write_text(encoded)
    print('Sentry common corpses:896complete predicate contexts; six current trap rows reject, positive zombie control passes; ASan/UBSan clean. GUID lifetime remains separate.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
