#!/usr/bin/env python3
"""Bind ordinary prepared Arctic mode acceptance; raw learned input remains gated."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/arctic-modes'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    names=['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','arctic-mode-fixture.json','arctic-stream-fixtures.json',
        'recycler-casting-fixtures.json','recycler-world-fixtures.json','recycler-body-fixtures.json','recycler-recovery-fixtures.json',
        'body-fixtures.json','world-fixtures.json','ground-fixtures.json','poison-fixtures.json','casting-fixtures.json','wolf-world-fixtures.json']
    artifacts=[STAGE/n for n in names]
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==96
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for command,count in [('arctic-mode-rate-check',3),('arctic-mode-reload-check',2),('arctic-mode-live-check',3),('arctic-mode-live-result',3),('arctic-stream-live-result',3),('arctic-stream-reload-check',2)]:assert log.count(command+': passed')==count,command
    assert log.count('Arctic/Inferno:144ordinary source rate/scheduler controls match sequence256/action10/end15; no FCR queries')==3
    assert log.count('Prepared Arctic mode:threshold96FP,debit/first suppression,reentry/stop/expiry,phase timestamps,move/equipment/FHR/respec,live rank and target/deletion/free aim pass')==3
    assert log.count('Arctic:4590cold source packets/14280complete-factory controls pass')==3
    for n in ['recycler-recovery-live','recycler-world-live','recycler-casting-live']:assert log.count(n+'-result: passed')==3,n
    assert 'accepted raw input/payment cadence remains separate' in (STAGE/'run.log').read_text()
    proof=ROOT/'tools/reference/player-inferno-rate-source-evidence.json';source=json.loads(proof.read_text());assert source['contexts']==144 and source['referenceVersion']=='3.3.93847';artifacts.append(proof)
    for name,digest in source['artifacts'].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
    frozen_receipt('6f9af8d','tools/reference/arctic-stream-native-evidence.json')
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name,digest in a['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(a);artifacts.append(p)
    old=json.loads(subprocess.check_output(['git','show','6f9af8d:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions']
    py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
    artifacts += [ROOT/n for n in ['tests/check_arctic_modes.py','tools/record_arctic_modes.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[s for s in status['skills'] if s['implementation']=='handler-present'];assert len(handlers)==182 and sum(s['characterClass']=='Druid' for s in handlers)==26 and status['completedClasses']==[]
    result=dict(referenceVersion='3.3.93847',component='Prepared ordinary native Arctic SQ18 mode controller',installed=False,learnedHandlerEnabled=False,currentExecutableParity=False,goalComplete=False,
        handlers=182,druidHandlers=26,completedClasses=[],verified=dict(passMarkers=96,retainedMarkers=85,retainedWorldBoots=3,actualRetainedWorldReloads=2,sourceRateControls=144,rateNativeControlsPerBoot=144,
            automaticModeRuns=3,automaticStreamRuns=3,retainedAutomaticRecyclerRuns=9,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[len(a['InputHashes']) for a in audits]),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},frozenPredecessor=dict(commit='6f9af8d',receipt='tools/reference/arctic-stream-native-evidence.json'),
        established=[
            'Complete older ordinary rate routing plus current seqtransSQ/UseAttackRate0 retain sequence256/action10/end15for Inferno and Arctic in144metadata/FCR controls. FCR service is not queried. This replaces a merely supplied normal-rate assumption with bounded source/current-data evidence,not modern executable parity.',
            'DruidArcticModes connects the checked sequence/resources to saved real cold emissions at their phase timestamps. Explicit fresh/reentry/stop/cancel/aim calls retain96FP rank1start debit,six-mana threshold,first-do suppression,pending player rewind and independent lease/stream lifetimes. Present target positions are requeried; deletion uses the retained point; aim and item rank update later emissions.',
            'Movement,selected-weapon commit,accepted FHR,respec and disconnect cancellation are exercised. Wardrobe/death hooks are wired but need separate native coverage. Real connected timers run entry through damage/chill/expiry in all three boots. Player mana persists across two actual reloads; modes remain transient. This does not claim production active-channel serialization.',
            'A fixture first lacked elite sword requirements; its attributes were corrected. Subsequent checks found deferred equip and direct hit-recovery hooks missed the controller. Post-transaction equip and native recovery hooks now notify it; diagnostic logs remain staged. Earlier streams,source sequence/resource and recycler tests are retained unchanged.'],
        remaining=[
            'Raw left/right/quick held-input acceptance,heartbeat/reentry frequency,current client release versus mode-change composition and modern mana-per-second remain unmeasured. Explicit accepted callbacks are not raw client heartbeats. The no-reentry control pays once and ends at its source lease; it is not a sustained modern drain policy.',
            'Ordinary unmorphed Druid mode only; fresh entry with an existing shared lease,item-only/cross-class channel,full global queue/room/geometry/seed and current executable animation acceptance remain outside scope. Native pending-mode block/recovery/equipment/movement guards are shared Sanctuary adapters; wardrobe/death/facet and competing-action cancellation still need specific native controls. Modern source-owner death/facet/reentrant missile behavior and Slow Missiles damage/potion order retain earlier gaps.',
            'Final S1/cast/stream/contact/chill/expiry/audio and talent/left/right/quick icons remain separately missing. Learned Arctic is gated; main4c5d3a0build,original saves/configuration/client remain preserved. No full-class regression,installation or class completion. Vine exposure timing,Solar formula/Command,earlier classes and Warlock remain in scope.'])
    p=ROOT/'tools/reference/arctic-modes-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==text
    else:p.write_text(text)
    print('Prepared Arctic mode:96checks/3boots/2reloads;144rate controls and3automatic mode runs; raw learned input/cadence stays gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
