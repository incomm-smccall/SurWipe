using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SurStore.Utils
{
    public static class MutexHelper
    {
        private static Mutex _appMutex;
        public static bool CreateMutex(string name)
        {
            _appMutex = new Mutex(false, name, out bool createNew);
            return createNew;
        }

        public static void CloseMutex()
        {
            if (_appMutex == null) return;

            _appMutex.Close();
            _appMutex = null;
        }
    }
}
