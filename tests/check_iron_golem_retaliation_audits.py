#!/usr/bin/env python3
"""Retain the real Paladin Thorns lane and export current catalogue evidence."""
import export_runtime_audits as native
if __name__=='__main__':
    native.export(native.ROOT/'.local/class-skills/iron-golem-retaliation',
        extra_commands=('paladin-thorns-check',),extra_wait_results={'paladin-thorns-check':'paladin-thorns-result'})
