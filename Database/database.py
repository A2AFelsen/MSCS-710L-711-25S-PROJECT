###########################################################################################
# File: database.py                                                                       #
# Purpose: Overall database manager for metrics the gathering.                            #
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


# Read the metrics json file.
def read_metrics_json(json_file):
    pass


# Insert gathered metrics into a specified table.
def insert_metrics(db, table, data):
    pass


# Check the sql before actually passing it on.
def check_sql(in_sql):
    pass
