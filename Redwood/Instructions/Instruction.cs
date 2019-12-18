using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Instructions
{
    internal interface Instruction
    {
        int Execute(Frame frame);
    }
}
