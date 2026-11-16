#!/usr/bin/env python3
"""Fury parent and source0 child ordered delivery retain existing burst and path controllers."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_lightning_bolt_contact import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/lightning-fury-contact'
def configuration():
    c=prior();c.update(stage=STAGE);add_fixture(c,'LightningFury')
    c.update(extra_families=('lightning-fury-contact',*c['extra_families']),extra_target_input_commands=(*c['extra_target_input_commands'],'lightning-fury-contact-right','lightning-fury-contact-quick'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/review_lightning_fury_contact.py',ROOT/'tools/reference/lightning-fury-contact-source-review.json',ROOT/'tools/reference/lightning-bolt-contact-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
