namespace HumankindMods.Core
{
    /// <summary>The largest value reported within the current frame (several renderers report each frame), with a
    /// value that travels with it; a new frame starts over.</summary>
    public sealed class FrameMax
    {
        private int frame = -1;

        public int Value { get; private set; }
        public int Extra { get; private set; }

        public void Observe(int frame, int value, int extra)
        {
            if (frame != this.frame)
            {
                this.frame = frame;
                Value = int.MinValue;
            }

            if (value < Value) return;
            Value = value;
            Extra = extra;
        }
    }
}
