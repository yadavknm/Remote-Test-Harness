///////////////////////////////////////////////////////////////////////
// ChannelDemo.cs - Demonstrate use of channel with a single process //
// Author: Yadav Narayana Murthy, SUID: 990783888                    //
// Source:                                                           //
// Jim Fawcett, CSE681 - Software Modeling and Analysis, Fall 2016   //
///////////////////////////////////////////////////////////////////////
/*
 * Package Operations:
 * -------------------
 * The ChannelDemo package defines one class, ChannelDemo, that uses 
 * the Comm<Client> and Comm<Server> classes to pass messages to one 
 * another.
 * 
 * Required Files:
 * ---------------
 * - ChannelDemo.cs
 * - ICommunicator.cs, CommServices.cs
 * - Messages.cs, MessageTest, Serialization
 *
 * Maintenance History:
 * --------------------
 * Ver 1.0 : 10 Nov 2016
 * - first release 
 *  
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Utilities;

namespace TestHarness
{
    ///////////////////////////////////////////////////////////////////
    // ChannelDemo class
    // - Shows how to define Sender and Receiver classes using packages
    //   CommService.cs, ICommunicator.cs, BlockingQueue.cs, Messages.cs.
    // - Each endpoint would have one of each.
    // - This demo does that, but simply sends messages to itself
    //
    class ChannelDemo<T>
    {
        public Comm<T> comm { get; set; } = new Comm<T>();

        public string name { get; set; } = typeof(T).Name;

        //----< intialize sender and receiver >--------------------------

        public ChannelDemo()
        {
        }
        //----< define receive thread processing >-----------------------

        public void rcvThreadProc()
        {
            Message msg = new Message();
            while (true)
            {
                msg = comm.rcvr.GetMessage();
                Console.Write("\n  getting message on rcvThread {0}", Thread.CurrentThread.ManagedThreadId);
                if (msg.type == "TestRequest")
                {
                    TestRequest tr = msg.body.FromXml<TestRequest>();
                    if (tr != null)
                    {
                        Console.Write(
                          "\n  {0}\n  received message from:  {1}\n{2}\n  deserialized body:\n{3}",
                          msg.to, msg.from, msg.body.shift(), tr.showThis()
                          );
                        if (msg.body == "quit")
                            break;
                    }
                }
                else
                {
                    Console.Write("\n  {0}\n  received message from:  {1}\n{2}", msg.to, msg.from, msg.body.shift());
                    if (msg.body == "quit")
                        break;
                }
            }
            Console.Write("\n  receiver {0} shutting down\n", msg.to);
        }
        //----< message creator >----------------------------------------
        /*
         * - creating MessageBody class for each message body type.
         * - using serializer, as demoed in TestDeserializer, to generate the
         *   body XML.
         * - On the other end deserialize, using the MessageBody type.
         */
        public string makeTestRequest()
        {
            TestElement te1 = new TestElement("test1");
            te1.addDriver("td1.dll");
            te1.addCode("t1.dll");
            te1.addCode("t2.dll");
            TestElement te2 = new TestElement("test2");
            te2.addDriver("td2.dll");
            te2.addCode("tc3.dll");
            te2.addCode("tc4.dll");
            TestRequest tr = new TestRequest();
            tr.author = "Jim Fawcett";
            tr.tests.Add(te1);
            tr.tests.Add(te2);
            return tr.ToXml();
        }
    }
    class Client { }
    class Server { }

    // test stub
#if (TEST_COMMDEMO)
    class TestDemoChannel
    {
        static void Main(string[] args)
        {
            Console.Write("\n  Demonstrating Project #4 Channel Prototype");
            Console.Write("\n ============================================\n");

            ChannelDemo<Client> demo1 = new ChannelDemo<Client>();

            string sndrEndPoint1 = Comm<Client>.makeEndPoint("http://localhost", 8080);
            string rcvrEndPoint1 = Comm<Server>.makeEndPoint("http://localhost", 8080);
            demo1.comm.rcvr.CreateRecvChannel(rcvrEndPoint1);
            Thread rcvThread1 = demo1.comm.rcvr.start(demo1.rcvThreadProc);
            Console.Write("\n  rcvr thread id = {0}", rcvThread1.ManagedThreadId);
            Console.WriteLine();

            ChannelDemo<Server> demo2 = new ChannelDemo<Server>();

            string sndrEndPoint2 = Comm<Client>.makeEndPoint("http://localhost", 8081);
            string rcvrEndPoint2 = Comm<Server>.makeEndPoint("http://localhost", 8081);
            demo2.comm.rcvr.CreateRecvChannel(rcvrEndPoint2);
            Thread rcvThread2 = demo2.comm.rcvr.start(demo2.rcvThreadProc);
            Console.Write("\n  rcvr thread id = {0}", rcvThread2.ManagedThreadId);
            Console.WriteLine();

            // make a TestRequest message and send five times to two different endpoints

            Message msg = null;
            string rcvrEndPoint;

            for (int i = 0; i < 5; ++i)
            {
                msg = new Message(demo1.makeTestRequest());
                msg.type = "TestRequest";
                msg.from = sndrEndPoint1;
                if (i < 3)
                    msg.to = rcvrEndPoint = rcvrEndPoint1;
                else
                    msg.to = rcvrEndPoint = rcvrEndPoint2;
                msg.author = "Fawcett";
                msg.time = DateTime.Now;

                demo1.comm.sndr.PostMessage(msg);

                Console.Write("\n  {0}\n  posting message with body:\n{1}", msg.from, msg.body.shift());
                Thread.Sleep(20);
            }
            msg = new Message();
            msg.from = sndrEndPoint1;
            msg.to = rcvrEndPoint2;
            msg.body = "quit";

            demo1.comm.sndr.PostMessage(msg);
            rcvThread2.Join();
            Console.Write("\n  rcvThread1.state = {0}", rcvThread1.ThreadState.ToString());
            Console.Write("\n  rcvThread2.state = {0}", rcvThread2.ThreadState.ToString());
            msg = new Message();
            msg.from = sndrEndPoint2;
            msg.to = rcvrEndPoint1;
            msg.body = "quit";

            demo2.comm.sndr.PostMessage(msg);
            Thread.Sleep(500);
            Console.Write("\n  rcvThread1.state = {0}", rcvThread1.ThreadState.ToString());

            rcvThread1.Join();
            Console.Write("\n  passed 2nd Join");

            Console.Write("\n\n");
        }
    }
#endif
}
