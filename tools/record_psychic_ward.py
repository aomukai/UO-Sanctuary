#!/usr/bin/env python3
"""Bind the prepared Ward pool/group package without certifying new callbacks."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/psychic-ward'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json')
    names=['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log','psychic-ward-fixtures.json','cyclone-armor-fixtures.json','class-skills-checks.json',
        'fissure-fixtures.json','miasma-packet-fixtures.json','levitation-fixtures.json','barbarian-masteries.json',
        'warlock-foundation-fixtures.json','arctic-mode-fixture.json','arctic-stream-fixtures.json','recycler-casting-fixtures.json',
        'recycler-world-fixtures.json','recycler-body-fixtures.json','recycler-recovery-fixtures.json','body-fixtures.json',
        'world-fixtures.json','ground-fixtures.json','poison-fixtures.json','casting-fixtures.json','wolf-world-fixtures.json']
    artifacts=[STAGE/n for n in names]
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==135
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for n in ['psychic-ward','cyclone-armor','class-skills','miasma-launch','fissure','miasma-packets','levitation','barbarian-masteries','warlock-foundation']:
        assert log.count(n+'-check: passed')==1 and log.count(n+'-reload-check: passed')==2,n
    assert log.count('Psychic Ward:2295current formula controls,44frame rank1stun and fractional pool conservation pass; event37delivery remains supplied')==3
    for n in ['fissure-live','arctic-mode-live','arctic-stream-live','recycler-recovery-live','recycler-world-live','recycler-casting-live']:assert log.count(n+'-result: passed')==3,n
    saved=json.loads((STAGE/'psychic-ward-fixtures.json').read_text());assert len(saved)==2 and {x['Female'] for x in saved}=={True,False}
    assert all(x['State']['Rank']==21 and x['State']['Maximum256']==208640 and x['State']['Remaining256']==123457 and x['State']['StunFrames']==77 for x in saved)
    proof=ROOT/'tools/reference/psychic-ward-source-evidence.json';source=json.loads(proof.read_text());assert source['cases']==2304 and source['handlerEnabled']==False;artifacts.append(proof)
    for group in ['inputs','artifacts']:
        for name,digest in source[group].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
    frozen_receipt('f5175e3','tools/reference/miasma-launch-native-evidence.json')
    for name,needle in [('resistance-fixture-diagnostic','Resistance fixture'),('audit-path-diagnostic','DirectoryNotFoundException')]:
        d=STAGE/name;assert needle in (d/'stats-native-checks.log').read_text();artifacts += list(d.iterdir())
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name,digest in a['InputHashes'].items():assert sha(ROOT/name)==digest,name
        audits.append(a);artifacts.append(p)
    old=json.loads(subprocess.check_output(['git','show','f5175e3:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions']
    assert [len(a['InputHashes']) for a in audits]==[695,21]
    py=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in py and '\nOK\n' in py and 'FAILED' not in py
    artifacts += [ROOT/n for n in ['tests/check_psychic_ward.py','tools/record_psychic_ward.py','tools/probe_psychic_ward.py','tools/check_cyclone_armor_packets.py',
        'tools/build_class_skills.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/skill-effects.json','data/Sanctuary/Balance/skill-icons.json']]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[s for s in status['skills'] if s['implementation']=='handler-present'];assert len(handlers)==183 and status['completedClasses']==[]
    ward=next(s for s in status['skills'] if s['talentId']=='warlock.psychicward');assert ward['implementation']=='blocked' and ward['verification']=='partial-native-coverage'
    result=dict(referenceVersion='3.3.93847',component='Prepared Psychic Ward physical pool/group and supplied reaction',installed=False,handlerEnabled=False,currentExecutableParity=False,goalComplete=False,
        handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=135,retainedMarkers=126,worldBoots=3,actualWorldReloads=2,
            completeLegacyBuffContexts=2304,nativeFormulaControlsPerBoot=2295,savedWarlockWitchPools=2,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[695,21]),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='f5175e3',receipt='tools/reference/miasma-launch-native-evidence.json'),
        established=['Prepared Psychic Ward has an immutable FP256 pool with separate maximum, captured effective rank and stun duration. Native incoming physical AOS damage reaches it before physical resistance in the existing Bone Armor adapter; elemental damage bypasses it. Fractional debit, depletion/overflow and real Shaftstop30percent resistance controls pass. The shared pool operation is reused by Cyclone Armor without changing its channel order; its native source and incoming-damage regression passes.',
            'Current capacity is15+10*(rank-1)+15*(hardLevitation+hardCleave), mana20+2*(rank-1), and stun uses the current shared dm12(37,85)curve:44frames at rank1. Actual trained Warlock/Witch with hard20synergies and Tarnhelm retain rank21/815HP maximum/123457FP remaining/77frames across both real reloads. Removing the helm leaves the existing capture unchanged. Malformed restore is atomic and rejects invalid pools/curves.',
            'Implemented group1armors now replace one another: Bone Armor, Frozen/Shiver/Chilling Armor and prepared Psychic Ward. The complete legacy cast/group/writers/removal routines execute2304contexts, including allocation failure; current event descriptors register40and37with captured rank. Native cast/group/factory-failure/low-mana controls pass; low mana preserves old armor, while allocation failure after group clear loses old armor without paying. justhit/mindbarrier have no native persistent armor projection and are not mapped to a generic recovery reset.',
            'Death/resurrection, respec and class-history transitions clear the Ward. An explicitly supplied eligible reaction reaches the shared native stun consumer; ineligible input does not. No automatic on-hit or HP-loss event rule is installed. Learned binding remains rejected.',
            'The first resistance test used an unrelated native resistance override, which class-derived defenses do not use. It was replaced with actual Shaftstop gear. A later run stopped because of a mistyped audit export path; the complete corrected135-check run has no errors. Both diagnostic logs are retained. All573named-item decisions are unchanged.'],
        remaining=['Modern event40absorption/overflow/display and priority are not certified by the legacy Bone Armor callback; its suspicious exhaustion branch is explicitly recorded rather than copied. The candidate physical pool follows existing Sanctuary min-pool behavior. Energy Shield coexistence is refused by the prepared caster; exact ordering and broader simultaneous shields need further evidence.',
            'Automatic event37qualification/order for fully absorbed or breaking hits remains unverified; the user has not trained the skill and cannot confirm this detail; no respec or point allocation is requested. Supplied reaction controls do not prove that callback fires on any particular real hit. Captured registration rank is established by the shared legacy caster,not independently by the modern callback body. PvP and complete modern eligibility remain separate.',
            'Learned SC windup/release/payment/input and current-client timing/effects remain gated. No new Ward handler, full-class certificate, full regression or installation is claimed. Main4c5d3a0working build and original saves/configuration/client remain preserved; accepted older stages remain frozen.',
            'Ward shield/cast and supplied-stun visuals use identified UO flashes/audio. Final psychicward/cast/contact/depletion/psychicward_hit0/stun/audio and talent/right/quick icons remain separately tracked. Earlier class, Miasma and Levitation gaps stay in the goal.'])
    p=ROOT/'tools/reference/psychic-ward-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Prepared Psychic Ward:135 checks/3 boots/2 reloads,70 Python;183 handlers unchanged, new callbacks/learned cast gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
