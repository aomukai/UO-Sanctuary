#!/usr/bin/env python3
"""Bind Blessed Aim's unique checks and reused Amazon/aura evidence honestly."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_might_aura import passed
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/blessed-aim-aura';sha=inventory.sha
PARENT='1784b5cacc601ba40c505ccf07f342ea643177c5'
def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-1.json',STAGE/'birth-inputs.json')
    log,counts=passed(STAGE/'birth-native-checks.log',499);assert 'pass across3boots/2reloads' in (STAGE/'focused-run-1.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'));from check_blessed_aim_aura import configuration
    c=configuration();assert (len(c['extra_families']),len(c['extra_checks']),len(c['extra_target_input_commands']))==(28,11,53)
    for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
    for name in c['extra_checks']:assert counts[name]==3,name
    for name in c['extra_target_input_commands']:
        assert counts[name+'-check']==counts[name+'-result']==3,name
        assert log.count(' '+name+(': acknowledged' if name in c['extra_instant_input_commands'] else ': windup'))==3,name
    assert log.count('Blessed Aim:hard-only inactive passive;real Tarnhelm active effective rank;')==3
    old=ROOT/'.local/class-skills/defiance-aura';prior=json.loads((old/'birth-inputs.json').read_text())
    for path,h in tested['sourceInputs'].items():
        if path.startswith(('src/','shared/')):assert prior['sourceInputs'][path]==h,path
    assert tested['copiedData']==prior['copiedData']
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][path]==prior['artifacts'][path]
    retained=dict(counts)
    for name in ('blessed-aim-aura-check','blessed-aim-aura-right-check','blessed-aim-aura-right-result','blessed-aim-aura-quick-check','blessed-aim-aura-quick-result','amazon-penetrate-check','amazon-penetrate-reload-check'):retained.pop(name)
    assert retained==passed(old/'birth-native-checks.log',481)[1]
    saved=json.loads((STAGE/'paladin-aura-checks.json').read_text());owner=saved['Owners'][2]
    assert any(e['Owner']==owner and e['Ability']==105 for e in saved['State']['Emitters'])
    assert any(l['Source']==owner and l['Target']==saved['Ally'] and l['Ability']==105 and l['Rank']==1 and l['Percent']==75 for l in saved['State']['Leases'])
    amazon=json.loads((STAGE/'amazon-penetrate-fixtures.json').read_text());assert all(amazon[k]>0 for k in ('Owner','Weapon','Helm','Amulet','Rating'))
    references=['defiance-aura-native-evidence.json','might-callback-source-evidence.json','amazon-penetrate-engine-evidence.json','amazon-penetrate-native-evidence.json']
    for name in references:assert (ROOT/'tools/reference'/name).read_bytes()==historical('tools/reference/'+name)
    ar=json.loads((ROOT/'tools/reference/amazon-penetrate-engine-evidence.json').read_text())['probe'];assert sha(ROOT/ar['dataPath'])==ar['dataSha256']
    for path,h in json.loads((ROOT/'tools/reference/might-callback-source-evidence.json').read_text())['currentTables'].items():assert sha(ROOT/path)==h,path
    tables=ROOT/'tools/reference/d2r-named-equipment/json';row=json.loads((tables/'skills.json').read_text())['108'];state=json.loads((tables/'states.json').read_text())['blessedaim'];stat=json.loads((tables/'itemstatcost.json').read_text())['item_tohit_percent']
    assert (row['srvdofunc'],row['aurafilter'],row['Param3'],row['Param4'],row['Param8'],row['mana'],state['*ID'],stat['*ID'],stat.get('direct',0))==(65,73731,75,15,5,0,40,119,0)
    assert row['passivecalc1']=="skill('Blessed Aim'.blvl) * par8"
    for part in ('content','integration'):
        s=(STAGE/(part+'-build-1.log')).read_text();assert all(x in s for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    a=json.loads((STAGE/'audit-inputs.json').read_text());assert a['artifacts']==tested['artifacts'] and a['copiedData']==tested['copiedData']
    s=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in s and ' ERR]' not in s and sum(markers(STAGE/'audit-native-checks.log').values())==4
    for name,count in [('named-item-runtime-audit.json',911),('first-loop-affix-audit.json',21)]:
        n=json.loads((STAGE/name).read_text());assert n==json.loads((ROOT/'tools/reference'/name).read_text()) and len(n['InputHashes'])==count and n['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,h in n['InputHashes'].items():assert sha(ROOT/path)==h,path
        o=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in n.items() if k not in ignore}=={k:v for k,v in o.items() if k not in ignore}
    s=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in s and '\nOK\n' in s and 'FAILED' not in s
    s=(STAGE/'catalogue-checks.log').read_text()
    for text in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert text in s
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==h,path
    docs=['paladin-blessed-aim-preflight.md','paladin-blessed-aim-checkpoint.md','paladin-resource-aura-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md']
    files=[Path(__file__).resolve(),ROOT/'tests/check_blessed_aim_aura.py',*(ROOT/'tools/reference'/n for n in references),*(ROOT/'docs'/n for n in docs),
        *(STAGE/n for n in ('birth-inputs.json','birth-native-checks.log','pre-build-1.json','focused-run-1.log','content-build-1.log','integration-build-1.log','paladin-aura-checks.json','amazon-penetrate-fixtures.json','audit-inputs.json','audit-native-checks.log','audit-run-1.log','python-tests.log','catalogue-checks.log')),
        *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Blessed Aim active/passive AR and reused Amazon coexistence',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(focusedNativeMarkers=499,retainedFocusedMarkers=481,previousFullMarkers=1121,boots=3,reloads=2,saves=3,newServerWireActions=6,auditMarkers=4,pythonTests=71,unchangedInstalledArtifacts=106),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Production C#/shared sources and gameplay data are unchanged from accepted Defiance. A separately fingerprinted clean build passes499focused markers,retaining481previous markers and adding the existing AmazonPenetrate fixture. Prior1121-marker full Might acceptance remains separate evidence;no new full run or byte-identical DLL claim across output/commit changes.',
            'Inactive Blessed Aim uses hard points only;real Tarnhelm raises active effective rank without raising that passive. Active aura suppresses the inactive passive,refreshes separately from training,and preserves stronger recipient leases when self switches/lower-rank reselects. Native Ogre host-adapter hit boundaries distinguish active and passive AR for both melee/projectiles. Zero-mana selection and six actual right/quick inputs pass.',
            'Existing Amazon fixture executes120source AR statement comparisons and20rank goldens each boot,real gear and Enchant/Fanaticism/Blessed Aim coexistence without removing Penetrate,and original gear/rank/AR reloads. Existing original Blessed Aim emitter/lease reloads resume refresh with explicit inspection clock holds/rearming;no uninterrupted short-lifetime claim.',
            'Pinned state40/stat119is non-direct;active75+15perrank and inactive5perhardpoint match current tables. Complete generic non-direct callback/list/modifier proof and published modern Penetrate coexistence evidence are reused within their declared scopes;no new compiled Blessed Aim callback matrix or modern executable certificate. Four audits/71Python/573named/240catalogue/generators pass;106installed artifacts unchanged.'],
        remaining=['Complete modern active/passive stat graph,search/events/owner/modes/item-granted/species/client behavior and native world projections remain acceptance limits.',
            'Final aura overlays/audio and talent/right/quick icons remain separate placeholders. No class completion,deployment or save reset.'])
    output=ROOT/'tools/reference/blessed-aim-aura-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Blessed Aim:499focused/481retained markers,6new instant wire actions,hard/soft passive/native hit and reused Amazon coexistence/reloads;4audits/71Python/106installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
