using System;
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace JellyfinForRayNeo.Editor
{
    public sealed class AndroidCompanionBuildProcessor : IPostGenerateGradleAndroidProject
    {
        private const string ActivityName =
            "com.jellyfinforrayneo.companion.JellyfinRayNeoActivity";
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            XmlDocument document = new XmlDocument
            {
                PreserveWhitespace = true
            };
            document.Load(manifestPath);

            XmlElement targetActivity = null;
            foreach (XmlNode node in document.GetElementsByTagName("activity"))
            {
                XmlElement activity = node as XmlElement;
                if (activity != null
                    && string.Equals(
                        activity.GetAttribute("name", AndroidNamespace),
                        ActivityName,
                        StringComparison.Ordinal))
                {
                    targetActivity = activity;
                    break;
                }
            }

            if (targetActivity == null)
            {
                throw new BuildFailedException(
                    "Could not find the Jellyfin RayNeo companion activity in the generated Android manifest.");
            }

            XmlAttribute acceleration = targetActivity.GetAttributeNode(
                "hardwareAccelerated",
                AndroidNamespace);
            if (acceleration == null)
            {
                acceleration = document.CreateAttribute(
                    "android",
                    "hardwareAccelerated",
                    AndroidNamespace);
                targetActivity.Attributes.Append(acceleration);
            }
            acceleration.Value = "true";

            document.Save(manifestPath);
            Debug.Log("Enabled hardware acceleration for the phone companion WebView window.");
        }
    }
}
