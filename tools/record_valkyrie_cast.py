#!/usr/bin/env python3
"""Bind learned Valkyrie transactions, real inputs and retained regressions."""
import argparse, json, sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected

ROOT=e.ROOT; BASE=ROOT/'.local/class-skills'; STAGE=BASE/'valkyrie-cast'
WIDE=BASE/'valkyrie-cast-retained'; ISO=BASE/'valkyrie-cast-isolate'; SHARED=BASE/'valkyrie-cast-shared'
PARENT='d2d42b0e397123f3bab331af75b84d007635789d'

def record(check=False):
    born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-8.json')
    built=e.load(STAGE/'source-snapshot-3.json')['inputs']
    tested=e.load(STAGE/'source-snapshot-8.json')['inputs']
    assert built['projectInputs']==tested['projectInputs'] and built['copiedData']==tested['copiedData']
    assert {p:h for p,h in built['sourceInputs'].items() if not p.startswith('tests/')}=={p:h for p,h in tested['sourceInputs'].items() if not p.startswith('tests/')}
    frozen=STAGE/'frozen-c3-i7'
    for name,h in e.load(frozen/'hashes.json').items(): assert e.sha(frozen/name)==h,name
    for kind in ['Content','Integration']:
        name=f'Sanctuary.{kind}.dll'
        assert e.sha(frozen/name)==born['artifacts']['Assemblies/'+name]
    assert e.sha(frozen/'source-snapshot-8.json')==e.sha(STAGE/'source-snapshot-8.json')
    sys.path.insert(0,str(ROOT/'tests'))
    from check_valkyrie_cast import configuration
    parent=e.load(ROOT/'tools/reference/valkyrie-buffs-native-evidence.json')
    extra=Counter({'valkyrie-cast-check':1,'valkyrie-cast-reload-check':2})
    for command in ['valkyrie-cast-right','valkyrie-cast-quick']:
        extra[command+'-check']=extra[command+'-result']=3
    for previous,stage,count in [(BASE/'valkyrie-buffs',STAGE,135),(BASE/'valkyrie-buffs-retained',WIDE,443)]:
        p=previous/'birth-native-checks.log'
        assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))]
        e.cohort(stage,markers(p)+extra,count)
    e.cohort(ISO,expected(configuration(isolate=True)),42)
    e.cohort(SHARED,expected(configuration(shared=True)),255)
    phrase='Learned Valkyrie:shared Amazon SC/right/quick admission,exact fractional once-only payment,candidate rollback/forward allocation and learned-only replacement;town parking and original items/resources/wounds/RNG/paid roster through two reloads,unpaid windup not replayed;current executable/search/item-only/sunder/client limits remain'
    for stage,run in [(STAGE,1),(WIDE,1),(ISO,8),(SHARED,1)]:
        assert 'pass across3boots/2reloads' in (stage/f'native-run-{run}.log').read_text()
        snapshot(STAGE/'source-snapshot-8.json',stage/'birth-inputs.json')
        other=e.load(stage/'birth-inputs.json')
        assert other['artifacts']==born['artifacts'] and other['copiedData']==born['copiedData']
        for p,h in other['sourceInputs'].items(): assert e.sha(ROOT/p)==h,p
        if stage==SHARED: continue
        assert (stage/'birth-native-checks.log').read_text().count(phrase)==3
        saved=e.load(stage/'valkyrie-cast-fixtures.json'); held=saved['Body']; body=json.loads(held['Text'])
        assert saved['Generation']==2 and body['LearnedCast'] is True
        assert body['Owner']==saved['Owner'] and held['Random']['Actor']==held['Body']
        assert held['Life']>0 and saved['Mana']>0 and len(held['Items'])==2
        assert body['Initialization'] is not None and body['EquipmentContactProfile']=='valkyrie-original-proc-contact-v1'
        assert body['GroundMotion']['StartupProfile']=='source-valkyrie-mode-entry-v1'
        assert json.loads(held['Resources'])['Actor']==held['Body']
    for n in ['content-build-3.log','integration-build-7.log']:
        assert all(s in (STAGE/n).read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    commands=e.load(STAGE/'verification-commands.json')
    assert len(commands)==9 and all(c['exitCode']==0 for c in commands)
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    source=ROOT/'tools/reference/valkyrie-cast-source-evidence.json'; proof=e.load(source)
    assert proof['sourceId']==32 and proof['firstThinkFrames']==20 and proof['sourceDispatchCases']==15 and proof['sourceSchedulerCases']==2072
    for p,h in proof['inputs'].items(): assert e.sha(ROOT/p)==h,p
    fingerprints=e.audits(STAGE,PARENT,born['artifacts'])
    protected=preservation(); assert protected==e.load(STAGE/'protection-check.json')
    client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json')
    assert len(client['patchHashes'])==41
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items(): assert e.sha(ROOT/p)==h,p
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert sum(s['implementation']=='handler-present' for s in status['skills'])==203 and not status['completedClasses']
    files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED) if not p.name.startswith('receipt-run-')]
    files+=list((STAGE/'frozen-c3-i7').iterdir())+[Path(__file__).resolve(),ROOT/'tests/check_valkyrie_cast.py',ROOT/'tools/review_valkyrie_cast.py',source]
    files+=[ROOT/'docs'/n for n in ['valkyrie-cast-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=203,amazonHandlers=29,completedClasses=[],learnedValkyrie=True,currentExecutableParity=False,sourceSnapshot=8,contentBuild=3,integrationBuild=7,focusedMarkers=135,retainedMarkers=443,isolatedMarkers=42,sharedMarkers=255,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,verificationCommands=9,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=['Trained Amazon right/quick ground casting uses shared SC timing and original initialized controller. Exact once-only fractional mana, candidate rollback/forward RNG, learned-only replacement and cancellation verified.','Town parking, original items/resources/wounds/RNG and paid roster persist through two actual reloads; unpaid windup is not replayed. Normal parked source-clock advancement is derived exactly rather than frozen.','Actual socket-created Valkyries deal natural source-controller damage with native notification/return packets. Test recipient uses the existing PreventHealing fixture setup; no forced hits, filtered loadouts or source RNG changes.'],limits=['Full current executable/client and exact D2 room-search chronology remain unverified; placement uses the existing Druid native adapter. Item-only/proc/foreign-class histories are outside the trained route.','Shared nonzero sunder and legacy single-item buff publishing audit remain separate. No class completion or deployment. Effects, body/equipment/audio and talent/hotkey icons remain placeholders.'])
    e.write('valkyrie-cast-native-evidence.json',result,files,check)
    print('Learned Valkyrie:135 focused/443 retained/42 isolated/255 shared;203 handlers/Amazon29;no deployment.')

if __name__=='__main__':
    p=argparse.ArgumentParser(); p.add_argument('--check',action='store_true'); record(p.parse_args().check)
