#!/usr/bin/env python3
"""Approved ordinary-chill corpse rule, actual Mage cold kills and saved bodies."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_cold_corpse_proposal import configuration as previous
STAGE=ROOT/'.local/class-skills/cold-corpse-rule-adoption'
def configuration():
    c=previous();c['stage']=STAGE;c['extra_families']+=('mage-basics','mage-missile')
    c['extra_inputs']+=tuple(ROOT/p for p in ('tests/check_cold_corpse_adoption.py','tools/build_skill_roadmap.py','tools/reference/ordinary-cold-corpse-approval.json','tools/reference/cold-corpse-proposal-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)));return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
