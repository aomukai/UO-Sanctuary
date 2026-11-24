#!/usr/bin/env python3
"""T29 War Cry producer capture and live potion with prior physical/source/save scope."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_warcry_link import configuration as prior
STAGE=ROOT/'.local/class-skills/warcry-potion'
def configuration():
    c=prior();c['stage']=STAGE;c['extra_families']+=('warcry-potion',)
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/warcry-link-native-evidence.json',ROOT/'tools/reference/rules/t29-combat-potions.md')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
