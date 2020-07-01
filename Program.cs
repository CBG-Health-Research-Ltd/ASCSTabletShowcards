using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BluetoothServerTest
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            //string pdf = "question14 other123";
            //OpenPDFpage(pdf);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
            killActiveProcess(); //kills app .exe that is for some reason constructed upon form closing.
      

        }
        static void killActiveProcess()
        {
            foreach (var process in Process.GetProcessesByName("BluetoothTest"))//name of backgroud app .exe
            {
                process.Kill();
            }
        }

       /* static void OpenPDFpage(string inputText)
        {
            if (inputText.Substring(0, 8) == "question") //Makesure question14 other123 document is passed through. Emulating survey.
            {
                char splitter = ' ';//Splitting at the spac between e.g. "question14 other123"
                string[] subStrings = inputText.Split(splitter);

                string pageNum = subStrings[0].Substring(8); //Page number hardcoded to correspond to question number.
                string otherInfo = subStrings[1].Substring(5);//Required info after the word "other". e.g 123
                string User = Environment.UserName;
                Process myProcess = new Process();
                myProcess.StartInfo.FileName = "FoxitReader.exe"; //Acrobat reader program
                myProcess.StartInfo.Arguments = //Assuming that showcard file is stored on the desktop.
                    "\"C:\\Users\\" + User + "\\Desktop\\NZHSChildShowcards.pdf\"" + " /A page=" + pageNum; //Page ID to be determined by look-up table.
                myProcess.Start();
            }
        }*/


    }
    }

