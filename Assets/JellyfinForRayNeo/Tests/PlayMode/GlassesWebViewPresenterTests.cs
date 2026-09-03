using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JellyfinForRayNeo.Tests
{
    public sealed class GlassesWebViewPresenterTests
    {
        [Test]
        public void Commands_AreQueuedUntilTheGlassesPageIsReady()
        {
            GameObject owner = new GameObject("Glasses WebView Presenter Test");
            GlassesWebViewPresenter presenter =
                owner.AddComponent<GlassesWebViewPresenter>();
            FakeGlassesWebViewHost host = new FakeGlassesWebViewHost();
            presenter.InitializeForTests(host);

            Assert.IsTrue(presenter.Show());
            Assert.IsTrue(presenter.DispatchRemoteCommand(CompanionRemoteCommand.Up));
            Assert.IsTrue(presenter.DispatchRemoteCommand(CompanionRemoteCommand.Submit));
            Assert.IsTrue(presenter.DispatchRemoteCommand(CompanionRemoteCommand.Back));
            Assert.IsTrue(presenter.DispatchVolume(140));
            Assert.AreEqual(4, presenter.PendingCommandCount);
            Assert.IsEmpty(host.Commands);

            host.Ready = true;
            presenter.PumpPendingCommands();

            CollectionAssert.AreEqual(
                new[] { "up", "enter", "back", "volume:100" },
                host.Commands);
            Assert.AreEqual(0, presenter.PendingCommandCount);

            Object.DestroyImmediate(owner);
            Assert.IsTrue(host.Hidden);
        }

        [TestCase(CompanionRemoteCommand.Up, "up")]
        [TestCase(CompanionRemoteCommand.Down, "down")]
        [TestCase(CompanionRemoteCommand.Left, "left")]
        [TestCase(CompanionRemoteCommand.Right, "right")]
        [TestCase(CompanionRemoteCommand.Submit, "enter")]
        [TestCase(CompanionRemoteCommand.Back, "back")]
        public void RemoteCommands_MapToWebCommands(
            CompanionRemoteCommand command,
            string expected)
        {
            Assert.AreEqual(expected, GlassesWebViewPresenter.ToWebCommand(command));
        }

        [Test]
        public void ValidWebMessages_AreParsedAndPublished()
        {
            GameObject owner = new GameObject("Glasses WebView Message Test");
            GlassesWebViewPresenter presenter =
                owner.AddComponent<GlassesWebViewPresenter>();
            GlassesWebMessage received = null;
            presenter.MessageReceived += message => received = message;

            presenter.OnGlassesWebMessage(
                "{\"type\":\"playback_state\",\"state\":\"playing\","
                + "\"itemId\":\"episode-7\",\"title\":\"Violet\","
                + "\"subtitle\":\"Episode 7\",\"playMethod\":\"Transcode\","
                + "\"positionTicks\":123000000,\"durationTicks\":456000000}");

            Assert.IsNotNull(received);
            Assert.AreEqual(GlassesWebMessageType.PlaybackState, received.Type);
            Assert.AreEqual("playing", received.State);
            Assert.AreEqual("episode-7", received.ItemId);
            Assert.AreEqual("Violet", received.Title);
            Assert.AreEqual("Episode 7", received.Subtitle);
            Assert.AreEqual("Transcode", received.PlayMethod);
            Assert.AreEqual(123000000L, received.PositionTicks);
            Assert.AreEqual(456000000L, received.DurationTicks);

            Object.DestroyImmediate(owner);
        }

        [TestCase("{\"type\":\"manage_login\"}", "ManageLogin")]
        [TestCase("{\"type\":\"logout\"}", "Logout")]
        public void ControlWebMessages_AreRecognized(
            string payload,
            string expected)
        {
            Assert.IsTrue(GlassesWebMessage.TryParse(payload, out GlassesWebMessage message));
            Assert.AreEqual(expected, message.Type.ToString());
        }

        [TestCase("")]
        [TestCase("{\"type\":\"unknown\"}")]
        [TestCase("{\"type\":\"playback_state\",\"state\":\"invalid\"}")]
        public void InvalidWebMessages_AreRejected(string payload)
        {
            Assert.IsFalse(GlassesWebMessage.TryParse(payload, out _));
        }

        private sealed class FakeGlassesWebViewHost : IGlassesWebViewHost
        {
            public bool IsSupported => true;
            public bool Ready { get; set; }
            public bool Hidden { get; private set; }
            public List<string> Commands { get; } = new List<string>();

            public bool Show()
            {
                return true;
            }

            public void Hide()
            {
                Hidden = true;
            }

            public bool SendCommand(string command)
            {
                if (!Ready)
                {
                    return false;
                }
                Commands.Add(command);
                return true;
            }

            public void RefreshBootstrapState()
            {
            }
        }
    }
}
