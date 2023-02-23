/**
 * FILE             : LoggingServiceMessage.cs
 * PROJECT          : SENG2040 Assignment 3, Logging Service (Client)
 * PROGRAMMER       : Alex Kozak
 * FIRST VERSION    : 2020-11-15
 * DESCRIPTION      :
 *      This document contains the class definition of the message packet, with the heirerchy serialized to the intended JSON the server wants. The
 *      Parameters are all set to null as to allow the serializer to ignore any null values, allowing this to work for both 'Log' and "Add_User' actions.
 */
namespace Network_A3
{
    class LoggingServiceMessage
    {
        public LoggingServiceMessage(string action)
        {
            this.Action = action;
        }
        public string User;         // the name of the user
        public string Session_Key;  // the session key to validate the user's credibility without sending the key every time
        public string Action;       // the intended action (one of "Log" and "Add_User") to be performed by the service
        public MessageParameters Parameters = new MessageParameters();    // The sublist of parameters with the meat of the content stored here
    }

    // Name:        Parameters
    // Description: All possible parameters needed to be sent, need to be nullable to prevent the sample JSON display from displaying all of them all the time
    //              even in the circumstance where only one needs to be sent.
    class MessageParameters
    {
        public string Timestamp = null;
        public string Level = null;
        public string FileName = null;
        public int? FileLine = null;
        public string Message = null;
        public string[] Tags = null;
        public string User = null;
    }

    /**
     * FILE             : Response.cs
     * PROJECT          : SENG2040 Assignment 3, Logging Service (Client)
     * PROGRAMMER       : Alex Kozak
     * FIRST VERSION    : 2020-11-15
     * DESCRIPTION      :
     *      Each sent message comes back with a response which fits this scheme. A code and a message about the code. If there is no error the code will be 0,
     *      and if there is an error there is a non-zero code and some form of message attached describing the likely issue.
     */
    class Response
    {
        public int code;
        public string message;
    }

    /**
     * FILE             : LoggingServieLoginResponse.cs
     * PROJECT          : SENG2040 Assignment 3, Logging Service (Client)
     * PROGRAMMER       : Alex Kozak
     * FIRST VERSION    : 2020-11-15
     * DESCRIPTION      :
     *      This document houses the class meant to deserialize the incoming JSON given by the server post-login. The server
     *      returns a number of fields, most importantly the Port and the Session_Key, which allows the user to connect away from the locin port and into
     *      the given logging port where they are given their own port to freely send to.
     */
    class LoggingServieLoginResponse
    {
        public string Key;
        public int Port;
        public string Session_Key;
        public string User;
    }
}
