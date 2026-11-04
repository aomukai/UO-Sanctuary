#!/usr/bin/env python3
"""Bind actual earlier-writer warrior/four-element Mage upgrade acceptance."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess

from record_grizzly_body import sha
from record_poison_dagger_contact import markers
from record_necro_legacy_upgrade import load, hashes
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/necro-legacy-army'
PARENT='7ca8ad481e1c8a73d0d522d32859f877c1dc2c6d'


def record(check=False):
    now=snapshot(STAGE/'pre-build-5.json')['inputs']
    prior_receipt_path=ROOT/'tools/reference/necro-legacy-upgrade-evidence.json'
    prior=load(prior_receipt_path)
    prior_stage=STAGE.with_name('necro-legacy-upgrade')
    prior_input=prior_stage/'migration-inputs.json'
    assert sha(prior_input)==prior['evidence'][str(prior_input.relative_to(ROOT))]
    baseline=load(prior_stage/'pre-build-2.json')['inputs']
    added='tests/Sanctuary.Integration/NecromancerLegacyArmyChecks.cs'
    assert set(now['sourceInputs'])-set(baseline['sourceInputs'])=={added}
    assert all(now['sourceInputs'][p]==h for p,h in baseline['sourceInputs'].items())
    assert now['projectInputs']==baseline['projectInputs'] and now['copiedData']==baseline['copiedData']
    for path,digest in load(STAGE/'seed-pre-build-4.json').items():
        assert sha(ROOT/path)==digest,path
    proof=load(STAGE/'migration-inputs.json')
    for path,digest in proof['sourceInputs'].items():
        assert sha(ROOT/path)==digest,path
    assert proof['seedAssemblySha256']==sha(STAGE/'legacy-seed/Sanctuary.LegacySeed.dll')
    install_path=ROOT/'tools/reference/creeper-frame-installation-evidence.json'
    installed=load(install_path);runtime_root=Path(installed['runtimeRoot'])
    old_path=ROOT/'tools/reference/creeper-frame-native-evidence.json'
    assert sha(old_path)==installed['sourceArtifacts'][str(old_path.relative_to(ROOT))]
    old=load(old_path)
    assert proof['oldArtifacts']==proof['actualOldArtifacts']==old['inputInventory']['artifacts']
    for path,digest in proof['oldArtifacts'].items():
        source=runtime_root/('.local/class-skills/creeper-frame-correction/integration/Sanctuary.Integration.dll'
            if path.endswith('Sanctuary.Integration.dll') else '.local/server/'+path)
        assert sha(source)==digest,path
    assert proof['oldCopiedData']==hashes(runtime_root/'.local/server/Data/Sanctuary')
    assert {'Data/Sanctuary/'+p:h for p,h in proof['oldCopiedData'].items()}==old['inputInventory']['copiedData']
    previous_artifacts=load(prior_input)['currentArtifacts']
    for path,digest in proof['currentArtifacts'].items():
        kind='integration' if path.endswith('Sanctuary.Integration.dll') else 'content'
        source=STAGE/kind/Path(path).name if path.startswith('Assemblies/Sanctuary.') else STAGE/'native/Distribution'/path
        assert sha(source)==digest,path
        if not path.endswith('Sanctuary.Integration.dll'):
            assert previous_artifacts[path]==digest,path
    assert {'Data/Sanctuary/'+p:h for p,h in proof['currentCopiedData'].items()}==now['copiedData']
    history=load(STAGE/'old-source-inputs.json')
    assert len(history['sources'])==6 and history['commit']==installed['originalMainSourceCommit']
    for path,digest in history['sources'].items():
        assert (STAGE/'old-source'/path).read_bytes()==subprocess.check_output(['git','show',history['commit']+':'+path],cwd=ROOT)
        assert sha(STAGE/'old-source'/path)==old['inputInventory']['sourceInputs'][path]==digest
    total=Counter()
    assert [row['writer'] for row in proof['boots']]==['installed','current','current']
    for boot,row in enumerate(proof['boots']):
        log=STAGE/f'boot-{boot}.log';text=log.read_text()
        assert ': failed'not in text and ' ERR]'not in text
        assert text.count('Writing world save snapshot done')==text.count('Shutting down')==1
        total+=markers(log)
        assert row['saves'] and hashes(STAGE/f'world-{boot}')==row['saves']
    expected=Counter({'class-skills-check':1,'necro-legacy-seed':1,'necro-legacy-upgrade-check':2,
        'necro-legacy-army-seed':1,'necro-legacy-army-check':2})
    assert total==expected and sum(total.values())==7
    seeds=[load(STAGE/f'boot-{boot}-necro-legacy-army-seed.json')for boot in range(3)]
    assert seeds[0]==seeds[1]==seeds[2];seed=seeds[0]
    assert (seed['Life'],seed['Maximum'],seed['Mana256'])==(151,192,50048)
    assert 4<=seed['ElementAttempts']<=256 and len(seed['Actors'])==5
    assert sorted(a['Element']for a in seed['Actors'])==[-1,0,1,2,3]
    assert len({a['Serial']for a in seed['Actors']})==len({a['Order']for a in seed['Actors']})==5
    for actor in seed['Actors']:
        assert actor['Rank']==actor['Mastery']==20 and actor['Level']==99 and actor['Order']>0
        if actor['Element']<0:
            assert actor['Type']=='Sanctuary.SkeletonGuardian' and (actor['Life'],actor['Maximum'])==(1147,1719)
        else:
            assert actor['Type']=='Sanctuary.NecromancerMinion' and (actor['Life'],actor['Maximum'])==(398,596)
            ranks=json.loads(actor['Ranks'])
            assert ranks['Raise Skeletal Mage']==ranks['base:Raise Skeletal Mage']==ranks['Skeleton Mastery']==20
    for boot in (1,2):
        progress=load(STAGE/f'boot-{boot}-necro-legacy-army-progress.json')
        assert progress['Generation']==boot and len(progress['Mages'])==4
        assert {m['Actor']for m in progress['Mages']}=={a['Serial']for a in seed['Actors']if a['Element']>=0}
        for mage in progress['Mages']:
            assert mage['Seed']['Actor']==mage['Actor'] and mage['Seed']['Owner']==seed['Owner']
        retained=load(STAGE/f'boot-{boot}-necro-legacy-progress.json')
        assert retained==dict(Generation=boot,Remaining256=620*256-64*boot)
    source_path=ROOT/'data/Sanctuary/Balance/necromancer-summons.json'
    source=next(row['fields']for row in load(source_path)if row['key']=='necromage')
    skills_path=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';skills=load(skills_path)
    assert source['minHP']==source['maxHP']==61 and skills['80']['Param2']==10 and skills['69']['Param1']==8
    base=source['minHP']+20*skills['69']['Param1'];percent=skills['80']['Param2']*(20-3)
    assert base*(100+percent)//100==596
    maximum256=base*256+base*256*percent//100
    assert maximum256==152755 and (maximum256+255)//256==597
    for name in ('legacy-seed-build-4.log','integration-build-3.log'):
        assert all(word in(STAGE/name).read_text()for word in('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        path=ROOT/'tools/reference'/name;audit=load(path)
        assert audit==json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT))
        assert audit['ContentAssemblySha256']==proof['currentArtifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest,path
    for name,word in (('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),
        ('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):
        assert word in(STAGE/name).read_text()
    assert '\nOK\n'in(STAGE/'python-tests.log').read_text()
    assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(runtime_root/path)==digest,path
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        assert len(protected[key])==count and hashes(runtime_root/'.local/server'/relative)==protected[key]
    status=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert not status['completedClasses'] and sum(r['implementation']=='handler-present'for r in status['skills'])==191
    files=[Path(__file__).resolve(),prior_receipt_path,prior_input,old_path,install_path,source_path,skills_path,
        *(ROOT/path for path in proof['sourceInputs']),
        *(ROOT/'src/Sanctuary.Content'/name for name in ('SkillHealth.cs','NecromancerMageBirth.cs','MageWorld.cs','MageBasicSkills.cs','SkeletonPetUpgrades.cs')),
        *(ROOT/'docs'/name for name in ('necro-legacy-army-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md',
            'class-skill-reuse-notes.md','skill-effects.md','skill-icons.md','mage-elements-checkpoint.md','mage-historical-checkpoint.md'))]
    files +=[STAGE/name for name in ('pre-build-5.json','seed-pre-build-4.json','old-source-inputs.json','migration-inputs.json',
        'legacy-seed-build-4.log','integration-build-3.log','migration-run-5.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log')]
    files +=[p for p in STAGE.glob('boot-*')if p.is_file()]
    files +=[STAGE/'old-source'/path for path in history['sources']]
    for attempt in range(1,5):
        files +=[p for p in(STAGE/f'attempt-{attempt}').rglob('*')if p.is_file()and'runtime-1'not in p.relative_to(STAGE).parts]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Actual earlier-writer original warrior/four-element Mage upgrade and retained Bone Armor/Clay controls',
        productionChanged=False,installed=False,goalComplete=False,completedClasses=[],
        builds=dict(seed=4,integration=3),run=5,
        verified=dict(commandMarkers=7,retainedPredecessorMarkers=4,boots=3,actualReloads=2,worldSaves=3,
            originalArmyActors=5,oldMageElements=[0,1,2,3],convertedPoisonToIce=True,approvedBasicReleases=8,
            createdProjectiles=150,actualDamagingReleases=8,warriorFlatReturn=34,warriorExtraFastMovement=True,
            originalWoundsRanksOwnerOrderRetained=True,adoptedSourceStreamsPersist=True,legacyMageMaximum256=152755,
            correctedMageNativeMaximum=[596,597],pythonTests=71,reusedAuditFingerprints=[1020,21],namedDecisionsUnchanged=573,
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        oldArtifacts=proof['oldArtifacts'],currentArtifacts=proof['currentArtifacts'],
        evidence={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))},
        limitations=['Seven focused old-writer upgrade command markers,not a fresh full-class or487-marker regression. Production/data/native/content bytes are reused unchanged.',
            'Actual old factories/RNG/corpse rights produce actor bytes;the seed fixture reads state but does not manufacture actor serialization or missing phase/random history.',
            'Actors are internalized for inspection;adoption and basic flights execute in controlled checks. Natural online AI,secondary spell probabilities,active-flight/unheld saves and arbitrary old snapshots remain separate.',
            'Mage exact source maximum intentionally supersedes the old whole-HP floor;actual wounded life remains unchanged. Old source-frame/RNG history is unavailable;first adoption uses the documented neutral transition.',
            'Current-engine room/geometry/event/PvP/Sunder/client parity and final effects/audio/icons remain open. No deployment,original character access,protocol/schema change,new handler or class completion.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/necro-legacy-army-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Actual earlier-writer army:7 command markers/3 boots/2 reloads,5 original actors,8 damaging basic volleys/150 projectiles;installed build and saves preserved.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
