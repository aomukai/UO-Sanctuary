#!/usr/bin/env python3
"""Bind released-state Fury entry while preserving the full held-input gap."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/blade-fury-reentry'
TARGET=ROOT/'.local/class-skills/blade-fury-reentry-targeted'
PARENT='494ef5a779c54baf500946ad8325d796c1aeba93'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-2.json',STAGE/'birth-inputs.json')
    inventory.STAGE=TARGET;focused=inventory.validate_inventory(TARGET/'birth-inputs.json');inventory.STAGE=STAGE
    assert tested==focused
    sys.path.insert(0,str(ROOT/'tests'))
    from check_blade_fury_reentry import configuration
    c=configuration();assert (len(c['extra_families']),len(c['extra_checks']),len(c['extra_target_input_commands']))==(24,14,12)
    log=(STAGE/'birth-native-checks.log').read_text();small=(TARGET/'birth-native-checks.log').read_text()
    for text,count in [(log,228),(small,39)]:
        assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==count
        assert text.count('Writing world save snapshot done')==text.count('Shutting down')==3
        for name in ('blade-fury-mode','blade-fury-reentry'):
            assert text.count(name+'-live-check: passed')==text.count(name+'-live-result: passed')==3
        assert text.count('blade-fury-reentry-check: passed')==1 and text.count('blade-fury-reentry-reload-check: passed')==2
    for name in c['extra_families']:assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2,name
    for name in c['extra_checks']:assert log.count(name+': passed')==3,name
    for name in c['extra_target_input_commands']:
        assert log.count(name+'-check: passed')==log.count(name+'-result: passed')==3,name
        assert log.count(name+(': acknowledged' if name.endswith('left') else ': windup'))==3,name
    for stage in (STAGE,TARGET):
        saved=json.loads((stage/'blade-fury-reentry-fixtures.json').read_text());assert saved['Owner']>0 and saved['Mana']==1151
        flight=json.loads((stage/'blade-fury-missile-fixture.json').read_text());f=flight['Flight']
        assert f['Owner']==flight['Owner'] and f['Ability']==207 and f['Frame']==2
        assert f['MechanicalProfile']=='source-player-missile-plus-current-fury-v1'
    proof=json.loads((ROOT/'tools/reference/blade-fury-reentry-source-evidence.json').read_text())
    assert proof['contexts']==576 and proof['frames']==13824 and proof['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    assert sha(ROOT/proof['fixture']['path'])==proof['fixture']['sha256']
    assert sha(ROOT/'.local/class-skills/blade-fury-reentry-source-probe.cpp')==proof['probeSha256']
    assert sha(ROOT/'.local/class-skills/blade-fury-sequence-source-probe.cpp')==proof['baseCompositionSha256']
    assert (ROOT/'.local/class-skills/blade-fury-reentry-sanitizer.log').read_bytes()==b''
    for path,digest in proof['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/path)==digest
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
    assert audit['sourceInputs']=={p:h for p,h in tested['sourceInputs'].items() if p.endswith('.cs')}
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',916),('first-loop-affix-audit.json',21)]:
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-2.log')).read_text();assert all(w in text for w in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for word in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.','Current sentry monster/models:601 fields','Local sentry animation:11records pinned','Owned missile guide:5fields pinned'):assert word in text
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses']
    assert sum(s['implementation']=='handler-present' for s in status['skills'])==191
    assert sum(s['implementation']=='handler-present' and s['characterClass']=='Assassin' for s in status['skills'])==22
    row=next(s for s in status['skills'] if s['talentId']=='assassin.bladefury');assert row['implementation']=='pending' and row['verification']=='partial-native-coverage' and not row['complete']
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    prior=json.loads((ROOT/'.local/class-skills/wake-fire-automatic/birth-inputs.json').read_text())
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][path]==tested['artifacts'][path]
    changed={p for p,h in tested['sourceInputs'].items() if p.startswith(('src/','shared/')) and prior['sourceInputs'].get(p)!=h}
    assert changed=={'src/Sanctuary.Content/BladeFuryModes.cs','src/Sanctuary.Content/BladeFurySequence.cs'}
    assert {p for p,h in tested['copiedData'].items() if prior['copiedData'].get(p)!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    old=json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    assert [a['talentId'] for a,b in zip(old['skills'],status['skills']) if a!=b]==['assassin.bladefury']
    diagnostic=TARGET/'diagnostic-result-order-1';captured=json.loads((diagnostic/'birth-inputs.json').read_text())
    for kind in ('content','integration'):assert sha(diagnostic/kind/('Sanctuary.'+kind.title()+'.dll'))==captured['artifacts']['Assemblies/Sanctuary.'+kind.title()+'.dll']
    assert sha(diagnostic/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
    assert sha(diagnostic/'BladeFuryReentryChecks.cs')==captured['sourceInputs']['tests/Sanctuary.Integration/BladeFuryReentryChecks.cs']
    text=(diagnostic/'birth-native-checks.log').read_text();assert text.index('blade-fury-reentry-live-result: passed')<text.index('blade-fury-reentry-live-check: passed')
    files=[Path(__file__).resolve(),ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/probe_blade_fury_reentry.py',ROOT/'tools/reference/blade-fury-reentry-source-evidence.json',ROOT/'tests/check_blade_fury_reentry.py',
        *(ROOT/'docs'/n for n in ('blade-fury-reentry-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-2.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','content-build-2.log','integration-build-2.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','python-tests.log','catalogue-checks.log','blade-fury-reentry-fixtures.json','blade-fury-missile-fixture.json')),
        *(TARGET/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-2.log','blade-fury-reentry-fixtures.json','blade-fury-missile-fixture.json')),
        *(ROOT/'.local/class-skills'/n for n in ('blade-fury-reentry-oracles.log','blade-fury-reentry-source.log','blade-fury-reentry-source-probe.cpp','blade-fury-reentry-source-probe','blade-fury-reentry-sanitizer.log'))]
    files.extend(c['extra_inputs']);files.extend(c['extra_files'].values());files.extend(f for f in diagnostic.rglob('*') if f.is_file())
    data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Prepared released-state fresh Fury entry verified; raw held-input/current fresh-rewind gates remain pending; no learned handler added.',
        verified=dict(fullMarkers=228,targetedMarkers=39,retainedMarkers=198,bootsPerRun=3,reloadsPerRun=2,savesPerRun=3,reentrySourceContexts=576,reentrySourceFrames=13824,wireRequestsRetained=36,originalSavedReentryMana256=1151,auditMarkers=2,pythonTests=71,handlerCount=191,assassinHandlers=22,unchangedInstalledArtifacts=106),
        limits=['Only inactive released-state ready/equal-deadline fresh entry is supported. Active-bit/future-shot fresh rewinds can address before the unguarded old sequence array; modern equivalents are not invented.',
            'Complete source mode entry uses declared accepted intents, per-owner FIFO and normal SQ23 services. It does not establish current client heartbeat, outer accepted input, held release tail or graphical sequence acceptance.',
            'The same original character/mana survives two real reloads; transient modes/intents are not serialized. Existing independent weapon-flight inspection clocks retain their explicit hold/rearm policy.',
            'Current AR/mastery/slow rounding, full item/proc/owner/PvP and native source geometry/event/seed/client remain acceptance gaps. No deployment/save reset/full class/final effects/icons.'],
        tested=tested,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/blade-fury-reentry-native-evidence.json';encoded=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Fury released-state entry:228full/39focused markers;576source contexts/13824frames;3boots/2reloads each;original1151FP character;191handlers;106installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
