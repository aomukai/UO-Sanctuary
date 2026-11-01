#!/usr/bin/env python3
"""Approved Fire upgrades with retained Iron/Blood/Mage/player/sentry scopes."""
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    base=configuration('check_iron_golem_spectral.py')
    base['stage']=ROOT/'.local/class-skills/fire-upgrades'
    base['extra_families']=('fire-upgrades',*base['extra_families'])
    base['extra_live_commands']=('fire-upgrades-live',*base['extra_live_commands'])
    base['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tests/check_iron_golem_spectral.py')
    check(**base)
