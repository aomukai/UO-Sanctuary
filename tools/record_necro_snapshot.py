#!/usr/bin/env python3
"""Bind current Mage/golem births separately from historical upgrade scopes."""
import argparse
from collections import Counter
from pathlib import Path
from record_warrior_snapshot import expected as previous_expected
import skill_checkpoint_evidence as evidence

ROOT=evidence.ROOT
STAGE=ROOT/'.local/class-skills/necro-snapshot'
WIDE=ROOT/'.local/class-skills/necro-snapshot-broader'
PARENT='d6a57ff3180d5240f83ab1a0cdd20ef5067c7fea'
FAMILIES=('necro-snapshot','mage-pet-buff','mage-basics','mage-chain','mage-glacial','mage-meteor','mage-static','clay-upgrades','fire-upgrades','npc-open-wounds','iron-golem-strong','iron-golem-spectral')
LIVE=('mage-basics','mage-chain','mage-glacial','mage-meteor','mage-static','clay-upgrades','fire-upgrades','iron-golem-strong','iron-golem-spectral')


def expected(broader):
    result=previous_expected(broader)
    for family in FAMILIES:result[family+'-check']+=1;result[family+'-reload-check']+=2
    for family in LIVE:result[family+'-live-result']+=3
    return result


def build(check=False):
    born=evidence.inventory(STAGE,WIDE,STAGE/'pre-build-5.json')
    before=evidence.load(ROOT/'.local/class-skills/warrior-snapshot/pre-build-1.json')['inputs']
    now=evidence.load(STAGE/'pre-build-5.json')['inputs']
    production={'MagePetBuffs','ClayGolemUpgrades','FireGolemUpgrades','IronGolemUpgrades','NpcOpenWounds','NpcHitSkills','NecromancerSummons','NecromancerCombat','IronGolemCasting','SummonUpgradeSnapshots'}
    tests={'MagePetBuffChecks','MageBasicChecks','MageChainChecks','MageMeteorChecks','MageGlacialChecks','MageStaticFieldChecks','ClayGolemUpgradeChecks','FireGolemUpgradeChecks','NpcOpenWoundsChecks','BloodLifeTapChecks','UnitEventsChecks','IronGolemStrongChecks','IronGolemSpectralChecks','NecroSnapshotChecks'}
    changed={p for p in before['sourceInputs'].keys()|now['sourceInputs'].keys() if before['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}
    assert changed=={f'src/Sanctuary.Content/{n}.cs'for n in production}|{f'tests/Sanctuary.Integration/{n}.cs'for n in tests}
    assert before['copiedData']==now['copiedData'] and before['projectInputs']==now['projectInputs']
    content=evidence.load(STAGE/'pre-build-3.json')['inputs']
    assert {p:h for p,h in content['sourceInputs'].items()if not p.startswith('tests/')}=={p:h for p,h in now['sourceInputs'].items()if not p.startswith('tests/')}
    for stage,broader,count in ((STAGE,False,311),(WIDE,True,527)):
        log=evidence.cohort(stage,expected(broader),count)
        assert 'Necromancer snapshots:18actual Mage/golem births' in log
        saved=evidence.load(stage/'necro-snapshot-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==18
        assert len({r['Owner']for r in saved['Actors']})==len({r['Body']for r in saved['Actors']})==18
        skills=evidence.load(ROOT/'data/Sanctuary/Balance/class-skills.json')
        abilities={r['talentId']:r['ability']for r in skills}
        for talent,hard in [('skeletalmage',[4,5,9,10,19,20]),('claygolem',[9,10,20]),('bloodgolem',[9,10,20]),('irongolem',[9,10,20]),('firegolem',[9,10,20])]:
            rows=[r for r in saved['Actors']if r['Ability']==abilities['necromancer.'+talent]]
            assert [r['Hard']for r in rows]==hard
            for row in rows:
                assert row['Upgrade']==dict(Actor=row['Body'],Owner=row['Owner'],Ability=row['Ability'],HardRank=row['Hard'])
                assert row['OwnerHard']==(1 if row['Hard']==20 else 20) and row['Rank']==row['Hard']+1
                assert 0<row['Life']<row['Maximum'] and row['Order']>0
                assert bool(row['Item'])==(talent=='irongolem') and row['Numeric']
        for name in ('warrior-snapshot','mage-pet-buff','mage-basics','mage-chain','mage-glacial','mage-meteor','mage-static','clay-upgrades','fire-upgrades','npc-open-wounds','iron-golem-strong','iron-golem-spectral'):
            assert evidence.load(stage/(name+'-fixtures.json'))['Generation']==2,name
    for name in ('content-build-2.log','integration-build-5.log'):
        assert all(s in (STAGE/name).read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    audit=evidence.audits(STAGE,PARENT,born['artifacts']);evidence.verification(STAGE);protected=evidence.protection()
    status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert not status['completedClasses'] and sum(r['implementation']=='handler-present'for r in status['skills'])==197
    prior=evidence.load(ROOT/'tools/reference/warrior-snapshot-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==prior['artifacts'][name]
    files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_necro_snapshot.py',ROOT/'tools/reference/summon-snapshot-decision.json']+[ROOT/p for p in changed]
    files += [ROOT/'docs'/name for name in ('necro-snapshot-checkpoint.md','summon-stat-snapshots.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
    files += evidence.stage_files(STAGE,WIDE)
    for name in ('failed-integration-build-1','failed-focused-1','failed-focused-2','failed-focused-3'):
        folder=STAGE/name;files += [p for p in folder.rglob('*')if p.is_file()]
        if name.startswith('failed-focused'):
            old=evidence.load(folder/'birth-inputs.json')
            for kind in ('Content','Integration'):assert evidence.sha(folder/kind.lower()/f'Sanctuary.{kind}.dll')==old['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],globalSnapshotRolloutComplete=False,
        scope='Current learned Mage/four golem upgrade snapshots and paid ordinary-summon transaction;explicit constructed missing-metadata historical controls remain separate.',
        artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=2,integrationBuild=5,sourceSnapshot=5,focusedRun=4,broaderRun=1,
        focusedMarkers=311,broaderMarkers=527,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,originalSavedCurrentBirthsPerCohort=18,
        currentMageBirthHardRanks=[4,5,9,10,19,20],currentGolemBirthHardRanks=[9,10,20],ownerHardRanksAtSave=[1,20],allocationManaLossControls=2,
        currentActualEffects=['fixed10Glacial damage/freeze','Blood wounds/fixed5LifeTap','Fire CBF/cold immunity','free fixed20FireMageEnchant'],
        retainedNaturalScenesPerCohort=27,pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=audit,protection=protected,
        diagnostics=['Integration1 used the wrong Iron fixture controller method;corrected to RestoreController.',
            'Focused1 rejected canonical-case summon/body names;registry now uses the existing case-insensitive monster identifier convention.',
            'Focused2/3 found remaining item-wound/Iron live-transition fixtures not yet marked as constructed historical missing-metadata scopes. Marked those fixtures explicitly;current18public-cast cases remain unchanged. Failed inputs,source,binaries and logs are archived.'],
        limits=['Historical missing-metadata profiles retain their prior live rule until resummoned. Compatibility fixtures are deliberately constructed;no separate old-binary writer run is claimed. Current public-cast scopes prove the new policy independently.',
            'Druid and remaining cross-class owner readers still need rollout. Other class-roadmap,shared sunder,path/current-executable/graphical and asset gaps remain. No class is complete.',
            'No deployment,native/client/protocol/existing actor layout or artwork change. Existing optional upgrade persistence192is reused.'],)
    evidence.write('necro-snapshot-native-evidence.json',receipt,files,check)
    print('Necromancer snapshots:311focused/527broader markers;18current original births through2reloads,actual effects/payment rollback and separate legacy controls;71Python/audits/protection pass.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
