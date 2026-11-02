#!/usr/bin/env python3
"""Bind offensive multi-stat callbacks,real gear packets and retained recovery/timing."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_might_aura import passed
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/offensive-aura';sha=inventory.sha
PARENT='a143d3beb6e4929d5b5b899995197ca4ea2ede88'
def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-2.json',STAGE/'birth-inputs.json')
    log,counts=passed(STAGE/'birth-native-checks.log',568);assert 'pass across3boots/2reloads' in (STAGE/'focused-run-2.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'));from check_offensive_aura import configuration
    c=configuration();assert (len(c['extra_families']),len(c['extra_checks']),len(c['extra_target_input_commands']))==(28,13,63)
    for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
    for name in c['extra_checks']:assert counts[name]==3,name
    for name in c['extra_target_input_commands']:
        assert counts[name+'-check']==counts[name+'-result']==3,name
        assert log.count(' '+name+(': acknowledged' if name in c['extra_instant_input_commands'] else ': windup'))==3,name
    assert log.count('Offensive auras:actual Tarnhelm rank2;Concentration75/chance20/Hammer37;Fanaticism18/45/33and provisional67positive-mana self;')==3
    prior=ROOT/'.local/class-skills/resource-aura';pi=json.loads((prior/'birth-inputs.json').read_text())
    for path,h in tested['sourceInputs'].items():
        if path.startswith(('src/','shared/')):assert pi['sourceInputs'][path]==h,path
    assert tested['copiedData']==pi['copiedData']
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][path]==pi['artifacts'][path]
    retained=dict(counts)
    for name in ['offensive-aura-check',*(n+s for n in c['extra_target_input_commands'] if n.startswith('offensive-aura-') for s in ('-check','-result'))]:retained.pop(name)
    assert retained==passed(prior/'birth-native-checks.log',541)[1]
    saved=json.loads((STAGE/'paladin-offense-checks.json').read_text());leases=[l for l in saved['State']['Leases'] if l['Target']==saved['Ally']]
    assert any(l['Rank']==1 and l['Percent']==60 and l['UninterruptChance']==20 for l in leases)
    assert any(l['Rank']==1 and l['Percent']==14 and l['AttackPercent']==40 and l['DamagePercent']==25 for l in leases)
    references=['resource-aura-native-evidence.json','paladin-offense-evidence.json','might-callback-source-evidence.json']
    for name in references:assert (ROOT/'tools/reference'/name).read_bytes()==historical('tools/reference/'+name)
    source=ROOT/'tools/reference/offensive-aura-callback-source-evidence.json';proof=json.loads(source.read_text());assert proof['cases']==120 and len(proof['routineHashes'])==3 and proof['sanitizers']=='ASan/UBSan clean'
    for path,h in (proof['sourceHashes']|proof['currentTables']|proof['artifacts']).items():assert sha(ROOT/path)==h,path
    tables=ROOT/'tools/reference/d2r-named-equipment/json';skills=json.loads((tables/'skills.json').read_text());states=json.loads((tables/'states.json').read_text());stats=json.loads((tables/'itemstatcost.json').read_text())
    for id,name,state in [('113','Concentration',42),('122','Fanaticism',49)]:
        r=skills[id];assert (r['skill'],r['srvdofunc'],r['perdelay'],r['immediate'],r['mana'],r['aurafilter'],states[r['aurastate']]['*ID'])==(name,65,50,1,0,73731,state)
    assert (skills['122']['Param5'],skills['122']['Param6'],skills['122']['aurastatcalc3'],skills['122']['passivecalc1'])==(50,17,'ln56/2','ln56')
    assert [stats[n]['*ID'] for n in ('damagepercent','skill_concentration','attackrate','other_animrate','item_tohit_percent')]==[25,164,68,69,119]
    for part in ('content','integration'):
        for build in (1,2):
            s=(STAGE/(part+f'-build-{build}.log')).read_text();assert all(x in s for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    diagnostic=STAGE/'diagnostic-full-half-rounding-1';di=json.loads((diagnostic/'birth-inputs.json').read_text());s=(diagnostic/'birth-native-checks.log').read_text()
    assert 'offensive-aura-check: failed' in s and 'Provisional positive-mana self/ally Fanaticism soft-rank packet' in s
    old=(diagnostic/'OffensiveAuraChecks.cs').read_text();assert old.replace('DamagePercent:66','DamagePercent:67').replace('provisional66positive-mana','provisional67positive-mana')==(ROOT/'tests/Sanctuary.Integration/OffensiveAuraChecks.cs').read_text()
    assert sha(diagnostic/'OffensiveAuraChecks.cs')==di['sourceInputs']['tests/Sanctuary.Integration/OffensiveAuraChecks.cs']
    assert sha(diagnostic/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
    oldproof=json.loads((diagnostic/'offensive-aura-callback-source-evidence.json').read_text());assert oldproof['routineHashes']==proof['routineHashes']
    for path,h in oldproof['artifacts'].items():
        p=diagnostic/'source-probe'/Path(path).name if path.startswith('.local/class-skills/offensive-aura-callback-source/') else diagnostic/Path(path).name if path=='tools/probe_offensive_aura_callback.py' else ROOT/path
        assert sha(p)==h,path
    before=json.loads((diagnostic/'source-probe/cases.json').read_text());after=json.loads((ROOT/'.local/class-skills/offensive-aura-callback-source/cases.json').read_text());assert any(a[11]==66 and b[11]==67 for a,b in zip(before,after,strict=True))
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
    docs=['paladin-offensive-aura-preflight.md','paladin-offensive-aura-checkpoint.md','paladin-remaining-auras-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md']
    files=[Path(__file__).resolve(),ROOT/'tests/check_offensive_aura.py',source,*(ROOT/path for path in proof['artifacts']),*(ROOT/'tools/reference'/n for n in references),*(ROOT/'docs'/n for n in docs),
        *(STAGE/n for n in ('birth-inputs.json','birth-native-checks.log','pre-build-2.json','focused-run-2.log','content-build-2.log','integration-build-2.log','paladin-offense-checks.json','audit-inputs.json','audit-native-checks.log','audit-run-2.log','python-tests.log','catalogue-checks.log')),
        *(diagnostic/n for n in ('birth-inputs.json','birth-native-checks.log','OffensiveAuraChecks.cs','check_offensive_aura.py','probe_offensive_aura_callback.py','offensive-aura-callback-source-evidence.json','source-probe/probe.cpp','source-probe/probe','source-probe/cases.json','source-probe/sanitizer.log','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
        *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Concentration/Fanaticism multi-stat callbacks and real skill-gear packets',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(focusedNativeMarkers=568,retainedFocusedMarkers=541,previousFullMarkers=1121,sourceCallbackCases=120,boots=3,reloads=2,saves=3,newServerWireActions=12,auditMarkers=4,pythonTests=71,unchangedInstalledArtifacts=106),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Complete unchanged callback/list/modifier bodies execute120supplied Concentration/Fanaticism multi-stat packets with cold consult,attackrate/other_animrate,independent AR/damage/chance and rank/creator behavior,ASan/UBSan clean. This does not execute complete do65or modern rate/world behavior.',
            'Actual Tarnhelm yields Concentration75/chance20/Hammer37 and Fanaticism18/45/33;legacy positive-mana self67versus zero-mana33is retained provisionally. Lower self rank leaves stronger recipient packets intact. Twelve new right/quick actions pass while all541prior markers retain resource/damage/IAS/recovery/original aura reload controls with explicit inspection clocks.',
            'Initial expectation66incorrectly doubled truncated allied33. Pinned50+17gives67full/33half. Native expectation and supplied source payloads are corrected with immutable-source function hashes unchanged;archived diagnostic and identical production DLL prove a test correction. Production sources/data remain unchanged from resource acceptance;old1121full markers are separate evidence,not a new full run. Four fresh audits/71Python/573named/240catalogue/generators pass;106installed artifacts unchanged.'],
        remaining=['Modern zero-mana self damage and equal-rank self-passive interaction remain unresolved,not approved Sanctuary deviations. User aura cost observation does not resolve them.',
            'Source probe supplies stat/list/state/allocation/clock/cold/rate/event and payload services. Complete source/modern rate/cold/mode/ownership/item-granted/filter/seed/client behavior remains bounded. Final separate overlays/audio and talent/right/quick artwork remain placeholders;no class completion,deployment or save reset.'])
    output=ROOT/'tools/reference/offensive-aura-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Offensive auras:568focused/541retained,12new instant inputs,120complete multi-stat callbacks;67full/33half correction;modern zero-mana self remains uncertain;4audits/71Python/106installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
