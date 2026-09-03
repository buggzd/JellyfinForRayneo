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
