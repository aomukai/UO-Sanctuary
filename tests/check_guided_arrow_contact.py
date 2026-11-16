#!/usr/bin/env python3
"""Guided Arrow auto-hit impact reuses shared weapon missile packet and controller."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_ice_arrow_contact import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/guided-arrow-contact'
def configuration():
    c=prior();c.update(stage=STAGE);add_fixture(c,'GuidedArrow')
    c.update(extra_families=('guided-arrow-contact',*c['extra_families']),extra_target_input_commands=(*c['extra_target_input_commands'],'guided-arrow-contact-right','guided-arrow-contact-quick'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/review_guided_arrow_contact.py',ROOT/'tools/reference/guided-arrow-contact-source-review.json',ROOT/'tools/reference/ice-arrow-contact-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
