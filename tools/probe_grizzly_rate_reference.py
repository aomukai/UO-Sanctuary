#!/usr/bin/env python3
"""Reuse the complete source nonweapon rate writer for BearSmite mode14."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    parent_path = ROOT / 'tools/reference/spirit-wolf-rate-engine-evidence.json'
    if not parent_path.exists():
        parent_path = ROOT / 'tools/reference/spirit-wolf-rate-source-evidence.json'
    parent = json.loads(parent_path.read_text())
    original = BASE / 'spirit-wolf-rate-source-probe.cpp'
    assert sha(original) == parent['generatedSourceSha256']
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == parent['legacyCommit'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes = dict(parent['sourceHashes'])
    for relative, digest in hashes.items():
        assert sha(BASE / 'd2moo-pinned-source' / relative) == digest
    preflight_path = ROOT / 'tools/reference/grizzly-preflight.json'
    preflight = json.loads(preflight_path.read_text())
    assert preflight['currentRows']['monstats']['druidbear']['*hcIdx'] == '428'
    npc = preflight['currentRows']['skills']['BearSmite']
    assert npc['UseAttackRate'] == '1'
    rows = preflight['sequence']['rows']
    assert len(rows) == 17 and [int(r['event'] or 0) for r in rows] == [0]*8+[1]+[0]*8
    cpp = original.read_text().split('int main()', 1)[0]

    def replace(old, new):
        nonlocal cpp
        assert cpp.count(old) == 1, old
        cpp = cpp.replace(old, new, 1)

    replace('struct D2AnimSeqTxt{};', 'struct D2AnimSeqTxt{int nEvent=0;}rows[17];')
    replace('dwClassId=420,dwAnimMode=4', 'dwClassId=428,dwAnimMode=14')
    replace('D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){assert(false);return nullptr;}',
        'D2SkillsTxt skillRow{{SKILLSFLAG_USEATTACKRATE,0},326};D2SkillStrc skill{&skillRow};bool hasSkill=true;\nD2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return hasSkill?&skill:nullptr;}')
    replace('D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){assert(false);return nullptr;}',
        'D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){assert(id==326);return &skillRow;}')
    relative = 'source/D2Common/src/DataTbls/SequenceTbls.cpp'
    raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest() == next(
        x['sha'] for x in tree['tree'] if x['path'] == relative)
    hashes[relative] = hashlib.sha256(raw).hexdigest()
    event = function(raw.decode(), 'DATATBLS_GetSequenceEvent')
    replace('void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int*){assert(false);}', event)
    cpp += r'''
int main(){bool first=true;printf("[");rows[8].nEvent=1;
hasSkill=false;assert(!UNITS_CanAnimModeUseAttackRate(UNIT_MONSTER,14,&owner,428));hasSkill=true;
skillRow.dwFlags[0]=0;assert(!UNITS_CanAnimModeUseAttackRate(UNIT_MONSTER,14,&owner,428));skillRow.dwFlags[0]=SKILLSFLAG_USEATTACKRATE;
for(int base:{248,256,32767})for(int ias:{-119,-10,0,20,100,10000})
for(int rate:{-100,0,14,15,50,100,175,250})for(int melee:{0,1})for(int alt:{0,128}){
owner={};owner.pAnimSeq=rows;owner.dwSeqFrameCount=17*256;owner.dwFrameCountPrecise=17*256;
anim={};anim.dwAnimSpeed=base;faster=ias;attack=rate;meleeOnly=melee;alternate=alt;merges=0;events.clear();
assert(UNITS_CanAnimModeUseAttackRate(UNIT_MONSTER,14,&owner,428));UNITS_UpdateAttackAnimRateAndVelocity(&owner);sub_6FCBCE70(&game,&owner);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",base,ias,rate,melee,alt,owner.wAnimSpeed,owner.dwSeqSpeed,merges);first=false;
bool sep=false;for(auto e:events){printf("%s[%d,%d,%d,%d]",sep?",":"",e.type,e.frame-1000,e.kind,e.param);sep=true;}printf("]]");
}printf("]\n");}
'''
    generated = BASE / 'grizzly-rate-source-probe.cpp'
    generated.write_text(cpp)
    with (BASE / 'grizzly-rate-compiler.log').open('wb') as log:
        subprocess.run(['g++','-std=c++20','-O1','-g','-Wall','-Wextra','-Werror','-fsanitize=address,undefined',
                        '-fno-sanitize-recover=all',str(generated),'-o',str(generated.with_suffix(''))],
                       stdout=log,stderr=subprocess.STDOUT,check=True)
    result = subprocess.run([str(generated.with_suffix(''))],capture_output=True)
    (BASE / 'grizzly-rate-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-2000:]
    comparisons = json.loads(result.stdout)
    assert len(comparisons) == 576
    fixture = BASE / 'grizzly-rate-source-probe.json'
    fixture.write_text(json.dumps(comparisons,separators=(',',':'))+'\n')
    evidence = dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],installed=False,handlerEnabled=False,
        nativeConsumer=False,preflightSha256=sha(preflight_path),reusedAbiSha256=sha(original),
        reusedEvidence=dict(path=str(parent_path.relative_to(ROOT)),sha256=sha(parent_path)),
        sourceHashes=hashes,unchangedRoutineHashes={**parent['unchangedRoutineHashes'],
            'DATATBLS_GetSequenceEvent':hashlib.sha256(event.encode()).hexdigest()},
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture),cases=576,
                     generatedSourceSha256=sha(generated),sanitizers='ASan+UBSan'),
        established=[
            'Complete attack-rate predicate selects monster mode14only with a used skill whose current row enablesUseAttackRate. Missing-skill/flag-off controls reject. Current NPC326enables it. Monster sequence has no player sequence minus30penalty.',
            'Complete unchanged nonweapon writer and initial scheduler execute over current17sequence rows/action8and supplied base248,plus256/32767numeric controls. Existing SourceAttackRate.Speed is a reuse candidate;no owner weapon/IAS inheritance is introduced.',
        ],limits=[
            'Mode-info14actually selects the infinite per-frame monster callback,as retained in sentry-controller-source-evidence.json. This initial sparse scheduler matrix is a helper control and must not be used to schedule BearSmite release.',
            'Skill/stat/inventory/non-dual-wield/melee-only and animation/table services are explicit. Mode conversion,base-row selection,full stat transport/global event queue and modern first-think/tie behavior remain separate.',
            'Initial queued requests do not prove dispatch timing or mid-sequence rescheduling. Sequence step callbacks,start32/do2,world events and saved cancellation still require native integration. No Grizzly attack or learned handler is enabled.',
        ])
    path = ROOT / 'tools/reference/grizzly-rate-engine-evidence.json'
    rendered = json.dumps(evidence,indent=2)+'\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Grizzly mode14rate:576complete source writer/scheduler cases;sanitizers clean;native action pending.')


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true')
    build(parser.parse_args().check)
