#!/usr/bin/env python3
"""Real Stealskull co-op hits beside retained Sentinel/Fury/Shield/link/save contracts."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_blade_link import configuration as prior
import check_sentinel_stealskull_link as actual
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/sentinel-stealskull-retained'
def configuration():
    c=prior();c['stage']=STAGE;actual.STAGE=STAGE
    c['extra_wire_callbacks']=(*c.get('extra_wire_callbacks',()),actual.wires)
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],Path(__file__).resolve(),*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
