using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bomberman.Shared
{
    public class Counter
    {
        private int m_Id;
        public Counter()
        {
            m_Id = 0;
        }
        public int Next()
        {
            return ++m_Id;
        }
    }
}
