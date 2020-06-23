using System.Threading;

namespace SurWipe.Utils
{
    public static class MutexHelper
    {
        private static Mutex _appMutex;

        public static bool CreateMutex(string name)
        {
            _appMutex = new Mutex(false, name, out bool createdNew);
            return createdNew;
        }

        public static void CloseMutex()
        {
            if (_appMutex != null)
            {
                _appMutex.Close();
                _appMutex = null;
            }
        }
    }
}
