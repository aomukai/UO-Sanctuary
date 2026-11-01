#!/usr/bin/env python3
"""Verify compiled Mage inspection with the retained mechanical/save regression."""
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    base=configuration('check_mage_basics.py')
    base['stage']=ROOT/'.local/class-skills/mage-inspection'
    base['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tests/check_mage_basics.py')
    check(**base)
