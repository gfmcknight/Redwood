using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Runtime
{
    class RedwoodObject
    {
        // The fields/values attached to this object
        internal object[] slots;
        
        public RedwoodType Type { get; set; }

        public object this[string key]
        {
            get
            {
                return slots[Type.slotMap[key]];
            }

            set
            {
                slots[Type.slotMap[key]] = value;
            }
        }
    }
}
