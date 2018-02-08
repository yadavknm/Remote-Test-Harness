/////////////////////////////////////////////////////////////////////
// Repository.cs - holds test code for TestHarness                 //
// Author: Yadav Narayana Murthy, SUID: 990783888                  //
// Source:                                                         //
// Jim Fawcett, CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * -------------------
 * comm, endPoint, rcvThread used for WCF channel that is created for transferring messages.
 * 
 * File streaming channel created for uploading and downloading files. CreateServiceChannel() is used.
 * 
 * Required Files:
 * - Client.cs, ITest.cs, Logger.cs, Messages.cs, Communication.cs, HiResTimer.cs, IstreamService.cs
 * 
 * Maintenance History:
 * --------------------
 * ver 1.0 : 20 Oct 2016
 * - first release
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;

namespace TestHarness
{

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class Repository : IRepository, IStreamService
    {

        // properties used for file streaming 
        string repoStoragePath = "..\\..\\..\\Repository\\RepositoryStorage\\";

        string filename;
        string savePath = "..\\..\\RepositoryStorage";
        string ToSendPath = "..\\..\\RepositoryStorage";
        int BlockSize = 1024;
        byte[] block;
        HRTimer.HiResTimer hrt = null;

        // properties used for WCF channel creation
        public Comm<Repository> comm { get; set; } = new Comm<Repository>();

        public string endPoint { get; } = Comm<Repository>.makeEndPoint("http://localhost", 8095);

        private Thread rcvThread = null;

        public Repository()
        {
            block = new byte[BlockSize];
            hrt = new HRTimer.HiResTimer();
        }

        void rcvThreadProc()
        {
            while (true)
            {
                Message msg = comm.rcvr.GetMessage();
                msg.time = DateTime.Now;
                Console.Write("\n\n REQ 9 - Repository shall support client queries about Test Results from the Repository storage");
                Console.Write("\n  Received Log query from {0} with search string: {1}", msg.clientName, msg.body);

                if (msg.body == "quit")
                    break;

                queryLogs(msg);

            }
        }

        // not used in this project. But could be implemented for future use.
        public void sendLog(string log)
        {

        }

        //----< search for text in log files >---------------------------
        /*
         * This function returns a message for the query.
         */
        public void queryLogs(Message msg)
        {

            List<string> queryResults = new List<string>();
            string path = System.IO.Path.GetFullPath(repoStoragePath);
            string[] files = System.IO.Directory.GetFiles(repoStoragePath, "*.txt");
            foreach (string file in files)
            {
                string contents = File.ReadAllText(file);
                if (contents.Contains(msg.body))
                {
                    string name = System.IO.Path.GetFileName(file);
                    queryResults.Add(name);
                }
            }
            queryResults.Sort();
            queryResults.Reverse();

            Message rMsg = new Message();
            rMsg.author = "Repository";
            rMsg.to = msg.from;
            rMsg.from = "Repository";
            rMsg.toName = msg.clientName;
            rMsg.files = queryResults;

            comm.sndr.PostMessage(rMsg);

        }

        //----< send files with names on fileList >----------------------
        public bool getFiles(string path, string fileList)
        {
            string[] files = fileList.Split(new char[] { ',' });

            foreach (string file in files)
            {
                string fqSrcFile = repoStoragePath + file;
                string fqDstFile = "";
                try
                {
                    fqDstFile = path + "\\" + file;
                    File.Copy(fqSrcFile, fqDstFile);
                }
                catch
                {
                    Console.Write("\n  could not copy \"" + fqSrcFile + "\" to \"" + fqDstFile);
                    return false;
                }
            }
            return true;
        }

        //----< downLoadFile function >---------------------------------------
        /*
        *   function to read the uploaded file from clients
        */
        public void upLoadFile(FileTransferMessage msg)
        {
            int totalBytes = 0;
            hrt.Start();
            filename = msg.filename;
            string rfilename = Path.Combine(savePath, filename);
            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);
            using (var outputStream = new FileStream(rfilename, FileMode.Create))
            {
                while (true)
                {
                    int bytesRead = msg.transferStream.Read(block, 0, BlockSize);
                    totalBytes += bytesRead;
                    if (bytesRead > 0)
                        outputStream.Write(block, 0, bytesRead);
                    else
                        break;
                }
            }
            hrt.Stop();
            Console.Write(
              "\n  Received file \"{0}\" of {1} bytes in {2} microsec.",
              filename, totalBytes, hrt.ElapsedMicroseconds
            );
        }

        //----< downLoadFile function >---------------------------------------
        /*
        *   This function is used to download the files from the channel 
        */
        public Stream downLoadFile(string filename)
        {
            hrt.Start();
            string sfilename = Path.Combine(ToSendPath, filename);
            FileStream outStream = null;
            if (File.Exists(sfilename))
            {
                outStream = new FileStream(sfilename, FileMode.Open);
            }
            else
                throw new Exception("open failed for \"" + filename + "\"");
            hrt.Stop();
            Console.Write("\n  Sent \"{0}\" in {1} microsec.", filename, hrt.ElapsedMicroseconds);
            return outStream;
        }

        //----< CreateServiceChannel function >---------------------------------------
        /*
        *   This function is used to create a channel. 
        */
        static ServiceHost CreateServiceChannel(string url)
        {
            // Can't configure SecurityMode other than none with streaming.
            // This is the default for BasicHttpBinding.
            //   BasicHttpSecurityMode securityMode = BasicHttpSecurityMode.None;
            //   BasicHttpBinding binding = new BasicHttpBinding(securityMode);

            BasicHttpBinding binding = new BasicHttpBinding();
            binding.TransferMode = TransferMode.Streamed;
            binding.MaxReceivedMessageSize = 50000000;
            Uri baseAddress = new Uri(url);
            Type service = typeof(Repository);
            ServiceHost host = new ServiceHost(service, baseAddress);
            host.AddServiceEndpoint(typeof(IStreamService), binding, baseAddress);
            return host;
        }


        public static void Main()
        {
            Console.WriteLine("/////////////////////////////////////////////////////////////////");
            Console.WriteLine("            CSE681 - Software Modeling & Analysis                ");
            Console.WriteLine("               Project 4 - Remote Test Harness                   ");
            Console.WriteLine("          Yadav Narayana Murthy - SUID: 990783888                ");
            Console.WriteLine("//////////////////////////////////////////////////////////////////\n");

            Console.Title = "Repository";
            Console.Write("REQ 1 - Shall be implemented in C# using the facilities of the .Net Framework Class Library and Visual Studio 2015");


            Repository rep = new Repository();

            Console.Write("\n REQ 10 - Creating a WCF channel");
            rep.comm.rcvr.CreateRecvChannel(rep.endPoint);
            rep.rcvThread = rep.comm.rcvr.start(rep.rcvThreadProc);

            ServiceHost host = CreateServiceChannel("http://localhost:8000/StreamService");


            host.Open();
            Console.Write("\n REQ 12 - Shall include means to time test executions and communication latency");
            Console.Write("\n  SelfHosted File Stream Service started");
            Console.Write("\n ========================================\n");
            Console.Write("\n  Press key to terminate service:\n");
            Console.ReadKey();
            Console.Write("\n");
            host.Close();
        }


    }
}
