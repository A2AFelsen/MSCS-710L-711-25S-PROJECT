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

import os
import random
import sqlite3


# Check to see if the database exists.
# If it doesn't then create it.
def check_db(db):
    if os.path.exists(db):
        print("Found DB!")
        return

    conn = sqlite3.connect(db)
    conn.close()


# Read the Metrics from the database
def read_metrics(db="metrics.db"):
    conn = sqlite3.connect(db)
    cursor = conn.cursor()
    output = cursor.execute("""
        SELECT t1.serial_number, t1.timestamp, t1.usage, t1.total_ram, t2.device_type
        FROM 'component_statistic' t1 
        JOIN 'component' t2 
        ON t1.serial_number = t2.serial_number
    """).fetchall()

    component_dict = {}
    for entry in output:
        serial_number = entry[0]
        timestamp     = entry[1]
        usage         = entry[2]
        total_ram     = entry[3]
        device_type   = entry[4]
        key   = f"{serial_number} ({device_type})"
        value = [timestamp, usage, total_ram, random.randint(1, 100)]
        if key not in component_dict:
            component_dict[key] = []
        component_dict[key].append(value)
    return component_dict

