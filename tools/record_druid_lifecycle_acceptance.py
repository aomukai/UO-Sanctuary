#!/usr/bin/env python3
"""Bind current Druid integration to exact reused binaries and ordinary inputs."""
import argparse,hashlib,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/druid-lifecycle-acceptance'
FOCUS=ROOT/'.local/class-skills/druid-item-form-retention'
sys.path.insert(0,str(ROOT/'tests'))
from check_druid_lifecycle_acceptance import configuration
from check_druid_planned_acceptance import ELEMENT_INPUTS

def expected():
    c=configuration();r=Counter()
    def add(n,v):
        r[n]+=v;results=c['extra_wait_results'].get(n,())
        for result in((results,)if isinstance(results,str)else results):r[result]+=v
    add('iron-golem-birth-check',3)
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):add(f+'-check',1);add(f+'-reload-check',2)
    for n in c['extra_checks']:add(n,3)
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    for n in c['extra_live_commands']:
        if n in c.get('checked_live_commands',()):r[n+'-check']+=3
        r[n+'-result']+=3
    for n in c['extra_pre_save_commands']:r[n]+=3
    for n in ELEMENT_INPUTS:r[n+'-input-check']+=3;r[n+'-input-result']+=3
    for n in ('shock-wave-live-check','shock-wave-live-result','arctic-held-input-save-stage-check','druid-fury-stage-check','druid-fury-stage-result'):r[n]+=3
    for n in (0,1,9):r[f'arctic-held-wire-{n}-result']+=3
    assert sum(r.values())==1139
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/assassin-lifecycle-acceptance/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    assert {p:d for p,d in born['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}=={p:d for p,d in parent['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}
    assert born['copiedData']==parent['copiedData']
    wanted=sorted('tests/Sanctuary.Integration/'+n+'Checks.cs'for n in ('DruidBasicProc','DruidCapturedProc','DruidActionProc','DruidSetupItem','DruidWeaponPoison'))
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==wanted
    for p,d in born['sourceInputs'].items():
        assert e.sha(ROOT/p)==d,p
        if p.endswith('.cs')and p not in wanted:assert parent['sourceInputs'][p]==d,p
    before=e.load(STAGE/'source-snapshot-1.json')['inputs']['sourceInputs']
    with tarfile.open(STAGE/'failed-native-1.tar.gz')as frozen:
        for p in wanted:
            original=frozen.extractfile(p).read().decode();assert hashlib.sha256(original.encode()).hexdigest()==before[p]
            needle='var point=p.Location;ClassCombat.Stop(p);p.Internalize();'if p.endswith('/DruidBasicProcChecks.cs')else'var point=p.Location;p.Internalize();'
            assert (ROOT/p).read_text()==original.replace(needle,'DruidFormWorldChecks.RetainForInspection(p);'+needle,1)
        prefix=str(STAGE.relative_to(ROOT))+'/'
        old=e.load(STAGE/'failed-native-1-native-inputs.json');assert old['Assemblies/Sanctuary.Integration.dll']==parent['artifacts']['Assemblies/Sanctuary.Integration.dll']
        assert 'druid-basic-proc-reload-check: failed'in frozen.extractfile(prefix+'birth-native-checks.log').read().decode()
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz')as a:
        for k in ('Content','Integration'):
            assert hashlib.sha256(a.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
    log=e.cohort(STAGE,expected(),1139);assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
    snapshot(FOCUS/'source-snapshot-1.json',FOCUS/'birth-inputs.json');assert e.load(FOCUS/'birth-inputs.json')['artifacts']==born['artifacts']
    focused=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for n in ('druid-basic-proc','druid-captured-proc','druid-action-proc','druid-setup-items','druid-weapon-poison'):focused[n+'-check']+=1;focused[n+'-reload-check']+=2
    e.cohort(FOCUS,focused,24)
    assert all(t in(STAGE/'integration-build-2.log').read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('druid-planned-acceptance',675),('druid-pet-link',684)):
        old=Counter(e.load(ROOT/f'tools/reference/{name}-native-evidence.json')['markers']);assert sum(old.values())==count and not(old-expected())
    audit=e.load(ROOT/'tools/reference/druid-current-input-coverage.json');assert(audit['activeTalents'],audit['passiveTalents'],audit['verifiedActiveInputRoutes'],audit['heldInputSequences'],audit['legacyInputSequences'])==(29,1,29,9,27)
    assert audit['artifacts']==born['artifacts']
    for p,d in audit['files'].items():assert e.sha(ROOT/p)==d,p
    named=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert named==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    from build_named_readiness import validate
    validate(named,assembly=STAGE/'content/Sanctuary.Content.dll')
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete'in l]
    unsupported=[l for l in log.splitlines()if ' WRN]'in l and 'unsupported'in l.lower()]
    assert all('"WardrobeSet"'in l for l in warnings)and all('Defiler link sharing refused unsupported context'in l for l in unsupported)
    c=configuration();assert tuple(len(c[k])for k in ('extra_families','extra_checks','extra_target_input_commands','extra_files','extra_pre_save_commands'))==(78,24,107,149,17)
    assert c['extra_early_reload_families']==('firestorm','player-item-proc-world')and c['extra_pre_save_commands'][-1]=='player-item-proc-world-save-check'
    ledger=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(ledger['skills'])==240 and not ledger['completedClasses']
    paths=['tools/record_druid_lifecycle_acceptance.py','tools/review_druid_input_coverage.py','tools/class_input_coverage.py','tools/reference/druid-current-input-coverage.json','docs/druid-lifecycle-acceptance-checkpoint.md','docs/druid-coverage-matrix.md','docs/class-skill-roadmap.md','docs/class-skill-status-overview.md','docs/main-consolidation-plan.md','README.md','docs/skill-effects.md','docs/skill-icons.md']
    files=[*(ROOT/p for p in paths),*c['extra_inputs'],ROOT/'tests/check_druid_item_form_retention.py',*e.stage_files(STAGE,FOCUS),STAGE/'accepted-assemblies-1.tar.gz',FOCUS/'accepted-assemblies-1.tar.gz',STAGE/'failed-native-1.tar.gz']
    connections=log.count('Client: 127.0.0.1: Connected.');assert connections==401
    result=dict(component='Current Druid lifecycle/input and shared combat integration',referenceVersion='3.3.93847',baselineCommit='bba3161419f0002e871357986b92af240c4bd3ed',nativeMarkers=1139,markers=dict(sorted(expected().items())),retainedPredecessorMarkers=684,fixtureFamilies=78,sharedChecks=24,ordinaryInputCommands=107,explicitDruidInputCases=audit['ordinaryInputCases'],foreignInputCases=audit['foreignInputCasesSeparatelyExcluded'],basicAttackControls=audit['basicAttackControlsSeparatelyExcluded'],legacyElementalSequences=27,heldArcticSequences=9,activeTalents=29,passiveTalents=1,connections=connections,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],productionCodeChanged=False,runtimeDataChanged=False,contentRebuilds=0,integrationRebuilds=2,nativeRebuilds=0,nativeRun=2,sourceSnapshot=3,focusedMarkers=24,changedTestSources=changed,frozenNativeFailures=1,saveDeletionWarnings=warnings,intentionalScalarRefusalWarnings=len(unsupported),classCertified=False,goalComplete=False,installed=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Retains684Druid elemental/pet Health Link and original lifecycle results,adding current form item-proc/setup/weapon-poison,basic contact,native shield,cold-corpse and natural Armageddon scopes. All29active routes have explicit-class,legacy elemental or held Arctic evidence;Lycanthropy remains a separate passive consumer. Existing birth snapshots/difficulty ratio/parking,source pet identities,forms/charges/infections/stream/weather saves remain attributable to their original fixtures. Five test-only saved item/form inspections reuse the established hold after frozen ordinary expiry;captured duration/resources and natural-expiry controls retain. Production/data/native remain unchanged;Integration2 only. No source compiler or newer D2R access. Exact modern executable/event/RNG/stat/geometry/PvP/full item callback and graphical-client limits remain;effects/audio/icons remain separate.')
    e.write('druid-lifecycle-acceptance-native-evidence.json',result,files,check)
    print(f'Druid:1139exact results/29active+Lycanthropy/{connections}connections/two reloads;accepted build reused.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
