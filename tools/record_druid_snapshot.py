#!/usr/bin/env python3
"""Bind current Druid beast birth policy, original saves and legacy controls."""
import argparse
from collections import Counter
import json
from pathlib import Path
from record_necro_snapshot import expected as previous_expected
import skill_checkpoint_evidence as evidence

ROOT=evidence.ROOT
STAGE=ROOT/'.local/class-skills/druid-snapshot'
WIDE=ROOT/'.local/class-skills/druid-snapshot-broader'
PARENT='5749844bd37293f8e662d8ce1681bec2bf0c2f3e'
FAMILIES=('druid-snapshot','spirit-wolf-upgrades','dire-wolf-upgrades','grizzly-upgrades','dire-wolf-world','grizzly-world','spirit-wolf-mode')
LIVE=('spirit-wolf-upgrades-live','dire-wolf-upgrades-live','grizzly-upgrades-live')
def expected(broader):
    result=previous_expected(broader)
    for family in FAMILIES:result[family+'-check']+=1;result[family+'-reload-check']+=2
    for command in LIVE:result[command+'-check']+=3;result[command+'-result']+=3
    return result

def build(check=False):
    born=evidence.inventory(STAGE,WIDE,STAGE/'pre-build-11.json')
    before=evidence.load(ROOT/'.local/class-skills/necro-snapshot/pre-build-5.json')['inputs']
    now=evidence.load(STAGE/'pre-build-11.json')['inputs']
    production={'DruidBeastAttackStats',*[f'Druid{n}{kind}'for n in ('SpiritWolf','DireWolf','Grizzly')for kind in ('Body','World')],*[n+'Upgrades'for n in ('SpiritWolf','DireWolf','Grizzly')]}
    tests={'DruidSnapshotChecks','SpiritWolfUpgradeChecks','DireWolfUpgradeChecks','GrizzlyUpgradeChecks','SpiritWolfModeChecks','DireWolfWorldChecks','GrizzlyWorldChecks','SpiritWolfWorldChecks'}
    changed={p for p in before['sourceInputs'].keys()|now['sourceInputs'].keys()if before['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}
    assert changed=={f'src/Sanctuary.Content/{n}.cs'for n in production}|{f'tests/Sanctuary.Integration/{n}.cs'for n in tests}
    assert before['copiedData']==now['copiedData']and before['projectInputs']==now['projectInputs']
    built=evidence.load(STAGE/'pre-build-2.json')['inputs']
    assert {p:h for p,h in built['sourceInputs'].items()if not p.startswith('tests/')}=={p:h for p,h in now['sourceInputs'].items()if not p.startswith('tests/')}
    abilities={r['talentId']:r['ability']for r in evidence.load(ROOT/'data/Sanctuary/Balance/class-skills.json')}
    for stage,broader,count in ((STAGE,False,350),(WIDE,True,566)):
        log=evidence.cohort(stage,expected(broader),count);assert 'Druid snapshots:12 public births' in log
        saved=evidence.load(stage/'druid-snapshot-fixtures.json');assert saved['Generation']== 2 and len(saved['Actors'])==12
        assert len({r['Owner']for r in saved['Actors']})==len({r['Body']for r in saved['Actors']})==12
        for talent in ('summonspiritwolf','summondirewolf','summongrizzly'):
            rows=[r for r in saved['Actors']if r['Ability']==abilities['druid.'+talent]]
            assert [r['Hard']for r in rows]==[9,10,19,20]
            for row in rows:
                assert row['OwnerHard']==(1 if row['Hard']== 20 else 20)
                assert row['Upgrade']==dict(Actor=row['Body'],Owner=row['Owner'],Ability=row['Ability'],HardRank=row['Hard'])
                birth=json.loads(row['Birth']);assert birth['Birth']['Rank']==row['Hard']+1
                assert row['Life']> 0 and row['Life']<row['Maximum']and row['Order']>0
        assert evidence.load(stage/'necro-snapshot-fixtures.json')['Generation']==2
    for name in ('content-build-2.log','integration-build-10.log'):
        assert all(t in (STAGE/name).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    audit=evidence.audits(STAGE,PARENT,born['artifacts']);evidence.verification(STAGE);protected=evidence.protection()
    prior=evidence.load(ROOT/'tools/reference/necro-snapshot-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==prior['artifacts'][name]
    status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
    files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_druid_snapshot.py',ROOT/'tools/reference/summon-snapshot-decision.json']+[ROOT/p for p in changed]
    files += [ROOT/'docs'/name for name in ('druid-snapshot-checkpoint.md','summon-stat-snapshots.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
    files+=evidence.stage_files(STAGE,WIDE)
    for name in ('failed-content-build-1','failed-integration-build-1','failed-integration-build-4','failed-focused-1','failed-focused-2','failed-focused-3','diagnostic-random-seed-pass','failed-focused-4','failed-reload-diagnostic-1','failed-reload-diagnostic-2'):
        folder=STAGE/name;files += [p for p in folder.rglob('*')if p.is_file()]
        if name.startswith(('failed-focused','failed-reload-diagnostic')):
            failed=evidence.load(folder/'birth-inputs.json')
            for kind in ('Content','Integration'):assert evidence.sha(folder/kind.lower()/f'Sanctuary.{kind}.dll')==failed['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    folder=WIDE/'failed-broader-1';files += [p for p in folder.rglob('*')if p.is_file()]
    failed=evidence.load(folder/'birth-inputs.json')
    for kind in ('Content','Integration'):assert evidence.sha(folder/kind.lower()/f'Sanctuary.{kind}.dll')==failed['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    diagnostic=ROOT/'.local/class-skills/druid-snapshot-reload-diagnostic'
    diagnostic_log=evidence.cohort(diagnostic,Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'druid-snapshot-check':1,'druid-snapshot-reload-check':2}),12)
    assert 'Druid snapshots:12 public births' in diagnostic_log
    assert evidence.load(diagnostic/'birth-inputs.json')['artifacts']==born['artifacts']
    files+=evidence.stage_files(diagnostic)
    receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],globalSnapshotRolloutComplete=False,
        artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=2,integrationBuild=10,sourceSnapshot=11,focusedRun=5,broaderRun=2,
        focusedMarkers=350,broaderMarkers=566,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,originalSavedCurrentBirthsPerCohort=12,
        species=['Spirit Wolf','Dire Wolf','Grizzly'],hardRanks=[9,10,19,20],pythonTests=71,auditFingerprints=audit,protection=protected,
        scope='New public Druid beast born hard upgrades and ordinary/feeding/sequence inherited mode rank. Original12actors/owners/birth/resources/order/RNG through2reloads;separate retained historical transition scopes.',
        limits=['Actual new AI entry checks use an explicit source clock;no claim of new naturally scheduled current-profile combat or full native geometry parity.',
            'Missing-metadata historical profiles are explicitly constructed,not a separate old-binary writer run. Normal external buffs,innate Battle Command,permission/cap and facet rescaling retain their rules.',
            'Raven and other cross-class live readers,remaining handlers,sunder/path/current executable/graphical and effects/icons remain unfinished. No class is complete;no deployment/native/client/protocol change.'])
    evidence.write('druid-snapshot-native-evidence.json',receipt,files,check)
    print('Druid snapshots:350focused/566broader markers;12current originals through2reloads;71Python/audits/protection pass.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
