#!/usr/bin/env python3
"""Record current Amazon integration and adopted Impale/Pierce/cold rules."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/amazon-lifecycle-acceptance'
FOCUS=ROOT/'.local/class-skills/amazon-remaining-input'
CONT=ROOT/'.local/class-skills/amazon-pierce-continuation'
CONTACT=ROOT/'.local/class-skills/amazon-pierce-contacts'
BASE='c8bb26521445938664546587331806ff9dba2e48'
sys.path.insert(0,str(ROOT/'tests'))
from check_amazon_lifecycle_acceptance import configuration
from check_amazon_remaining_input import INPUTS

def expected():
    m=Counter(e.load(ROOT/'tools/reference/amazon-current-acceptance-native-evidence.json')['markers']);assert sum(m.values())==725
    for f in ('amazon-sequence-contact','impale-contact','amazon-thrust-contact','pierce-action','cold-corpse-proposal','defiler-health-link'):
        m[f+'-check']+=1;m[f+'-reload-check']+=2
    for label in (*INPUTS,'pierce-action-right','pierce-action-quick'):m[label+'-check']+=3;m[label+'-result']+=3
    m['pierce-seed-check']+=3;m['defiler-health-link-check-result']+=1;m['defiler-health-link-reload-check-result']+=2
    assert sum(m.values())==809
    return m

def basic():return Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
def archived(stage,artifacts):
    with tarfile.open(stage/'accepted-assemblies-1.tar.gz','r:gz')as t:
        for kind in ('Content','Integration'):
            assert hashlib.sha256(t.extractfile(kind.lower()+f'/Sanctuary.{kind}.dll').read()).hexdigest()==artifacts[f'Assemblies/Sanctuary.{kind}.dll']

def old_scope(stage,archive):
    # These successful focused runs precede later test-only repairs. Bind
    # their actual source/DLLs to the preserved pre-fix full-run archive;
    # never reinterpret their old checks as executions of current C#.
    born=e.load(stage/'birth-inputs.json');s=e.load(stage/'source-snapshot-1.json')['inputs']
    assert s['sourceInputs']=={p:d for p,d in born['sourceInputs'].items()if p.endswith('.cs')}and s['copiedData']==born['copiedData']
    with tarfile.open(archive,'r:gz')as a:
        for p,d in s['sourceInputs'].items():assert hashlib.sha256(a.extractfile(p).read()).hexdigest()==d,p
        for kind in ('Content','Integration'):
            path=str(STAGE.relative_to(ROOT)/kind.lower()/f'Sanctuary.{kind}.dll')
            assert hashlib.sha256(a.extractfile(path).read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    archived(stage,born['artifacts']);return born

def record(check=False):
    assert 'pass across3boots/2reloads'in(STAGE/'native-run-3.log').read_text()
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/barbarian-lifecycle-acceptance/birth-inputs.json')
    current=e.load(CONTACT/'birth-inputs.json');assert current['artifacts']==born['artifacts']and current['copiedData']==born['copiedData']
    for stage,snap in ((STAGE,'source-snapshot-3.json'),(CONTACT,'source-snapshot-1.json')):
        snapshot(stage/snap,stage/'birth-inputs.json');archived(stage,born['artifacts'])
        for p,d in e.load(stage/'birth-inputs.json')['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    older=old_scope(FOCUS,STAGE/'failed-native-1.tar.gz');continuation=old_scope(CONT,STAGE/'failed-native-2.tar.gz')
    assert older['artifacts']==parent['artifacts']and older['copiedData']==continuation['copiedData']==born['copiedData']
    assert {p:d for p,d in born['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}=={p:d for p,d in parent['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed_data=[p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]
    assert changed_data==['Data/Sanctuary/Balance/class-skill-status.json']
    status='data/Sanctuary/Balance/class-skill-status.json';ledger=e.load(ROOT/status)
    old=json.loads(subprocess.check_output(['git','show',BASE+':'+status],cwd=ROOT));impale=next(r for r in ledger['skills']if r['talentId']=='amazon.impale')
    next(r for r in old['skills']if r['talentId']=='amazon.impale')['remaining']=impale['remaining'];assert old==ledger
    assert 'approved Sanctuary rule'in impale['remaining']and impale['remaining']in(ROOT/'tools/build_skill_roadmap.py').read_text()
    assert len(ledger['skills'])==240 and not ledger['completedClasses']
    cs={p:d for p,d in born['sourceInputs'].items()if p.endswith('.cs')}
    changed=sorted(p for p,d in cs.items()if parent['sourceInputs'].get(p)!=d)
    wanted=[f'tests/Sanctuary.Integration/{n}Checks.cs'for n in ('PoisonJavelin','LightningBolt','LightningFury','ExplodingArrow','FreezingArrow','ImmolationArrow')]
    wanted +=[f'tests/Sanctuary.Integration/{n}ContactChecks.cs'for n in ('PlagueJavelin','PoisonJavelin','LightningBolt','LightningFury','Strafe','MagicArrow','MultipleShot')]
    assert changed==sorted(wanted)
    for p in wanted:assert 'PlayerPierceFixtures.NextBudget(p,4)'in(ROOT/p).read_text()
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]
    for kind in ('Content','Integration'):assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    build=(STAGE/'integration-build-2.log').read_text();assert all(s in build for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected(),809);assert log.count('Client: 127.0.0.1: Connected.')==262
    m=basic()
    for f in ('amazon-sequence-contact','impale-contact','amazon-thrust-contact'):m[f+'-check']+=1;m[f+'-reload-check']+=2
    for label in INPUTS:m[label+'-check']+=3;m[label+'-result']+=3
    e.cohort(FOCUS,m,66)
    m=basic();m['pierce-seed-check']+=3
    for f in ('poison-javelin','lightning-bolt','lightning-fury','exploding-arrow','freezing-arrow','immolation-arrow'):m[f+'-check']+=1;m[f+'-reload-check']+=2
    m['freezing-arrow-state-result']+=1;m['immolation-arrow-state-result']+=1;e.cohort(CONT,m,32)
    m=basic();m['pierce-seed-check']+=3
    for f in ('plague-javelin-contact','poison-javelin-contact','lightning-bolt-contact','lightning-fury-contact','strafe-contact','magic-arrow-contact','multiple-shot-contact','pierce-action'):m[f+'-check']+=1;m[f+'-reload-check']+=2
    e.cohort(CONTACT,m,36)
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete'in l]
    unsupported=[l for l in log.splitlines()if ' WRN]'in l and 'unsupported'in l.lower()];assert len(unsupported)==12 and all('Defiler link sharing refused unsupported context'in l for l in unsupported)
    audit=e.load(ROOT/'tools/reference/amazon-current-input-coverage.json');assert(audit['verifiedActiveInputRoutes'],audit['activeTalents'],audit['passiveTalents'],audit['ordinaryInputCases'],audit['foreignInputCasesSeparatelyExcluded'],audit['legacyCastCases'])==(24,24,6,153,42,45)
    assert audit['artifacts']==born['artifacts']
    for p,d in audit['files'].items():assert e.sha(ROOT/p)==d,p
    decision=e.load(ROOT/'tools/reference/impale-defense-policy-approval.json');assert decision['approved']and decision['referenceVersion']=='3.3.93847'
    assert e.sha(ROOT/decision['testedCandidateEvidence'])==decision['testedCandidateEvidenceSha256']
    original=subprocess.check_output(['git','show',decision['proposalCommitAtApproval']+':'+decision['proposal']],cwd=ROOT);assert hashlib.sha256(original).hexdigest()==decision['proposalAtApprovalSha256']
    named=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert named==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    previous_named=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    previous_named['InputHashes'][status]=e.sha(ROOT/status);assert previous_named==named
    from build_named_readiness import validate
    validate(named,assembly=STAGE/'content/Sanctuary.Content.dll')
    c=configuration();assert tuple(len(c[k])for k in ('extra_families','extra_checks','extra_target_input_commands'))==(81,6,65)
    paths=['tools/record_amazon_lifecycle_acceptance.py','tools/class_input_coverage.py','tools/review_amazon_input_coverage.py','tools/reference/amazon-current-input-coverage.json','tools/reference/impale-defense-policy-approval.json','tools/build_skill_roadmap.py',status,'docs/amazon-lifecycle-acceptance-checkpoint.md','docs/amazon-coverage-matrix.md','docs/impale-defense-policy-proposal.md','docs/class-skill-status-overview.md','docs/class-skill-roadmap.md','docs/skill-effects.md','docs/skill-icons.md']
    files=[*(ROOT/p for p in paths),ROOT/'tools/reference/named-item-runtime-audit.json',*c['extra_inputs'],*(ROOT/'tests'/n for n in ('check_amazon_remaining_input.py','check_amazon_pierce_continuation.py','check_amazon_pierce_contacts.py')),*e.stage_files(STAGE,FOCUS,CONT,CONTACT),*(s/'accepted-assemblies-1.tar.gz'for s in (STAGE,FOCUS,CONT,CONTACT)),STAGE/'failed-native-1.tar.gz',STAGE/'failed-native-2.tar.gz',STAGE/'post-run-audit-1.tar.gz']
    result=dict(component='Current Amazon integration and adopted Impale/Pierce/cold qualification',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=809,markers=dict(sorted(expected().items())),retainedPredecessorMarkers=725,focusedInputMarkers=66,focusedInputCases=24,focusedContinuationMarkers=32,focusedContactMarkers=36,fixtureFamilies=81,sharedChecks=6,currentInputCommands=65,totalWireCases=240,explicitAmazonInputCases=153,legacyCastControls=45,foreignInputCases=42,verifiedActiveTalents=24,passiveTalents=6,connections=262,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],contentReused='cold-corpse-rule-adoption Content1',integrationBuild=2,sourceSnapshot=3,nativeRun=3,productionCodeChanged=False,numericTablesChanged=False,statusAnnotationChanged='Approved Impale paid-defense policy;only its remaining text changes',contentRebuilds=0,nativeRebuilds=0,integrationBuilds=2,correctedCompileFailures=0,correctedNativeFailures=2,changedTestSources=changed,saveDeletionWarnings=warnings,intentionalScalarRefusals=12,installed=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/amazon-lifecycle-acceptance',limits='All725earlier markers retain. Four missing active input scopes and approved Impale,source-indexed Pierce,ordinary-chill corpses and current typed HealthLink are combined. All24active routes have current class/ability input evidence;153Amazon ordinary inputs,45legacyCast controls and42foreign ordinary inputs stay separate. Six passive consumers retain separate tests. Thirteen fixture setups select a legal per-owner Pierce index rather than using retired launch RNG;all damage/proc/poison/quantity/burst/collision/save assertions retain. Two failed full runs were frozen before fixes;66/32earlier focused passes remain bound to their archived pre-fix C#/DLLs,not current sources. Approved Impale describes existing paid bypass with normal low-mana defenses;modern D2R/PvP/unsupported-recipient parity remains unclaimed. Reference/world/item/event/geometry and final asset limits remain explicit;installed build/saves protected.')
    result.update(correctedPostRunAuditFailures=1,readinessMetadataRefresh='Only the skill-status input hash changes for approved Impale wording;all item definitions/results/fingerprints retain.')
    e.write('amazon-lifecycle-acceptance-native-evidence.json',result,files,check);print('Amazon:809exactresults/24active+6passive scopes/240wire controls/2reloads;Impale approval and indexed Pierce retained.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
