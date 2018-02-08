/////////////////////////////////////////////////////////////////////
// TestHarness.cs - TestHarness Engine: creates child domains      //
// ver 2.0                                                         //
// Author: Yadav Narayana Murthy, SUID: 990783888                  //
// Source:                                                         //
// Jim Fawcett, CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * -------------------
 * TestHarness package provides integration testing services.  It:
 * - receives structured test requests
 * - retrieves cited files from a repository
 * - executes tests on all code that implements an ITest interface,
 *   e.g., test drivers.
 * - reports pass or fail status for each test in a test request
 * - stores test logs in the repository
 * It contains classes:
 * - TestHarness that runs all tests in child AppDomains
 * - Callback to support sending messages from a child AppDomain to
 *   the TestHarness primary AppDomain.
 * - Test and RequestInfo to support transferring test information
 *   from TestHarness to child AppDomain
 * 
 * Required Files:
 * ---------------
 * - TestHarness.cs, BlockingQueue.cs
 * - ITest.cs
 * - LoadAndTest, Logger, Messages
 * - HiResTimer.cs, IStremService.cs
 *
 * Maintanence History:
 * --------------------
 * ver 2.0 : 13 Nov 2016
 * - added creation of threads to run tests in ProcessMessages
 * - removed logger statements as they were confusing with multiple threads
 * - added locking in a few places
 * - added more error handling
 * - No longer save temp directory name in member data of TestHarness class.
 *   It's now captured in TestResults data structure.
 * ver 1.1 : 11 Nov 2016
 * - added ability for test harness to pass a load path to
 *   LoadAndTest instance in child AppDomain
 * ver 1.0 : 16 Oct 2016
 * - first release
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Security.Policy;    // defines evidence needed for AppDomain construction
using System.Runtime.Remoting;   // provides remote communication between AppDomains
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;


namespace TestHarness
{
    ///////////////////////////////////////////////////////////////////
    // Callback class is used to receive messages from child AppDomain
    //
    public class Callback : MarshalByRefObject, ICallback
    {
        public void sendMessage(Message message)
        {
            Console.Write("\n  received msg from childDomain: \"" + message.body + "\"");
        }
    }
    ///////////////////////////////////////////////////////////////////
    // Test and RequestInfo are used to pass test request information
    // to child AppDomain
    //
    [Serializable]
    class Test : ITestInfo
    {
        public string testName { get; set; }
        public List<string> files { get; set; } = new List<string>();
    }
    [Serializable]
    class RequestInfo : IRequestInfo
    {
        public string tempDirName { get; set; }
        public List<ITestInfo> requestInfo { get; set; } = new List<ITestInfo>();
    }
    ///////////////////////////////////////////////////////////////////
    // class TestHarness

    public class TestHarness : ITestHarness
    {
        public SWTools.BlockingQueue<Message> inQ_ { get; set; } = new SWTools.BlockingQueue<Message>();
        private ICallback cb_;
        private IRepository repo_;
        private IClient client_;
        private string repoPath_ = "../../../Repository/RepositoryStorage/";
        private string filePath_;
        object sync_ = new object();
        List<Thread> threads_ = new List<Thread>();

        IStreamService channel;
        //string ToSendPath = "..\\..\\ToSend";
        //string SavePath = "..\\..\\SavedFiles";
        static int BlockSize = 1024;
        byte[] block = new byte[BlockSize];
        HRTimer.HiResTimer hrt = new HRTimer.HiResTimer();
        HRTimer.HiResTimer hrt2 = new HRTimer.HiResTimer();

        public TestHarness(IRepository repo)
        {
            Console.Write("\n  creating instance of TestHarness");
            repo_ = repo;
            repoPath_ = System.IO.Path.GetFullPath(repoPath_);
            cb_ = new Callback();

            block = new byte[BlockSize];
            hrt = new HRTimer.HiResTimer();
        }
        //----< called by TestExecutive >--------------------------------

        public void setClient(IClient client)
        {
            client_ = client;
        }
        //----< called by clients >--------------------------------------

        public void sendTestRequest(Message testRequest)
        {
            Console.Write("\n  TestHarness received a testRequest - REQUIREMENT #2");
            inQ_.enQ(testRequest);
        }
        //----< not used for Project #2 >--------------------------------

        public Message sendMessage(Message msg)
        {
            return msg;
        }
        //----< make path name from author and time >--------------------

