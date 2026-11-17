#!/usr/bin/env python3
"""Poison parent and source0 cloud ordered delivery retain existing trail and poison clocks."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_poison_javelin_contact import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/plague-javelin-contact'
def configuration():
    c=prior();c.update(stage=STAGE);add_fixture(c,'PlagueJavelin')
    c.update(extra_families=('plague-javelin-contact',*c['extra_families']),extra_target_input_commands=(*c['extra_target_input_commands'],'plague-javelin-contact-right','plague-javelin-contact-quick'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/review_plague_javelin_contact.py',ROOT/'tools/reference/plague-javelin-contact-source-review.json',ROOT/'tools/reference/poison-javelin-contact-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
