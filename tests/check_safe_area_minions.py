#!/usr/bin/env python3
"""Approved safe-area parking and retained Necromancer mechanical scopes."""
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    base=configuration('check_mage_inspection.py')
    base['stage']=ROOT/'.local/class-skills/safe-area-minions'
    base['extra_families']=('safe-area-minion',*base['extra_families'])
    base['extra_live_commands']=('safe-area-minion-live',*base['extra_live_commands'])
    base['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tests/check_mage_inspection.py')
    check(**base)
