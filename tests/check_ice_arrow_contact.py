#!/usr/bin/env python3
"""Ice Arrow packet freeze retains Dragon duration and standalone freeze controls."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_elemental_arrow_contact import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/ice-arrow-contact'
def configuration():
    c=prior();c.update(stage=STAGE)
    add_fixture(c,'IceArrow');add_fixture(c,'DragonColdDuration',family=False);c['extra_families']+=('dragon-cold-duration',)
    add_fixture(c,'FreezeState',family=False);c['extra_checks']=(*c.get('extra_checks',()),'freeze-state-check');c.setdefault('extra_wait_results',{})['ice-arrow-check']='ice-arrow-state-result'
    c.update(extra_families=('ice-arrow-contact',*c['extra_families']),extra_target_input_commands=(*c['extra_target_input_commands'],'ice-arrow-contact-right','ice-arrow-contact-quick'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/review_ice_arrow_contact.py',ROOT/'tools/reference/ice-arrow-contact-source-review.json',ROOT/'tools/reference/elemental-arrow-contact-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
