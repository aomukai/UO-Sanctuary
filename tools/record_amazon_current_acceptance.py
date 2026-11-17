#!/usr/bin/env python3
"""Bind the current combined Amazon class and shared-contact cohort."""
import argparse,json,sys
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/amazon-current-acceptance'
sys.path.insert(0,str(ROOT/'tests'))
from check_amazon_current_acceptance import configuration
from check_amazon_planned_acceptance import LEGACY_INPUTS

def expected_markers():
    c=configuration();r=Counter()
    def add(command,count):
        r[command]+=count
        waiting=c['extra_wait_results'].get(command,())
        for name in ((waiting,)if isinstance(waiting,str)else waiting):r[name]+=count
    add('iron-golem-birth-check',3)
    for family in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        add(family+'-check',1);add(family+'-reload-check',1 if family in c.get('extra_one_reload_families',())else 2)
    for command in (*c['extra_checks'],*c['extra_pre_save_commands']):add(command,3)
    for command in c['extra_target_input_commands']:r[command+'-check']+=3;r[command+'-result']+=3
    assert not c['extra_live_commands'] and not c['extra_save_results']
    for name in LEGACY_INPUTS:r[name+'-input-check']+=3;r[name+'-input-result']+=3
    r.update({'decoy-check':1,'decoy-reload-check':2,'decoy-result':3,'decoy-disconnect-check':3,'plague-javelin-snapshot-check':3})
    return r

def connections():
    c=configuration();r=expected_markers()
    return sum(r[name]for name in c['extra_connected_checks'])+3*(len(c['extra_target_input_commands'])+len(LEGACY_INPUTS)+1)

