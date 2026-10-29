#!/usr/bin/env python3
"""Bind current action/choice helpers to retained native-build acceptance."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-actions'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
    _,prior=frozen_receipt('db42371','tools/reference/item-property-writers-native-evidence.json');assert prior['verified']['passMarkers']==219
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==222
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    marker=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(marker,(ROOT/'.local/class-skills/item-property-writers/stats-native-checks.log').read_text()));after=Counter(re.findall(marker,log))
    assert sum(before.values())==219 and not before-after and after-before==Counter({'generic-pet-actions-check':1,'generic-pet-actions-reload-check':2})
    assert log.count('GenericPet actions:18070captured-current disposition/action/weighted/composed cases,108fatal-selector boundaries;exact memory,raw mode payloads,weights and pet RNG pass;real world/mode/skill services remain gated')==3
    sourcepath=ROOT/'tools/reference/generic-pet-actions-source-evidence.json';source=json.loads(sourcepath.read_text());artifacts.append(sourcepath)
    assert source['verified']==dict(contexts=18070,byKind=dict(disposition=5446,act=10512,weighted=1056,composed=1056),machineCodeBytes=1601,fatalBoundaries=108,booleanReturnUpperWordControls=6164)
    assert not source['processMemoryRead'] and source['sourceCodeExecutedInOfflineEmulator'] and not source['completeAiVerified'] and not source['handlerEnabled']
    for name,digest in source['artifacts'].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
    lib=ROOT/source['emulatorLibrary']['path'];assert sha(lib)==source['emulatorLibrary']['sha256'];artifacts.append(lib)
    # The further approach reference is preserved for later analysis, not used
    # as an accepted world movement implementation here.
    capture=ROOT/'.local/class-skills/warlock-runtime-reference/callees-live-action-approach-20261029.json';r=json.loads(capture.read_text());artifacts.append(capture)
    assert r['records'][0]['rva']=='0x4b1600' and r['records'][0]['bytes']==179 and r['records'][0]['allUnwindFragmentsCaptured'] and r['bytesRead']==918
    assert not any(r[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
    for name,h in r['artifacts'].items():assert sha(ROOT/name)==h,name;artifacts.append(ROOT/name)
    for label in ['inverted-mode-skill-branch','boolean-return-register-width']:
        d=STAGE/'diagnostics'/label;failed=(d/'stats-native-checks.log').read_text();assert 'generic-pet-actions-check: failed' in failed
        for p in d.rglob('*'):
            if p.is_file():artifacts.append(p)
    for p in (STAGE/'diagnostics/boolean-register-width').rglob('*'):
        if p.is_file():artifacts.append(p)
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
        audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
    old=json.loads(subprocess.check_output(['git','show','db42371:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[734,21]
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    mainpath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainpath.read_text());assert len(main['installedArtifacts'])==106
    for name,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/name)==h,name
    for name in ['class-skill-status.json','skill-effects.json','skill-icons.json']:
        p=ROOT/'data/Sanctuary/Balance'/name;assert sha(p)==hashlib.sha256(subprocess.check_output(['git','show','db42371:'+str(p.relative_to(ROOT))],cwd=ROOT)).hexdigest();artifacts.append(p)
    artifacts += [STAGE/n for n in ['stats-inputs.json','stats-native-checks.log','run.log','source-run.log','python-tests.log']]
    artifacts += list((STAGE/'focused-fixtures').glob('*fixtures.json'))
    artifacts += [ROOT/n for n in ['tools/record_generic_pet_actions.py','tests/check_generic_pet_actions.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]+[mainpath]
    result=dict(referenceVersion='3.3.93847',component='Current GenericPet action/disposition/weighted-choice policy',installed=False,handlerEnabled=False,fullAiVerified=False,realWorldModeSkillServicesAccepted=False,sharedActorSeedPersistenceAccepted=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=222,retainedMarkers=219,worldBoots=3,actualWorldReloads=2,contextsPerBoot=18070,dispositionContexts=5446,actionContexts=10512,weightedContexts=1056,fullThreeHelperCompositions=1056,machineCodeBytes=1601,fatalSelectorBoundaries=108,booleanUpperWordControls=6164,exactDescriptorsMemoryRequestsTraces=True,exactPetRng=True,cumulativeEqualityAndFallback=True,sourceRandomReused=True,frozenPriorFullClassMarkers=1184,newFullClassRun=False,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[734,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='db42371',receipt='tools/reference/item-property-writers-native-evidence.json'),established=[
        'Three complete current captured helpers totaling1601bytes execute offline. Native results match18070contexts per boot:5446dispositions,10512actions,1056weighted choices and1056three-helper compositions. Exact service traces,descriptors,request payload words,memory and pet seed match. The further647-byte disposition reference was captured through preserved direct-call provenance;all code remains pinned to3.3.93847.',
        'Range policy preserves configured bounds/collision ordering,coordinate kinds,low-byte movement requests,move completion and monster-bit/distance approach hint. Action distinguishes skill execution from ordinary modes,source skill-row lookup after failed skill execution,target-memory suppression,accepted versus remembered state,same-slot counter increments and108fatal-selector boundaries. Actual world/action effects remain explicit services.',
        'Weighted choice preserves priority/unusable markers,signed cumulative wrap,equality at the selected boundary,fallback pointer precedence and repeat attempts. It reuses SourceRandom.Roll and consumes zero/one pet RNG draw exactly. The seed object is supplied;real shared actor RNG ownership/persistence is not certified.',
        'The first managed branch was inverted;independent current-code comparison caught it before any live actor used this path. Fixture booleans require low-byte arguments/AL returns,while disposition usesEAX;6164upper-word controls verify that distinction. Source instructions were never patched. Both failed native runs,inputs,DLLs,source/oracle versions and ABI diagnostics remain archived. No action effect or trace expectation was weakened.',
        'All219previous native marker counts remain. Both builds have zero warnings/errors;70Python checks,573unchanged named eligibility decisions and106unchanged main installation hashes pass. Existing saved fixtures survive two actual reloads. This prepared action/seed policy adds no world persistence. Frozen417cd39separately retains1184full-class checks;no new full-suite run is claimed.'
    ],remaining=[
        'Real metadata,range/collision/approach/movement,mode and skill execution,current gathering/preparation/rating/ordering/follow executor and complete dispatcher/owner/lifecycle integration remain unfinished. Reentrant vector resizing and invalid source ranged descriptors are unsupported. The additional179-byte approach body is preserved only for later analysis.',
        'Learned Goatman/Tainted/Defiler casting and actor lifecycle,extended passives/events/Blood Oath/sunder and new Frenzy/Cleave remain gated. Continue unaffected class and raw-item/Necromancer dependencies. Effects/audio and talent/hotkey icons remain separate placeholders;no class is complete.'
    ])
    out=ROOT/'tools/reference/generic-pet-actions-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('GenericPet actions:222native/70Python;18070current-code contexts per boot;world AI/mode/skill services unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
