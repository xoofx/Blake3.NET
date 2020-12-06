using System;
using NUnit.Framework;

namespace Blake3.Tests
{
    public abstract class Blake3TestsBase
    {
      
        protected static void AssertTextAreEqual(string expected, string result)
        {
            if (expected != result)
            {
                Console.WriteLine($"Expected: {expected}");
                Console.WriteLine($"  Result: {result}");
            }
            Assert.AreEqual(expected, result);
        }
    }
}