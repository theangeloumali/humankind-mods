using System;
using System.Collections.Generic;
using System.Linq;

namespace HumankindMods.Core
{
    public enum DistrictRenderState
    {
        BPending,
        BOrphaned,
        BStuck,
        ADropped,
    }

    public sealed class TrackedChannel
    {
        public TrackedChannel(int componentId, int channel)
        {
            ComponentId = componentId;
            Channel = channel;
        }

        public int ComponentId { get; }
        public int Channel { get; }
        public DistrictRenderState State { get; internal set; }
        public int Column { get; internal set; } = -1;
        public int Row { get; internal set; } = -1;
        public double PendingSince { get; internal set; }
    }

    public readonly struct SpawnCounts
    {
        public SpawnCounts(int bPending, int bOrphaned, int bStuck, int aDropped, int totalDrops, int worstDropFrame, int worstDropFrameCount)
        {
            BPending = bPending;
            BOrphaned = bOrphaned;
            BStuck = bStuck;
            ADropped = aDropped;
            TotalDrops = totalDrops;
            WorstDropFrame = worstDropFrame;
            WorstDropFrameCount = worstDropFrameCount;
        }

        public int BPending { get; }
        public int BOrphaned { get; }
        public int BStuck { get; }
        public int ADropped { get; }
        public int TotalDrops { get; }
        public int WorstDropFrame { get; }
        public int WorstDropFrameCount { get; }
        public int Invisible => BOrphaned + BStuck + ADropped;
    }

    /// <summary>
    /// Tracks district channels whose model was retired and not respawned (causes A and B in
    /// the late-game rendering notes). Pure logic, no Unity.
    /// </summary>
    public sealed class DistrictSpawnTracker
    {
        private readonly double stuckAfterSeconds;
        private readonly Dictionary<(int, int), TrackedChannel> channels = new Dictionary<(int, int), TrackedChannel>();
        private readonly Dictionary<int, (int Column, int Row)> tiles = new Dictionary<int, (int, int)>();
        private int totalDrops;
        private int currentDropFrame = -1;
        private int currentDropFrameCount;
        private int worstDropFrame = -1;
        private int worstDropFrameCount;

        public DistrictSpawnTracker(double stuckAfterSeconds)
        {
            this.stuckAfterSeconds = stuckAfterSeconds;
        }

        public void OnRefresh(int componentId, int channel, bool hasMaterial, bool ableToSpawn, int column, int row, double now)
        {
            tiles[componentId] = (column, row);
            var key = (componentId, channel);
            if (!hasMaterial || ableToSpawn)
            {
                channels.Remove(key);
                return;
            }

            if (channels.TryGetValue(key, out var existing) && existing.State == DistrictRenderState.BPending)
            {
                return;
            }

            channels[key] = NewEntry(componentId, channel, DistrictRenderState.BPending, now);
        }

        /// <summary>The adder writes channels in order, so the first <paramref name="written"/> succeeded.</summary>
        public void OnSpawn(int componentId, IReadOnlyList<int> expectedChannels, int written, int frame)
        {
            for (var i = 0; i < expectedChannels.Count; i++)
            {
                var key = (componentId, expectedChannels[i]);
                if (i < written)
                {
                    channels.Remove(key);
                    continue;
                }

                channels[key] = NewEntry(componentId, expectedChannels[i], DistrictRenderState.ADropped, 0);
                CountDrop(frame);
            }
        }

        public void OnUnload(int componentId)
        {
            tiles.Remove(componentId);
            foreach (var key in channels.Keys.Where(k => k.Item1 == componentId).ToList())
            {
                channels.Remove(key);
            }
        }

        public void Sweep(double now, Func<int, int, bool> isAbleToSpawn)
        {
            foreach (var entry in channels.Values)
            {
                if (entry.State != DistrictRenderState.BPending && entry.State != DistrictRenderState.BStuck)
                {
                    continue;
                }

                if (isAbleToSpawn(entry.ComponentId, entry.Channel))
                {
                    entry.State = DistrictRenderState.BOrphaned;
                }
                else if (now - entry.PendingSince >= stuckAfterSeconds)
                {
                    entry.State = DistrictRenderState.BStuck;
                }
            }
        }

        public DistrictRenderState? StateOf(int componentId, int channel) =>
            channels.TryGetValue((componentId, channel), out var entry) ? entry.State : (DistrictRenderState?)null;

        public IEnumerable<TrackedChannel> Invisible() =>
            channels.Values.Where(c => c.State != DistrictRenderState.BPending);

        public SpawnCounts GetCounts()
        {
            int Count(DistrictRenderState state) => channels.Values.Count(c => c.State == state);
            return new SpawnCounts(
                Count(DistrictRenderState.BPending),
                Count(DistrictRenderState.BOrphaned),
                Count(DistrictRenderState.BStuck),
                Count(DistrictRenderState.ADropped),
                totalDrops,
                worstDropFrame,
                worstDropFrameCount);
        }

        private TrackedChannel NewEntry(int componentId, int channel, DistrictRenderState state, double now)
        {
            var entry = new TrackedChannel(componentId, channel) { State = state, PendingSince = now };
            if (tiles.TryGetValue(componentId, out var tile))
            {
                entry.Column = tile.Column;
                entry.Row = tile.Row;
            }

            return entry;
        }

        private void CountDrop(int frame)
        {
            totalDrops++;
            if (frame != currentDropFrame)
            {
                currentDropFrame = frame;
                currentDropFrameCount = 0;
            }

            currentDropFrameCount++;
            if (currentDropFrameCount > worstDropFrameCount)
            {
                worstDropFrame = frame;
                worstDropFrameCount = currentDropFrameCount;
            }
        }
    }
}
