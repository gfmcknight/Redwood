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

        public static SampleClass operator +(SampleClass a, SampleClass b)
        {
            return new SampleClass
            {
                SampleField = a.SampleField + b.SampleField,
                SampleProperty = a.SampleProperty + b.SampleProperty
            };
        }
    }
}
