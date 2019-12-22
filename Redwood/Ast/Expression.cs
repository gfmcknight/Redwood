﻿using System;
using System.Collections.Generic;
using System.Text;
using Redwood.Instructions;
using Redwood.Runtime;

namespace Redwood.Ast
{
    public abstract class Expression : Statement
    {
        public bool Constant { get; } = false;

        public virtual RedwoodType GetKnownType()
        {
            // Null will be the default for "I don't know" or
            // "it may vary"
            return null;
        }
    }
}
