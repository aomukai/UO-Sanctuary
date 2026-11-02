#!/usr/bin/env python3
"""Bind remaining-aura snapshot/atomic restore acceptance and the preserved regressions."""
import argparse,hashlib,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_might_aura import passed
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/paladin-aura-persistence';sha=inventory.sha
PARENT='edc579b8961e3760509dfe2e9c36fc61aae1d44a'
CHANGED={'src/Sanctuary.Content/'+n for n in ('PaladinHolyFreeze.cs','PaladinConviction.cs','SourceKnockbacks.cs')}
def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-1.json',STAGE/'birth-inputs.json')
    log,counts=passed(STAGE/'birth-native-checks.log',1232);assert 'pass across3boots/2reloads' in (STAGE/'full-run-1.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'));from check_paladin_aura_persistence import configuration
    c=configuration(True);assert (len(c['extra_families']),len(c['extra_checks']),len(c['extra_target_input_commands']))==(79,23,126)
    for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
    for name in c['extra_checks']:assert counts[name]==3,name
    for name in c['extra_target_input_commands']:
        assert counts[name+'-check']==counts[name+'-result']==3,name
        assert log.count(' '+name+(': acknowledged' if name in c['extra_instant_input_commands'] else ': windup'))==3,name
    assert counts['holy-freeze-result']==1 and counts['holy-freeze-reload-result']==2
    assert log.count('Aura persistence:Holy Freeze/Conviction/knockback malformed tails,duplicate/null heads,null arrays refuse atomically;')==3
    focus=STAGE/'focused-accepted-2';fi=json.loads((focus/'birth-inputs.json').read_text());_,fc=passed(focus/'birth-native-checks.log',577)
    assert fi['artifacts']==tested['artifacts'] and fi['copiedData']==tested['copiedData'] and fi['sourceInputs'].items()<=tested['sourceInputs'].items()
    for part in ('content','integration'):
        name=part+'/Sanctuary.'+part.title()+'.dll';assert sha(focus/name)==sha(STAGE/name)
        s=(STAGE/(part+'-build-1.log')).read_text();assert all(x in s for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    parent=ROOT/'tools/reference/offensive-aura-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)));pc=json.loads(parent.read_text())['markers'];assert sum(pc.values())==568 and all(fc[k]==v for k,v in pc.items())
    oldfull=ROOT/'tools/reference/might-aura-native-evidence.json';assert oldfull.read_bytes()==historical(str(oldfull.relative_to(ROOT)));mc=json.loads(oldfull.read_text())['markers'];assert sum(mc.values())==1121 and all(counts[k]==v for k,v in mc.items())
    prior=json.loads((ROOT/'.local/class-skills/offensive-aura/birth-inputs.json').read_text());changed=set()
    for path,h in tested['sourceInputs'].items():
        if path.startswith(('src/','shared/')):
            assert hashlib.sha256(historical(path)).hexdigest()==prior['sourceInputs'][path]
            if prior['sourceInputs'][path]!=h:changed.add(path)
    assert changed==CHANGED and tested['copiedData']==prior['copiedData']
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][path]==prior['artifacts'][path]
    negative=STAGE/'negative-restore-1';ni=json.loads((negative/'birth-inputs.json').read_text());s=(negative/'birth-native-checks.log').read_text()
    assert 'paladin-aura-persistence-check: failed' in s and 'Holy Freeze atomic malformed-tail' in s
    assert sha(negative/'content/Sanctuary.Content.dll')==prior['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
    assert sha(negative/'PaladinAuraPersistenceChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/PaladinAuraPersistenceChecks.cs']==sha(ROOT/'tests/Sanctuary.Integration/PaladinAuraPersistenceChecks.cs')
    for path in CHANGED:assert (negative/Path(path).name).read_bytes()==historical(path)
    diagnostic=STAGE/'diagnostic-holy-freeze-manifest-1';di=json.loads((diagnostic/'birth-inputs.json').read_text());s=(diagnostic/'focused-run-1.log').read_text()
    assert 'FileNotFoundError' in s and 'holy-freeze-fixtures.json' in s and 'holy-freeze-result: passed' in (diagnostic/'birth-native-checks.log').read_text()
    old=(diagnostic/'check_paladin_aura_persistence.py').read_text();line="    c['extra_manifest_names']['holy-freeze']='holy-freeze-checks.json'\n";assert (ROOT/'tests/check_paladin_aura_persistence.py').read_text().replace(line,'')==old
    assert sha(diagnostic/'check_paladin_aura_persistence.py')==di['sourceInputs']['tests/check_paladin_aura_persistence.py']
    for part in ('content','integration'):
        name=part+'/Sanctuary.'+part.title()+'.dll';assert sha(diagnostic/name)==sha(STAGE/name)
    for name in ('holy-freeze-engine-evidence.json','paladin-resistance-evidence.json','paladin-travel-evidence.json','paladin-element-evidence.json','paladin-thorns-engine-evidence.json','paladin-conviction-engine-evidence.json','sanctuary-engine-evidence.json','sanctuary-native-evidence.json'):
        p=ROOT/'tools/reference'/name;assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
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
    docs=['paladin-remaining-auras-preflight.md','paladin-aura-persistence-checkpoint.md','barbarian-class-roadmap-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md']
    files=[Path(__file__).resolve(),ROOT/'tests/check_paladin_aura_persistence.py',parent,oldfull,*(ROOT/'docs'/n for n in docs),
        *(STAGE/n for n in ('birth-inputs.json','birth-native-checks.log','pre-build-1.json','full-run-1.log','content-build-1.log','integration-build-1.log','holy-freeze-checks.json','paladin-conviction-checks.json','sanctuary-aura-checks.json','audit-inputs.json','audit-native-checks.log','audit-run-1.log','python-tests.log','catalogue-checks.log','installed-unchanged-during-full.json')),
        *(focus/n for n in ('birth-inputs.json','birth-native-checks.log','focused-run-2.log','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
        *(negative/n for n in ('birth-inputs.json','birth-native-checks.log','PaladinAuraPersistenceChecks.cs','PaladinHolyFreeze.cs','PaladinConviction.cs','SourceKnockbacks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
        *(diagnostic/n for n in ('birth-inputs.json','birth-native-checks.log','focused-run-1.log','check_paladin_aura_persistence.py','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
        *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Holy Freeze,Conviction and shared source-knockback save safety;remaining Paladin aura review',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(fullNativeMarkers=1232,focusedNativeMarkers=577,retainedPreviousFullMarkers=1121,retainedPreviousFocusedMarkers=568,boots=3,reloads=2,saves=3,retainedServerWireActions=378,auditMarkers=4,pythonTests=71,unchangedInstalledArtifacts=106),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Three existing profiles capture immutable record arrays on the game loop before worker serialization. Whole candidate dictionaries validate before commit;malformed/duplicate/null payloads preserve previous state. Version0/field meanings,independent shatter and optional PauseFrozen stay intact. No skill formula,selection,damage,slow,immunity,motion or timing behavior changed.',
            'Parent DLL fails the preserved Holy Freeze malformed-tail atomicity control;candidate passes all three controllers on actual mapped states and JSON roundtrips. First focused manifest-copy failure is archived;the explicit correct filename changes only the runner,and accepted focused2/full1binaries and pre-build1source/data are identical.',
            'Dedicated Holy Freeze and retained aura/resistance/travel/elemental/Thorns/Sanctuary/Conviction/Charge cohorts verify their scoped mechanics and original reloads,including independent shatter/corpse loot and knockback trajectory. Held/rearmed inspection clocks and native conditioning remain explicit;no uninterrupted short-clock claim. Full1232retains1121old full markers and378actual input actions;focused577retains568old focused markers. Fresh4audits/71Python/573named/240catalogue/generators pass;106installed artifacts unchanged.'],
        remaining=['Modern Sanctuary broad bypass/low-mana supplier,Fanaticism zero-mana self,HolyFreeze lethal-pulse/death/other-modes,full proximity/owner/item/native-world/client gaps remain documented. User-suggested reference-site prose is supplementary and does not resolve missing executable contracts.',
            'Final effects/audio and talent/right/quick icons remain separate placeholders. No class completion,deployment or save reset. Continue the class roadmap through unaffected mechanics without treating a historical superseded gate as current state.'])
    output=ROOT/'tools/reference/paladin-aura-persistence-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Paladin aura persistence:1232full/577focused;3atomic controllers;parent failure/manifest diagnostic;original reloads/378retained inputs;4audits/71Python/106installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
