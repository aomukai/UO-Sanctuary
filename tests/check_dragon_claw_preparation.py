#!/usr/bin/env python3
"""Distinct dual-claw source preparation retaining learned Talon and Tail checks."""
import argparse
import os
from pathlib import Path
from check_dragon_talon import ROOT, configuration as previous
from check_iron_golem_birth import check


def configuration(broader=False,isolate=False):
    c=dict(extra_files={},extra_families=(),extra_inputs=()) if isolate else previous(broader)
    suffix='-isolate' if isolate else '-broader' if broader else ''
    c['stage']=ROOT/('.local/class-skills/dragon-claw-preparation'+suffix)
    c['extra_files'].update({name:ROOT/'.local/class-skills'/name for name in ['assassin-claw-modern-rate-probe.json','assassin-claw-sequence-source-probe.json']})
    c['extra_families']+=('dragon-claw-preparation',)
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/assassin-claw-modern-rate-evidence.json',ROOT/'tools/reference/dragon-claw-source-evidence.json')
    return c


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--isolate',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.isolate))
