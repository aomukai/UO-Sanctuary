#!/usr/bin/env python3
"""Reuse original contact fixtures for four missing Amazon input scopes."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients

INPUTS=tuple(f'amazon-sequence-contact-{a}-{m}'for a in ('jab','fend')for m in ('right','quick'))+tuple(
    f'impale-contact-{m}'for m in ('right','quick'))+tuple(f'amazon-thrust-contact-powerstrike-{m}'for m in ('right','quick'))

def configuration():
    return dict(stage=ROOT/'.local/class-skills/amazon-remaining-input',
        extra_families=('amazon-sequence-contact','impale-contact','amazon-thrust-contact'),
        extra_target_input_commands=INPUTS,extra_inputs=(Path(__file__).resolve(),
            ROOT/'tools/reference/impale-defense-policy-approval.json',ROOT/'tools/reference/impale-defense-policy-native-evidence.json'))

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
