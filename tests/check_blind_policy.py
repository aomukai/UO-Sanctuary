#!/usr/bin/env python3
"""Check the unused blind decision foundation and current shared audit consumers."""
from pathlib import Path
from export_runtime_audits import export

if __name__ == '__main__':
    export(Path(__file__).resolve().parents[1] / '.local/class-skills/blind-policy',
        extra_commands=('blind-policy-check', 'paladin-thorns-check'), extra_probes=('blind-policy',),
        extra_wait_results={'paladin-thorns-check': 'paladin-thorns-result'})
