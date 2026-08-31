using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace JellyfinForRayNeo
{
    internal static class UnityWebRequestExtensions
    {
        public static Task SendRequestAsync(this UnityWebRequest request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            CancellationTokenRegistration registration = default(CancellationTokenRegistration);
            UnityWebRequestAsyncOperation operation;

            try
            {
                operation = request.SendWebRequest();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
                return completion.Task;
            }

            registration = cancellationToken.Register(() =>
            {
                request.Abort();
                completion.TrySetCanceled(cancellationToken);
            });

            operation.completed += _ =>
            {
                registration.Dispose();
                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);
                }
                else
                {
                    completion.TrySetResult(true);
                }
            };

            return completion.Task;
        }
    }
}

