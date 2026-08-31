using System;
using System.Threading;
using System.Threading.Tasks;

namespace JellyfinForRayNeo
{
    public sealed class PlaybackReporter
    {
        private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(10);
        private readonly JellyfinApiClient _api;
        private JellyfinPlaybackPlan _plan;
        private DateTime _lastProgressUtc;
        private bool _started;
        private bool _stopped;
        private bool _requestInFlight;

        public PlaybackReporter(JellyfinApiClient api)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public async Task StartAsync(JellyfinPlaybackPlan plan, long positionTicks, CancellationToken cancellationToken)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _started = true;
            _stopped = false;
            _lastProgressUtc = DateTime.UtcNow;
            await _api.ReportPlaybackStartAsync(plan, false, positionTicks, cancellationToken);
        }

        public async Task ReportProgressIfDueAsync(bool paused, long positionTicks, bool force, CancellationToken cancellationToken)
        {
            if (!_started || _stopped || _plan == null || _requestInFlight)
            {
                return;
            }
            if (!force && DateTime.UtcNow - _lastProgressUtc < ProgressInterval)
            {
                return;
            }

            _requestInFlight = true;
            try
            {
                await _api.ReportPlaybackProgressAsync(_plan, paused, positionTicks, cancellationToken);
                _lastProgressUtc = DateTime.UtcNow;
            }
            finally
            {
                _requestInFlight = false;
            }
        }

        public async Task StopAsync(long positionTicks, bool failed, CancellationToken cancellationToken)
        {
            if (!_started || _stopped || _plan == null)
            {
                return;
            }

            _stopped = true;
            await _api.ReportPlaybackStoppedAsync(_plan, positionTicks, failed, cancellationToken);
        }

        public void Reset()
        {
            _plan = null;
            _started = false;
            _stopped = false;
            _requestInFlight = false;
        }
    }
}