        string makeKey(string author)
        {
            DateTime now = DateTime.Now;
            string nowDateStr = now.Date.ToString("d");
            string[] dateParts = nowDateStr.Split('/');
            string key = "";
            foreach (string part in dateParts)
                key += part.Trim() + '_';
            string nowTimeStr = now.TimeOfDay.ToString();
            string[] timeParts = nowTimeStr.Split(':');
            for (int i = 0; i < timeParts.Count() - 1; ++i)
                key += timeParts[i].Trim() + '_';
            key += timeParts[timeParts.Count() - 1];
            key = author + "_" + key + "_" + "ThreadID" + Thread.CurrentThread.ManagedThreadId;
            return key;
        }
        //----< retrieve test information from testRequest >-------------

        List<ITestInfo> extractTests(Message testRequest)
        {
            Console.Write("\n  parsing test request");
            List<ITestInfo> tests = new List<ITestInfo>();
            XDocument doc = XDocument.Parse(testRequest.body);
            foreach (XElement testElem in doc.Descendants("test"))
            {
                Test test = new Test();
                string testDriverName = testElem.Element("testDriver").Value;
                test.testName = testElem.Attribute("name").Value;
                test.files.Add(testDriverName);
                foreach (XElement lib in testElem.Elements("library"))
                {
                    test.files.Add(lib.Value);
                }
                tests.Add(test);
            }
            return tests;
        }
        //----< retrieve test code from testRequest >--------------------

        List<string> extractCode(List<ITestInfo> testInfos)
        {
            Console.Write("\n  retrieving code files from testInfo data structure");
            List<string> codes = new List<string>();
            foreach (ITestInfo testInfo in testInfos)
                codes.AddRange(testInfo.files);
            return codes;
        }
        //----< create local directory and load from Repository >--------

        RequestInfo processRequestAndLoadFiles(Message testRequest)
        {
            string localDir_ = "";
            RequestInfo rqi = new RequestInfo();
            rqi.requestInfo = extractTests(testRequest);
            List<string> files = extractCode(rqi.requestInfo);

            localDir_ = makeKey(testRequest.author);            // name of temporary dir to hold test files
            rqi.tempDirName = localDir_;
            filePath_ = System.IO.Path.GetFullPath(localDir_);  // LoadAndTest will use this path
            Console.Write("\nREQ 8 - creating local test directory \"" + localDir_ + "\"");
            System.IO.Directory.CreateDirectory(localDir_);

            channel = CreateServiceChannel("http://localhost:8000/StreamService");
            Console.Write("\n  loading code from Repository");
            foreach (string file in files)
            {
                string name = System.IO.Path.GetFileName(file);
                string src = System.IO.Path.Combine(repoPath_, file);
                if (System.IO.File.Exists(src))
                {
                    string dst = System.IO.Path.Combine(localDir_, name);
                    try
                    {
                        download(file, localDir_);

                    }
                    catch
                    {
                        Console.Write("\n    TID" + Thread.CurrentThread.ManagedThreadId + ": could not load file \"" + name + "\"");
                    }
                    Console.Write("\n    TID" + Thread.CurrentThread.ManagedThreadId + ": retrieved file \"" + name + "\"");
                }
                else
                {
                    Console.Write("\n    TID" + Thread.CurrentThread.ManagedThreadId + ": could not retrieve file \"" + name + "\"");
                }
            }
            Console.WriteLine();
            return rqi;
        }
        //----< save results and logs in Repository >--------------------

