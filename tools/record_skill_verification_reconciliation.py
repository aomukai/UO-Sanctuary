#!/usr/bin/env python3
"""Reconcile ten stale build-only labels using preserved native evidence."""
import argparse
from collections import Counter
import json,subprocess
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
ROOT=e.ROOT
BASE='d40b0203'
# Each entry binds actual completed marker results and unchanged mechanic/test
# sources. It makes no claim of a current-head rerun or full dependency parity.
SPECS={
 'barbarian.finditem':('barbarian-planned-acceptance',{'find-item-result':3,'find-item-reload-check':2,'find-item-snapshot-check':3},('BarbarianFindItem.cs','FindItemChecks.cs','FindItemPrimitiveChecks.cs')),
 'barbarian.warcry':('barbarian-planned-acceptance',{'warcry-result':3,'warcry-reload-check':2},('BarbarianWarCry.cs','WarCryChecks.cs')),
 'druid.hurricane':('druid-planned-acceptance',{'hurricane-check':1,'hurricane-reload-check':2,'hurricane-input-result':3},('DruidHurricane.cs','DruidWeather.cs','HurricaneChecks.cs')),
 'druid.armageddon':('druid-planned-acceptance',{'armageddon-check':1,'armageddon-reload-check':2,'armageddon-input-result':3},('DruidArmageddon.cs','DruidWeather.cs','ArmageddonChecks.cs')),
 'druid.rabies':('druid-planned-acceptance',{'druid-rabies-check':1,'druid-rabies-reload-check':2,'druid-rabies-infection-reload-check':2,'druid-rabies-lifecycle-reload-check':2,'rabies-right-result':3,'rabies-quick-result':3},('DruidRabies.cs','DruidRabiesPackets.cs','DruidRabiesEmitterClock.cs','DruidRabiesInfections.cs','DruidRabiesChecks.cs','DruidRabiesInfectionChecks.cs','DruidRabiesLifecycleChecks.cs')),
 'druid.poisoncreeper':('druid-planned-acceptance',{'creeper-learned-reload-check':2,'creeper-learned-live-result':3,'creeper-learned-right-result':3,'creeper-learned-quick-result':3,'creeper-balance-reload-check':2},('DruidCreeperCasting.cs','DruidCreeperStats.cs','DruidCreeperPoison.cs','CreeperLearnedChecks.cs','CreeperBalanceChecks.cs')),
 'druid.carrionvine':('druid-planned-acceptance',{'recycler-learned-reload-check':2,'recycler-recovery-reload-check':2,'carrion-right-result':3,'carrion-quick-result':3},('DruidRecyclerCasting.cs','DruidRecyclerRecovery.cs','DruidRecyclerStats.cs','RecyclerLearnedChecks.cs','RecyclerRecoveryChecks.cs')),
 'druid.solarcreeper':('druid-planned-acceptance',{'recycler-learned-reload-check':2,'recycler-recovery-reload-check':2,'solar-right-result':3,'solar-quick-result':3},('DruidRecyclerCasting.cs','DruidRecyclerRecovery.cs','DruidRecyclerStats.cs','RecyclerLearnedChecks.cs','RecyclerRecoveryChecks.cs')),
 'warlock.bladewarp':('warlock-weapons-acceptance',{'blade-warp-check':1,'blade-warp-reload-check':2,'blade-warp-left-result':3,'blade-warp-right-result':3,'blade-warp-quick-result':3},('BladeWarp.cs','BladeWarpChecks.cs')),
 'warlock.sigildeath':('warlock-chaos-acceptance',{'sigil-death-check-result':1,'sigil-death-reload-check-result':2},('SigilDeathProposal.cs','SigilDeathEvents.cs','SigilDeathChecks.cs')),
}

def record(check=False):
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert before|{'skills':after['skills']}==after
    changed=[]
    for old,new in zip(before['skills'],after['skills'],strict=True):
        assert old['talentId']==new['talentId']
        if old!=new:
            assert old['verification']=='build-only' and new['verification']=='partial-native-coverage'
            assert old|{'verification':new['verification']}==new
            changed.append(old['talentId'])
    assert set(changed)==set(SPECS) and len(changed)==10
    assert len(after['skills'])==240 and all(r['implementation']=='handler-present' and r['verification']=='partial-native-coverage' and not r['complete'] for r in after['skills'])
    assert after['completedClasses']==[]
    cohorts={};entries={};files=set()
    for talent,(stage,subset,suffixes) in SPECS.items():
        folder=ROOT/'.local/class-skills'/stage
        receipt=ROOT/'tools/reference'/(stage+'-native-evidence.json')
        prior=e.load(receipt);manifest=folder/'birth-inputs.json';logfile=folder/'birth-native-checks.log'
        for p in (manifest,logfile):assert e.sha(p)==prior['files'][str(p.relative_to(ROOT))],p
        born=e.load(manifest);actual=markers(logfile);assert actual==Counter(prior['markers']) and sum(actual.values())==prior['nativeMarkers']
        text=logfile.read_text();assert ': failed' not in text and ' ERR]' not in text
        assert text.count('Writing world save snapshot done')==text.count('Shutting down')==3
        for name,count in subset.items():assert actual[name]==count,(talent,name)
        sources={p:h for p,h in born['sourceInputs'].items() if p.endswith('.cs') and Path(p).name in suffixes}
        assert {Path(p).name for p in sources}==set(suffixes)
        for p,h in sources.items():assert e.sha(ROOT/p)==h,p
        for kind in ('Content','Integration'):
            artifact=folder/kind.lower()/f'Sanctuary.{kind}.dll'
            assert e.sha(artifact)==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll'];files.add(artifact)
        files.update((receipt,manifest,logfile));files.update(ROOT/p for p in sources)
        cohorts[stage]=dict(receipt=str(receipt.relative_to(ROOT)),receiptSha256=e.sha(receipt),nativeMarkers=prior['nativeMarkers'],boots=3,saves=3,reloads=2,artifacts=born['artifacts'])
        entries[talent]=dict(cohort=stage,verifiedMarkerSubset=subset,unchangedRelevantSources=sources,verification='partial-native-coverage')
    files.update(ROOT/p for p in ('tools/record_skill_verification_reconciliation.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','docs/class-skill-verification-reconciliation.md','tools/reference/consume-learned-client-evidence.json'))
    result=dict(component='Historical native coverage label reconciliation',referenceVersion='3.3.93847',baselineCommit=BASE,changedTalents=changed,onlyRuntimeFieldChanged='verification',beforeBuildOnly=10,afterBuildOnly=0,partialNativeTalents=240,handlers=240,newNativeRuns=0,newBuilds=0,gameplayChanged=False,classCertified=False,goalComplete=False,cohorts=cohorts,entries=entries,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Preserved completed native cohorts and unchanged relevant per-talent source/test hashes justify partial-native-coverage,not current-head or whole-dependency certification. No old receipt is regenerated. Remaining strings,complete flags,completedClasses,content scope and source reference are unchanged. No mechanical gap,unverified D2R branch,graphical acceptance or asset is declared resolved by metadata.')
    e.write('class-skill-verification-reconciliation.json',result,sorted(files),check)
    print('Ten labels reconciled from four preserved cohorts;240partial-native,0build-only;no new native run/build or completion claim.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
