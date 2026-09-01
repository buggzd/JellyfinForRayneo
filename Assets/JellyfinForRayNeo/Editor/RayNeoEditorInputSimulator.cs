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
        }
    }
}