        bool saveResultsAndLogs(ITestResults testResults)
        {
            string logName = testResults.testKey + ".txt";
            System.IO.StreamWriter sr = null;
            try
            {
                sr = new System.IO.StreamWriter(System.IO.Path.Combine(repoPath_, logName));
                sr.WriteLine(logName);
                foreach (ITestResult test in testResults.testResults)
                {
                    sr.WriteLine("-----------------------------");
                    sr.WriteLine(test.testName);
                    sr.WriteLine(test.testResult);
                    sr.WriteLine(test.testLog);
                }
                sr.WriteLine("-----------------------------");
            }
            catch
            {
                sr.Close();
                return false;
            }
            sr.Close();
            return true;
        }
        //----< run tests >----------------------------------------------
        /*
         * In Project #4 this function becomes the thread proc for
         * each child AppDomain thread.
         */
        ITestResults runTests(Message testRequest)
        {
            AppDomain ad = null;
            ILoadAndTest ldandtst = null;
            RequestInfo rqi = null;
            ITestResults tr = null;

            try
            {
                lock (sync_)
                {
                    rqi = processRequestAndLoadFiles(testRequest);
                    ad = createChildAppDomain();
                    ldandtst = installLoader(ad);
                }
                if (ldandtst != null)
                {
                    tr = ldandtst.test(rqi);
                }
                // unloading ChildDomain, and so unloading the library

                Console.Write("\n REQ 7 - Test Harness shall store the test results and logs in the Repository");
                saveResultsAndLogs(tr);

                lock (sync_)
                {
                    Console.Write("\nREQ 7 -  TID" + Thread.CurrentThread.ManagedThreadId + ": unloading: \"" + ad.FriendlyName + "\"\n");
                    AppDomain.Unload(ad);
                    try
                    {
                        System.IO.Directory.Delete(rqi.tempDirName, true);
                        Console.Write("\n  TID" + Thread.CurrentThread.ManagedThreadId + ": removed directory " + rqi.tempDirName);
                    }
                    catch (Exception ex)
                    {
                        Console.Write("\n  TID" + Thread.CurrentThread.ManagedThreadId + ": could not remove directory " + rqi.tempDirName);
                        Console.Write("\n  TID" + Thread.CurrentThread.ManagedThreadId + ": " + ex.Message);
                    }
                }
                return tr;
            }
            catch (Exception ex)
            {
                Console.Write("\n\n---- {0}\n\n", ex.Message);
                return tr;
            }
        }
        //----< make TestResults Message >-------------------------------

        Message makeTestResultsMessage(ITestResults tr, Message testRequest)
        {
            Message trMsg = new Message();
            trMsg.author = "TestHarness";
            trMsg.to = testRequest.from;
            trMsg.from = "TH";
            XDocument doc = new XDocument();
            XElement root = new XElement("testResultsMsg");
            doc.Add(root);
            XElement testKey = new XElement("testKey");
            testKey.Value = tr.testKey;
            root.Add(testKey);
            XElement timeStamp = new XElement("timeStamp");
            timeStamp.Value = tr.dateTime.ToString();
            root.Add(timeStamp);
            XElement testResults = new XElement("testResults");
            root.Add(testResults);
            foreach (ITestResult test in tr.testResults)
            {
                XElement testResult = new XElement("testResult");
                testResults.Add(testResult);
                XElement testName = new XElement("testName");
                testName.Value = test.testName;
                testResult.Add(testName);
                XElement result = new XElement("result");
                result.Value = test.testResult;
                testResult.Add(result);
                XElement log = new XElement("log");
                log.Value = test.testLog;
                testResult.Add(log);
            }
            trMsg.body = doc.ToString();
            return trMsg;
        }


        public void processMessages()
        {
            AppDomain main = AppDomain.CurrentDomain;
            Console.Write("\n  Starting in AppDomain " + main.FriendlyName + "\n");

            ThreadStart doTests = () =>
            {
                Message testRequest = inQ_.deQ();
                if (testRequest.body == "quit")
                {
                    inQ_.enQ(testRequest);
                    return;
                }
                hrt2.Start();
                ITestResults testResults = runTests(testRequest);
                hrt2.Stop();
                lock (sync_)
                {
                    comm.sndr.PostMessage(makeTestResultsMessage(testResults, testRequest));
                }
                Console.Write("\n REQ 12 - Time taken for test Execution {0} is {1}", testRequest.clientName, hrt2.ElapsedMicroseconds);
            };
            Console.Write("\n  Creating AppDomain thread");
            int numThreads = 8;

            for (int i = 0; i < numThreads; ++i)
            {
          
                Thread t = new Thread(doTests);
                threads_.Add(t);
                t.Start();
            }
        }
        //----< was used for debugging >---------------------------------

        void showAssemblies(AppDomain ad)
        {
            Assembly[] arrayOfAssems = ad.GetAssemblies();
            foreach (Assembly assem in arrayOfAssems)
                Console.Write("\n  " + assem.ToString());
        }
        //----< create child AppDomain >---------------------------------

