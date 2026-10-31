#!/usr/bin/env python3
"""Shared skeleton target bindings and retained Iron Golem world controller."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/necromancer-pet-targets',
        extra_files={
            'iron-golem-birth-graph-source-cases.json':ROOT/'.local/class-skills/iron-golem-birth-graph-source/cases.json',
            'iron-golem-body-source-cases.json':ROOT/'.local/class-skills/iron-golem-body-source/cases.json',
            'necromancer-pet-ai-source-probe.json':ROOT/'.local/class-skills/necromancer-pet-ai-source-probe.json'},
        extra_families=('iron-golem-birth-graph','iron-golem-body','iron-golem-world'),
        extra_checks=('necromancer-pet-ai-check','necromancer-pet-targets-check'),
        extra_live_commands=('iron-golem-world-live',),extra_inputs=(Path(__file__).resolve(),))
