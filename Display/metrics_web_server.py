###########################################################################################
# File: metrics_web_server.py                                                             #
# Purpose: Set up the web server that will be run locally on the users pc.                #
#                                                                                         #
# v0.0.1 Initial version. Mostly a skeleton.                                              #
###########################################################################################
# TODO:                                                                                   #
###########################################################################################

from flask import Flask, request, render_template
from flask_socketio import SocketIO, emit


# Set up the Flask instance as well as the socket IO
app = Flask(__name__)
socketio = SocketIO(app)


# index.html is the standard default case.
@app.route('/')
def index():
    return render_template('index.html')


if __name__ == '__main__':
    socketio.run(app, debug=True, host='127.0.0.1')

