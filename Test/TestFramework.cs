using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Test
{
    public class RAssert
    {
        public int AssertionsCount { get; private set; }

        public void Equal(object expected, object actual)
        {
            AssertionsCount++;
            Assert.Equal(expected, actual);
        }

        public void IsType(RedwoodType type, object item)
        {
            AssertionsCount++;

            Assert.NotNull(type);
            Assert.NotNull(item);

            if (item is RedwoodObject ro)
            {
                Assert.Null(type.CSharpType);
                Assert.Equal(type, ro.Type);
            }
            else
            {
                Assert.NotNull(type.CSharpType);
                Assert.Equal(type.CSharpType, item.GetType());
            }
        }
    }
}