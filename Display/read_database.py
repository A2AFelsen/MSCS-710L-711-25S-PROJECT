###########################################################################################
# File: read_database.py                                                                  #
# Purpose: Read metrics from the database                                                 #
#                                                                                         #
# v0.0.1 Initial version. Mostly a skeleton.                                              #
###########################################################################################
# TODO:                                                                                   #
#    - Split file where necessary                                                         #
#    - Actually fill out methods                                                          #
###########################################################################################

import subprocess
import os
import sqlite3
import database_manager


def get_git_root():
    try:
        root = subprocess.check_output(
            ['git', 'rev-parse', '--show-toplevel'],
            stderr=subprocess.DEVNULL).decode('utf-8').strip()
        return root
    except subprocess.CalledProcessError:
        return None


def get_database(debug):
    if get_git_root():
        root = os.path.join(get_git_root(), "Display/tests/sample_dbs")
    else:
        root = "."

    if debug == 0:
        root = "."
        return os.path.join(root, "../metrics.db")
    if debug == 1:
        return os.path.join(root, "metrics.db")
    if debug == 2:
        return os.path.join(root, "normal.db")
    if debug == 3:
        return os.path.join(root, "empty.db")
    if debug == 4:
        return os.path.join(root, "missing.db")
    if debug == 5:
        return os.path.join(root, "unexpected.db")
    if debug == 6:
        return os.path.join(root, "corrupted.db")
    else:
        return os.path.join(root, "metrics.db")


# Check to see if the database exists.
# If it doesn't then create it.
def check_db(db):
    if os.path.exists(db):
        print("Found DB!")
        return

    conn = sqlite3.connect(db)
    conn.close()


# Read the Metrics from the database
def read_metrics(debug=0):
    db = get_database(debug)
    conn = sqlite3.connect(db)
    database_manager.create_mtg_database(conn)

    cursor = conn.cursor()
    output = cursor.execute("""
        SELECT t1.serial_number, t1.timestamp, t1.temperature, t1.usage, t1.power_consumption, 
               t1.core_speed, t1.memory_speed, t1.total_ram, t2.device_type
        FROM 'component_statistic' t1 
        JOIN 'component' t2 
        ON t1.serial_number = t2.serial_number
    """).fetchall()

    component_dict = {}
    for entry in output:
        serial_number = entry[0]
        timestamp     = entry[1]
        temperature   = entry[2]
        usage         = entry[3]
        power_consume = entry[4]
        core_speed    = entry[5]
        memory_speed  = entry[6]
        total_ram     = entry[7]
        device_type   = entry[8]
        key = f"{serial_number} ({device_type})"
        value = [timestamp, temperature, usage, power_consume, core_speed, memory_speed, total_ram]
        if key not in component_dict:
            component_dict[key] = []
        component_dict[key].append(value)
    return component_dict


def read_processes(debug=0):
    db = get_database(debug)
    conn = sqlite3.connect(db)
    database_manager.create_mtg_database(conn)
    cursor = conn.cursor()
    output = cursor.execute("""SELECT pid, timestamp, cpu_usage, memory_usage, end_of_life
                               FROM process 
                               ORDER BY pid""").fetchall()
    return output
