using System.Reflection;
using System.Runtime.CompilerServices;
using FfalconXR.Editor;
using UnityEditor;

namespace JellyfinForRayNeo.Editor
{
    [InitializeOnLoad]
    internal static class RayNeoSdkDebugSuppressor
    {
        static RayNeoSdkDebugSuppressor()
        {
            RuntimeHelpers.RunClassConstructor(typeof(DebugWindow).TypeHandle);
            EditorApplication.update -= DebugWindow.Update;

            // The SDK leaves ReimportDll subscribed when its version check returns early,
            // causing a full AssetDatabase scan on every Editor update.
            RuntimeHelpers.RunClassConstructor(typeof(EnvFix).TypeHandle);
            EditorApplication.update -= FinishEnvFixInitialization;
            EditorApplication.update += FinishEnvFixInitialization;
        }

        private static void FinishEnvFixInitialization()
        {
            if (EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= FinishEnvFixInitialization;
            MethodInfo reimportDll = typeof(EnvFix).GetMethod(
                "ReimportDll",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (reimportDll == null)
            {
                return;
            }

            try
            {
                reimportDll.Invoke(null, null);
            }
            finally
            {
                var callback = (EditorApplication.CallbackFunction)System.Delegate.CreateDelegate(
                    typeof(EditorApplication.CallbackFunction),
                    reimportDll);
                EditorApplication.update -= callback;
            }
        }
    }
}
