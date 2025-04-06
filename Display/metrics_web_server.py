###########################################################################################
# File: metrics_web_server.py                                                             #
# Purpose: Set up the web server that will be run locally on the users pc.                #
#                                                                                         #
# v0.0.1 Initial version. Mostly a skeleton.                                              #
# v0.1.0 Can now pull data from the interface script and pass it to the user_report page  #
###########################################################################################
# TODO:                                                                                   #
###########################################################################################

import subprocess as sp

import read_database
from flask import Flask, render_template, request
from flask_socketio import SocketIO, emit
from web_server_database_interface import generate_datasets

# Set up the Flask instance as well as the socket IO
app = Flask(__name__)
socketio = SocketIO(app)


# index.html is the standard default case.
@app.route("/")
def index():
    return render_template("index.html")


@app.route("/gather", methods=["POST"])
def gather_report():
    return render_template("gather.html")


@app.route("/user_report", methods=["POST"])
def user_report():
    datasets = generate_datasets()
    datasets = read_database.read_metrics()
    max_datapoints = max((len(lst) for lst in datasets.values()), default=0)
    return render_template(
        "reports.html", datasets=datasets, max_datapoints=max_datapoints
    )


if __name__ == "__main__":
    socketio.run(app, debug=True, host="127.0.0.1")
