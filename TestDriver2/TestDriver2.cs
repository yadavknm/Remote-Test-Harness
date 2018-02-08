/////////////////////////////////////////////////////////////////////
// TestDriver.cs - defines testing process                         //
// Author: Yadav Narayana Murthy, SUID: 990783888                  //
// Source:                                                         //
// Jim Fawcett, CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////
/*
*   Test driver needs to know the types and their interfaces
*   used by the code it will test.  It doesn't need to know
*   anything about the test harness.
*/
/*
 *   Build Process
 *   -------------
 *   - Required files:   AnotherTestDriver.cs, AnotherTested.cs, ITest.cs
 * 
 *   Maintenance History
 *   -------------------
 *   ver 1.0 : 22 October 2013
 *     - first release
 *   ver 2.0 : 11 October 2016
 *      - by Yadav Narayana Murthy, SUID: 990783888, Syracuse University
 *      - modifications to test() function to suit the Project 4.
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestHarness;

namespace TestHarness
{
    public class TestDriver : ITest
    {
        public bool test()// test function deriving from ITest interface
        {
            Console.Write("\n REQ 5 - test driver derives from an ITest interface that declares a method test()");
            TestHarness.Tested2 tested = new TestHarness.Tested2();
            return tested.performAnyTest();  // calling the function to perform a test as defined by the user
        }
        public string getLog() // used for logging
        {
            return "demo test that always passes";
        }
#if (TEST_TESTDRIVER)
        static void Main(string[] args)
        {
            TestDriver td = new TestDriver();
            td.test();
            td.getLog();
        }
        }
#endif
    }
}
