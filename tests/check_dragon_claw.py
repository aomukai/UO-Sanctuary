#!/usr/bin/env python3
"""Approved Dragon Claw action with distinct dual clock and retained class checks."""
import argparse
import os
from pathlib import Path
from check_dragon_claw_preparation import ROOT, configuration as previous
from check_iron_golem_birth import check


def configuration(broader=False,isolate=False):
    c=previous(broader,isolate)
    suffix='-isolate' if isolate else '-broader' if broader else ''
    c['stage']=ROOT/('.local/class-skills/dragon-claw-action'+suffix)
    c['extra_families']+=('dragon-claw',)
    c['extra_checks']=(*c.get('extra_checks',()),'dragon-claw-mechanics-check')
    c['extra_target_input_commands']=(*c.get('extra_target_input_commands',()),'dragon-claw-left','dragon-claw-right','dragon-claw-quick')
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'docs/dragon-claw-approved-action-rule.md')
    return c


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--isolate',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.isolate))
