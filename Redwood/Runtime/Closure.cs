using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Runtime
{
    internal class Closure
    {
        internal WeakReference<Frame> frame;
        internal object[] data;
        internal Closure(Frame frame, int size)
        {
            this.frame = new WeakReference<Frame>(frame);
            data = new object[size];
        }
    }
}
