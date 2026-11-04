#!/usr/bin/env python3
"""Retain the curse/aura cohort with recipient-scoped pulse winners."""
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT, check
if __name__=='__main__':
    config=configuration('check_elite_amplify.py')
    config['stage']=ROOT/'.local/class-skills/elite-aura-winner'
    config['extra_checks']=(*config.get('extra_checks',()),'elite-aura-winner-check')
    config['extra_inputs']=(*config.get('extra_inputs',()),Path(__file__).resolve(),ROOT/'tests/check_elite_amplify.py')
    check(**config)
