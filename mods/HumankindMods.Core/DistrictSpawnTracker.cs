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
        public TrackedChannel(int componentId, int channel, DistrictRenderState state, (int Column, int Row) tile, double pendingSince)
        {
            ComponentId = componentId;
            Channel = channel;
            State = state;
            Column = tile.Column;
            Row = tile.Row;
            PendingSince = pendingSince;
        }

        public int ComponentId { get; }
        public int Channel { get; }
        public DistrictRenderState State { get; internal set; }
        public int Column { get; }
        public int Row { get; }
        public double PendingSince { get; }
    }

    public readonly struct SpawnCounts : IEquatable<SpawnCounts>
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

        public bool Equals(SpawnCounts other) =>
            BPending == other.BPending && BOrphaned == other.BOrphaned && BStuck == other.BStuck && ADropped == other.ADropped && TotalDrops == other.TotalDrops;

        public override bool Equals(object? obj) => obj is SpawnCounts other && Equals(other);

        public override int GetHashCode() => (BPending, BOrphaned, BStuck, ADropped, TotalDrops).GetHashCode();
    }

    /// <summary>
    /// Tracks district channels whose model was retired and not respawned (causes A and B in
    /// the late-game rendering notes). Pure logic, no Unity.
    /// </summary>
    public sealed class DistrictSpawnTracker
    {
        private readonly double stuckAfterSeconds;
        private readonly Dictionary<int, Dictionary<int, TrackedChannel>> byComponent = new Dictionary<int, Dictionary<int, TrackedChannel>>();
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
            if (!hasMaterial || ableToSpawn)
            {
                Remove(componentId, channel);
                return;
            }

            var channels = ChannelsOf(componentId);
            if (channels.TryGetValue(channel, out var existing) && existing.State == DistrictRenderState.BPending) return;
            channels[channel] = new TrackedChannel(componentId, channel, DistrictRenderState.BPending, TileOf(componentId), now);
        }

        /// <summary>AddParticle writes the due channels in ascending order, so the first <paramref name="written"/> succeeded.</summary>
        public void OnSpawn(int componentId, ulong dueChannelMask, int written, int frame)
        {
            for (var channel = 0; channel < 64 && dueChannelMask >> channel != 0; channel++)
            {
                if ((dueChannelMask & (1UL << channel)) == 0) continue;
                if (written-- > 0)
                {
                    Remove(componentId, channel);
                    continue;
                }

                ChannelsOf(componentId)[channel] = new TrackedChannel(componentId, channel, DistrictRenderState.ADropped, TileOf(componentId), 0);
                CountDrop(frame);
            }
        }

        public void OnUnload(int componentId)
        {
            tiles.Remove(componentId);
            byComponent.Remove(componentId);
        }

        public void Sweep(double now, Func<int, int, bool> isAbleToSpawn)
        {
            foreach (var entry in All())
            {
                if (entry.State != DistrictRenderState.BPending && entry.State != DistrictRenderState.BStuck) continue;
                if (isAbleToSpawn(entry.ComponentId, entry.Channel)) entry.State = DistrictRenderState.BOrphaned;
                else if (now - entry.PendingSince >= stuckAfterSeconds) entry.State = DistrictRenderState.BStuck;
            }
        }

        internal DistrictRenderState? StateOf(int componentId, int channel) =>
            byComponent.TryGetValue(componentId, out var channels) && channels.TryGetValue(channel, out var entry) ? entry.State : (DistrictRenderState?)null;

        public IEnumerable<TrackedChannel> Invisible() => All().Where(c => c.State != DistrictRenderState.BPending);

        public SpawnCounts GetCounts()
        {
            int pending = 0, orphaned = 0, stuck = 0, dropped = 0;
            foreach (var entry in All())
            {
                switch (entry.State)
                {
                    case DistrictRenderState.BPending: pending++; break;
                    case DistrictRenderState.BOrphaned: orphaned++; break;
                    case DistrictRenderState.BStuck: stuck++; break;
                    case DistrictRenderState.ADropped: dropped++; break;
                }
            }

            return new SpawnCounts(pending, orphaned, stuck, dropped, totalDrops, worstDropFrame, worstDropFrameCount);
        }

        private IEnumerable<TrackedChannel> All() => byComponent.Values.SelectMany(channels => channels.Values);

        private Dictionary<int, TrackedChannel> ChannelsOf(int componentId)
        {
            if (!byComponent.TryGetValue(componentId, out var channels))
            {
                channels = new Dictionary<int, TrackedChannel>();
                byComponent[componentId] = channels;
            }

            return channels;
        }

        private void Remove(int componentId, int channel)
        {
            if (!byComponent.TryGetValue(componentId, out var channels)) return;
            channels.Remove(channel);
            if (channels.Count == 0) byComponent.Remove(componentId);
        }

        private (int Column, int Row) TileOf(int componentId) => tiles.TryGetValue(componentId, out var tile) ? tile : (-1, -1);

        private void CountDrop(int frame)
        {
            totalDrops++;
            if (frame != currentDropFrame)
            {
                currentDropFrame = frame;
                currentDropFrameCount = 0;
            }

            if (++currentDropFrameCount > worstDropFrameCount)
            {
                worstDropFrame = frame;
                worstDropFrameCount = currentDropFrameCount;
            }
        }
    }
}
