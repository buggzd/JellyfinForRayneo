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
        public void Project_UsesSingleLegacyInputBackendOnAndroid()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(
                "ProjectSettings/ProjectSettings.asset");
            Assert.IsNotEmpty(assets);

            SerializedObject settings = new SerializedObject(assets[0]);
            SerializedProperty inputHandler = settings.FindProperty("activeInputHandler");
            Assert.NotNull(inputHandler);
            Assert.AreEqual(0, inputHandler.intValue);
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
        public void QuickConnectPayload_RequiresOnlyServerAddress()
        {
            const string payload =
                "{\"serverUrl\":\" http://192.168.1.20:8096 \"}";

            bool parsed = CompanionLoginBridge.TryParseQuickConnectPayload(
                payload,
                out CompanionQuickConnectRequest request,
                out string validationMessage);

            Assert.IsTrue(parsed, validationMessage);
            Assert.NotNull(request);
            Assert.AreEqual("http://192.168.1.20:8096", request.ServerUrl);

            Assert.IsFalse(CompanionLoginBridge.TryParseQuickConnectPayload(
                "{\"serverUrl\":\"\"}",
                out request,
                out validationMessage));
            Assert.IsNull(request);
            Assert.IsNotEmpty(validationMessage);
        }

        [Test]
        public void NativeSessionPayload_ImportsTokenWithoutPassword()
        {
            const string payload =
                "{\"serverUrl\":\"http://server:8096\",\"serverName\":\"Living Room\"," +
                "\"serverVersion\":\"10.10.7\",\"serverId\":\"server-id\"," +
                "\"accessToken\":\"token\",\"userId\":\"user-id\"," +
                "\"userName\":\"alice\",\"deviceId\":\"phone-id\"}";

            bool parsed = CompanionLoginBridge.TryParseSessionPayload(
                payload,
                out CompanionSessionRequest request,
                out string validationMessage);

            Assert.IsTrue(parsed, validationMessage);
            Assert.NotNull(request);
            JellyfinSession session = request.ToSession();
            Assert.IsTrue(session.IsValid);
            Assert.AreEqual("http://server:8096", session.ServerUrl);
            Assert.AreEqual("token", session.AccessToken);
            Assert.AreEqual("alice", session.UserName);
            Assert.IsNull(typeof(CompanionSessionRequest).GetProperty("Password"));
        }

        [Test]
        public void NativeSessionPayload_RejectsMissingToken()
        {
            const string payload =
                "{\"serverUrl\":\"http://server:8096\",\"userId\":\"user-id\"," +
                "\"deviceId\":\"phone-id\"}";

            Assert.IsFalse(CompanionLoginBridge.TryParseSessionPayload(
                payload,
                out CompanionSessionRequest request,
                out string validationMessage));
            Assert.IsNull(request);
            Assert.IsNotEmpty(validationMessage);
        }

        [TestCase("1", true)]
        [TestCase("2", false)]
        public void RayNeoGlassEvent_ParsesOfficialSdkValues(
            string value,
            bool expectedConnected)
        {
            Assert.IsTrue(CompanionLoginBridge.TryParseGlassesEvent(
                new[] { value },
                out bool connected));
            Assert.AreEqual(expectedConnected, connected);
        }

        [Test]
        public void RayNeoGlassEvent_RejectsUnknownValue()
        {
            Assert.IsFalse(CompanionLoginBridge.TryParseGlassesEvent(
                new[] { "ready" },
                out _));
        }

        [TestCase(0, 15, 0)]
        [TestCase(7, 15, 47)]
        [TestCase(15, 15, 100)]
        [TestCase(-3, 15, 0)]
        [TestCase(19, 15, 100)]
        [TestCase(8, 0, 0)]
        public void MediaVolumePercentage_IsRoundedAndClamped(
            int currentVolume,
            int maximumVolume,
            int expectedPercentage)
        {
            Assert.AreEqual(
                expectedPercentage,
                CompanionVolume.CalculatePercentage(currentVolume, maximumVolume));
        }

        [TestCase("volume:0", 0)]
        [TestCase(" VOLUME:47 ", 47)]
        [TestCase("volume:100", 100)]
        public void NativeMediaVolumeEvent_ParsesPercentage(
            string value,
            int expectedPercentage)
        {
            Assert.IsTrue(CompanionVolume.TryParseNativeEvent(value, out int percentage));
            Assert.AreEqual(expectedPercentage, percentage);
        }

        [TestCase("volume:-1")]
        [TestCase("volume:101")]
        [TestCase("volume:loud")]
        [TestCase("left")]
        public void InvalidNativeMediaVolumeEvent_IsRejected(string value)
        {
            Assert.IsFalse(CompanionVolume.TryParseNativeEvent(value, out _));
        }

        [Test]
        public void EditorVolumeSubmission_IsDeliveredOnlyWhenBridgePumps()
        {
            CompanionLoginBridge bridge = new CompanionLoginBridge();
            int? delivered = null;
            bridge.VolumeChanged += percentage => delivered = percentage;

            try
            {
                CompanionVolumeRuntime.Submit(62);
                Assert.IsNull(delivered);

                bridge.Pump();

                Assert.AreEqual(62, delivered);
            }
            finally
            {
                bridge.Dispose();
            }
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
        public void QuickConnectSubmissionAndCancellation_AreDeliveredWhenBridgePumps()
        {
            CompanionLoginBridge bridge = new CompanionLoginBridge();
            CompanionQuickConnectRequest delivered = null;
            int cancellationCount = 0;
            bridge.QuickConnectRequested += request => delivered = request;
            bridge.QuickConnectCancelRequested += () => cancellationCount++;

            try
            {
                Assert.IsTrue(CompanionLoginRuntime.SubmitQuickConnect(
                    "http://server:8096"));
                Assert.IsNull(delivered);

                bridge.Pump();

                Assert.NotNull(delivered);
                Assert.AreEqual("http://server:8096", delivered.ServerUrl);
                Assert.IsTrue(CompanionLoginRuntime.CancelQuickConnect());
                Assert.AreEqual(0, cancellationCount);

                bridge.Pump();

                Assert.AreEqual(1, cancellationCount);
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
                    CompanionLoginState.QuickConnectWaiting,
                    "等待授权",
                    false,
                    "http://server:8096",
                    string.Empty,
                    "482731");

                CompanionLoginSnapshot snapshot = CompanionLoginRuntime.Current;
                Assert.AreEqual(CompanionLoginState.QuickConnectWaiting, snapshot.State);
                Assert.AreEqual("http://server:8096", snapshot.ServerUrl);
                Assert.AreEqual("482731", snapshot.QuickConnectCode);
                Assert.IsNull(typeof(CompanionLoginSnapshot).GetProperty("Password"));
                Assert.IsNull(typeof(CompanionLoginSnapshot).GetProperty("QuickConnectSecret"));
            }
            finally
            {
                bridge.Dispose();
            }
        }
    }
}
