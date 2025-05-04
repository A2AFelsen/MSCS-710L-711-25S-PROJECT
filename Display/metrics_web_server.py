###########################################################################################
# File: metrics_web_server.py                                                             #
# Purpose: Set up the web server that will be run locally on the users pc.                #
#                                                                                         #
# v0.0.1 Initial version. Mostly a skeleton.                                              #
# v0.1.0 Can now pull data from the interface script and pass it to the user_report page  #
# v0.1.1 Removing unnecessary imports as well as socketio since we don't need to talk to  #
#        other instances.                                                                 #
# v1.0.0 Initial Production version. mthuffer 2025-04-30                                  #
###########################################################################################

import db_interface
import ohm_interface
import sys
import os
from flask import Flask, render_template, request

# If running as an executable (to set up for pyinstaller)
if getattr(sys, 'frozen', False):
    base_path = sys._MEIPASS
else:
    base_path = os.path.join(db_interface.get_proj_root(), "Display")

# Set up the Flask instance based on what type of run we are.
app = Flask(__name__,
            template_folder=os.path.join(base_path, 'templates'),
            static_folder=os.path.join(base_path, 'static'))


@app.route("/")
def index():
    """Default Case: index.html"""
    return render_template("index.html")


@app.route("/gather", methods=["POST"])
def gather_report():
    """Calls the Gathering. User interface for metrics collection"""
    return render_template("gather.html")


@app.route("/user_report", methods=["POST"])
def user_report():
    """Calls reports.html. Show graphs of metrics."""
    # Debug value for testing purposes. Default 0 since we should only get a debug flag in testing.
    debug = request.form.get("debug", type=int, default=0)

    # Get the data from db_interface.
    # Should be a dictionary where the key is the serial_number + component and the value is a list of metrics.
    datasets = db_interface.read_metrics(debug)

    # Find the max datapoints, so we can set the 'end' value when loading the page.
    max_datapoints = max((len(lst) for lst in datasets.values()), default=0)
    return render_template(
        "reports.html", datasets=datasets, max_datapoints=max_datapoints
    )


@app.route("/proc_table", methods=["POST"])
def proc_table():
    """Calls processes.html. Shows a table of the processes"""
    # Debug value for testing purposes. Default 0 since we should only get a debug flag in testing.
    debug = request.form.get("debug", type=int, default=0)

    # Get the data from db_interface. Simply a tuple from the SQL command
    data = db_interface.read_processes(debug)
    return render_template(
        "processes.html", data=data
    )


@app.route("/metrics", methods=['POST'])
def run_metrics():
    """Sets up the metrics executable when called."""
    # Get user data.
    data = request.get_json()
    years = data.get('years')
    months = data.get('months')
    weeks = data.get('weeks')
    days = data.get('days')
    job = data.get('job')  # Should either be 'collect' or 'prune'

    # If we are pruning then we call the prune method
    if job == 'prune':
        return ohm_interface.prune_data(years, months, weeks, days)

    # If we aren't pruning we should be collecting. First check if the metrics is already running.
    # This will stop the collector if it is currently running.
    if ohm_interface.is_metrics_running():
        return "Metrics Stopped"

    # Otherwise call the collector
    return ohm_interface.call_executable(years, months, weeks, days)


if __name__ == "__main__":
    from waitress import serve
    serve(app, host="127.0.0.1", port=8080)
