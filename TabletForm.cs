using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using InTheHand.Net.Ports;
using System.Threading;

namespace BluetoothServerTest
{
    public partial class Form1 : Form
    {
        Guid mUUID;
        string previousSurvey;
        string currentSurvey;
        public Form1()
        {               
            InitializeComponent();
            this.TopMost = false;
            closeFirstInstance();
            currentSurvey = null;
            previousSurvey = null;
            //CloseFoxit();
            mUUID = new Guid("8a63d9e7-ab03-4fd1-b835-9fa143b02c10");
            txtStatus.Text = "Not doing anything..";
            receivedTextMessage.Text = "First make sure CBG Laptop and Tablet are paired. Then click connect on the Laptop.";
            receivedTextMessage.AppendText(Environment.NewLine + Environment.NewLine + "You will be notified here when to begin the survey.");
            connectAsServer();
            
        }



        private void connectAsServer()
        {
            Thread bluetoothServerThread = new Thread(new ThreadStart(ServerConnectThread));
            bluetoothServerThread.IsBackground = true;
            bluetoothServerThread.Start();
        }

        private void Form1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //CloseFoxit();
        }

        private void closeFirstInstance()
        {           
            Process[] pname = Process.GetProcessesByName(AppDomain.CurrentDomain.FriendlyName.Remove(AppDomain.CurrentDomain.FriendlyName.Length - 4));
            if (pname.Length > 1)
            {
                pname[1].Kill();
            }
        }

        BluetoothListener bluListener;
        BluetoothClient conn;
        public void ServerConnectThread()
        {

            updateStatus("Bluetooth started, awaiting CBG laptop connection.");
            bool tryAgain = true;
            bool ticking = false;
            Stopwatch s = new Stopwatch();

            //TabletShowcards restarts upon a bluetooth connection failure. In such an instance this connection
            //attempt detects re-enabling of bluetooth, with a timeout period of 20 seconds.
            while (tryAgain)
                {
                    try
                    {
                        bluListener = new BluetoothListener(mUUID);
                        tryAgain = false;
                        bluListener.Start();
                        conn = bluListener.AcceptBluetoothClient();
                        if (ticking == true) { s.Stop(); }                       
                    }
                    catch (Exception e)
                    {
                        tryAgain = true;
                        if (ticking == false) { s.Start(); ticking = true; }                                        

                        if (s.ElapsedMilliseconds > 60000)//60 second timeout period. Restart Application.
                        {
                        AutoClosingMessageBox.Show("A Bluetooth connection could not be established."
                            + Environment.NewLine + Environment.NewLine + "Please ensure Bluetooth is enabled."
                            + Environment.NewLine + "Once enabled, restart TabletShowcards and try again.", "Failed Connection",
                            5000);


                        s.Stop();
                        tryAgain = false;
                        this.Invoke((MethodInvoker)delegate
                        {
                            System.Diagnostics.Process.Start(Application.ExecutablePath);
                            Application.Exit();
                        });
                    }
                    }
                }
            
            updateStatus("Laptop has connected. Do not close this program or LaptopShowcards until survey is complete.");
            receivedTextMessage.Text = "You may now begin the survey.";
            updateReceived(null);
            string User = Environment.UserName;

            Stream sentStream = conn.GetStream();
            while (true)
            {


                try
                {
                    //handling data sent by client and then displaying it.
                    byte[] received = new byte[1024];
                    sentStream.Read(received, 0, received.Length); //Reads stream in it's entirety.
                    updateReceived(Encoding.ASCII.GetString(received));
                    string info = Encoding.ASCII.GetString(received);
                    if (SurveyChangedFoxitOpen(info) == true)
                    {
                        //CloseFoxit();
                    }
                    if (!info.Contains("user-input"))
                    {
                        CloseChrome();
                    }
                    OpenPDFpage(info);
                }
                catch(IOException)
                {
                    updateStatus("Laptop has disconnected. Please restart both applications.");
                }

                if (!conn.Connected)
                {
                    //CloseFoxit();
                    updateStatus("Laptop has disconnected." + Environment.NewLine + Environment.NewLine +
                    "Select on LaptopShowcards if you wish to re-connect or close.");
                    updateReceived("Laptop has disconnected." + Environment.NewLine + Environment.NewLine +
                    "Select on LaptopShowcards if you wish to re-connect or close.");
                    //Thread.Sleep(2000);//Giving bluetooth time to sort itself out.

                    //Avoid cross-threading errors when attempting  a restart.
                    this.Invoke((MethodInvoker)delegate
                    {
                        System.Diagnostics.Process.Start(Application.ExecutablePath);
                        Application.Exit();
                    });
                }

                if (string.IsNullOrEmpty(receivedTextMessage.Text))//Dialog box becomes empty when laptop has dicsonnected.
                {
                    //CloseFoxit();
                    updateStatus("Laptop has disconnected." + Environment.NewLine + Environment.NewLine +
                    "Select on LaptopShowcards if you wish to re-connect or close.");
                    updateReceived("Laptop has disconnected." + Environment.NewLine + Environment.NewLine +
                    "Select on LaptopShowcards if you wish to re-connect or close.");
                    //Thread.Sleep(5000);

                    //Avoid cross-threading errors when attempting  a restart.
                    this.Invoke((MethodInvoker)delegate
                    {
                        System.Diagnostics.Process.Start(Application.ExecutablePath);
                        Application.Exit();
                    });
                }
                
            }
        }

