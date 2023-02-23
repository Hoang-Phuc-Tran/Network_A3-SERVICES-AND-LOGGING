/*
 * FILE:        MainWindow.xaml.cs
 * Project:	    A03 – SERVICES AND LOGGING
 * Author:	    Hoang Phuc Tran - ID: 8789102
                Bumsu Yi - ID: 8110678
 * Date:		February 21, 2022
 * Description: This file contains all event handlers for UI in XAML file.
 */
using System.Windows.Documents;
using System.Net.Sockets;
using System.Windows;
using Newtonsoft.Json;
using System;
using System.Windows.Controls;
using System.IO;
using System.Net;

namespace Network_A3
{
    /// <summary>
    /// CLASS NAME:  MainWindow
    /// PURPOSE : The MainWindow class is inherited from the Window classes.It has properties
    /// and event handlers. This class is used to handle UI in XAML file.
    /// </summary>
    public partial class MainWindow : Window
    {

        // Local variables with regards to the TCP connection to be used for communication to the backend client.
        private TcpClient tcp;
        private StreamWriter SwSender;
        private StreamReader SrReciever;
        
        // custom classes which are received and sent. Used as a source of truth to populate UI elements and Send data.
        LoggingServieLoginResponse loginResponse;
        LoggingServiceMessage loggingToSend = new LoggingServiceMessage("Log");
        LoggingServiceMessage addUserToSend = new LoggingServiceMessage("Add_User");

        // the index at which each of the UI tabs exist. Used to easily convert the selectedIndex.
        enum TAB_INDEX 
        {
            AUTOMATIC_TASKS,
            LOG_MESSAGE,
            ADD_USER
        }
        public MainWindow()
        {
            InitializeComponent();
            
            // Set the UI in the "logged out" state
            LoginUpdateUI(false);
        }

        /**
         * Function:    btnLogout_Click
         * Description: close the TCP connection and set the UI to a logged out state.
         * Parameters:  STANDARD_EVENT_HANDLER
         * Returns:     void
         */
        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            // close the connection and 
            tcp.Close();
            LoginUpdateUI(false);
        }

        /**
         * Function:    btnLogin_Click
         * Description: Handle the login process including the error checking for the input of username/key. Read back the response object and setup the TCP 
         *              connections for the subsequent calls.
         * Parameters:  STANDARD_EVENT_HANDLER
         * Returns:     void
         */
        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            // Connect to the IP/Port given by the user
            TcpClient loginTcp = new TcpClient();
            int port = 0;
            if (Int32.TryParse(tbLoggingServicePort.Text, out port))
            {
                loginTcp.Connect(IPAddress.Parse(tbLoggingServiceIP.Text), port);
            }
            
            // Build the login JSON to be sent later
            string loginMsg = $"{{\"User\":\"{tbLoggingServiceUser.Text}\", \"Key\":\"{tbLoggingServiceKey.Text}\" }}";

