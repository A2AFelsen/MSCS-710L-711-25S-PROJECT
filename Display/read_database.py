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

import sqlite3
import os


# Check to see if the database exists.
# If it doesn't then create it.
def check_db(db):
    if os.path.exists(db):
        return

    conn = sqlite3.connect(db)
    conn.close()


# Read the Metrics from the database
def read_metrics(db):
    return
