#!/usr/bin/env python3

# FILE             :  server.py
# PROJECT          : SENG2040 Assignment 3, Logging Service (Client)
# PROGRAMMER       : Alex Kozak
# FIRST VERSION    : 2020-11-15
# DESCRIPTION      :
#      The main server file responsible for running the login endpoints as well as the logging endpoints and the saving of files to the disk.

import socket, threading, json, uuid, os, hashlib, sys
from datetime import datetime


# Creation of a constant class, the values here are defaults and are overwritten if there is already a config.json file
class _Const(object):
    LOGGING_SERVICE_IP = "127.0.0.1"
    MAX_LOG_LENGTH = 4096
    LOGIN_PORT = 8089
    SOCKET_TIMEOUT = 5
    LOGIN_SERVICE_PORTS = 5
    PORT_RANGE = list(range(8020, 8088))
    LOGGING_FOLDERS = "logs"

# Name:         Settings
# Description:  The settings class is responsible for getting and creating the settings from the config.json file stored on the disk.
class Settings():
    def sGet():
        print("Getting from config file...")
        try:
            with open("config.json") as config_file:
                js = json.load(config_file)
                CONST.LOGGING_SERVICE_IP  = js["LOGGING_SERVICE_IP"]
                CONST.MAX_LOG_LENGTH      = js["MAX_LOG_LENGTH"]
                CONST.LOGIN_PORT          = js["LOGIN_PORT"]
                CONST.SOCKET_TIMEOUT      = js["SOCKET_TIMEOUT"]
                CONST.LOGIN_SERVICE_PORTS = js["LOGIN_SERVICE_PORTS"]
                CONST.PORT_RANGE          = list(range(js["PORT_RANGE_MIN"], js["PORT_RANGE_MAX"]))
                CONST.LOGGING_FOLDERS     = js["LOGGING_FOLDERS"]
        except Exception as e:
            print("Error: Invalid configuration file with message -> %s" % str(e))
            print("Restoring the defaults to config.json, invalid config file is moved to 'ERROR_config.json' for future use")
            os.rename("config.json", "ERROR_config.json")
            Settings.sSetDefault()

    def sSetDefault():
        print("Setting default Config file...")
        if not os.path.isfile("config.json"):
            with open("config.json", "w") as newConfig:
                settings = {
                    "LOGGING_SERVICE_IP"    : "127.0.0.1",
                    "MAX_LOG_LENGTH"        : 1024,
                    "LOGIN_PORT"            : 8089,
                    "SOCKET_TIMEOUT"        : 5,
                    "LOGIN_SERVICE_PORTS"   : 5,
                    "PORT_RANGE_MIN"        : 8020,
                    "PORT_RANGE_MAX"        : 8088,
                    "LOGGING_FOLDERS"       : "logs",
                }
                json.dump(settings, newConfig, indent=4, sort_keys=True)
        print("Config file created.")

    def sPrint():
        with open("config.json") as config_file:
            js = json.load(config_file)
            print(json.dumps(js, indent=4, sort_keys=True))

# Function:     generateResponse
# Description:  enable the buttons required for use while logged in and disable those which can't be used
# Parameters:
#               code (any)          : The number code for the response to return converted to a string
#               response (any)      : A response message to send with the code
# Returns:      byte array of the JSON obj to be sent over the socket
def generateResponse(code, response):
    x = ("{\"code\":\"%s\",\"message\":\"%s\"}" % (str(code), str(response)))
    print(x)
    return x.encode('utf-8')

# Function:     executeMessage
# Description:  takes the message sent to the server and redirects it appropriately
# Parameters:
#               address             : The address of the sender
#               user_credentials    : The user's credentials
#               message             : The message the user sent
# Returns:
#               isValidMessage bool : if the mesage was correctly handled
#               returnMessage string: a message for if the message did not get handled correctly to be sent to the user
def executeMessage(address, user_credentials, message):
    isValidMessage = True
    returnMessage = ""

    print(address, json.dumps(message, indent=4, sort_keys=True))
    try:
        isValidMessage = message["Session_Key"] in active_sessions
    except:
        print("\"Session_Key\" was not found in the given JSON")
        raise

    if isValidMessage:
        # Since the Session_Key was validated, we can go ahead and process the message
        if message["Action"] == "Log":
            try:
                with open("%s/%s/%s.LOG" % (CONST.LOGGING_FOLDERS, user_credentials["Key"], datetime.today().strftime('%Y-%m-%d')), "a") as logFile:
                    logFile.write(generateLogLine(message["Parameters"], address))
                returnMessage = str(len(message))
            except:
                returnMessage = "Not all expected parameters are set. Invalid JSON."
        elif message["Action"] == "Add_User":
            returnMessage = addTrustedUser(active_sessions[message["Session_Key"]]["Key"], message["Parameters"]["User"])
        else:
            returnMessage = "Invalid Action"
    else:
        returnMessage = "Invalid Session_Key"

    return isValidMessage, returnMessage

