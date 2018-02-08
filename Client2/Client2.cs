/////////////////////////////////////////////////////////////////////
// Client2.cs - sends TestRequests, displays results               //
// Author: Yadav Narayana Murthy, SUID: 990783888                  //
// Source:                                                         //
// Jim Fawcett, CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * -------------------
 * CreateServiceChannel(), download(), uploadFile() - helper functions for creating a 
 * file straming transfer functionality.
 * 
 * comm , endPoint , rcvThreadProc(), makeMessage(), makeRepoMessage() - properties and functions
 * for creating a WCF channel
 * 
 * Required Files:
 * - Client2.cs, ITest.cs, Logger.cs, CS-BlockingQueue.cs, Communication.cs, Messages.cs
 * 
 * Maintenance History:
 * --------------------
 * ver 1.0 : 20 Oct 2016
 * - first release
 * ver 2.0 : 21 Nov 2016
 * - by Yadava Narayana Murthy, SUID: 990783888 
 * - adds WCF communication channel to send and receive messages
 * - adds File Streaming capability to send DLL files to Repository
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Xml;
using System.IO;

namespace TestHarness
{
    public class Client : IClient
    {
        public SWTools.BlockingQueue<string> inQ_ { get; set; }
        private ITestHarness th_ = null;
        private IRepository repo_ = null;


        public Client(ITestHarness th)
        {
            Console.Write("\n  Creating instance of Client2");
            th_ = th;
        }
        public void setRepository(IRepository repo)
        {
            repo_ = repo;
        }

        //----< sendTestRequest function >---------------------------------------
        /*
         *  Used only if Repository and Test Harness are running on a single machine.
        *   Function to send test request to test harness. 
        */
        public void sendTestRequest(Message testRequest)
        {
            th_.sendTestRequest(testRequest);
        }


        //----< sendResults function >---------------------------------------
        /*
         *  Function to receive results 
        */
        public void sendResults(Message results)
        {
            Console.Write("\n  Client received results message:");
            Console.Write("\n  " + results.ToString());
            Console.WriteLine();
        }

        IStreamService channel;
        string ToSendPath = "..\\..\\Client2_ToSend";
        string SavePath = "..\\..\\SavedFiles";
        int BlockSize = 1024;
        byte[] block;
        HRTimer.HiResTimer hrt = null;

        public Comm<Client> comm { get; set; } = new Comm<Client>();

        public string endPoint { get; } = Comm<Client>.makeEndPoint("http://localhost", 8082);

        private Thread rcvThread = null;


        //----< initialize receiver >------------------------------------

        public Client()
        {
            comm.rcvr.CreateRecvChannel(endPoint);
            rcvThread = comm.rcvr.start(rcvThreadProc);

            block = new byte[BlockSize];
            hrt = new HRTimer.HiResTimer();
        }
        //----< join receive thread >------------------------------------

        public void wait()
        {
            rcvThread.Join();
        }

        string xmlReq = @"..\..\..\XMLRequestsFolder\Client2_TestRequest.xml";


        public string GetXMLAsString(XmlDocument myxml)
        {
            return myxml.OuterXml;
        }
        //----< construct a basic message >------------------------------

        public Message makeMessage(string author, string fromEndPoint, string toEndPoint)
        {

            XmlDocument doc = new XmlDocument();
            doc.Load(xmlReq);

            Message msg = new Message();
            msg.author = author;
            msg.from = fromEndPoint;
            msg.to = toEndPoint;
            msg.clientName = "Client2";
            msg.body = GetXMLAsString(doc);
            return msg;
        }

        //----< construct a repository query message>------------------------------
        public Message makeRepoMessage(string author, string clientName, string fromEndPoint, string toEndPoint, string text)
        {
            Message repMsg = new Message();
            repMsg.author = author;
            repMsg.from = fromEndPoint;
            repMsg.to = toEndPoint;
            repMsg.clientName = clientName;
            repMsg.toName = "Repository";
            repMsg.body = text;

            return repMsg;
        }

        //----< use private service method to receive a message >--------

        void rcvThreadProc()
        {
            while (true)
            {
                Message msg = comm.rcvr.GetMessage();
                msg.time = DateTime.Now;

                if (msg.from != "Repository")
                {
                    Console.Write("\n  {0} received message:", comm.name);
                    msg.showMsg();
                }

                // querying capability for first 10 responses
                if (msg.from == "Repository")
                {
                    List<string> files_ = msg.files;
                    Console.Write("\n  first 10 reponses to query: \n");
                    for (int i = 0; i < 10; ++i)
                    {
                        if (i == files_.Count())
                            break;
                        Console.Write("\n  " + files_[i]);
                    }
                }


                if (msg.body == "quit")
                    break;
            }
        }

