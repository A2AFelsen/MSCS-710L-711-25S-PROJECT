###########################################################################################
# File: ohm_interface.py (Open Hardware Monitor Interface)                                #
# Purpose: The actual calls to the database will be run through this file                 #
#                                                                                         #
# v0.0.1 Initial version.                                                                 #
###########################################################################################

import psutil
import os
import subprocess


def is_metrics_running():
    for proc in psutil.process_iter(['pid', 'name', 'username']):
        try:
            if "OpenHardwareMonitor.exe" == proc.info['name']:
                proc.kill()
                return True
        except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.ZombieProcess):
            pass
    return False


def call_executable(years=0, months=0, weeks=0, days=0, prune_now=False):
    parent_dir = os.path.dirname(os.getcwd())
    metrics_exe = os.path.join(parent_dir, "OpenHardwareMonitor.exe")
    if os.path.exists(metrics_exe):
        subprocess.run(metrics_exe, cwd=parent_dir, capture_output=True, text=True, check=True)
        return "Metrics Started"
    else:
        return "Can't Find Metrics"
