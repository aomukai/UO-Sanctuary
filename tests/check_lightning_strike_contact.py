#!/usr/bin/env python3
"""Amazon chain delivery with retained shared Sorceress/Mage controllers."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_charged_strike_contact import configuration as prior
STAGE=ROOT/'.local/class-skills/lightning-strike-contact'
def configuration():
    c=prior();c.update(stage=STAGE)
    c['extra_families']+=('lightning-strike','player-chain','mage-chain')
    c['extra_checks']+=('chain-child-search-check',)
    c['extra_files'].update({'lightning-strike-damage-source.json':ROOT/'.local/class-skills/lightning-strike-damage-source/cases.json','chain-child-filter-source.json':ROOT/'.local/class-skills/chain-child-filter-source/cases.json'})
    c['extra_wait_results']['player-chain-reload-check']='player-chain-reload-result'
    c['extra_connected_checks']=(*c.get('extra_connected_checks',()),'player-chain-reload-check')
    c.update(extra_families=('lightning-strike-contact',*c['extra_families']),extra_target_input_commands=(*c['extra_target_input_commands'],'lightning-strike-right','lightning-strike-quick','lightning-strike-contact-right','lightning-strike-contact-quick'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/review_lightning_strike_contact.py',ROOT/'tools/reference/lightning-strike-contact-source-review.json',ROOT/'tools/reference/charged-strike-contact-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
