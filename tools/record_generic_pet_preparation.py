#!/usr/bin/env python3
"""Bind current buff recipient preparation to its native-build acceptance."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-preparation'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
    _,prior=frozen_receipt('55d316d','tools/reference/generic-pet-gather-native-evidence.json');assert prior['verified']['passMarkers']==225
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==228
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    marker=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(marker,(ROOT/'.local/class-skills/generic-pet-gather/stats-native-checks.log').read_text()));after=Counter(re.findall(marker,log))
    assert sum(before.values())==225 and not before-after and after-before==Counter({'generic-pet-preparation-check':1,'generic-pet-preparation-reload-check':2})
    assert log.count('GenericPet preparation:4682captured-current contexts,200fatal boundaries;exact descriptor targets,state groups,recipient callbacks and service order pass;real world iterators/state/geometry remain gated')==3
    sourcepath=ROOT/'tools/reference/generic-pet-preparation-source-evidence.json';source=json.loads(sourcepath.read_text());artifacts.append(sourcepath)
    assert source['verified']==dict(contexts=4682,fatalBoundaries=200,capturedBytes=2139,boundedLeafBytes=16,leafInstructionsBytes=11,stateVectorGrowths=192,stateVectorFrees=32)
    assert not source['processMemoryRead'] and source['sourceCodeExecutedInOfflineEmulator'] and not source['completeAiVerified'] and not source['handlerEnabled']
    for name,h in source['artifacts'].items():assert sha(ROOT/name)==h,name;artifacts.append(ROOT/name)
    lib=ROOT/source['emulatorLibrary']['path'];assert sha(lib)==source['emulatorLibrary']['sha256'];artifacts.append(lib)
    for name,size,read in [('callees-live-preparation-services-20261029.json',405,1370),('live-preparation-callback-20261029.json',541,1642),('live-preparation-followup-20261029.json',16,592)]:
        p=ROOT/'.local/class-skills/warlock-runtime-reference'/name;r=json.loads(p.read_text());artifacts.append(p)
        assert sum(x['bytes'] for x in r['records'])==size and r['bytesRead']==read
        assert r['executableSha256']=='e1f5436e3d9687f644ef16938b1b183d1fdef434f18cf66d852cf68f48cc8936'
        assert not any(r[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
        if name=='live-preparation-followup-20261029.json':
            assert not r['records'][0]['allUnwindFragmentsCaptured'] and r['records'][0]['boundedLeafReference'] and r['records'][0]['rangeEndsAtPreservedCallback']
            assert r['callbackReference']['callbackRva']=='0x4b25e0' and r['callbackReference']['iteratorRva']=='0x35e6d0'
        else:assert all(x['allUnwindFragmentsCaptured'] for x in r['records'])
        for n,h in r['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
        audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
    old=json.loads(subprocess.check_output(['git','show','55d316d:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[736,21]
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    mainpath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainpath.read_text());assert len(main['installedArtifacts'])==106
    for name,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/name)==h,name
    for name in ['class-skill-status.json','skill-effects.json','skill-icons.json']:
        p=ROOT/'data/Sanctuary/Balance'/name;assert sha(p)==hashlib.sha256(subprocess.check_output(['git','show','55d316d:'+str(p.relative_to(ROOT))],cwd=ROOT)).hexdigest();artifacts.append(p)
    artifacts += [STAGE/n for n in ['stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','python-tests.log']]
    artifacts += [p for p in (STAGE/'focused-fixtures').iterdir() if p.is_file()]
    artifacts += [p for p in (STAGE/'diagnostics').rglob('*') if p.is_file()]
    artifacts += [ROOT/n for n in ['tools/record_generic_pet_preparation.py','tests/check_generic_pet_preparation.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]+[mainpath]
    result=dict(referenceVersion='3.3.93847',component='Current GenericPet buff preparation and recipient selection',installed=False,handlerEnabled=False,fullAiVerified=False,realMetadataIteratorsGeometryAccepted=False,newActorPersistenceAccepted=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=228,retainedMarkers=225,worldBoots=3,actualWorldReloads=2,contextsPerBoot=4682,fatalBoundaries=200,completeFunctions=4,completeFunctionBytes=2123,boundedLeafBytes=16,leafInstructionsBytes=11,stateVectorGrowths=192,stateVectorFinalFrees=32,exactDescriptorsAndServiceTraces=True,selfOwnerAndNestedRecipients=True,relatedStateGroups=True,frozenPriorFullClassMarkers=1184,newFullClassRun=False,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[736,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='55d316d',receipt='tools/reference/generic-pet-gather-native-evidence.json'),established=[
        'Four complete captured functions execute offline:1177-byte preparation,541-byte recipient callback,253-byte group append and152-byte cleanup. A separate16-byte bounded leaf contains11bytes forwarding registers/tail-jumping to the callback plus five unreachable INT3 bytes. It has no unwind entry;no unwind completeness is claimed. Native results match4682contexts per boot with200explicit fatal boundaries.',
        'Eligible type1/non-base descriptors clear their targets before metadata lookup. Valid nonnegative states are grouped by signed source group values;related state IDs are scanned in source order,including duplicates/negative groups. Self assignment checks every relevant state and precedes owner enumeration. Unprepared or unsupported descriptors retain the exact source fields.',
        'Captured callbacks preserve the signed squared-distance cutoff1024,static versus unsigned dynamic coordinates,unit-type filters,existing-target precedence,per-skill flags4/2,first eligible monster and player replacement behavior. Owner/nested enumeration membership/order is declared,while both callback bodies execute as captured. No geometric radius conversion or guessed pet traversal is introduced.',
        'The actual group append and cleanup bodies run within embedded group capacity. State-ID reserve/allocation/free are explicit memory services:192growth requests and32final frees are observed;real allocator behavior,group-vector growth and arbitrary reentrant enumeration are not certified. Initial oracle allocation-budget and unsigned-path fixture diagnostics are preserved;source instructions were never patched. No native failure occurred.',
        'All225prior native markers remain. Both builds have zero warnings/errors;70Python checks,573unchanged named eligibility decisions and106unchanged main installation hashes pass. Existing saved fixtures survive two actual reloads. This policy adds no actor persistence. Frozen417cd39retains1184full-class checks separately;no new full-suite run claimed. Three bounded read-only captures total3604read bytes,with no input/attach/security change/memory write.'
    ],remaining=[
        'Real skill/state metadata and state-presence,owner/nested iterators,geometry,downstream target search/rating/ordering,approach/follow executor and actual mode/skill effects remain required. Protected event/proc helpers and ordering are still unavailable;continue unaffected work.',
        'Learned Goatman/Tainted/Defiler casting,extended passives/events/Blood Oath/sunder,new Frenzy/Cleave and complete owner/facet/logout/death/respec/save behavior remain gated. Raw-item/Necromancer dependencies continue. Effects/audio and talent/hotkey icons remain separate placeholders;no class is complete.'
    ])
    out=ROOT/'tools/reference/generic-pet-preparation-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('GenericPet preparation:228native/70Python;4682current-code contexts per boot;real world bindings unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
