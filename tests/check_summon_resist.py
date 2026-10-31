#!/usr/bin/env python3
"""Shared passive curve/receiver and live summon inheritance; disposable saves."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/summon-resist',
        extra_files={'summon-resist-source-cases.json':ROOT/'.local/class-skills/summon-resist-source/cases.json'},
        extra_families=('summon-resist',),extra_inputs=(Path(__file__).resolve(),))
