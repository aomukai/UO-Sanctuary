#!/usr/bin/env python3
"""Verify prepared Iron Golem controller with retained birth/body/original/resource scopes."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    check(stage=ROOT/'.local/class-skills/iron-golem-world',
        extra_files={
            'iron-golem-birth-graph-source-cases.json':ROOT/'.local/class-skills/iron-golem-birth-graph-source/cases.json',
            'iron-golem-body-source-cases.json':ROOT/'.local/class-skills/iron-golem-body-source/cases.json',
            'necromancer-pet-ai-source-probe.json':ROOT/'.local/class-skills/necromancer-pet-ai-source-probe.json'},
        extra_families=('iron-golem-birth-graph','iron-golem-body','iron-golem-world'),
        extra_checks=('necromancer-pet-ai-check',),extra_inputs=(Path(__file__).resolve(),),extra_live_commands=('iron-golem-world-live',))