            if (loginTcp.Connected == true)
            {
                StreamWriter sWriter;
                StreamReader sReader;

                // Create the writer and send the login JSON to request the login 
                sWriter = new StreamWriter(loginTcp.GetStream());
                sWriter.Write(loginMsg);
                sWriter.Flush();

                sReader = new StreamReader(loginTcp.GetStream());

                string loginResponseString = "";
                char[] buffer = new char[1024];
                
                // Read the response from the server
                var readByteCount = sReader.Read(buffer, 0, buffer.Length);
                if (readByteCount > 0)
                {
                    loginResponseString = new string(buffer, 0, readByteCount);
                }

                // Deserialize the response print a formatted version to the Command Response text box
                loginResponse = JsonConvert.DeserializeObject<LoggingServieLoginResponse>(loginResponseString);
                tbCommandResponse.Text = JsonConvert.SerializeObject(loginResponse, Formatting.Indented);

                // Check to see if the response deserialized properly as if there was an error, it would not send back a LoggingServieLoginResponse but a standard message
                if (loginResponse.Key == null)
                {
                    // Since there was a login error, display the message to the user and exit
                    dynamic parsedJson = JsonConvert.DeserializeObject(loginResponseString);
                    tbCommandResponse.Text = JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
                }
                else
                {
                    // Successfully connected, so the login ports need to close and we need to reconnect to the final logging service ports
                    sWriter.Close();
                    sReader.Close();

                    // Give the session key to the places that need it
                    tbLoggingServiceKey.Text = loginResponse.Key;
                    //lblSessionKey.Content = $"Session Key: {loginResponse.Session_Key}";
                    loggingToSend.Session_Key = loginResponse.Session_Key;
                    loggingToSend.User = loginResponse.User;
                    addUserToSend.Session_Key = loginResponse.Session_Key;
                    addUserToSend.User = loginResponse.User;

                    // Create the new TCP client for the final logging service destination.
                    tcp = new TcpClient();
                    tcp.Connect(IPAddress.Parse(tbLoggingServiceIP.Text), loginResponse.Port);
                    SrReciever = new StreamReader(tcp.GetStream());
                    SwSender = new StreamWriter(tcp.GetStream());
                    LoginUpdateUI(true); // Update the UI for the user to be logged in and enable the right buttons

                }
                updatePreview();    // Update the preview window if there are any changes
            }
        }

        /**
         * Function:    logInButtons
         * Description: enable the buttons required for use while logged in and disable those which can't be used
         * Parameters:  STANDARD_EVENT_HANDLER
         * Returns:     void
         */
        void LoginUpdateUI(bool isLoggedIn)
        {
            btnLogin.IsEnabled = !isLoggedIn;
            tbLoggingServiceKey.IsEnabled = !isLoggedIn;
            tbLoggingServicePort.IsEnabled = !isLoggedIn;
            tbLoggingServiceUser.IsEnabled = !isLoggedIn;
            tbLoggingServiceIP.IsEnabled = !isLoggedIn;
            btnLogout.IsEnabled = isLoggedIn;
            tcMain.IsEnabled = isLoggedIn;

            
        }

        /**
         * Function:    sendCommand
         * Description: 
         * Parameters:  
         *      int tabIndex (Default: -1): Allows the automated testing to override the type of message being sent with a manual index (mapped to enum TAB_INDEX
         *      string sendOverride (Default: null): string which if set overrides any JSON to be sent and simply sends this string in its place
         *      bool dcAfterSending (Default: false): if true, disconnect immediately after sending the message without acknowledging the response.
         * Returns:     void
         */
        public void sendCommand(int tabIndex = -1, string sendOverride = null, bool dcAfterSending = false)
        {
            if (sendOverride == null)
            {
                updatePreview(tabIndex);
            }
            try
            {
                // send the message of either what the preview has or the manual override
                if (sendOverride == null)
                {
                    SwSender.Write(new TextRange(rtbCommandPreview.Document.ContentStart, rtbCommandPreview.Document.ContentEnd).Text);
                }
                else
                {
                    SwSender.Write(sendOverride);
                }
                SwSender.Flush();

                if (dcAfterSending)
                {
                    tcp.Close();
                    LoginUpdateUI(false);
                }
                else
                {
                    char[] buffer = new char[1000];
                    var readByteCount = SrReciever.Read(buffer, 0, buffer.Length);
                    if (readByteCount > 0)
                    {
                        // Dynamically parse the returned json and display it in whatever form it is in to the response textbox
                        string data = new string(buffer, 0, readByteCount);
                        dynamic parsedJson = JsonConvert.DeserializeObject(data);
                        tbCommandResponse.Text = JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
                    }
                }
            }
            catch (Exception ex)
            {
                // an issue occured and the service disconnected, reconnecting is the next step
                Console.WriteLine(ex.Message);
                LoginUpdateUI(false);
                tbCommandResponse.Text = "Disconnected from server. Try logging in again.";
            }
        }


        /**
         * Function:    TabControl_SelectionChanged
         * Description: Update the preview window when the user changed the selected tab
         * Parameters:  STANDARD_EVENT_HANDLER
         * Returns:     void
         */
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            updatePreview();
        }

        /**
         * Function:    printToPreview
         * Description: an easier way to shell the outputting of text to the rich text box (to allow for scorlling) as it occurs often and is more than a single line
         * Parameters:  string stringToDisplay: the string to be displayed in the preview window
         * Returns:     void
         */
        private void printToPreview(string stringToDisplay)
        {
            rtbCommandPreview.Document.Blocks.Clear();
            rtbCommandPreview.Document.Blocks.Add(new Paragraph(new Run(stringToDisplay)));
        }

        /**
         * Function:    updatePreview
         * Description: Update the preview based on the current index of the tab control.
         * Parameters:  int tabIndexOverride(Default: -1): can be used to override the current index for automation purposes
         * Returns:     void
         */
        private void updatePreview(int tabIndexOverride = -1)
        {
            if (tabIndexOverride == -1)
                tabIndexOverride = tcMain.SelectedIndex;
            switch ((TAB_INDEX)tabIndexOverride)
            {
                case TAB_INDEX.AUTOMATIC_TASKS:
                    printToPreview("");
                    break;
                case TAB_INDEX.LOG_MESSAGE:
                    updateLogMessage();
                    printToPreview(JsonConvert.SerializeObject(loggingToSend, Formatting.Indented, new JsonSerializerSettings{ NullValueHandling = NullValueHandling.Ignore }));
                    break;
                case TAB_INDEX.ADD_USER:
                    printToPreview(JsonConvert.SerializeObject(addUserToSend, Formatting.Indented, new JsonSerializerSettings{ NullValueHandling = NullValueHandling.Ignore }));
                    break;
            }
        }

        /**
         * Function:    updateLogMessage
         * Description: update the 'Log' message to match the UI elements and their values
         * Parameters:  None
         * Returns:     void
         */
        private void updateLogMessage()
        {
            loggingToSend.Parameters.FileLine = Int32.TryParse(tbFileLine.Text, out int x) ? Int32.Parse(tbFileLine.Text) : 0;
            loggingToSend.Parameters.FileName = tbFileName.Text;
            loggingToSend.Parameters.Level = tbLogLevel.Text;
            loggingToSend.Parameters.Message = tbMessage.Text;
            loggingToSend.Parameters.Tags = tbTags.Text.Split(',');
            loggingToSend.Parameters.Timestamp = DateTime.UtcNow.ToString();
        }


        /**
         * Function:    Log_TextChanged
         * Description: Event handler for whenever any of the UI elements on the log config screen changes
         * Parameters:  STANDARD_EVENT_HANDLER
         * Returns:     void
         */
        private void Log_TextChanged(object sender, TextChangedEventArgs e)
        {
            updatePreview();
        }

        /**
         * Function:    btnSendSequentialLogs_Click
         * Description: Sends a set number of sequential logs to the logging service, going from 1 -> X of FileLine 
         * Parameters:  STANDARD_EVENT_HANDLER
         * Returns:     void
         */
        private void btnSendSequentialLogs_Click(object sender, RoutedEventArgs e)
        {
            int sequenceSize = 0;
            if (Int32.TryParse(tbSequentialLogs.Text, out sequenceSize))
            {
                for (int i = 1; i <= sequenceSize; i++)
                {
                    tbFileLine.Text = i.ToString();
                    sendCommand((int)TAB_INDEX.LOG_MESSAGE);
                }
            }
            else
            {
                printToPreview("Invalid X value");
            }
            printToPreview($"Completed sending {sequenceSize} logs to the service!");
        }

        

        /**
         * Function:    btnMalformedJson_Click
         * Description: Send a message with malformed JSON to the logging service
         * Parameters:  STANDARD_EVENT_HANDLER
         * Returns:     void
         */
        private void btnMalformedJson_Click(object sender, RoutedEventArgs e)
        {
            string malformedJSON = "{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{{}";
            printToPreview(malformedJSON);
            sendCommand((int)TAB_INDEX.AUTOMATIC_TASKS, malformedJSON);
        }

        
        /**
         * Function:    btnSendIncorrectSession_Click
         * Description: Send a "Log" action with an incorrect session in the JSON
         * Parameters:  STANDARD_EVENT_HANDLER
         * Returns:     void
         */
        private void btnSendIncorrectSession_Click(object sender, RoutedEventArgs e)
        {
            string tmpSession = loggingToSend.Session_Key;
            loggingToSend.Session_Key = "INCORRECT SESSION";
            sendCommand((int)TAB_INDEX.LOG_MESSAGE);
            loggingToSend.Session_Key = tmpSession;
        }

    }
}
