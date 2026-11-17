#!/usr/bin/env python3
"""Bind the tested saved player Pierce action-index mapping candidate."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_pierce_source_seed import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/pierce-action-policy';BASE='93bc0fc51c4f4004296780ef0dc568c3bf4962d6'
sys.path.insert(0,str(ROOT/'tests'))
from check_pierce_action_policy import configuration

def expected_markers():
    r=prior_markers();r.update({'pierce-action-check':1,'pierce-action-reload-check':2,'pierce-action-right-check':3,'pierce-action-right-result':3,'pierce-action-quick-check':3,'pierce-action-quick-result':3});return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/pierce-source-seed/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/AmazonBow.cs','src/Sanctuary.Content/AmazonJavelins.cs','src/Sanctuary.Content/AmazonStrafe.cs','src/Sanctuary.Content/PlayerPierceActions.cs','tests/Sanctuary.Integration/PierceActionChecks.cs'],changed
    first=e.load(STAGE/'source-snapshot-1.json')['inputs']['sourceInputs'];second=e.load(STAGE/'source-snapshot-2.json')['inputs']['sourceInputs']
    assert {p:d for p,d in first.items()if not p.startswith('tests/')}=={p:d for p,d in second.items()if not p.startswith('tests/')}
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for kind,build in (('Content',1),('Integration',2)):
        assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll'];text=(STAGE/(kind.lower()+f'-build-{build}.log')).read_text();assert all(t in text for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==33;log=e.cohort(STAGE,expected_markers(),33);assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==6 and 'denied by connection filter' not in log
    assert 'Warning: Attempted to delete' not in log
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as a:
        for kind in ('Content','Integration'):assert hashlib.sha256(a.extractfile(kind.lower()+f'/Sanctuary.{kind}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    with tarfile.open(STAGE/'failed-integration-build-1.tar.gz','r:gz')as a:
        text=a.extractfile(str(STAGE.relative_to(ROOT)/'integration-build-1.log')).read().decode();assert 'CS0128' in text and "'account'" in text
    s=e.load(STAGE/'pierce-action-fixtures.json');assert s['Generation']==2 and s['Index']==3 and s['Life']>0 and s['Mana']==17 and s['Ammo']==99
    assert len(s['Gear'])==3 and s['Flight']['Owner']==s['Owner'] and s['Flight']['Ability']==28
    # The final index3/chance56 outcome is independently bound to the source helper.
    seed=(666<<32)|3;budget=0
    while budget<4:
        seed=0x6AC690C5*(seed&0xffffffff)+(seed>>32)
        if (seed&0xffffffff)%100>=56:break
        budget+=1
    assert s['Flight']['Pierces']==budget
    code=(ROOT/'src/Sanctuary.Content/PlayerPierceActions.cs').read_text();assert 'Approved=>false' in code and '258' in code and 'unchecked(before+1)' in code and 'Candidates.Contains(owner.Serial.Value)' in code
    bow=(ROOT/'src/Sanctuary.Content/AmazonBow.cs').read_text();assert bow.index('if(created==0){return false;}')<bow.index('pierce?.Commit();')
    jav=(ROOT/'src/Sanctuary.Content/AmazonJavelins.cs').read_text();assert jav.index('if(id<=0){return false;}')<jav.index('pierce?.Commit();')
    strafe=(ROOT/'src/Sanctuary.Content/AmazonStrafe.cs').read_text();assert 'series.Pierce??PlayerPierceActions.Capture(owner)'in strafe and 'if(projectile>0&&pierce!=null)'in strafe
    fixture=(ROOT/'tests/Sanctuary.Integration/PierceActionChecks.cs').read_text();assert 'Saved counter enabled unapproved gameplay' in fixture
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert status==json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    files=[ROOT/'tools/record_pierce_action_policy.py',ROOT/'docs/pierce-action-policy-proposal.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/amazon-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-integration-build-1.tar.gz',*(ROOT/p for p in changed)]
    result=dict(component='Saved player ranged-action Pierce index policy candidate,Amazon integration',referenceVersion='3.3.93847',baselineCommit=BASE,policyStatus='pending-approval',nativeMarkers=33,markers=dict(sorted(expected_markers().items())),routedAmazonTalents=14,newWireInputs=6,connections=6,boots=3,saves=3,reloads=2,changedSources=changed,changedRuntimeData=[],artifacts=born['artifacts'],contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,failedIntegrationBuilds=1,failedNativeAttempts=0,newProfile=dict(name='SanctuaryPlayerPierce',priority=258,format=0),existingProfileVersionsChanged=False,releasedFlightFormatsChanged=False,onlineEnabled=False,nativeRebuilt=False,installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Candidate only:Approvedfalse and saved counter rows never enroll gameplay. New profile stores uint owner/index;initial0,next index per successful released action,one per fan/Strafe series and unsigned wrap are explicit proposed adaptations. Current stats/chance,critical selection and mana/ammo/timing/contact remain. All14Amazon routes,unapproved fallback RNG,candidate critical-only launch draws,stale/repeated tickets,invalid restore,cancellation/aim,Strafe capture,javelins,wrap and same-index budget pass;original owner/gear/proc state/wounds/fractional mana/ammo/index/flight persist and existing flight resumes without index/resource replay over2reloads. Real right/quick inputs are verified. Other player producers and NPC index ownership remain unwired or separate. Fault-injected allocation refusal and all held/left variants are not newly native-qualified;commit-after-success is source-bound. Exact current D2R owner-index initialization/input callbacks,RNG/event/geometry/PvP/full class/client/assets remain unverified. One frozen fixture compile failure corrected duplicate account declaration;productionC1unchanged.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/pierce-action-policy')
    e.write('pierce-action-policy-native-evidence.json',result,files,check);print('Pierce action candidate:33results/14Amazon routes/6inputs/2reloads;explicit shared player rule awaits adoption.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
