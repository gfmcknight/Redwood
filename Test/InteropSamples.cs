using System;
using System.Collections.Generic;
using System.Text;

namespace Test
{
    public class SampleClass
    {
        public int SampleField;
        public int SampleProperty { get; set; }

        public SampleClass()
        {

        }

        public SampleClass(int fieldVal, int propVal)
        {
            SampleField = fieldVal;
            SampleProperty = propVal;
        }

        public int SampleMethod(int value)
        {
            int lastValue = SampleField;
            SampleField = value;
            return lastValue;
        }

        public static int GetStatic(int i)
        {
            return i + 2;
        }

        public static int StaticOverload() { return 4; }
        public static int StaticOverload(int i) { return 5; }
        public static int StaticOverload(string s) { return 6; }

        public static SampleClass operator +(SampleClass a, SampleClass b)
        {
            return new SampleClass
            {
                SampleField = a.SampleField + b.SampleField,
                SampleProperty = a.SampleProperty + b.SampleProperty
            };
        }
    }

    public class OverloadClass1
    {
        public int f() { return 31; }
        public int f(object a) { return 32; }
        public int f(int a) { return 33; }
        public int f(string a) { return 34; }
        public int f(bool a) { return 35; }
        public int f(int a, object b) { return 36; }
        public int f(int a, string b) { return 37; }
        public int f(object a, int b) { return 38; }
    }

    public class OverloadClass2
    {
        public int f() { return 41; }
        public int f(dynamic a) { return 42; }
        public int f(int a) { return 43; }
        public int f(string a) { return 44; }
        public int f(bool a) { return 45; }
        public int f(int a, dynamic b) { return 46; }
        public int f(int a, string b) { return 47; }
        public int f(dynamic a, int b) { return 48; }
    }

    public class LLNode
    {
        public int Value { get; set; }
        public LLNode next;

        public LLNode(int value)
        {
            Value = value;
        }
    }
}
