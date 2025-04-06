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
    output = cursor.execute(
        "SELECT serial_number, timestamp, usage, total_ram FROM 'component_statistic'"
    ).fetchall()

    component_dict = {}
    for entry in output:
        if entry[0] not in component_dict:
            component_dict[entry[0]] = []
        component_dict[entry[0]].append(
            [entry[1].split(".")[0], entry[2], entry[3], random.randint(1, 100)]
        )
    return component_dict
