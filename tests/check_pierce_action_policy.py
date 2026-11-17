#!/usr/bin/env python3
"""Saved candidate owner-index mapping across all fourteen Amazon ranged talents."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_pierce_source_seed import configuration as prior
STAGE=ROOT/'.local/class-skills/pierce-action-policy'
def configuration():
    c=prior();c.update(stage=STAGE,extra_families=('pierce-action',*c['extra_families']),extra_target_input_commands=('pierce-action-right','pierce-action-quick'))
    c['extra_inputs']=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/pierce-source-seed-native-evidence.json')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
