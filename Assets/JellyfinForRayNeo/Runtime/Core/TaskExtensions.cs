using System;
using System.Threading.Tasks;
using UnityEngine;

namespace JellyfinForRayNeo
{
    internal static class TaskExtensions
    {
        public static async void Forget(this Task task, Action<Exception> onError = null)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // Cancellation is an expected part of screen transitions and shutdown.
            }
            catch (Exception exception)
            {
                if (onError != null)
                {
                    onError(exception);
                }
                else
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}