        private void transmitText(string text, Stream stream)
        {
            byte[] message = Encoding.ASCII.GetBytes(text);
            stream.Write(message, 0, message.Length);
        }

        private void updateReceived(string message)
        {
            Func<int> del = delegate ()
            {
                receivedTextMessage.Text = (message + System.Environment.NewLine);
                return 0;
            }; Invoke(del);
        }

        private void updateStatus(string text)
        {
            Func<int> del = delegate ()
            {
                txtStatus.Text = (text + System.Environment.NewLine);
                return 0;
            }; Invoke(del);
        }


        private void WarningMessage(string text)
        {
            Func<int> del = delegate ()
            {
                MessageBox.Show(receivedTextMessage, text);
                return 0;
            }; Invoke(del);
        }

        //editted for new logo inserted at first page of child health survey!! Default logo when no showcard exists.
        //IMPORTANT: PDF naming format: "NZHSChild.pdf" or "NZHSAdult.pdf". Make room for third survey.
        public void OpenPDFpage(string inputText)
        {
            if (inputText.Substring(0, 4) == "page") //Makesure page&PN& format passed through from laptop.
            {
                if (!inputText.Contains("user-input"))
                {
                    //CloseChrome();
                    runFoxit(inputText);
                }
                else if (inputText.Contains("user-input"))
                {
                    runChrome(inputText);
                }
            }
        }

        private void runFoxit(string inputText)
        {
                    string User = Environment.UserName;
                    char splitter = ' ';//Splitting at the space between e.g. "page1 adult"
                    string[] subStrings = inputText.Split(splitter);
                    int pageNum = Int32.Parse(subStrings[0].Substring(4)) + 1; //IMPORTANT: Page +1 due to default logo at page 2.
                    string stringPageNum = pageNum.ToString();
                    string surveyInfo = null; 
                    string survey = subStrings[1].Substring(0,5); //pageturner.txt surv ID parameter.
            if (survey == "Adult") { surveyInfo = "NZHS" + "Adult.pdf\""; }
            else if (survey == "NHA13") { surveyInfo = "NZHS" + "AdultY13.pdf\""; }
            else if (survey == "NHC13") { surveyInfo = "NZHS" + "ChildY13.pdf\""; }
            else if (survey == "Y7CVS") { surveyInfo = "NZCVSY7.pdf\""; }
            else if (survey == "Y7PPM") { surveyInfo = "PPMY7.pdf\""; }
            else if (survey == "NHA14") { surveyInfo = "NZHS" + "AdultY14.pdf\""; }
            else if (survey == "NHC14") { surveyInfo = "NZHS" + "ChildY14.pdf\""; }
            else if (survey == "Y8CVS") { surveyInfo = "NZCVSY8.pdf\""; }
            else surveyInfo = "Invalid call to pageturner.exe, check pageturner.txt file call.";

            //Opens desired showcard PDF page dependent on survey. this is determined by surveyInfo variable.
            Process myProcess = new Process();
                    myProcess.StartInfo.FileName = "FoxitReader.exe"; //Foxit reader program
                    myProcess.StartInfo.Arguments = //Assuming that showcard file is stored on the desktop.
                            "\"C:\\ShowcardFiles\\" + surveyInfo + " /A page=" + stringPageNum; //Page ID to be determined by look-up table.
                    myProcess.Start();
                    previousSurvey = subStrings[1].Substring(0, 5);//Global variable stores which survey is open.
        }

