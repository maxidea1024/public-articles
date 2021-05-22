using System;
using System.Threading.Tasks;

namespace G.Util
{
    public class TaskHelper
    {
        public static void FireAndForget(
                Func<Task> task,
                Action<Exception> handle = null)
        {
            Task.Run(
                () =>
                {
                    ((Func<Task>)(async () =>
                    {
                        try
                        {
                            await task();
                        }
                        catch (Exception e)
                        {
                            handle?.Invoke(e);
                        }
                    }))();
                });
        }
    }
}
