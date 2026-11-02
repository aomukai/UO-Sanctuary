#!/usr/bin/env python3
"""Bind resource callback,delayed-input and existing original-state reload evidence."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_might_aura import passed
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/resource-aura';sha=inventory.sha
PARENT='5f3d7ccd8a7178bb265984543ae36d801ceac73b'
def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-2.json',STAGE/'birth-inputs.json')
    log,counts=passed(STAGE/'birth-native-checks.log',541);assert 'pass across3boots/2reloads' in (STAGE/'focused-run-2.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'));from check_resource_aura import configuration
    c=configuration();assert (len(c['extra_families']),len(c['extra_checks']),len(c['extra_target_input_commands']))==(28,12,59)
    for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
    for name in c['extra_checks']:assert counts[name]==3,name
    for name in c['extra_target_input_commands']:
        assert counts[name+'-check']==counts[name+'-result']==3,name
        assert log.count(' '+name+(': acknowledged' if name in c['extra_instant_input_commands'] else ': windup'))==3,name
    assert counts['resource-aura-result']==3 and log.count('Resource auras:source frame/curability goldens;forced-due strong/refused-weak/equal/full/one-FP Prayer')==3
    prior=ROOT/'.local/class-skills/blessed-aim-aura';pi=json.loads((prior/'birth-inputs.json').read_text())
    for path,h in tested['sourceInputs'].items():
        if path.startswith(('src/','shared/')):assert pi['sourceInputs'][path]==h,path
    assert tested['copiedData']==pi['copiedData']
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][path]==pi['artifacts'][path]
    retained=dict(counts)
    for name in ['resource-aura-check','resource-aura-result',*(n+s for n in c['extra_target_input_commands'] if n.startswith('resource-aura-') for s in ('-check','-result'))]:retained.pop(name)
    assert retained==passed(prior/'birth-native-checks.log',499)[1]
    saved=json.loads((STAGE/'paladin-resource-checks.json').read_text());assert saved['HealthFraction']==128 and saved['ManaFraction']==17
    assert {e['Ability'] for e in saved['State']['Emitters']}=={92,97,99}
    prayer=next(e for e in saved['State']['Emitters'] if e['Ability']==92);assert prayer['SuppressBaseMana'] and prayer['FlatManaNumerator']==123
    references=['blessed-aim-aura-native-evidence.json','paladin-resource-engine-evidence.json','paladin-aura-user-observations.json','might-callback-source-evidence.json']
    for name in references:assert (ROOT/'tools/reference'/name).read_bytes()==historical('tools/reference/'+name)
    source=ROOT/'tools/reference/resource-aura-callback-source-evidence.json';proof=json.loads(source.read_text());assert proof['cases']==1920 and len(proof['routineHashes'])==3 and proof['sanitizers']=='ASan/UBSan clean'
    for path,h in (proof['sourceHashes']|proof['currentTables']|proof['artifacts']).items():assert sha(ROOT/path)==h,path
    tables=ROOT/'tools/reference/d2r-named-equipment/json';skills=json.loads((tables/'skills.json').read_text());states=json.loads((tables/'states.json').read_text());stats=json.loads((tables/'itemstatcost.json').read_text())
    for id,name,state,filter in [('99','Prayer',34,73731),('109','Cleansing',45,73731),('120','Meditation',48,73729)]:
        r=skills[id];assert (r['skill'],r['srvdofunc'],r['perdelay'],r.get('immediate',0),r['aurafilter'],states[r['aurastate']]['*ID'])==(name,65,50,0,filter,state)
    assert (skills['99']['mana'],skills['99']['lvlmana'],skills['99']['manashift'],skills['99']['minmana'])==(16,3,4,1)
    assert (stats['hitpoints']['*ID'],stats['hitpoints']['direct'],stats['hitpoints']['maxstat'],stats['item_poisonlengthresist']['*ID'],stats['manarecoverybonus']['*ID'])==(6,1,'maxhp',110,27)
    for part in ('content','integration'):
        for build in (1,2):
            s=(STAGE/(part+f'-build-{build}.log')).read_text();assert all(x in s for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    negative=STAGE/'diagnostic-input-release-1';ni=json.loads((negative/'birth-inputs.json').read_text());s=(negative/'birth-native-checks.log').read_text()
    assert 'resource-aura-prayer-right-result: failed' in s and 'Aura input timeout' in s
    old=(negative/'MightAuraChecks.cs').read_text();new=(ROOT/'tests/Sanctuary.Integration/MightAuraChecks.cs').read_text()
    assert old.replace('bool received=right?action.Sequence==1:SkillBindings.Instance.Get(p).Right==ability;','bool received=acknowledged||(right?action.Sequence==1:SkillBindings.Instance.Get(p).Right==ability);')==new
    assert sha(negative/'MightAuraChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/MightAuraChecks.cs']
    assert sha(negative/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
    a=json.loads((STAGE/'audit-inputs.json').read_text());assert a['artifacts']==tested['artifacts'] and a['copiedData']==tested['copiedData']
    s=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in s and ' ERR]' not in s and sum(markers(STAGE/'audit-native-checks.log').values())==4
    for name,count in [('named-item-runtime-audit.json',911),('first-loop-affix-audit.json',21)]:
        n=json.loads((STAGE/name).read_text());assert n==json.loads((ROOT/'tools/reference'/name).read_text()) and len(n['InputHashes'])==count and n['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,h in n['InputHashes'].items():assert sha(ROOT/path)==h,path
        o=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in n.items() if k not in ignore}=={k:v for k,v in o.items() if k not in ignore}
    s=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in s and '\nOK\n' in s and 'FAILED' not in s
    s=(STAGE/'catalogue-checks.log').read_text()
    for text in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert text in s
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==h,path
    docs=['paladin-resource-aura-preflight.md','paladin-resource-aura-checkpoint.md','paladin-offensive-aura-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md']
    files=[Path(__file__).resolve(),ROOT/'tests/check_resource_aura.py',source,*(ROOT/path for path in proof['artifacts']),*(ROOT/'tools/reference'/n for n in references),*(ROOT/'docs'/n for n in docs),
        *(STAGE/n for n in ('birth-inputs.json','birth-native-checks.log','pre-build-2.json','focused-run-2.log','content-build-2.log','integration-build-2.log','paladin-resource-checks.json','audit-inputs.json','audit-native-checks.log','audit-run-2.log','python-tests.log','catalogue-checks.log')),
        *(negative/n for n in ('birth-inputs.json','birth-native-checks.log','MightAuraChecks.cs','ResourceAuraChecks.cs','check_resource_aura.py','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
        *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Prayer/Cleansing/Meditation direct callbacks and delayed actual inputs',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(focusedNativeMarkers=541,retainedFocusedMarkers=499,previousFullMarkers=1121,sourceCallbackCases=1920,boots=3,reloads=2,saves=3,newServerWireActions=18,auditMarkers=4,pythonTests=71,unchangedInstalledArtifacts=106),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Complete unchanged aura/list/modifier bodies now execute1920bounded direct HP,Cleansing deadline/event and Meditation recovery cases with original enums and controlled services,ASan/UBSan clean. Formula/world/event execution and modern executable remain separate;Might non-direct proof is not relabelled as healing proof.',
            'Native forced-due supplier controls establish strong/refused-weak/equal/full/one-FP Prayer healing,original creator and useful-only once fractional payment. Six actual right/quick modes per boot separately establish delayed natural first healing,zero selection cost,no cast reservation and correct Prayer payment/suppression. The shared immediate input paths retain499prior markers.',
            'Existing resource cohort retains actual delayed cadence,status/rate/filter/synergy/flat/native recovery and original emitter/lease/status/fractional wounds/mana through two real reloads. Forced due and held/rearmed inspection clocks remain explicit;no uninterrupted short-clock claim. Source-poison/curable/noncurable services remain bounded to supported adapters.',
            'First delayed fixture incorrectly rechecked the initial sequence after release;archived diagnostic and one-line acknowledgement latch correction preserve identical production DLLs. Production sources/data remain unchanged from Blessed Aim;prior1121full markers are separate evidence,not a new full run. Fresh4audits/71Python/573named/240catalogue/generators pass;106installed artifacts unchanged.'],
        remaining=['Absolute D2R max-mana recovery/native first-tick retiming,Blood Mana,unrelated UO poison/curse/shrine adapters and modern full stat/event/filter/owner/item/client parity remain unresolved. Preserve approved native regeneration baseline.',
            'Source probe supplies single-receiver stat/list/allocation/state/curability/clock/event services and payloads;it does not execute complete do65 enumeration/payment/formulas or a modern world. Final aura/effect/audio and talent/right/quick artwork remain separate placeholders;no class completion,deployment or save reset.'])
    output=ROOT/'tools/reference/resource-aura-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Resource auras:541focused/499retained,18delayed instant inputs,1920complete callbacks,2qualified resource reloads;4audits/71Python/106installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
