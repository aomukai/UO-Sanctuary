#!/usr/bin/env python3
"""Actual right/quick input for the twelve remaining Paladin auras."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_assassin_planned_acceptance import paced_clients

INPUTS=tuple('remaining-aura-'+a+'-'+m for a in (
    'resistfire','resistcold','resistlightning','salvation','vigor','thorns',
    'holyfire','holyfreeze','holyshock','sanctuary','conviction','redemption') for m in ('right','quick'))

def configuration():
    return dict(stage=ROOT/'.local/class-skills/paladin-aura-input',
        extra_target_input_commands=INPUTS,extra_instant_input_commands=INPUTS,
        extra_inputs=(Path(__file__).resolve(),ROOT/'tests/check_assassin_planned_acceptance.py'),
        extra_connection_settle=.15)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--only',choices=tuple(dict.fromkeys(n.split('-')[2]for n in INPUTS)));args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    c=configuration()
    if args.only:c['extra_target_input_commands']=tuple(n for n in INPUTS if n.split('-')[2]==args.only)
    with paced_clients():check(**c)
