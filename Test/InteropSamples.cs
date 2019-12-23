using System;
using System.Collections.Generic;
using System.Text;

namespace Test
{
    public class SampleClass
    {
        public int SampleField;
        public int SampleProperty { get; set; }

        public int SampleMethod(int value)
        {
            int lastValue = SampleField;
            SampleField = value;
            return lastValue;
        }
    }
}
