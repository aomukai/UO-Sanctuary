#!/usr/bin/env python3
"""Released throwing hit procs continue across process reloads without a new schema."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_thrown_contact import configuration as prior
STAGE=ROOT/'.local/class-skills/thrown-proc-flight'
def configuration():
    c=prior();c.update(stage=STAGE,extra_families=('thrown-proc-flight',*c['extra_families']),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/thrown-contact-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
