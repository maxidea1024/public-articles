using System;
using System.Threading;
using System.Threading.Tasks;

namespace G.DataSet
{
    public class _SemaphoreRefer : IDisposable
    {
        protected bool __disposed = false;
        private SemaphoreSlim __semaphore;

        public int __waits { get; private set; } = 0;
        public int __thread { get; private set; } = 0;

        public SemaphoreSlim GetSemaphore() { return __semaphore; }

        public _SemaphoreRefer(SemaphoreSlim semaphore)
        {
            __disposed = false;
            __waits = 0;

            __semaphore = semaphore;
        }

        ~_SemaphoreRefer()
        {
            Dispose(false);
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (false == disposing)
                return;

            if (true == __disposed)
                return;

            if (null == __semaphore)
            {
                __disposed = true;
                return;
            }

            if (0 == __waits) // no use..
                return;

            __disposed = true;
            __semaphore.Release();
        }

        public Task         WaitAsync()             { __waits++; var task = __semaphore.WaitAsync(); __thread = Thread.CurrentThread.ManagedThreadId; return task; }
        public Task<bool>   WaitAsync(int timeout)  { __waits++; var task = __semaphore.WaitAsync(timeout); __thread = Thread.CurrentThread.ManagedThreadId; return task; }
    }

    public class SemaphoreLockStr
    {
        private static NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();

        private _SemaphoreRefer __semaphore = new _SemaphoreRefer( new SemaphoreSlim(1, 1));
        private string __message = string.Empty;

        private string __caller = string.Empty;
        private string __file = string.Empty;
        private int __line = 0;
        private int __thread = 0;

        public SemaphoreLockStr()
        {
            __message = "SemaphoreLockStr";
        }

        public SemaphoreLockStr(int millisecondsTimeout)
        {
            //this.millisecondsTimeout = millisecondsTimeout;

        }

        public SemaphoreLockStr(TimeSpan timeout)
        {
            //this.timeout = timeout;

        }

        public SemaphoreLockStr(CancellationToken cancellationToken)
        {
            //this.cancellationToken = cancellationToken;
        }

        public async Task<_SemaphoreRefer> LockAsync(bool need = true,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            var temp = new _SemaphoreRefer(__semaphore.GetSemaphore());

            if (true == need)
            {
                try
                {
                    __thread = Thread.CurrentThread.ManagedThreadId;
                    if (__thread == temp.__thread)
                    {
                        log.Debug($"!! LockAsync Same Thread..\n last: {__message}.{__caller} : {__line} \n call: {_caller} \n file: {_file}: {_line}");
                        //return temp;
                    }

                    await temp.WaitAsync();

                    __message   = "true";
                    __caller    = _caller;
                    __file      = _file;
                    __line      = _line;
                }
                catch
                {
                    temp = null;
                    log.Debug($"!! LockAsync Fault..\n last: {__message}.{__caller} : {__line} \n call: {_caller} \n file: {_file}: {_line}");
                    throw;
                }
                finally
                {

                }
            }
            return temp;
        
        }

        public async Task<_SemaphoreRefer> LockAsync(string str,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            var temp = new _SemaphoreRefer(__semaphore.GetSemaphore());

            try
            {
                __thread = Thread.CurrentThread.ManagedThreadId;
                if (__thread == temp.__thread)
                {
                    log.Debug($"!! LockAsync Same Thread..\n last: {__message}.{__caller} : {__line} \n call: {_caller} \n file: {_file}: {_line}");
                    //return temp;
                }

                await temp.WaitAsync();

                __message   = str;
                __caller    = _caller;
                __file      = _file;
                __line      = _line;
            }
            catch
            {
                temp = null;
                log.Debug($"!! LockAsync Fault..\n last: {__message}.{__caller} : {__line} \n call: {_caller} \n file: {_file}: {_line}");
                throw;
            }
            finally
            {

            }
            return temp;
        }

        public async Task<bool> LockAsync(string str, int timeout,
            [System.Runtime.CompilerServices.CallerMemberName] string _caller = "",
            [System.Runtime.CompilerServices.CallerFilePath] string _file = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int _line = 0)
        {
            __thread = Thread.CurrentThread.ManagedThreadId;
            if (__thread == __semaphore.__thread)
            {
                log.Debug($"!! LockAsync Same Thread..\n last: {__message}.{__caller} : {__line} \n call: {_caller} \n file: {_file}: {_line}");
                //return false;
            }

            bool b = await __semaphore.WaitAsync(timeout);

            if (b == true) { __message = str; }
            else { log.Debug($"!! LockAsync Fault..\n last: {__message}.{__caller} : {__line} \n call: {_caller} \n file: {_file}: {_line}"); }

            return b;
        }
    }
}
