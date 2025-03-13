###########################################################################################
# File: metrics_web_server.py                                                             #
# Purpose: Set up the web server that will be run locally on the users pc.                #
#                                                                                         #
# v0.0.1 Initial version. Mostly a skeleton.                                              #
# v0.1.0 Can now pull data from the interface script and pass it to the user_report page  #
###########################################################################################
# TODO:                                                                                   #
###########################################################################################

from flask import Flask, request, render_template
from flask_socketio import SocketIO, emit
from web_server_database_interface import known_data, random_data


# Set up the Flask instance as well as the socket IO
app = Flask(__name__)
socketio = SocketIO(app)


# index.html is the standard default case.
@app.route('/')
def index():
    return render_template('index.html')


@app.route('/user_report', methods=['POST'])
def user_report():
    datasets = {
        "GPU0": random_data(),
        "GPU1": random_data(),
        "DIMM": random_data(),
        "CPU": random_data(),
        "Power Supply": random_data()
    }
    return render_template('reports.html', datasets=datasets)


if __name__ == '__main__':
    socketio.run(app, debug=True, host='127.0.0.1')

