#!/usr/bin/env python3
"""Approved basic Mage spells/Mastery/triples and retained class scopes."""
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    base=configuration('check_fire_upgrades.py')
    base['stage']=ROOT/'.local/class-skills/mage-basics'
    base['extra_families']=('mage-basics',*base['extra_families'])
    base['extra_live_commands']=('mage-basics-live',*base['extra_live_commands'])
    base['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tests/check_fire_upgrades.py')
    check(**base)