def record(check=False):
    import hashlib,subprocess,tarfile
    c=configuration();expected=expected_markers();count=sum(expected.values());assert count==725
    born=e.load(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    reuse=e.load(STAGE/'artifact-reuse-2.json');receipt=ROOT/reuse['parentCheckpoint']
    assert e.sha(receipt)==reuse['parentCheckpointSha256']
    parent=e.load(ROOT/'.local/class-skills/corpse-shatter-guard/birth-inputs.json')
    assert parent['artifacts']==reuse['artifacts'] and reuse['integrationAssemblySha256']==born['artifacts']['Assemblies/Sanctuary.Integration.dll']
    assert {p:d for p,d in born['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}=={p:d for p,d in parent['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}
    assert born['copiedData']==parent['copiedData']==reuse['copiedData']
    for name,digest in born['sourceInputs'].items():assert e.sha(ROOT/name)==digest,name
    changed=[p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d]
    assert changed==['tests/Sanctuary.Integration/PlayerItemProcWorldChecks.cs']
    build=(STAGE/'integration-build-1.log').read_text();assert all(t in build for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,digest in born['copiedData'].items():assert e.sha(ROOT/'data'/name.removeprefix('Data/'))==digest,name
    for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/name)==born['artifacts'][name]
    archive=STAGE/'accepted-assemblies-1.tar.gz'
    with tarfile.open(archive,'r:gz')as a:
        for kind in ('Content','Integration'):assert hashlib.sha256(a.extractfile(kind.lower()+'/Sanctuary.'+kind+'.dll').read()).hexdigest()==born['artifacts']['Assemblies/Sanctuary.'+kind+'.dll']
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-2.log').read_text()
    log=e.cohort(STAGE,expected,count);assert log.count('Client: 127.0.0.1: Connected.')==connections()==229 and 'denied by connection filter' not in log
    with tarfile.open(STAGE/'failed-native-1.tar.gz','r:gz')as a:
        failed=a.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode();assert 'valkyrie-initializer-seal: failed' in failed and 'World changed after the last checked family endpoint' in failed
    assert c['extra_pre_save_commands']==('player-item-proc-world-save-check','valkyrie-initializer-seal')
    fixture=(ROOT/'tests/Sanctuary.Integration/PlayerItemProcWorldChecks.cs').read_text();assert 'NpcWorldAllocationChecks.LoadedWorld==s.World' in fixture and 'NpcWorldAllocationChecks.Checked(final)' in fixture
    warnings=[line for line in log.splitlines()if 'Warning: Attempted to delete' in line]
    assert all('WardrobeSet' in line for line in warnings),warnings
    unsupported=[line for line in log.splitlines()if ' WRN]' in line and 'unsupported' in line.lower()]
    assert not unsupported,unsupported
    from record_amazon_planned_acceptance import expected_markers as historical
    from record_corpse_shatter_guard import expected_markers as current
    assert not historical()-expected and not current()-expected
    assert len(c['extra_families'])==75 and len(c['extra_checks'])==5 and len(c['extra_target_input_commands'])==55
    for boot in range(3):assert e.load(STAGE/f'decoy-boot-{boot}.json')
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');skills=[r for r in status['skills']if r['characterClass']=='Amazon']
    assert len(skills)==30 and all(r['implementation']=='handler-present'for r in skills)
    assert not status['contentScope']['charms'] and not status['contentScope']['runewords']
    assert status==json.loads(subprocess.check_output(['git','show',reuse['parentCommit']+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    files=[ROOT/'tools/record_amazon_current_acceptance.py',ROOT/'tests/check_amazon_current_acceptance.py',ROOT/'docs/amazon-current-acceptance-checkpoint.md',ROOT/'docs/amazon-coverage-matrix.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/skill-effects.md',ROOT/'docs/skill-icons.md',receipt,archive,STAGE/'failed-native-1.tar.gz',*c['extra_inputs'],*e.stage_files(STAGE)]
    result=dict(component='Current combined Amazon class and shared ranged-contact acceptance',referenceVersion='3.3.93847',contentRebuilt=False,integrationBuild=1,sourceSnapshot=2,nativeRun=2,changedSources=changed,gameplayChanged=False,runtimeDataChanged=False,frozenFailedAttempts=1,knownWardrobeSaveWarnings=len(warnings),baselineCommit=reuse['parentCommit'],nativeMarkers=count,markers=dict(sorted(expected.items())),connections=229,fixtureFamilies=75,sharedChecks=5,targetInputs=165,retainedCastInputs=42,decoyCastInputs=3,totalWireInputs=210,boots=3,saves=3,reloads=2,contentBuildReused=1,nativeRebuilt=False,artifacts=born['artifacts'],retainsHistoricalAmazonMarkers=334,retainsCurrentContactMarkers=489,saveDeletionWarnings=warnings,unsupportedWarnings=unsupported,contentScope=status['contentScope'],classCertified=False,goalComplete=False,installed=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='All30 existing Amazon handler fixtures,original Valkyrie initialization/AI/equipment/casting/buffs/town roster,Decoy expiry/socket loss and recent bow/javelin parent and child shared-contact cohorts run on current content/native and the corrected integration fixture with saved originals/resources/streams through two reloads. Union retains both full historical334and current489marker multisets without reinterpreting old receipts. This is server/input regression qualification,not current D2R executable or graphical-client parity. Paid Impale shield/avoidance,source Pierce owner seed,ordinary-cold shatter chance,Strafe cross-cast delay,modern animation/AR/event/owner/PvP/native geometry,full item/incoming and additional melee-contact qualification retain recorded limits. No class,asset,named-item gate or deployment completion follows.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/amazon-current-acceptance')
    e.write('amazon-current-acceptance-native-evidence.json',result,files,check);print('Current Amazon:725results/75families/210wire inputs/229connections/2reloads;accepted gameplay/rebuilt test witness,remaining limits explicit.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',action='store_true');p.add_argument('--check',action='store_true');a=p.parse_args()
    if a.plan:print(dict(results=sum(expected_markers().values()),connections=connections()))
    else:record(a.check)
