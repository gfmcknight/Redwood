using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Redwood.Instructions
{
    [DebuggerDisplay("Tag: {tag}")]
    internal class TagInstruction : Instruction
    {
        string tag;

        public TagInstruction(string tag)
        {
            this.tag = tag;
        }

        public int Execute(Frame frame)
        {
            // Do nothing
            return 1;
        }
    }
}