        public AppDomain createChildAppDomain()
        {
            try
            {
                Console.Write("\n  REQ 4 - creating child AppDomain");

                AppDomainSetup domaininfo = new AppDomainSetup();
                domaininfo.ApplicationBase
                  = "file:///" + System.Environment.CurrentDirectory;  // defines search path for LoadAndTest library

                //Create evidence for the new AppDomain from evidence of current

                Evidence adevidence = AppDomain.CurrentDomain.Evidence;

                // Create Child AppDomain

                AppDomain ad
                  = AppDomain.CreateDomain("ChildDomain", adevidence, domaininfo);

                Console.Write("\n  created AppDomain \"" + ad.FriendlyName + "\"");
                return ad;
            }
            catch (Exception except)
            {
                Console.Write("\n  " + except.Message + "\n\n");
            }
            return null;
        }
        //----< Load and Test is responsible for testing >---------------

        ILoadAndTest installLoader(AppDomain ad)
        {
            ad.Load("LoadAndTest");
            //showAssemblies(ad);
            //Console.WriteLine();

            // create proxy for LoadAndTest object in child AppDomain

            ObjectHandle oh
              = ad.CreateInstance("LoadAndTest", "TestHarness.LoadAndTest");
            object ob = oh.Unwrap();    // unwrap creates proxy to ChildDomain
                                        // Console.Write("\n  {0}", ob);

            // set reference to LoadAndTest object in child

            ILoadAndTest landt = (ILoadAndTest)ob;

            // create Callback object in parent domain and pass reference
            // to LoadAndTest object in child

            landt.setCallback(cb_);
            landt.loadPath(filePath_);  // send file path to LoadAndTest
            return landt;
        }

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

        void download(string filename, string savepath)
        {
            int totalBytes = 0;
            try
            {
                hrt.Start();
                Stream strm = channel.downLoadFile(filename);
                string rfilename = Path.Combine(savepath, filename);
                if (!Directory.Exists(savepath))
                    Directory.CreateDirectory(savepath);
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

        public Comm<TestHarness> comm { get; set; } = new Comm<TestHarness>();

        public string endPoint { get; } = Comm<TestHarness>.makeEndPoint("http://localhost", 8080);

        private Thread rcvThread = null;

        public TestHarness()
        {
            comm.rcvr.CreateRecvChannel(endPoint);
            rcvThread = comm.rcvr.start(rcvThreadProc);
        }

        public void wait()
        {
            rcvThread.Join();
        }
        public Message makeMessage(string author, string fromEndPoint, string toEndPoint)
        {
            Message msg = new Message();
            msg.author = author;
            msg.from = fromEndPoint;
            msg.to = toEndPoint;
            return msg;
        }

        void rcvThreadProc()
        {
            while (true)
            {
                Message msg = comm.rcvr.GetMessage();
                msg.time = DateTime.Now;
                Console.Write("\n  {0} received message:", comm.name);
                msg.showMsg();
                if (msg.body == "quit")
                    break;

                sendTestRequest(msg);
                processMessages();
            }
        }
        static void Main(string[] args)
        {

            Console.WriteLine("/////////////////////////////////////////////////////////////////");
            Console.WriteLine("            CSE681 - Software Modeling & Analysis                ");
            Console.WriteLine("               Project 4 - Remote Test Harness                   ");
            Console.WriteLine("          Yadav Narayana Murthy - SUID: 990783888                ");
            Console.WriteLine("//////////////////////////////////////////////////////////////////\n");

            Console.Title = "Test Harness";
            Console.Write("\n  Test Harness Server");
            Console.Write("\n =====================\n");
            Console.Write("REQ 1 - Shall be implemented in C# using the facilities of the .Net Framework Class Library and Visual Studio 2015");

            Console.Write("\n REQ 10 - Creating a WCF channel");
            TestHarness Server = new TestHarness();

            Message msg = Server.makeMessage("Fawcett", Server.endPoint, Server.endPoint);

            ///////////////////////////////////////////////////////////////
            // uncomment lines below to enable sending messages to Client

            //Server.comm.sndr.PostMessage(msg);

            //msg = Server.makeMessage("Fawcett", Server.endPoint, Server.endPoint);
            //msg.body = MessageTest.makeTestRequest();
            //Server.comm.sndr.PostMessage(msg);

            //string remoteEndPoint = Comm<Server>.makeEndPoint("http://localhost", 8081);
            //msg = msg.copy();
            //msg.to = remoteEndPoint;
            //Server.comm.sndr.PostMessage(msg);

            //Console.Write("\n  press key to exit: ");
            //Console.ReadKey();
            //msg.to = Server.endPoint;
            //msg.body = "quit";
            //Server.comm.sndr.PostMessage(msg);
            //Server.wait();
            //Console.Write("\n\n");
        }
    }
}
