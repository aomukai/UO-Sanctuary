#!/usr/bin/env python3
"""Approved learned Talon with retained kick/Tail/Flight/charge/trap checks."""
import argparse
import os
from pathlib import Path
from check_dragon_talon_clock import ROOT, configuration as previous
from check_iron_golem_birth import check


def configuration(broader=False,isolate=False):
    c=previous(broader,isolate)
    suffix='-isolate' if isolate else '-broader' if broader else ''
    c['stage']=ROOT/('.local/class-skills/dragon-talon-action'+suffix)
    c['extra_families']+=('dragon-talon',)
    c['extra_checks']=(*c.get('extra_checks',()),'dragon-talon-mechanics-check')
    c['extra_target_input_commands']=(*c.get('extra_target_input_commands',()),'dragon-talon-left','dragon-talon-right','dragon-talon-quick')
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'docs/dragon-talon-approved-action-rule.md')
    return c


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--isolate',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.isolate))
