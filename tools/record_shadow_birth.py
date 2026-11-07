#!/usr/bin/env python3
"""Bind prepared Shadow birth and the unchanged learned/gear regressions."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-birth';WIDE=BASE/'shadow-birth-retained';ISO=BASE/'shadow-birth-isolate';SHARED=BASE/'shadow-birth-shared'
PARENT='84ca34872e60905ba4519d88093ddb6e2f4f81e7'
def record(check=False):
    born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-2.json');sys.path.insert(0,str(ROOT/'tests'))
    from check_shadow_birth import configuration
    parent=e.load(ROOT/'tools/reference/valkyrie-cast-native-evidence.json');extra=Counter({'shadow-birth-check':1,'shadow-birth-reload-check':2})
    for previous,stage,count in [(BASE/'valkyrie-cast',STAGE,138),(BASE/'valkyrie-cast-retained',WIDE,446)]:
        p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))]
        e.cohort(stage,markers(p)+extra,count)
    e.cohort(ISO,expected(configuration(isolate=True)),45);e.cohort(SHARED,expected(configuration(shared=True)),255)
    phrase='Shadow birth:3456complete current-helper traces,10pinned skill/rank plans,368shared stat names;explicit owner/pet contexts,allocation failure and zero writes;actual gear ranks and immutable prepared numeric snapshots through two reloads;no live Shadow actor/cast or dispatch certificate'
    for stage in [STAGE,WIDE,ISO,SHARED]:
        assert 'pass across3boots/2reloads' in (stage/'native-run-1.log').read_text()
        snapshot(STAGE/'source-snapshot-2.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json')
        assert other['artifacts']==born['artifacts'] and other['copiedData']==born['copiedData']
        for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
        if stage==SHARED:continue
        assert (stage/'birth-native-checks.log').read_text().count(phrase)==3
        saved=e.load(stage/'shadow-birth-fixtures.json');assert saved['Generation']==2 and saved['Owner']>0 and saved['Helm']>0
        plans=[p['Writes']for p in saved['Plans']];assert len(plans)==2 and [p['Rank']for p in plans]==[10,21]
        assert [len(p['Auras'])for p in plans]==[4,3]
        assert [p['MaximumLifeWrite256']for p in plans]==[601600,1024000]
        for p in plans:
            assert p['SpawnMaximumLife256']==256000 and p['MaximumLifeWrite256']==p['CurrentLifeWrite256'] and p['ListAllocated'] and p['Modifier']==42
            assert len(p['Passives'])==9 and [s['Value']for s in p['Passives'][4:]]==[11,22,33,44,55]
    for n in ['content-build-2.log','integration-build-1.log']:assert all(s in (STAGE/n).read_text()for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==10 and all(c['exitCode']==0 for c in commands)
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n'in (STAGE/'python-tests.log').read_text()
    assert '3456 complete offline contexts'in (STAGE/'source-check.log').read_text()
    proof=ROOT/'tools/reference/shadow-birth-source-evidence.json';source=e.load(proof)
    assert source['sourceContexts']==3456 and source['ownedCompiledStatSlots']==8580 and source['preparedOnly'] and not source['handlerEnabled'] and not source['dispatchIdentityVerified']
    for p,h in source['inputs'].items():assert e.sha(ROOT/p)==h,p
    bindingPath=ROOT/'tools/reference/current-skill-dispatch-source-evidence.json';binding=e.load(bindingPath)
    assert binding['skillDoTableIdentityVerified'] and binding['shadowEntryLinked'] and not binding['fullExecutorVerified']
    assert binding['dispatchSliceContexts']==1440 and binding['ownedFieldControls']==1287
    for p,h in binding['inputs'].items():assert e.sha(ROOT/p)==h,p
    frozen=STAGE/'frozen-c2-i1'
    for name,h in e.load(frozen/'hashes.json').items():assert e.sha(frozen/name)==h,name
    for kind in ['Content','Integration']:assert e.sha(frozen/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
    client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==203 and not status['completedClasses']
    files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]
    files+=list(frozen.iterdir())+[Path(__file__).resolve(),ROOT/'tools/review_shadow_birth.py',ROOT/'tests/check_shadow_birth.py',proof]
    files+=[bindingPath,ROOT/'tools/record_current_skill_dispatch.py',ROOT/'tools/probe_current_skill_dispatch.py',ROOT/'tools/probe_shadow_skill_gateway.py',ROOT/'docs/current-skill-dispatch-checkpoint.md']
    files+=[ROOT/'docs'/n for n in ['shadow-birth-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,preparedOnly=True,learnedShadows=False,skillDoTableIdentityVerified=True,fullExecutorVerified=False,dispatchSliceContexts=1440,ownedDispatchFieldControls=1287,handlers=203,completedClasses=[],contentBuild=2,integrationBuild=1,sourceSnapshot=2,focusedMarkers=138,retainedMarkers=446,isolatedMarkers=45,sharedMarkers=255,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,sourceContexts=3456,pinnedSkillRankPlans=10,sharedStatNames=368,pythonTests=71,verificationCommands=10,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=['Exact source-helper call traces exercise existing percentage arithmetic,rank1,six indexed aura/fourteen passive slots,owner/pet formula contexts,allocation failure,zero writes and modifier gate.','Pinned current Warrior/Master formulas and explicit supplied owner stats produce prepared requested writes. Real Tarnhelm ranks and immutable numeric plans survive actual reloads;new plans observe changed gear/stats. This is not pet persistence or active sunder.','The368row canonical equipment-stat catalogue supplies name lookup/count. Existing learned Valkyrie,original equipment/events/resources/RNG,other retained classes and shared input/missile paths retain all parent checks.'],limits=['Current table/Shadow slot linkage has separate1440source-slice/1287owned-field proof;full executor,initialized Shadow actor/resources/original graph/equipment/events,AI105/106,NPC skills/combat,casting/payment/input and full lifecycle/pet saves remain. No generic NPC replacement or learned handler is enabled.','Formula and stat/allocation/modifier service bodies remain separately scoped. Shared nonzero sunder is not activated;prepared source overflow requests are retained rather than silently clamped. Effects/body/equipment/audio and talent/hotkey icons remain separate placeholders. No class completion or deployment.'])
    e.write('shadow-birth-native-evidence.json',result,files,check)
    print('Prepared Shadow birth:3456source traces;138focused/446retained/45isolated/255shared;203handlers,no learned Shadow/deployment.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