        // function to create a service channel for transfering files 
        static IStreamService CreateServiceChannel(string url)
        {
            BasicHttpSecurityMode securityMode = BasicHttpSecurityMode.None;

            BasicHttpBinding binding = new BasicHttpBinding(securityMode);
            binding.TransferMode = TransferMode.Streamed;
            binding.MaxReceivedMessageSize = 500000000;
            EndpointAddress address = new EndpointAddress(url);

            ChannelFactory<IStreamService> factory
              = new ChannelFactory<IStreamService>(binding, address);
            return factory.CreateChannel();
        }

        // function used to upload a file to the channel by file streaming
        void uploadFile(string filename)
        {
            string fqname = Path.Combine(ToSendPath, filename);
            try
            {
                hrt.Start();
                using (var inputStream = new FileStream(fqname, FileMode.Open))
                {
                    FileTransferMessage msg = new FileTransferMessage();
                    msg.filename = filename;
                    msg.transferStream = inputStream;
                    channel.upLoadFile(msg);
                }
                hrt.Stop();
                Console.Write("\n  Uploaded file \"{0}\" in {1} microsec.", filename, hrt.ElapsedMicroseconds);
            }
            catch
            {
                Console.Write("\n  can't find \"{0}\"", fqname);
            }
        }

        // function to download a file from the file stream channel
        void download(string filename)
        {
            int totalBytes = 0;
            try
            {
                hrt.Start();
                Stream strm = channel.downLoadFile(filename);
                string rfilename = Path.Combine(SavePath, filename);
                if (!Directory.Exists(SavePath))
                    Directory.CreateDirectory(SavePath);
                using (var outputStream = new FileStream(rfilename, FileMode.Create))
                {
                    while (true)
                    {
                        int bytesRead = strm.Read(block, 0, BlockSize);
                        totalBytes += bytesRead;
                        if (bytesRead > 0)
                            outputStream.Write(block, 0, bytesRead);
                        else
                            break;
                    }
                }
                hrt.Stop();
                ulong time = hrt.ElapsedMicroseconds;
                Console.Write("\n  Received file \"{0}\" of {1} bytes in {2} microsec.", filename, totalBytes, time);
            }
            catch (Exception ex)
            {
                Console.Write("\n  {0}", ex.Message);
            }
        }

        //----< run client demo >----------------------------------------

        static void Main(string[] args)
        {

            Console.WriteLine("/////////////////////////////////////////////////////////////////");
            Console.WriteLine("            CSE681 - Software Modeling & Analysis                ");
            Console.WriteLine("               Project 4 - Remote Test Harness                   ");
            Console.WriteLine("          Yadav Narayana Murthy - SUID: 990783888                ");
            Console.WriteLine("//////////////////////////////////////////////////////////////////\n");

            Console.Title = "Client 2";
            Console.Write("\n  Client 2");
            Console.Write("\n =========\n");
            Console.Write(" REQ 1 - Shall be implemented in C# using the facilities of the .Net Framework Class Library and Visual Studio 2015");

            Console.Write("\n REQ 10 - Creating a WCF channel");
            Client client = new Client();

            Console.WriteLine(" \n File stream service starting:");
            Console.Write("=================================\n");
            Console.Write(" REQ 2 - Each test driver and the code it will be testing is implemented as a dynamic link library (DLL) and" +
                     " sent by the client to the Repository server before sending the Test Request to the Test Harness.\n");
            
            client.channel = CreateServiceChannel("http://localhost:8000/StreamService");
            HRTimer.HiResTimer hrt = new HRTimer.HiResTimer();

            // sending the DLLs to Repository
            Console.Write("\n REQ 6 - File Transfer using streams");
            hrt.Start();
            client.uploadFile("TestDriver2.dll");
            client.uploadFile("TestedCode2.dll");
            hrt.Stop();
            Console.Write(
              "\n\n  total elapsed time for uploading = {0} microsec.\n",
              hrt.ElapsedMicroseconds
            );

            // sending the Test Request to Test Harness
            Console.Write(" REQ 2 -  Sending Test Request to Test Harness");
            Message msg = client.makeMessage("John", client.endPoint, client.endPoint);
            string remoteEndPoint = Comm<Client>.makeEndPoint("http://localhost", 8080);
            msg = msg.copy();
            msg.to = remoteEndPoint;
            client.comm.sndr.PostMessage(msg);


            // sending the query message to Repository
            string repoEndPoint = Comm<Client>.makeEndPoint("http://localhost", 8095);
            Message repoQuery = client.makeRepoMessage("John", "Client2", client.endPoint, repoEndPoint, "John");
            client.comm.sndr.PostMessage(repoQuery);

            Console.Write("\n  press key to exit: ");
            Console.ReadKey();
            msg.to = client.endPoint;
            msg.body = "quit";
            client.comm.sndr.PostMessage(msg);
            client.wait();
            Console.Write("\n\n");

        }
    }
}
