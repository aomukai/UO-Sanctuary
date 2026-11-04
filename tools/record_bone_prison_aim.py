#!/usr/bin/env python3
"""Bind approved Bone Prison acquisition to affected-system native evidence."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/bone-prison-aim'
PARENT='c9729a8d4f1787c2d9afd97ca6781521c99336df'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    paths=[STAGE/'birth-inputs.json',STAGE/'audit-inputs.json']
    native,audit=[inventory.validate_inventory(p)for p in paths]
    for p in paths:snapshot(STAGE/'pre-build-1.json',p)
    assert native['artifacts']==audit['artifacts']and native['copiedData']==audit['copiedData']
    prior=json.loads((STAGE.with_name('npc-iron-maiden')/'birth-inputs.json').read_text())
    sources={'src/Sanctuary.Content/'+n+'.cs'for n in ('NearClickTargets','PsychicHammers','BoneBarrierCasts','NecromancerCombat','ClassCombat')}
    sources|={'tests/Sanctuary.Integration/'+n+'.cs'for n in ('BoneBarrierBirthChecks','BonePrisonAimChecks')}
    assert {p for p in set(native['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and native['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}==sources
    assert {p for p in native['copiedData']if native['copiedData'][p]!=prior['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][n]==prior['artifacts'][n]==sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'bone-prison-aim-check':3,'bone-barrier-birth-save':3,'psychic-hammer-result':1,'psychic-hammer-reload-result':2})
    for family in ('iron-golem-items','npc-resource-callbacks','bone-barrier-cast','bone-barrier-birth','spirit-wolf-casting','psychic-hammer'):
        expected[family+'-check']=1;expected[family+'-reload-check']=2
    for command in ('bone-wall-target-right','bone-prison-self-quick','spirit-wolf-right','bone-prison-ground-right','bone-prison-ground-quick'):
        expected[command+'-check']=3;expected[command+'-result']=3
    log=(STAGE/'birth-native-checks.log').read_text();assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==60
    assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-1.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',1014),('first-loop-affix-audit.json',21)):
        fresh=json.loads((STAGE/name).read_text());assert fresh==json.loads((ROOT/'tools/reference'/name).read_text())and len(fresh['InputHashes'])==count
        assert fresh['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in fresh['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in fresh.items()if k not in skip}=={k:v for k,v in old.items()if k not in skip}
    assert(STAGE/'audit-native-checks.log').read_text().count(': passed')==2
    assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
    for name,text in (('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in(STAGE/name).read_text()
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old_status=json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    assert not status['completedClasses']and sum(s['implementation']=='handler-present'for s in status['skills'])==191
    changed=[(a,b)for a,b in zip(old_status['skills'],status['skills'])if a!=b];assert len(changed)==1
    a,b=changed[0];assert b['talentId']=='necromancer.boneprison'and {k for k in a if a[k]!=b[k]}=={'remaining'}
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key,relative,count in (('saves','.local/server/Saves',38),('configuration','.local/server/Configuration',20)):
        folder=Path(installed['runtimeRoot'])/relative;assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    files=[Path(__file__).resolve(),*(ROOT/p for p in sorted(sources)),ROOT/'tests/check_bone_prison_aim.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/reference/bone-prison-user-targeting-decision.json',ROOT/'data/Sanctuary/Balance/class-skill-status.json',
        *(ROOT/'docs'/n for n in ('bone-prison-aim-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-1.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','content-build-1.log','integration-build-1.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','native-run-1.log','failed-1/birth-inputs.json','failed-1/birth-native-checks.log','failed-1/check_bone_prison_aim.py'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Approved two-tile Bone Prison cursor acquisition;shared Psychic Hammer scan with preserved default tie refusal',
        verified=dict(affectedSystemNativeMarkers=60,boots=3,actualReloads=2,actualWorldSaves=3,actualGroundRightCasts=3,actualGroundQuickCasts=3,
            sharedPsychicHammerAndBarrierMechanicsRetained=True,cursorNearestNotCasterNearest=True,inclusiveCircleAndExplicitSerialTies=True,
            friendlyInvulnerableHeightRangeTownExclusion=True,heldRefreshRetainsPrimary=True,ordinaryMovingTargetTracking=True,
            unpaidWindupAndOnceReleasePayment=True,lostTargetNoReacquisitionOrPayment=True,noPrimaryNoCast=True,
            originalSelfQuickAndWallRightInputsRetained=True,normalExpiryAndCollisionCleanup=True,
            pythonTests=71,auditFingerprints=[1014,21],namedDecisionsUnchanged=573,installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=native['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Radius,circle geometry,serial ties and no facing restriction are explicit Sanctuary policy,not measured current-D2R client acquisition.',
            'This affected-system cohort is not a new565-marker original-summon regression. The prior accepted original-summon checkpoint remains at its frozen parent commit.',
            'Existing barrier footprint/terrain/projectile,current-executable/world/client and historical source limits remain. No class complete,deployment or final artwork.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/bone-prison-aim-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Bone Prison aim:60affected-system markers,6actual ground inputs,two reloads;shared Psychic Hammer behavior,exact payment/expiry and installed artifacts/saves preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
