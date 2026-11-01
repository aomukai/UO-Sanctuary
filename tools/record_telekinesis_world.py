#!/usr/bin/env python3
"""Bind Telekinesis world operations and routing without certifying combat parity."""
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
STAGE=ROOT/'.local/class-skills/sorceress-telekinesis-world'
BUILD=7
PARENT='3da9384'
sha=inventory.sha


def historical(name):return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)


def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
    counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(counts.values())==359 and ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_telekinesis_world import configuration
    config=configuration(True)
    assert len(config['extra_families'])==29 and len(config['extra_checks'])==6
    for family in config['extra_families']:
        assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
    for command in config['extra_checks']:assert counts[command]==3
    for family in config['extra_target_input_commands']:
        assert counts[family+'-check']==counts[family+'-result']==3
        assert log.count(' '+family+': windup')==3,family
    for kind in ('item','mobile'):
        for mode in ('right','quick'):
            assert counts['telekinesis-'+kind+'-'+mode+'-result']==3
    assert counts['telekinesis-world-reload-result']==2
    assert 'Telekinesis world:255independent rank contexts' in log
    saved=json.loads((STAGE/'telekinesis-world-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Gold']>0 and saved['Door']>0 and saved['Mana']>0 and saved['Life']>0
    focused=inventory.validate_inventory(STAGE/f'diagnostic-{BUILD}/birth-inputs.json')
    focused_log=STAGE/f'diagnostic-{BUILD}/birth-native-checks.log';text=focused_log.read_text()
    assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==tested['copiedData']
    assert sum(markers(focused_log).values())==38 and ' ERR]' not in text and ': failed' not in text
    assert text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    for kind in ('content','integration'):
        text=(STAGE/f'{kind}-build-{BUILD}.log').read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
    source=ROOT/'tools/reference/telekinesis-source-evidence.json';reference=json.loads(source.read_text())
    assert reference['referenceVersion']=='3.3.93847' and reference['startCases']==2880 and reference['releaseCases']==28224 and reference['sanitizers']=='ASan/UBSan clean'
    assert {k:reference['currentRow'][k] for k in ('*Id','srvstfunc','srvdofunc','ResultFlags','HitFlags','HitClass','Param1','Param2')}=={'*Id':43,'srvstfunc':12,'srvdofunc':21,'ResultFlags':5,'HitFlags':2,'HitClass':109,'Param1':25,'Param2':35}
    client=ROOT/'tools/reference/telekinesis-client-evidence.json';client_reference=json.loads(client.read_text())
    assert client_reference['patches']==38 and client_reference['affectedPaths']==41 and client_reference['buildErrors']==0 and not client_reference['graphicalInputVerified']
    for evidence in (reference,client_reference):
        for path,digest in evidence['artifacts'].items():assert sha(ROOT/path)==digest,path
    for path,digest in reference['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/path)==digest,path
    for path,digest in reference['currentTable'].items():assert sha(ROOT/path)==digest,path
    for path,digest in client_reference['patchHashes'].items():assert sha(ROOT/path)==digest,path
    prior=json.loads((ROOT/'.local/class-skills/sorceress-storm-pulse/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]
    assert tested['copiedData'].keys()==prior['copiedData'].keys()
    assert {p for p in tested['copiedData'] if tested['copiedData'][p]!=prior['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
    updated=next(x for x in now['skills'] if x['talentId']=='sorceress.telekinesis');old=next(x for x in before['skills'] if x['talentId']=='sorceress.telekinesis')
    assert old['verification']==updated['verification']=='partial-native-coverage'
    old['remaining']=updated['remaining'];assert before==now and not now['completedClasses']
    parent=ROOT/'tools/reference/storm-pulse-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text()
    fingerprints=[]
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        audit=json.loads((STAGE/name).read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest,path
        prior_audit=json.loads(historical('tools/reference/'+name));identity=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
        assert {k:v for k,v in audit.items() if k not in identity}=={k:v for k,v in prior_audit.items() if k not in identity}
        fingerprints.append(len(audit['InputHashes']))
    assert fingerprints==[890,21]
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    checks=(STAGE/f'checks-{BUILD}.log').read_text()
    for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in checks
    assert 'Traceback' not in checks
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    files=[Path(__file__).resolve(),source,client,parent,ROOT/'tools/probe_telekinesis_source.py',ROOT/'tools/review_telekinesis_client.py',ROOT/'tests/check_telekinesis_world.py',ROOT/'tools/build_skill_roadmap.py',
           *(ROOT/'docs'/name for name in ('sorceress-telekinesis-world-checkpoint.md','sorceress-telekinesis-preflight.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
           *(STAGE/name for name in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','telekinesis-world-fixtures.json',f'diagnostic-{BUILD}/birth-native-checks.log',f'diagnostic-{BUILD}/birth-inputs.json',f'diagnostic-run-{BUILD}.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
           *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Telekinesis world operations and entity routing',installed=False,graphicalClientVerified=False,completedClasses=[],
                verified=dict(nativeMarkers=359,focusedMarkers=38,boots=3,actualReloads=2,saves=3,newServerWireActions=12,retainedServerWireActions=72,postLoadPaidDoorOperations=2,nativeRankGoldens=255,sourceStartCases=2880,sourceReleaseCases=28224,fixtureFamilies=29,sharedChecks=6,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,clientPatches=38,clientWarnings=74,unchangedInstalledArtifacts=106),
                markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
                established=[
                    'Pinned older complete start12/do21 use inclusive squared range, separate combat/item/object eligibility, OR flags/event order, six pickup categories and non-whitelist ground refresh. Declared dependent engine services and modern executable remain separate.',
                    'Selected item serials survive actual server right/quick input. Native ground pickup retains lift/drop/access/weight checks and original-ground lift notification without drinking/casting collected items. Door locks and container hooks remain native; items can be targeted in town while hostile mobiles cannot.',
                    'Two reloads retain original inventory, binding, paid mana and fractional wounds and operate the original door with another payment, without restoring resources. Named fixture-only mana/hit regeneration exclusions preserve exact accounting; normal gameplay recovery is unchanged.',
                    'Thirty-eight client patches over41paths reverse to the exact native pin and reproduce built inputs. Client builds with74warnings/0errors; graphical input and fresh warning-baseline comparison are not claimed. Earlier diagnostic logs/binaries and interrupted predecessor are retained.',
                    'Only Telekinesis remaining-work metadata changes runtime data. Four fresh audits retain item/affix decisions;71Python/573named/240catalogue checks pass;106installed artifacts are unchanged. Separate effects and icons remain pending.'
                ],remaining=[
                    'Combat retains the pre-existing generic floating damage/one-tile push. Trace shared ExecuteEvents/ExecuteMissileDamage and native source-return consumers before replacing it with flags5/hit2/class109, source RNG/knockback and special-recipient policy; no unconditional stun/forced GH is invented.',
                    'Portals/gates/waypoints/shrines and unsupported native objects have no implicit remote operation. Full native house/trap/capacity/callback combinations, modern executable/initial targeting/PvP/Sunder/events, graphical cursor input and actual old-DLL save compatibility remain unverified.',
                    'Continue Sorceress contracts one at a time; Inferno learned held-input/payment/release remains separately unfinished. No class certificate or deployment.'
                ])
    output=ROOT/'tools/reference/telekinesis-world-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Telekinesis world:359native/38focused markers,three boots/two reloads,12new server inputs,38client patches,four audits/71Python;combat/travel/graphical parity remain open;installed artifacts preserved.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
