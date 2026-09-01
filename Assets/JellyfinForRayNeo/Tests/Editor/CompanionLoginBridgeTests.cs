using NUnit.Framework;
using UnityEditor;

namespace JellyfinForRayNeo.Tests
{
    public sealed class CompanionLoginBridgeTests
    {
        [Test]
        public void Project_AllowsLanHttpJellyfinServers()
        {
            Assert.AreEqual(
                InsecureHttpOption.AlwaysAllowed,
                PlayerSettings.insecureHttpOption);
        }

        [Test]
        public void ValidNativePayload_ParsesLoginRequest()
        {
            const string payload =
                "{\"serverUrl\":\" http://192.168.1.20:8096/ \",\"username\":\" alice \",\"password\":\"secret\"}";

            bool parsed = CompanionLoginBridge.TryParsePayload(
                payload,
                out CompanionLoginRequest request,
                out string validationMessage);

            Assert.IsTrue(parsed, validationMessage);
            Assert.NotNull(request);
            Assert.AreEqual("http://192.168.1.20:8096/", request.ServerUrl);
            Assert.AreEqual("alice", request.UserName);
            Assert.AreEqual("secret", request.Password);

            request.ClearPassword();
            Assert.IsNull(request.Password);
        }

        [TestCase("{\"serverUrl\":\"\",\"username\":\"alice\",\"password\":\"x\"}")]
        [TestCase("{\"serverUrl\":\"http://server\",\"username\":\"\",\"password\":\"x\"}")]
        [TestCase("")]
        public void InvalidNativePayload_IsRejected(string payload)
        {
            bool parsed = CompanionLoginBridge.TryParsePayload(
                payload,
                out CompanionLoginRequest request,
                out string validationMessage);

            Assert.IsFalse(parsed);
            Assert.IsNull(request);
            Assert.IsNotEmpty(validationMessage);
        }

        [Test]
        public void EditorSubmission_IsDeliveredOnlyWhenBridgePumps()
        {
            CompanionLoginBridge bridge = new CompanionLoginBridge();
            CompanionLoginRequest delivered = null;
            string passwordDuringDispatch = null;
            bridge.LoginRequested += request =>
            {
                delivered = request;
                passwordDuringDispatch = request.Password;
            };

            try
            {
                Assert.IsTrue(CompanionLoginRuntime.SubmitLogin(
                    "http://server:8096",
                    "alice",
                    "secret"));
                Assert.IsNull(delivered);

                bridge.Pump();

                Assert.NotNull(delivered);
                Assert.AreEqual("secret", passwordDuringDispatch);
                Assert.IsNull(delivered.Password, "The bridge must clear the request password after dispatch.");
            }
            finally
            {
                bridge.Dispose();
            }
        }

        [Test]
        public void PublishedSnapshot_ContainsNoPasswordField()
        {
            CompanionLoginBridge bridge = new CompanionLoginBridge();
            try
            {
                bridge.PublishState(
                    CompanionLoginState.Ready,
                    "已连接",
                    false,
                    "http://server:8096",
                    "alice");

                CompanionLoginSnapshot snapshot = CompanionLoginRuntime.Current;
                Assert.AreEqual(CompanionLoginState.Ready, snapshot.State);
                Assert.AreEqual("http://server:8096", snapshot.ServerUrl);
                Assert.AreEqual("alice", snapshot.UserName);
                Assert.IsNull(typeof(CompanionLoginSnapshot).GetProperty("Password"));
            }
            finally
            {
                bridge.Dispose();
            }
        }
    }
}
