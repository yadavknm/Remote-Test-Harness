/////////////////////////////////////////////////////////////////////
// AnotherTested.cs - code to test                                 //
// Author: Yadav Narayana Murthy, SUID: 990783888                  //
// Source:                                                         //
// Jim Fawcett, CSE681 - Software Modeling and Analysis, Fall 2016 //
/////////////////////////////////////////////////////////////////////
/*
 *   Build Process
 *   -------------
 *   - Required files:    AnotherTestDriver.cs, AnotherTested.cs, ITest.cs
 * 
 *   Maintenance History
 *   -------------------
 *   ver 1.0 : 22 October 2013
 *     - first release
 *   ver 2.0 : 11 October 2016
 *      - by Yadav Narayana Murthy, SUID: 990783888, Syracuse University
 *      - modifications to performAnyTest() function to suit the Project 4.
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestHarness
{
  public class Tested2
  {
    public bool performAnyTest()
    {
      return true;
    }
#if (TEST_TESTED)
    static void Main(string[] args)
    {
    }
#endif
  }
}
