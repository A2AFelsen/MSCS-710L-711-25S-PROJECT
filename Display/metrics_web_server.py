###########################################################################################
# File: metrics_web_server.py                                                             #
# Purpose: Set up the web server that will be run locally on the users pc.                #
#                                                                                         #
# v0.0.1 Initial version. Mostly a skeleton.                                              #
# v0.1.0 Can now pull data from the interface script and pass it to the user_report page  #
# v0.1.1 Removing unnecessary imports as well as socketio since we don't need to talk to  #
#        other instances.                                                                 #
###########################################################################################
# TODO:                                                                                   #
###########################################################################################

import read_database
import ohm_interface
from flask import Flask, render_template, request

# Set up the Flask instance
app = Flask(__name__)


# index.html is the standard default case.
@app.route("/")
def index():
    return render_template("index.html")


@app.route("/gather", methods=["POST"])
def gather_report():
    return render_template("gather.html")


@app.route("/user_report", methods=["POST"])
def user_report():
    debug = request.form.get("debug", type=int, default=0)
    datasets = read_database.read_metrics(debug)
    max_datapoints = max((len(lst) for lst in datasets.values()), default=0)
    return render_template(
        "reports.html", datasets=datasets, max_datapoints=max_datapoints
    )


@app.route("/proc_table", methods=["POST"])
def proc_table():
    debug = request.form.get("debug", type=int, default=0)
    data = read_database.read_processes(debug)
    return render_template(
        "processes.html", data=data
    )


@app.route("/metrics", methods=['POST'])
def run_metrics():
    data = request.get_json()
    years = data.get('years')
    months = data.get('months')
    weeks = data.get('weeks')
    days = data.get('days')

    if ohm_interface.is_metrics_running():
        return "Metrics Stopped"
    return ohm_interface.call_executable(years, months, weeks, days)


@app.route("/prune-now", methods=['POST'])
def prune_metrics():
    return ohm_interface.prune_data()


if __name__ == "__main__":
    app.run(host="127.0.0.1")
