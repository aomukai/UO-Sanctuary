#!/usr/bin/env python3
"""Bind extended Warlock formula/data checks without claiming skill handlers."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/warlock-foundation'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    names=['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','warlock-foundation-fixtures.json','arctic-mode-fixture.json','arctic-stream-fixtures.json',
        'recycler-casting-fixtures.json','recycler-world-fixtures.json','recycler-body-fixtures.json','recycler-recovery-fixtures.json',
        'body-fixtures.json','world-fixtures.json','ground-fixtures.json','poison-fixtures.json','casting-fixtures.json','wolf-world-fixtures.json']
    artifacts=[STAGE/n for n in names]
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==99
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('warlock-foundation-check: passed')==1 and log.count('warlock-foundation-reload-check: passed')==2
    assert log.count('Warlock foundation:23130complete linear-source/current-index controls and462600direct parameter lookups pass; diminishing arithmetic unchanged')==3
    for n in ['arctic-mode-live','arctic-stream-live','recycler-recovery-live','recycler-world-live','recycler-casting-live']:assert log.count(n+'-result: passed')==3,n
    assert 'no Warlock handler enabled' in (STAGE/'run.log').read_text()
    saved=json.loads((STAGE/'warlock-foundation-fixtures.json').read_text());assert len(saved)==2 and {x['Female'] for x in saved}=={True,False}
    proof=ROOT/'tools/reference/warlock-foundation-preflight.json';source=json.loads(proof.read_text());assert source['comparedPlayerFields']==1709 and len(source['playerSkills'])==30 and len(source['curveAliases'])==10;artifacts.append(proof)
    for name,digest in source['artifacts'].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
    frozen_receipt('dc65b89','tools/reference/arctic-modes-native-evidence.json')
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name,digest in a['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(a);artifacts.append(p)
    old=json.loads(subprocess.check_output(['git','show','dc65b89:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions']
    py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
    artifacts += [ROOT/n for n in ['tests/check_warlock_foundation.py','tools/record_warlock_foundation.py','tools/build_class_skills.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[s for s in status['skills'] if s['implementation']=='handler-present'];assert len(handlers)==182 and not any(s['characterClass']=='Warlock' for s in handlers) and status['completedClasses']==[]
    rows=json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text());assert all(len(s['sourceParameters'])==20 for s in rows)
    nonzero=[(s['sourceKey'],k,v) for s in rows for k,v in s['sourceParameters'].items() if int(k)>12 and v];assert nonzero==[('Bind Demon','13',50),('Bind Demon','14',25)]
    result=dict(referenceVersion='3.3.93847',component='Warlock extended formula aliases and full parameter projection',installed=False,warlockHandlers=0,currentExecutableParity=False,goalComplete=False,
        handlers=182,completedClasses=[],verified=dict(passMarkers=99,retainedMarkers=96,retainedWorldBoots=3,actualRetainedWorldReloads=2,
            linearArmAliasComparisonsPerBoot=23130,directParameterLookupsPerBoot=462600,savedWarlockWitchPlayers=2,playerRowsCompared=30,licensedPublicFields=1709,
            pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[len(a['InputHashes']) for a in audits]),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},frozenPredecessor=dict(commit='dc65b89',receipt='tools/reference/arctic-modes-native-evidence.json'),
        established=['Strict pinned local CASC metadata identifies ten curve alias pairs through parameters20. ln21/dm21mean11/12;ln43/dm43mean13/14. The resolver uses registered names and rejects unsupported digit pairs. Generated player/NPC/aura projections retain20parameters,including Bind Demon50/25flat-damage parameters previously dropped.',
            'Native checks remap2313unchanged legacy linear-arm controls through each of10current aliases,including absent-rank cases. Another462600direct parameter lookups pass per boot. Actual trained Warlock/Witch plus Tarnhelm resolve Demonic Mastery20,Goatman speed11and Bind Demon flat75at effective rank2,retaining player identity/ranks/gear across both reloads.',
            'All30Warlock player rows match1709licensed/public populated fields with one recorded outer-whitespace representation. Only Bind Demon adds nonzero13..20player parameters; non-Warlock values remain zero. Shared diminishing arithmetic is deliberately unchanged; current callback body/rounding acceptance is not claimed from metadata descriptions.',
            'The public Google document text is now retained locally with hash/revision/authorship-as-claimed metadata. The user observes rank2requirements Ripsaw68/48,Composite-Bow-shaped Rogue Bow24/34and Rusthandle36Strength,including unchanged unarmed/equipped Rusthandle. These match4percent subtraction-of-truncated-reduction; equip/item-ease/ethereal order still needs its own acceptance.'],
        remaining=['Levitation combat stats,owner-aware requirement/equip/tooltips,innate two-handed/offhand rules and passive critical/requirement stacking need dedicated implementation and native verification. Rusthandle displayed level18is plausibly its Vengeance grant,not a proven level-override implementation.',
            'Malformed Warlock formulas and new spell/minion/event/link/field callbacks remain explicit work. No Warlock talent handler,full-class regression,installation or class completion is claimed. Current executable diminishing math is not certified by alias routing.',
            'Final class/levitating-weapon/skill effects/audio and talent/hotkey icons remain separately tracked; source/data fixes need no new visual. Main4c5d3a0build,original saves/configuration/client and earlier accepted stages are preserved. Earlier class and Druid gates remain in the goal.'])
    p=ROOT/'tools/reference/warlock-foundation-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==text
    else:p.write_text(text)
    print('Warlock foundation:99checks/3boots/2reloads;23130linear aliases and462600parameter lookups per boot; no Warlock handler claimed.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
