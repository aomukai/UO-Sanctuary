#!/usr/bin/env python3
"""Bind prepared saved Arctic stream acceptance without enabling the channel."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/arctic-stream'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    names=['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','arctic-stream-fixtures.json',
        'recycler-casting-fixtures.json','recycler-world-fixtures.json','recycler-body-fixtures.json','recycler-recovery-fixtures.json',
        'body-fixtures.json','world-fixtures.json','ground-fixtures.json','poison-fixtures.json','casting-fixtures.json','wolf-world-fixtures.json']
    artifacts=[STAGE/n for n in names]
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==85
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for command,count in [('arctic-stream-check',1),('arctic-stream-reload-check',2),('arctic-stream-expiry-stage-check',2),('arctic-stream-live-check',3),('arctic-stream-live-result',3)]:
        assert log.count(command+': passed')==count,command
    assert log.count('Arctic:4590cold source packets/14280complete-factory controls pass')==3
    assert log.count('4608complete-source contexts/188928frames plus4224explicit current-cost controls/173184frames,17664JSON continuations pass')==3
    for name in ['recycler-recovery-live','recycler-world-live','recycler-casting-live']:assert log.count(name+'-result: passed')==3
    assert 'modern learned input/cadence remains open' in (STAGE/'run.log').read_text()
    saved=json.loads((STAGE/'arctic-stream-fixtures.json').read_text());assert saved['ExpiredChecks']==2
    assert any(s['Owner']==saved['Owner'] for s in saved['State']['Missiles'])
    sourcePath=ROOT/'tools/reference/arctic-stream-source-evidence.json';source=json.loads(sourcePath.read_text())
    assert source['referenceVersion']=='3.3.93847' and source['verified']['damageContexts']==4590 and source['verified']['factoryContexts']==14280
    artifacts.append(sourcePath)
    for name,digest in source['artifacts'].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
    frozen_receipt('0e367c2','tools/reference/player-inferno-sequence-native-evidence.json')
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name,digest in a['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(a);artifacts.append(p)
    before=json.loads(subprocess.check_output(['git','show','0e367c2:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==before['Definitions']
    py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
    paths=['tests/check_arctic_stream.py','tools/record_arctic_stream.py','tools/build_class_skills.py','tools/build_skill_roadmap.py',
        'data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/necromancer-summons.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']
    artifacts += [ROOT/n for n in paths]
    status=json.loads((ROOT/paths[4]).read_text());handlers=[s for s in status['skills'] if s['implementation']=='handler-present']
    assert len(handlers)==182 and sum(s['characterClass']=='Druid' for s in handlers)==26 and status['completedClasses']==[]
    result=dict(referenceVersion='3.3.93847',component='Prepared saved player Arctic cold stream',installed=False,learnedHandlerEnabled=False,currentExecutableParity=False,goalComplete=False,
        handlers=182,druidHandlers=26,completedClasses=[],
        verified=dict(passMarkers=85,retainedMarkers=74,retainedWorldBoots=3,actualRetainedWorldReloads=2,
            coldProducerContexts=4590,factoryContexts=14280,coldNativeComparisonsPerBoot=4590,factoryNativeControlsPerBoot=14280,
            automaticSequenceToStreamDamageChillExpiryRuns=3,expiredAtReloadNoReplayChecks=2,retainedAutomaticRecyclerRuns=9,
            pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[len(a['InputHashes']) for a in audits]),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        frozenPredecessor=dict(commit='0e367c2',receipt='tools/reference/player-inferno-sequence-native-evidence.json'),
        established=[
            'Current Arctic skill230/missile459cold endpoints use HitShift3,hard-only Cyclone Armor15percent and once-only birth mastery. The complete source getter/creator/writer and separate factory execute under clean ASan/UBSan; all255ranks compare with actual content packets and native creation.',
            'Factoryflags0x8020,velocity2304FP8,CanSlow0,lifetime floor((35+2*(rank-1))/2),LastCollideowner,source-size1contact and DamageRate41 remain explicit. Saved streams reuse the shared Q12/source-cell/terrain consumers; one recipient per phase and most-recent collision history allow later contacts without a lifetime hit set or shared NHD.',
            'Actual fractional cold health,immunity/chill separation,pure-cold shield bypass,allocation failure,offline aging,atomic invalid restores and detached snapshots pass. Three connected supplied-sequence/start/debit-to-real-stream runs commit damage and chilling and expire automatically. Two actual retained-world reloads preserve captured packets/seed/history/steps and skip overdue emissions without replay.',
            'The first diagnostic found a missing runtime source row; the generator now includes missile459. A later live fixture advanced through two callbacks while expecting the first suppression; its boundary now follows the complete-source trace at1010. Runtime source/input cadence was not changed to satisfy that test. Diagnostics remain staged.'],
        remaining=[
            'Learned SQ/input/FCR/accepted held intents,production channel persistence/cancellation and modern payment frequency are still gated. The source sequence normal256rate and explicit callbacks do not establish modern cadence or calibrate the user12–24DPS/rounded4mana tooltip.',
            'Native Q12/cell/room order/terrain/seed adapters are not current executable parity. Player/PvP/unmapped colliders,full owner death/facet/reentrant chronology,modern Slow Missiles damage and Sanctuary potion ordering remain unsupported or uncertain. Unsupported prepared owner/offline contexts age harmlessly; no modern lifecycle policy is inferred from NPC traps.',
            'Final stream/overlay/contact/chill/expiry/audio and separate talent/left/right/quick icons remain missing with identified placeholders. Vine exposure timing,Solar formula/Command,earlier classes and Warlock remain in scope. Main4c5d3a0build/saves/client are preserved; no full-class regression,installation or class completion is claimed.'])
    p=ROOT/'tools/reference/arctic-stream-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==text
    else:p.write_text(text)
    print('Prepared Arctic stream:85checks/3boots/2reloads;4590cold and14280factory controls per boot; learned channel stays gated.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
