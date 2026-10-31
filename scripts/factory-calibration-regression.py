#!/usr/bin/env python3
"""Run real native calibration against the existing stateful GitHub/Git simulation."""
from pathlib import Path
import runpy
import sys
sys.argv=[str(Path(__file__).with_name('factory-delivery-regression.py')),'--calibration-only',*sys.argv[1:]]
runpy.run_path(sys.argv[0],run_name='__main__')
