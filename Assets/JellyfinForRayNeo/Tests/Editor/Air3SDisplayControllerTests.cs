using NUnit.Framework;
using UnityEngine;

namespace JellyfinForRayNeo.Tests
{
    public sealed class Air3SDisplayControllerTests
    {
        [TestCase("mirror_2d", Air3SDisplayMode.Mirror2D)]
        [TestCase("2d", Air3SDisplayMode.Mirror2D)]
        [TestCase("mono", Air3SDisplayMode.Mirror2D)]
        [TestCase("stereo_screen", Air3SDisplayMode.StereoVirtualScreen)]
        [TestCase("3d", Air3SDisplayMode.StereoVirtualScreen)]
        [TestCase("stereo", Air3SDisplayMode.StereoVirtualScreen)]
        public void DisplayModePreference_ParsesSupportedAliases(
            string value,
            Air3SDisplayMode expected)
        {
            Assert.IsTrue(Air3SDisplayController.TryParsePreference(value, out Air3SDisplayMode mode));
            Assert.AreEqual(expected, mode);
        }

        [Test]
        public void UnknownDisplayModePreference_FallsBackSafelyToMirror2D()
        {
            Assert.IsFalse(Air3SDisplayController.TryParsePreference(
                "unsupported",
                out Air3SDisplayMode mode));
            Assert.AreEqual(Air3SDisplayMode.Mirror2D, mode);
        }

        [Test]
        public void DisplayModePreference_UsesStablePhoneValues()
        {
            Assert.AreEqual(
                Air3SDisplayController.Mirror2DPreference,
                Air3SDisplayController.ToPreferenceValue(Air3SDisplayMode.Mirror2D));
            Assert.AreEqual(
                Air3SDisplayController.StereoScreenPreference,
                Air3SDisplayController.ToPreferenceValue(Air3SDisplayMode.StereoVirtualScreen));
        }

        [Test]
        public void CanvasScale_FillsTheConfiguredPerEyeFieldOfView()
        {
            const float distance = 4.5f;
            const float fieldOfView = 27f;
            float scale = Air3SDisplayController.CalculateCanvasWorldScale(
                distance,
                fieldOfView);
            float expectedHeight = 2f
                * distance
                * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);

            Assert.AreEqual(
                expectedHeight,
                scale * Air3SDisplayController.ReferenceCanvasHeight,
                0.0001f);
        }
    }
}
