#!/usr/bin/env python3
"""Bind current descriptor gathering/search masks to native acceptance."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-gather'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
    _,prior=frozen_receipt('3755e41','tools/reference/generic-pet-actions-native-evidence.json');assert prior['verified']['passMarkers']==222
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==225
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    marker=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(marker,(ROOT/'.local/class-skills/generic-pet-actions/stats-native-checks.log').read_text()));after=Counter(re.findall(marker,log))
    assert sum(before.values())==222 and not before-after and after-before==Counter({'generic-pet-gather-check':1,'generic-pet-gather-reload-check':2})
    assert log.count('GenericPet gather:23348captured-current cases,4896three-helper compositions,1476fatal boundaries;exact descriptors,state108,service order and search masks pass;real metadata and actor AI remain gated')==3
    sourcepath=ROOT/'tools/reference/generic-pet-gather-source-evidence.json';source=json.loads(sourcepath.read_text());artifacts.append(sourcepath)
    assert source['verified']==dict(contexts=23348,byKind=dict(composed=4896,mask=18452),machineCodeBytes=1013,fatalBoundaries=1476)
    assert not source['processMemoryRead'] and source['sourceCodeExecutedInOfflineEmulator'] and not source['completeAiVerified'] and not source['handlerEnabled']
    assert source['constant']['values']==[0,0,-2147483648,-1] and source['inlineTable']==dict(rva='0x4b31d4',entries=12,bytes=48,executedAsData=True)
    for name,h in source['artifacts'].items():assert sha(ROOT/name)==h,name;artifacts.append(ROOT/name)
    lib=ROOT/source['emulatorLibrary']['path'];assert sha(lib)==source['emulatorLibrary']['sha256'];artifacts.append(lib)
    capture=ROOT/'.local/class-skills/warlock-runtime-reference/callees-live-skill-descriptor-20261029.json';r=json.loads(capture.read_text());artifacts.append(capture)
    assert r['directCallTargetsVerified'] and r['records'][0]['rva']=='0x4b4510' and r['records'][0]['bytes']==199 and r['records'][0]['allUnwindFragmentsCaptured'] and r['bytesRead']==958
    assert r['executableSha256']=='e1f5436e3d9687f644ef16938b1b183d1fdef434f18cf66d852cf68f48cc8936'
    assert not any(r[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
    for name,h in r['artifacts'].items():assert sha(ROOT/name)==h,name;artifacts.append(ROOT/name)
    parent=ROOT/r['parent'];artifacts.append(parent);selection=json.loads(parent.read_text())
    assert {row['rva'] for row in selection['records']}=={'0x4b42f0','0x4b30f0'} and all(row['allUnwindFragmentsCaptured'] for row in selection['records'])
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
        audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
    old=json.loads(subprocess.check_output(['git','show','3755e41:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[735,21]
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    mainpath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainpath.read_text());assert len(main['installedArtifacts'])==106
    for name,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/name)==h,name
    for name in ['class-skill-status.json','skill-effects.json','skill-icons.json']:
        p=ROOT/'data/Sanctuary/Balance'/name;assert sha(p)==hashlib.sha256(subprocess.check_output(['git','show','3755e41:'+str(p.relative_to(ROOT))],cwd=ROOT)).hexdigest();artifacts.append(p)
    artifacts += [STAGE/n for n in ['stats-inputs.json','stats-native-checks.log','run.log','source-run.log','python-tests.log']]
    artifacts += [p for p in (STAGE/'focused-fixtures').iterdir() if p.is_file()]
    artifacts += [ROOT/n for n in ['tools/record_generic_pet_gather.py','tests/check_generic_pet_gather.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]+[mainpath]
    result=dict(referenceVersion='3.3.93847',component='Current GenericPet descriptor gathering and search-mask policy',installed=False,handlerEnabled=False,fullAiVerified=False,realMetadataAndActorServicesAccepted=False,newActorPersistenceAccepted=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=225,retainedMarkers=222,worldBoots=3,actualWorldReloads=2,contextsPerBoot=23348,threeHelperCompositions=4896,searchMaskContexts=18452,capturedHelperBytes=1013,includedInlineTableBytes=48,fatalBoundaries=1476,exactDescriptorsStateAndServiceTraces=True,explicitUninitializedTailInputs=True,skillListAndMonsterSlots=True,frozenPriorFullClassMarkers=1184,newFullClassRun=False,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[735,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='3755e41',receipt='tools/reference/generic-pet-actions-native-evidence.json'),established=[
        'Three complete current captured helpers execute offline:538-byte gather,199-byte descriptor formatter and276-byte search-mask helper. Their1013captured bytes include48bytes of inline RVA table,which execute as data. The immutable initializer constant is bound to the pinned executable. Native results match23348contexts per boot,including4896compositions of all three captured bodies and1476explicit fatal boundaries.',
        'Gather clears the vector before adding its optional ordinary-mode descriptor. Source linked-list modes differ in entry68/76 filters;monster-slot mode performs eight lookups,validates selector/count when reached,uses the first matching entry68=-1,and can append repeated IDs. Signed skill IDs,positive-level gating,level-bonus flag,descriptor fields and wrapped field108 updates are preserved. Missing monster metadata is unsupported,not an invented empty skill list.',
        'Source leaves bytes38/39 unwritten. Distinct declared base/skill stack-tail inputs preserve their raw values without assigning gameplay meaning. Fixed preallocated vector append is a copying service;allocation/reentrancy and arbitrary stale native memory are not certified. Source fatal boundaries stop before process termination;managed code reports unsupported and retains already-produced vector/state.',
        'The previous Range label denotes target-search categories rather than a distance. SearchMask preserves all256byte categories,ordinary-mode suppression of skill lookup,skill flag1 contribution,category-dependent clearing and OR aggregation. The old orchestration API remains unchanged until downstream search binding. Unit byte445 remains neutral raw input;no difficulty semantics inferred.',
        'All222predecessor native markers remain. Both builds have zero warnings/errors;70Python checks,573unchanged named eligibility decisions and106unchanged main installation hashes pass. Existing saved fixtures survive two actual reloads. This pure policy adds no actor persistence. Frozen417cd39retains1184full-class checks separately;no new full-suite run claimed. The199-byte formatter was captured through direct-call provenance with958read bytes,no process input/attach/security change/memory write.'
    ],remaining=[
        'Real monster/skill metadata,skill rank and flag4 value providers,downstream target search,preparation/rating/ordering,approach/follow executor and actor scheduling/lifecycle remain unfinished. Protected event/proc helpers and ordering remain explicit unavailable references;continue unaffected work.',
        'Learned Goatman/Tainted/Defiler casting,extended passives/events/Blood Oath/sunder,new Frenzy/Cleave and complete owner/facet/logout/death/respec/save behavior remain gated. Raw-item/Necromancer dependencies continue. Effects/audio and talent/hotkey icons remain separate placeholders;no class is complete.'
    ])
    out=ROOT/'tools/reference/generic-pet-gather-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('GenericPet gather:225native/70Python;23348current-code contexts per boot;real metadata and actor AI unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
