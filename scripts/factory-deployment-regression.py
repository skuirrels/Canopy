#!/usr/bin/env python3
"""Run deployment-specific scenarios using the shared real delivery/Git/HTTP fixture."""
from pathlib import Path
import subprocess
import sys

subprocess.run([sys.executable, str(Path(__file__).with_name('factory-delivery-regression.py')), '--deployments-only'], check=True)