# Function:     generateLogLine
# Description:  takes inthe paramaters and formats them into a log line
# Parameters:
#               address     : The address of the sender
#               parameters  : The parameters to fill in the log line
# Returns:      the fully formed log line string
def generateLogLine(parameters, address):
    allTags = ", ".join(parameters["Tags"])

    return "%s %s [%s] - %s (%s:%d)[%s]\n" % (
        parameters["Timestamp"],
        address,
        parameters["Level"],
        parameters["Message"],
        parameters["FileName"],
        parameters["FileLine"],
        allTags
        )

# Function:     recieveMessages
# Description:  Thread to infinitely listen for messages in from the client and deal with them appropriately. Will stop on disconect from the client
# Parameters:
#               connection          : The socket connection to the user
#               address             : The address of the sender
#               user_credentials    : The user's credentials used to verify their identity
# Returns:      NONE
def recieveMessages(connection, address, user_credentials):
    message = ""
    print("Connection established with %s" % str(address))
    while True:
        if message == b'':
            print("Connection broken with %s" % str(address))
            del active_sessions[user_credentials["Session_Key"]]
            CONST.PORT_RANGE.append(user_credentials["Port"])
            break
        elif message != "":
            isValidMessage = True
            returnMessage = ""

            try:
                #load and execute on the JSON given
                msg = json.loads(message.decode("utf-8"))
                isValidMessage, returnMessage = executeMessage(address, user_credentials, msg)
            except:
                #An exception will be thrown if the JSON is invalid
                isValidMessage = False
                print("Invalid JSON", len(message))
                returnMessage = "Invalid JSON"


            if isValidMessage:
                #Message processed hapily, good news is shared back to the user
                connection.send(generateResponse(0, returnMessage))
            else:
                connection.settimeout(1)
                if len(message) >= CONST.MAX_LOG_LENGTH - 1:
                    connection.send(generateResponse(-2, "Message is too big for the MAX_LOG_LENGTH. Keep below the limit of %d bytes" % CONST.MAX_LOG_LENGTH))
                else:
                    connection.send(generateResponse(-1, returnMessage))
                # since the message was invalid, we need to dump out the buffer
                while True:
                    try:
                        connection.recv(CONST.MAX_LOG_LENGTH)
                    except:
                        break

        try:
            # try to get a message and time out every 5 seconds as a ping to make sure the recv isn't hung
            connection.settimeout(5)
            message = connection.recv(CONST.MAX_LOG_LENGTH)
        except socket.timeout:
            message = ""

# Function:     checkUserValidity
# Description:  checks the "trusted_users" for the md5 hash match of what the users sent as their username.
# Parameters:
#               key         : The user's base key
#               username    : The sender's intended username
# Returns:      isTrusted   : (bool) if the user should be trusted or not
def checkUserValidity(key, username):
    isTrusted = False
    hash_username = hashlib.md5(username.encode()).hexdigest()
    print("Hashed Username: ", hash_username)
    if os.path.isdir("%s/%s" % (CONST.LOGGING_FOLDERS, key)):
        if os.path.isfile("%s/%s/trusted_users" % (CONST.LOGGING_FOLDERS, key)):
            isTrusted = hash_username in [x.strip() for x in open("%s/%s/trusted_users" % (CONST.LOGGING_FOLDERS, key)).readlines()]
    return isTrusted

# Function:     redirectLoginPorts
# Description:  redirect the user from the login port to their own logging port
# Parameters:
#               connection  : The socket connection to the user
#               address     : The address of the sender
#               returnCreds : the credentials returned to the user (including destination port)
#               loggingPort : The destination logging port
# Returns:      NONE
def redirectLoginPorts(connection, address, returnCreds, loggingPort):
    connection.close()
    loggingSocket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    loggingSocket.bind((CONST.LOGGING_SERVICE_IP, loggingPort))
    loggingSocket.listen(1)

    try:
        loggingConnection, loggingAddress = loggingSocket.accept()
    except socket.timeout:
        print("Socket connection timed out")
        del active_sessions[returnCreds["Session_Key"]]
        CONST.PORT_RANGE.append(returnCreds["Port"])
        pass
    except:
        raise
    else:
        recieveMessages(loggingConnection, loggingAddress, returnCreds)

# Function:     addTrustedUser
# Description:  add a trusted user to their own logging directory
# Parameters:
#               user            : The username to be added
#               key             : The key to add the username to
# Returns:      addUserMessage  : String message to be sent back (total users)
def addTrustedUser(key, user):
    addUserMessage = ""
    print(key, user)
    if not checkUserValidity(key, user):
        with open("%s/%s/trusted_users" % (CONST.LOGGING_FOLDERS, key), "a") as trusted_users:
            trusted_users.write(hashlib.md5(user.encode()).hexdigest() + "\n")
    else:
        addUserMessage = "USER ALREADY EXISTS | "

    with open("%s/%s/trusted_users" % (CONST.LOGGING_FOLDERS, key), "r") as tu:
        addUserMessage += "Total Users: %d" % len(tu.readlines())
    return addUserMessage

