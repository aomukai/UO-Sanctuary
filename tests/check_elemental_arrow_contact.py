#!/usr/bin/env python3
"""Fire/Cold Arrow conversion impacts share the qualified weapon missile packet."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_multiple_shot_contact import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/elemental-arrow-contact'
def configuration():
    c=prior();c.update(stage=STAGE)
    for n in ('FireArrow','ColdArrow'):add_fixture(c,n)
    c.setdefault('extra_wait_results',{})['cold-arrow-check']='cold-arrow-state-result'
    c.update(extra_families=('elemental-arrow-contact',*c['extra_families']),extra_target_input_commands=(*c['extra_target_input_commands'],*(f'elemental-arrow-{a}-{m}'for a in ('fire','cold')for m in ('right','quick'))),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/review_elemental_arrow_contact.py',ROOT/'tools/reference/elemental-arrow-contact-source-review.json',ROOT/'tools/reference/multiple-shot-contact-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
