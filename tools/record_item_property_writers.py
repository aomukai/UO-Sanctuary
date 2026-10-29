#!/usr/bin/env python3
"""Bind raw original-item numeric property writers to scoped source/native evidence."""
import argparse
import hashlib
import json
import re
import subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/item-property-writers'
FROZEN='1e9e112'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def historical(name):return subprocess.check_output(['git','show',FROZEN+':'+name],cwd=ROOT)
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
    _,prior=frozen_receipt(FROZEN,'tools/reference/item-stat-values-native-evidence.json')
    assert (prior['verified']['passMarkers'],prior['verified']['frozenPriorFullClassMarkers'])==(216,1184)
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==219
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    pattern=r'\] ([A-Za-z0-9-]+): passed'
    before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/item-stat-values/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
    assert sum(before.values())==216 and not before-after and after-before==Counter({'item-property-writers-check':1,'item-property-writers-reload-check':2})
    assert log.count('Item property writers:45840complete-source dispatch/base-reset/ethereal contexts and exact write traces pass;actual original armor/sword project saved raw properties without reroll,retain FP256life-per-level and explicit socket omission;live NPC effects remain gated')==1
    assert log.count('Item property writers:saved original armor/sword retain identity,private ownership,complete rolls and raw numeric projection through actual reload;socket writer remains explicitly omitted')==2
    sourcepath=ROOT/'tools/reference/item-property-writers-source-evidence.json';source=json.loads(sourcepath.read_text());artifacts.append(sourcepath)
    assert source['verified']==dict(propertyRows=284,propertyFields=7952,supportedNumericPlans=191,sourceCases=45840,completeRoutines=15,ownedBases=4,ownedBaseFields=32)
    assert len(source['compiledUnchangedRoutineHashes'])==15 and not source['currentExecutableParity'] and not source['nativeConsumerAccepted']
    for group in ['inputs','artifacts']:
        for name,digest in source[group].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
    assert not (ROOT/'.local/class-skills/item-property-writers-source/sanitizer.log').read_bytes()
    assert not (ROOT/'.local/class-skills/item-property-writers-source/compiler.log').read_bytes()
    saved=json.loads((STAGE/'focused-fixtures/item-property-writers-fixtures.json').read_text());assert len(saved)==2
    assert len({x['Owner'] for x in saved})==1 and len({x['Pet'] for x in saved})==len({x['Item'] for x in saved})==2
    for row in saved:
        g=json.loads(row['Gear']);assert g['Identified'] and row['Life256']>0 and row['Defense']>=0
    assert saved[0]['LifeBonus256']==11840 and saved[0]['Armor']==430 and saved[0]['Omissions']==['sock:Numeric writer is not integrated']
    assert json.loads(saved[1]['Gear'])['BaseCode']=='bsd' and (saved[1]['Minimum'],saved[1]['Maximum'])==(7,14) and saved[1]['Omissions']==[]
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
        audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
    old=json.loads(historical('tools/reference/named-item-runtime-audit.json'))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions'] and [len(a['InputHashes']) for a in audits]==[733,21]
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    mainpath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainpath.read_text());assert len(main['installedArtifacts'])==106
    for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
    for name in ['class-skill-status.json','skill-effects.json','skill-icons.json']:
        p=ROOT/'data/Sanctuary/Balance'/name;assert sha(p)==hashlib.sha256(historical(str(p.relative_to(ROOT)))).hexdigest();artifacts.append(p)
    artifacts += [STAGE/n for n in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log']]
    artifacts += list((STAGE/'focused-fixtures').glob('*fixtures.json'))
    artifacts += [ROOT/n for n in ['tools/record_item_property_writers.py','tests/check_item_property_writers.py','tools/build_equipment_property_plans.py','data/Sanctuary/Balance/equipment-property-plans.json','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]+[mainpath]
    plans=json.loads((ROOT/'data/Sanctuary/Balance/equipment-property-plans.json').read_text());assert len(plans)==284 and sum(p['Supported'] for p in plans)==191
    backlog=json.loads((ROOT/'tools/reference/item-property-integration-backlog.json').read_text())['rawNpcPropertyWriters'];assert len(backlog['unsupported'])==93
    result=dict(referenceVersion='3.3.93847',component='Saved original-item raw numeric property writers',installed=False,handlerEnabled=False,currentExecutableParity=False,fullNpcStatGraphAccepted=False,liveNpcStatApplicationAccepted=False,sourceNpcLevelMappingAccepted=False,completeItemBirthAccepted=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],
        verified=dict(passMarkers=219,retainedMarkers=216,worldBoots=3,actualWorldReloads=2,propertyRows=284,ownedPropertyFields=7952,numericPlans=191,explicitOtherPlans=93,sourceCases=45840,completeRoutines=15,ownedBases=4,ownedBaseFields=32,exactBasePropertyWrites=True,exactOrderedServices=True,savedRawPropertiesNoReroll=True,rawLifePerLevelCoefficient256=2560,recipient37LifeBonus256=11840,actualArmorDefense=430,plainWeapon=[7,14],originalItems=2,explicitSocketOmission=True,liveNpcLifeDefenseUnchanged=True,actualReloadsRetainOriginals=True,frozenPriorFullClassMarkers=1184,newFullClassRun=False,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[733,21],unchangedMainInstallationArtifacts=106),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit=FROZEN,receipt='tools/reference/item-stat-values-native-evidence.json'),
        established=[
            'The numeric property policy preserves first-function result sharing,grade/base resets,flat damage channel clamping,enhanced damage fallback,shifted raw writes,poison-source counting and explicit ethereality. Metadata covers all seven func/stat/set/val slots for284properties with7952owned comparisons and32selected base fields. Fifteen complete unchanged source routines match45840dispatch/base-reset/ethereal cases and exact write traces across four declared owned base/type fixtures. Func4is compiled but no current property plan selects it.',
            'Source and native contexts cover affix0/grade1/unique3,five fixed values,allocation failure and optional post-property ethereality. Bounded RNG/allocation/type answers are explicit fixture services. Arbitrary roll ranges,all modifier combinations,full creation order and current executable parity are not certified. The unresolved dmg%stat token belongs to an unsupported extra-skill-dmg%-rand plan and remains explicit.',
            'The original-item adapter reads saved ordinary/named rolls into a nonextended property list attached to the original transient item graph. It cannot reroll the original. All191numeric plans are implemented;all93other raw-writer plans are listed separately from existing player support. Unsupported functions,unrepresented zero-parameter/shared-zero fallback rolls,quality/crafting/material/socket contents and conditional named-piece stats return explicit omissions rather than invented values.',
            'Actual original Spirit Forge armor retains raw item_hp_perlevel coefficient2560and yields11840FP256life at declared recipient37,distinct from owner47/item99. Its source base-defense reset/local430armor matches the existing reader for this item. Socket writing is explicitly omitted. A plain original sword retains7-14. Numeric contributions detach cleanly. Both originals retain serials,private parents,complete rolls and projections through two actual reloads without changing live creature life/defense or rerolling. Existing persistent trained minions are storage fixtures,not golem actors.',
            'All216prior focused markers remain alongside three new checks. Both builds have zero warnings/errors;70Python checks,573unchanged named eligibility decisions and106unchanged main installation hashes pass. The earlier417cd39package separately retains1184full-class checks;this isolated writer module has no new full-suite claim. The initial unresolved-token harness input is retained in diagnostics;there was no failed native attempt.'
        ],remaining=[
            'Full base/item birth and creation/ethereal ordering,modern low-ED fallback/repeated armor-reset/base-ED parity,special/skill/event/socket writers and real native NPC stat effects remain unfinished. Base arrays,type metadata and recipient level remain declared inputs. Multi-item ordering,cycles,reentrancy and automatic graph/actor lifecycle still need integration. Extreme/negative/overflowing ranges are not certified.',
            'Complete Iron Golem casting,body/AI,cap/placement and owner/facet/logout/death/respec/bound-item lifecycle remain gated. Skill-dependent item effects stay in the approved deferred ledger. Effects/audio and talent/hotkey icons remain separate placeholders;no class is complete.'
        ])
    path=ROOT/'tools/reference/item-property-writers-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Item property writers:219focused/70Python;191numeric plans/45840source cases;two original-item reloads;live NPC effects unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