# Function:     clientLogin
# Description:  execute the login process for a new user as its own thread to prevent hold ups whgere other users are attempting to login
# Parameters:
#               connection  : The socket connection to the user
#               address     : The address of the sender
# Returns:      NONE
def clientLogin(connection, address):
    loginFailed = True
    Invalid_Login = True

    # initial
    while Invalid_Login:
        try:
            loginAttempt = connection.recv(CONST.MAX_LOG_LENGTH).decode("utf-8")
            if loginAttempt == '':
                break
            login_credentials = json.loads(loginAttempt)
            print(json.dumps(login_credentials, indent=4, sort_keys=True))
            Invalid_Login = False
        except:
            pass

    # if it exited the loop and the login is still invalid, there was an issue
    if Invalid_Login:
        print("Disconnected from the client, shutting down the login process.")
        connection.close()
    else:
        # valid login attempt
        returnCreds = {"Key":""}

        port = min(CONST.PORT_RANGE)
        if login_credentials["User"] != "":
            #if there is an actual user field
            returnCreds["User"] = login_credentials["User"]
            if login_credentials["Key"] == "":
                #since there is a user field but not a key, make a key for the user and send it back to them
                while returnCreds["Key"] == "" or os.path.isdir("%s/%s" % (CONST.LOGGING_FOLDERS, returnCreds["Key"])):
                    returnCreds["Key"] = uuid.uuid4().hex
                    returnCreds["Port"] = port

                #make thier directory and add them to the trusted user list
                os.mkdir("%s/%s" % (CONST.LOGGING_FOLDERS, returnCreds["Key"]))
                addTrustedUser(returnCreds["Key"], returnCreds["User"])
                loginFailed = False
            else:
                #There is both a key and a user, check to see if the user has the right to this key and that the key exists
                if checkUserValidity(login_credentials["Key"], login_credentials["User"]):
                    returnCreds = login_credentials
                    returnCreds["Port"] = port
                    loginFailed = False
                else:
                    #either the user doenst have access or the key is invalid, either way dont tell them if the key is valid and open up to attacks
                    returnCreds = {"ERROR":"Credentials are Invalid"}
        else:
            returnCreds = {"ERROR":"Please Enter a Username"}

        if not loginFailed:
            #set this port aside for this client and generate it a session key for it to use
            CONST.PORT_RANGE.remove(port)
            returnCreds["Session_Key"] = uuid.uuid4().hex
            active_sessions[returnCreds["Session_Key"]] = { "Key": returnCreds["Key"], "Port": port }

        #return the result of the login to the user
        connection.send(json.dumps(returnCreds).encode('utf-8'))

        #either try the login process again or redirect to the logging ports
        if loginFailed:
            clientLogin(connection, address)
        else:
            redirectLoginPorts(connection, address, returnCreds, port)


# program wide items for cross/thread use
active_sessions = {}
CONST = _Const

# Function:     main
# Description:  The main login thread of the application, spins off threads to deal with logins->logging and initialize login sockets/configuration
# Parameters:   NONE
# Returns:      NONE
def main():
    if os.path.isfile("config.json"):
        Settings.sGet()
    else:
        Settings.sSetDefault()

    Settings.sPrint()

    #prime the login sockets for use
    socket.setdefaulttimeout(CONST.SOCKET_TIMEOUT)
    loginSocket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    loginSocket.bind((CONST.LOGGING_SERVICE_IP, CONST.LOGIN_PORT))
    loginSocket.listen(CONST.LOGIN_SERVICE_PORTS) # become a server socket, maximum x connections

    #create the base logging folder to store all the other folders
    if not os.path.isdir(CONST.LOGGING_FOLDERS):
        os.mkdir(CONST.LOGGING_FOLDERS)

    old_active_sessions = 0

    print("Program initialized, listening for login requests...")
    while True:
        # login loop searching for requests
        try:
            connection, address = loginSocket.accept()
        except socket.timeout:
            # since no connections have come through, if there are new connections there or gone, print the current connections
            if len(active_sessions) != old_active_sessions:
                print("There are currently %d active logging sessions:\n" % len(active_sessions), active_sessions)
                old_active_sessions = len(active_sessions)
            pass
        else:
            # Spin up a thread for logging in the newly connected user
            print("Logging In...\n", connection)
            x = threading.Thread(target=clientLogin, args=(connection, address))
            x.start()


# program start
if __name__ == "__main__":
    main()