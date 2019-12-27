using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Runtime
{
    public class GlobalContext
    {
        private Dictionary<string, object> elements;

        internal GlobalContext()
        {
            elements = new Dictionary<string, object>();
        }

        public object LookupVariable(string name)
        {
            return elements.GetValueOrDefault(name);
        }

        internal void AssignVariable(string name, object result)
        {
            elements[name] = result;
        }
    }
}
