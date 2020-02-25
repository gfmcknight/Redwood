using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Redwood.Instructions
{
    internal class DynamicConvertInstruction : Instruction
    {
        private RedwoodType to;

        public DynamicConvertInstruction(RedwoodType to)
        {
            this.to = to;
        }

        public int Execute(Frame frame)
        {
            object result = frame.result;
            if (to.IsAssignableFrom(result))
            {
                return 1;
            }

            RedwoodType type;
            if (result is RedwoodObject rwo)
            {
                type = rwo.Type;
            }
            else
            {
                type = RedwoodType.GetForCSharpType(result.GetType());
            }

            // TODO: this may be repeating existing work that occurs later
            if (!type.HasImplicitConversion(to))
            {
                throw new NotImplementedException();
            }

            Lambda conversion = RuntimeUtil.GetConversionLambda(type, to);
            frame.result = conversion.Run(frame.result);
            return 1;
        }
    }
}
