#!/usr/bin/env python3
"""Bind ordinary-NPC Shiver retaliation without certifying unresolved paths."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sorceress-shiver-armor'
BUILD=4
PARENT='805b18e'
sha=inventory.sha

def historical(name):
    return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)

def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
    counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(counts.values())==484 and ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_shiver_armor import configuration
    config=configuration(True)
    assert len(config['extra_families'])==37 and len(config['extra_checks'])==10
    for name in config['extra_families']:
        assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
    for name in config['extra_checks']:assert counts[name]==3,name
    assert len(config['extra_target_input_commands'])==42
    for name in config['extra_target_input_commands']:
        assert counts[name+'-check']==counts[name+'-result']==3,name
        assert log.count(' '+name+': windup')==3,name
    for message in ('Shiver Armor:765rank/hard goldens,7680complete-source roll/160native HP-return-cold comparisons',
                    'TK source/native:rolls=7680 deliveries=2560 deaths=864 pushes=848',
                    'Ordered monster release:1616native complete-source compositions',
                    'NPC return:60488complete-source policies/exact traces'):
        assert message in log,message
    saved=json.loads((STAGE/'shiver-armor-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Armor']['Rank']>0 and saved['Armor']['Ability']==39
    assert saved['Mana']>0 and saved['Life']>0 and 0<saved['TargetLife']<12800000
    focused=json.loads((STAGE/'diagnostic-4/birth-inputs.json').read_text())
    focused_log=(STAGE/'diagnostic-4/birth-native-checks.log').read_text()
    assert focused['artifacts']==tested['artifacts']
    assert sum(markers(STAGE/'diagnostic-4/birth-native-checks.log').values())==21
    assert focused_log.count('Shutting down')==focused_log.count('Writing world save snapshot done')==3
    assert ' ERR]' not in focused_log and ': failed' not in focused_log
    assert 'pass across3boots/2reloads' in (STAGE/'diagnostic-run-4.log').read_text()
    for path,digest in focused['sourceInputs'].items():assert sha(ROOT/path)==digest,path
    assert focused['copiedData']==tested['copiedData']
    for kind in ('content','integration'):
        text=(STAGE/f'{kind}-build-{BUILD}.log').read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        name=f'{kind}/Sanctuary.{"Content" if kind=="content" else "Integration"}.dll'
        assert sha(STAGE/name)==sha(STAGE/'focused-accepted-4'/name)
    prior=json.loads((ROOT/'.local/class-skills/sorceress-chilling-bolt/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]
    assert prior['copiedData'].keys()==tested['copiedData'].keys()
    assert {p for p in tested['copiedData'] if prior['copiedData'][p]!=tested['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    source=ROOT/'tools/reference/shiver-armor-source-evidence.json';proof=json.loads(source.read_text())
    assert proof['referenceVersion']=='3.3.93847' and proof['cases']==7680 and proof['sanitizers']=='ASan/UBSan clean'
    assert proof['currentRow']['*Id']==50 and len(proof['routineHashes'])==9
    for path,digest in proof['artifacts'].items():assert sha(ROOT/path)==digest,path
    for path,digest in proof['currentTable'].items():assert sha(ROOT/path)==digest,path
    for path,digest in proof['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/path)==digest,path
    preparation=json.loads((ROOT/'tools/reference/energy-shield-source-evidence.json').read_text())
    assert preparation['referenceVersion']=='3.3.93847' and preparation['cases']==25344 and preparation['guardCases']==8
    assert preparation['sanitizers']=='ASan/UBSan clean' and preparation['damageOnlyDifferences']==92
    for group in ('artifacts','currentTable'):
        for path,digest in preparation[group].items():assert sha(ROOT/path)==digest,path
    for path,digest in preparation['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/path)==digest,path
    parent=ROOT/'tools/reference/chilling-bolt-native-evidence.json'
    assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
    now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
    current=next(s for s in now['skills'] if s['talentId']=='sorceress.shiverarmor')
    old=next(s for s in before['skills'] if s['talentId']=='sorceress.shiverarmor')
    assert current['verification']==old['verification']=='partial-native-coverage'
    old['remaining']=current['remaining'];assert now==before and not now['completedClasses']
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text()
    fingerprints=[]
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        audit=json.loads((STAGE/name).read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest,path
        previous=json.loads(historical('tools/reference/'+name));ignore=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
        assert {k:v for k,v in audit.items() if k not in ignore}=={k:v for k,v in previous.items() if k not in ignore}
        fingerprints.append(len(audit['InputHashes']))
    assert fingerprints==[894,21]
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
    text=(STAGE/f'checks-{BUILD}.log').read_text()
    for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in text
    assert 'Traceback' not in text
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    files=[Path(__file__).resolve(),ROOT/'tests/check_shiver_armor.py',ROOT/'tools/probe_shiver_armor.py',ROOT/'tools/probe_energy_shield.py',ROOT/'tools/reference/energy-shield-source-evidence.json',source,parent,ROOT/'tools/build_skill_roadmap.py',
        *(ROOT/'docs'/n for n in ('sorceress-shiver-armor-checkpoint.md','sorceress-energy-shield-preflight.md','sorceress-armor-retaliation-preflight.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','shiver-armor-fixtures.json','diagnostic-4/birth-inputs.json','diagnostic-4/birth-native-checks.log','diagnostic-run-4.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
        *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Shiver Armor ordinary-NPC typed retaliation',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(nativeMarkers=484,focusedMarkers=21,boots=3,reloads=2,saves=3,rankHardGoldens=765,olderSourceCompositions=7680,nativeHpReturnColdComparisons=160,originalSavedReactions=2,retainedServerWireActions=126,fixtureFamilies=37,sharedChecks=10,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Complete older callback/elemental/typed mitigation/HP/NPC return composes in7680sanitized cases:flags16417,class61,cold afterHP,SOFTHIT refresh/notification without GH or defender RNG. Outer paths and formula/stat services remain seams.',
            'Ordinary NPC native hit/miss attempt scopes react at exit;early accuracy bridge is disabled. Shared Telekinesis damage/return and cold services retain live hard/gear at captured armor rank. Rankless armor keeps captured endpoints/length. Reaction costs no mana and bit32 suppresses pair-specific item callbacks;ordinary kill recovery positive control passes.',
            '765goldens/7680roll/160native HP-return-cold controls and original armor/gear/wounds/mana actors across two reloads pass. Focused/final DLLs match;only Shiver remaining-work metadata changes data. Retained Telekinesis/source-return/Thorns and126wire actions,four fresh audits,71Python/573named/240catalogue/generators pass;106installed hashes unchanged.',
            'Separate Energy Shield preparation composes the complete older callback/percentage helper in25344cases plus8guards with clean sanitizers.92damage-only contexts differ from naive exact division. Existing SourcePercentage.Calculate is a reuse candidate;no Energy Shield runtime change or modern executable equivalence is claimed.'],
        remaining=['Native lethal player hit completes death before late reaction;requires deferred player HP/death bridge. PvP/player recipients,hireling early/duplicate paths and exact sibling Thorns/event order remain unfinished and unsupported contexts are identified.',
            'Full modern event/global actor seed/stat/geometry/regen/special uninterruptible/death-delay/Sunder/charged/oskill/client and old-DLL/unheld concurrent-save acceptance remain open. Callback composition is not full modern executable equivalence.',
            'Final cast/armor/reaction/contact/expiry/audio and separate tree/hotkey icons remain identified placeholders. No class certificate or deployment.'])
    out=ROOT/'tools/reference/shiver-armor-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Shiver Armor:484combined/21focused markers,7680source/160native return cases,765goldens,two original saved reactions;four audits/71Python;106installed hashes preserved;lethal/modern/client/assets gaps explicit.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
