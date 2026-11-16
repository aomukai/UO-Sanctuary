#!/usr/bin/env python3
"""Magic Arrow ordered impact,captured armor and actual item hit-proc delivery."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_thrown_proc_flight import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/magic-arrow-contact'
def configuration():
    c=prior();c.update(stage=STAGE)
    for name in ('MagicArrow','BowLaunch'):add_fixture(c,name)
    c.update(extra_families=('magic-arrow-contact',*c['extra_families']),extra_target_input_commands=(*c['extra_target_input_commands'],'magic-arrow-contact-right','magic-arrow-contact-quick'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/review_magic_arrow_contact.py',ROOT/'tools/reference/magic-arrow-contact-source-review.json',ROOT/'tools/reference/thrown-proc-flight-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
