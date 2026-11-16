#!/usr/bin/env python3
"""Three captured Amazon lightning thrusts with retained bolt/chain controls."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_hellslayer import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/amazon-thrust-contact'
def configuration():
    c=prior();c.update(stage=STAGE);c.setdefault('extra_manifest_names',{});c.setdefault('extra_wait_results',{})
    for name in ('PowerStrike','ChargedStrike'):add_fixture(c,name)
    c['extra_families']+=('lightning-strike',)
    c['extra_manifest_names']['lightning-strike']='lightning-strike-fixtures.json'
    c['extra_files'].update({'chain-child-filter-source.json':ROOT/'.local/class-skills/chain-child-filter-source/cases.json','lightning-strike-damage-source.json':ROOT/'.local/class-skills/lightning-strike-damage-source/cases.json'})
    c['extra_wait_results'].update({'charged-strike-check':('charged-strike-native-bolts','charged-strike-expiry-start','charged-strike-expiry','charged-strike-result'),'charged-strike-reload-check':'charged-strike-reload-result'})
    c.update(extra_families=('amazon-thrust-contact',*c['extra_families']),extra_target_input_commands=(*c['extra_target_input_commands'],*(f'amazon-thrust-contact-{a}-{mode}'for a in ('powerstrike','chargedstrike','lightningstrike')for mode in ('right','quick')),'lightning-strike-right','lightning-strike-quick'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/review_amazon_thrust_contact.py',ROOT/'tools/reference/impale-contact-native-evidence.json',ROOT/'tools/reference/amazon-thrust-contact-source-review.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
