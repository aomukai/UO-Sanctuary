#!/usr/bin/env python3
"""Charged Strike elemental bolt packets,retained motion/melee and original contexts."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_plague_javelin_contact import configuration as prior
from check_charged_strike_velocity import configuration as motion
from check_druid_planned_acceptance import merge
STAGE=ROOT/'.local/class-skills/charged-strike-contact'
def configuration():
    c=prior();merge(c,motion());c.update(stage=STAGE)
    c.update(extra_families=('charged-strike-contact',*c['extra_families']),extra_target_input_commands=(*c['extra_target_input_commands'],'charged-strike-contact-right','charged-strike-contact-quick'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/review_charged_strike_contact.py',ROOT/'tools/reference/charged-strike-contact-source-review.json',ROOT/'tools/reference/plague-javelin-contact-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