        private void runChrome(string inputText)
        {
            //user input files need to be saved in UserInputChild or UserInputAdult and be user-input22 where 22 is question
            //number specific. these need to be kept consistent.
            string User = Environment.UserName;
            char splitter = ' ';//Splitting at the space between e.g. "question14 other123"
            string[] subStrings = inputText.Split(splitter);
            string userInputFile = subStrings[0].Substring(4) + ".html"; //Obtains "user-input&QN&" format.
            string surveyType = subStrings[1].Substring(0, 5);
            string strCmdText;
            //strCmdText = "-kiosk --incognito --disable-session-crashed-bubble \"C:\\Users\\" + User + "\\Desktop\\UserInput" + surveyType + "\\" + userInputFile + "\"";
            strCmdText = "--start-fullscreen --incognito --disable-session-crashed-bubble \"C:\\Users\\" + User + "\\Desktop\\UserInput" + surveyType + "\\" + userInputFile + "\"";
            System.Diagnostics.Process.Start("chrome.exe", strCmdText);

        }

        private void CloseFoxit()//Makes sure foxit is closed before launch so it can be launch full-screen mode.
        {
            if (Process.GetProcessesByName("FoxitReader").Length > 0)
            {
                foreach (Process proc in Process.GetProcessesByName("FoxitReader"))
                {
                    proc.Kill();
                }
            }
        }

        private void CloseChrome()//Makes sure foxit is closed before launch so it can be launch full-screen mode.
        {
            if (Process.GetProcessesByName("chrome").Length > 0)
            {
                foreach (Process proc in Process.GetProcessesByName("chrome"))
                {
                    proc.Kill();
                }
            }
        }

        private bool SurveyChangedFoxitOpen(string inputText)
        {
            if (inputText.Substring(0, 4) == "page") //Makesure page&PN& format passed through from laptop.
            {
                char splitter = ' ';//Splitting at the space.
                string[] subStrings = inputText.Split(splitter);
                currentSurvey = subStrings[1].Substring(0, 5); 
                Process[] pname = Process.GetProcessesByName("FoxitReader");

                if (currentSurvey != previousSurvey && pname.Length == 0)//Different surveys but foxit isn't open.
                {
                    return false;
                }
                else if (currentSurvey != previousSurvey && pname.Length != 0)//Different surveys but foxit is open.
                {
                    return true;
                }
                else //Foxit remains open in all sume survey scenarios.
                {
                    return false;
                }
            }

            return false;//default false retunr to keep fozit open (i.e. no action if there is a more significant error).
        }

        private string getLatest(string directory)//Gets the name of the latest file created/updated in QuestionLog directory.
        {
            string Username = Environment.UserName;
            DirectoryInfo questionDirectory = new DirectoryInfo(directory);
            string latestFile = Path.GetFileName(FindLatestFile(questionDirectory).Name);
            return latestFile;

        }

        private static FileInfo FindLatestFile(DirectoryInfo directoryInfo)//Gets file info of latest file updated/created in directory.
        {
            if (directoryInfo == null || !directoryInfo.Exists)
                return null;

            FileInfo[] files = directoryInfo.GetFiles();
            DateTime lastWrite = DateTime.MinValue;
            FileInfo lastWrittenFile = null;

            foreach (FileInfo file in files)
            {
                if (file.LastWriteTime > lastWrite)
                {
                    lastWrite = file.LastWriteTime;
                    lastWrittenFile = file;
                }
            }
            return lastWrittenFile;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            //MessageBox.Show("Shown event works");

            Process[] pname = Process.GetProcessesByName("FoxitReader");

            if (pname.Length != 0)
            {
                this.WindowState = FormWindowState.Minimized;

            }

        }
    }

    public class AutoClosingMessageBox
    {
        System.Threading.Timer _timeoutTimer;
        string _caption;
        DialogResult _result;
        DialogResult _timerResult;
        AutoClosingMessageBox(string text, string caption, int timeout, MessageBoxButtons buttons = MessageBoxButtons.OK, DialogResult timerResult = DialogResult.None)
        {
            _caption = caption;
            _timeoutTimer = new System.Threading.Timer(OnTimerElapsed,
                null, timeout, System.Threading.Timeout.Infinite);
            _timerResult = timerResult;
            using (_timeoutTimer)
                _result = MessageBox.Show(text, caption, buttons);
        }
        public static DialogResult Show(string text, string caption, int timeout, MessageBoxButtons buttons = MessageBoxButtons.OK, DialogResult timerResult = DialogResult.None)
        {
            return new AutoClosingMessageBox(text, caption, timeout, buttons, timerResult)._result;
        }
        void OnTimerElapsed(object state)
        {
            IntPtr mbWnd = FindWindow("#32770", _caption); // lpClassName is #32770 for MessageBox
            if (mbWnd != IntPtr.Zero)
                SendMessage(mbWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            _timeoutTimer.Dispose();
            _result = _timerResult;
        }
        const int WM_CLOSE = 0x0010;
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);
    }
}
