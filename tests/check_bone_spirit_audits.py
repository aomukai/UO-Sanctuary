#!/usr/bin/env python3
"""Retain timed Paladin Thorns and catalogue evidence for the Bone Spirit saved guidance."""
import export_runtime_audits as native

if __name__ == '__main__':
    native.export(native.ROOT / '.local/class-skills/bone-spirit',
        extra_commands=('paladin-thorns-check',),
        extra_wait_results={'paladin-thorns-check': 'paladin-thorns-result'})
