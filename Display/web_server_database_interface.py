###########################################################################################
# File: web_server_database_interface.py                                                  #
# Purpose: The webserver will talk to the database through this file.    .                #
#                                                                                         #
# v0.0.1 Initial version. Mostly a skeleton.                                              #
###########################################################################################
# TODO:                                                                                   #
###########################################################################################
import random


# This is a testing function that will return known lists so that the website can render data.
def known_data():
    list_up = [1, 2, 3, 4, 5, 6]
    list_down = [9, 8, 7, 6, 5, 4]
    list_even_first = [2, 4, 6, 8, 1, 3]
    list_odd_first = [1, 3, 5, 7, 9, 2]

    i = random.randint(0, 3)

    if i == 0:
        return list_up
    elif i == 1:
        return list_down
    elif i == 2:
        return list_even_first
    else:
        return list_odd_first


def random_data():
    random_list = [
        [f"DATE: {str(i)}", random.randint(1, 100)]
        for i in range(random.randint(5, 20))
    ]
    return random_list


def generate_datasets():
    datasets = {
        "GPU0": random_data(),
        "GPU1": random_data(),
        "DIMM": random_data(),
        "CPU": random_data(),
        "Power Supply": random_data(),
    }

    return datasets
