#!/usr/bin/env python3
"""Authenticate the focused Eldritch Blast supplier checkpoint offline."""
import argparse
import json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT
STAGE=ROOT/'.local/class-skills/eldritch-blast-suppliers'


def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text()) if path.suffix=='.json' else path.read_bytes()

    born=pin(STAGE/'birth-inputs.json')
    snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    pin(STAGE/'source-snapshot-1.json')
    for name,digest in born['sourceInputs'].items():
        assert e.sha(ROOT/name)==digest,name
    for kind in ['Content','Integration']:
        path=STAGE/kind.lower()/f'Sanctuary.{kind}.dll'
        assert e.sha(path)==born['artifacts']['Assemblies/'+path.name]
        log=(STAGE/(kind.lower()+'-build-1.log')).read_text()
        assert 'Build succeeded.' in log and '0 Warning(s)' in log and '0 Error(s)' in log
        pin(STAGE/(kind.lower()+'-build-1.log'))
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,
                      'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,
                      'npc-resource-callbacks-reload-check':2,'eldritch-blast-suppliers-check':1,
                      'eldritch-blast-suppliers-reload-check':2})
    log=e.cohort(STAGE,expected,12)
    assert 'Eldritch Blast:48 independent tier/hard-point controls' in log
    pin(STAGE/'birth-native-checks.log')
    source=ROOT/'tools/reference/d2r-named-equipment'
    manifest=pin(source/'sources.json')
    skills=pin(source/'json/skills.json');missiles=pin(source/'json/missiles.json')
    assert e.sha(source/'json/skills.json')==manifest['files']['json/skills.json']['sha256']
    assert e.sha(source/'json/missiles.json')==manifest['files']['json/missiles.json']['sha256']
    data=pin(ROOT/'data/Sanctuary/Balance/eldritch-blast-profile.json')
    assert data['referenceVersion']=='3.3.93847'
    assert data['missileSourceSha256']==e.sha(source/'json/missiles.json')
    assert data['missile']==dict(key='eldritchblastnova',fields=missiles['eldritchblastnova'])
    current=next(r for r in pin(ROOT/'data/Sanctuary/Balance/class-skills.json') if r['sourceId']==384)
    primary=skills['384']
    for key,value in primary.items():
        if key in current['sourceFields']:
            assert current['sourceFields'][key]==value,(key,value)
    assert primary['calc1']==primary['calc2']=='par1' and primary['Param1']==5
    assert primary['auralencalc']=="par3 + skill('Psychic Ward'.blvl) * par4"
    assert primary['EDmgSymPerCalc']=="(skill('Blade Warp'.blvl)+skill('Hex Purge'.blvl))*par8"

    # Retain the earlier access result. This recorder never opens process memory.
    capture=pin(ROOT/'.local/class-skills/warlock-runtime-reference/warlock-do-165-20261108/capture.json')
    assert capture['dispatcherIndexLinked'] and capture['referenceVersion']=='3.3.93847'
    for name,digest in capture['artifacts'].items():assert e.sha(ROOT/name)==digest,name
    row=capture['records'][0];fragments=row['fragments']
    assert row['slot']==165 and row['rva']=='0x450f40' and not row['allUnwindFragmentsCaptured']
    assert len(fragments)==9 and fragments[0]['captured']
    captured=sum(int(f['endRva'],16)-int(f['rva'],16) for f in fragments if f['captured'])
    total=sum(int(f['endRva'],16)-int(f['rva'],16) for f in fragments)
    assert captured==92 and total==1198
    assert all(not f['captured'] for f in fragments[1:])
    assert fragments[1]['permissions']=='cross-map'
    assert all(f['permissions']=='---s' for f in fragments[2:])

    installed=pin(ROOT/'tools/reference/creeper-frame-installation-evidence.json')
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():
        assert e.sha(Path(installed['runtimeRoot'])/name)==digest,name
    protected=pin(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind,relative,count in [('saves','Saves',38),('configuration','Configuration',20)]:
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative
        assert len(protected[kind])==count
        assert {str(p.relative_to(folder)):e.sha(p) for p in folder.rglob('*') if p.is_file()}==protected[kind]
    receipt=pin(ROOT/'tools/reference/ring-cast-client-evidence.json')
    client=pin(ROOT/receipt['inputManifest']['path'])
    assert e.sha(ROOT/receipt['inputManifest']['path'])==receipt['inputManifest']['sha256']
    assert receipt['patches']==len(client['patchHashes'])==43
    for group in ['patchHashes','sharedInputs']:
        for name,digest in client[group].items():assert e.sha(ROOT/name)==digest,name
    for name in ['tests/check_eldritch_blast_suppliers.py','docs/eldritch-blast-suppliers-checkpoint.md',
                 'docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
                 'docs/skill-mechanical-reference.md']:
        pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',learnedEnabled=False,handlers=213,warlockHandlers=4,
                completedClasses=[],contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,
                nativeMarkers=12,markers=dict(expected),boots=3,reloads=2,saves=3,
                tableBoundaryControls=48,newCompilerMatrices=0,newProcessReads=0,
                currentDo165=dict(dispatcherLinked=True,capturedPrefixBytes=captured,totalBytes=total,
                                  crossMapFragments=1,protectedFragments=7,callbackVerified=False),
                primary={key:primary[key] for key in ['calc1','calc2','auralencalc','aurarangecalc',
                                                     'perdelay','mana','lvlmana','manashift','EDmgSymPerCalc']},
                artifacts=born['artifacts'],
                preservation=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=43,installed=False),
                verified=['Pinned table-only magic suppliers,hard-only damage/duration,constant recovery fields and fractional costs;48 independent tier/hard-point controls.',
                          'Real Warlock/Witch hard/effective ranks,Tarnhelm context capture,subsequent gear/training independence,original saved helm and unchanged resources through two native reloads.',
                          'No binding or learned cast admission;no periodic timer,damage,resource transfer or nova emission was created.'],
                limits=['Only92of1198current do165 bytes were readable in the retained capture. Startup28,periodic state221 installation/refresh,first pulse and later cadence,range geometry,damage/recovery recipient ordering and actual nova role remain unverified.',
                        'Existing SkillValues arithmetic supplies unmastered magic endpoints;this does not qualify the current do165 getter/capture/roll/mastery/pierce context,especially large-rank arithmetic. Nonzero owner magic mastery is explicitly refused.',
                        'The raw radius5,period30and recovery5fields are not a combat or healing contract. Param2/Param9 descriptions do not override actual calc formulas. Full input/payment/world/special-recipient behavior is not implemented.',
                        'Effects/audio and talent/left/right/quick icons remain separately needed.'],
                inputs=inputs)
    out=ROOT/'tools/reference/eldritch-blast-suppliers-native-evidence.json'
    encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Eldritch Blast suppliers:48 table controls,12native markers/two reloads;213handlers;periodic combat remains gated.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
