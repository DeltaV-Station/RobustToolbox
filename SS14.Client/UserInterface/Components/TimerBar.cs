using SFML.Graphics;
using SFML.System;
using SFML.Window;
using SS14.Client.Interfaces.Resource;
using System;
using System.Diagnostics;

namespace SS14.Client.UserInterface.Components
{
    internal class Timer_Bar : Progress_Bar
    {
        private Stopwatch stopwatch;

        public Timer_Bar(Vector2i size, TimeSpan countdownTime, IResourceCache resourceCache)
            : base(size, resourceCache)
        {
            stopwatch = new Stopwatch();
            max = (float) Math.Round(countdownTime.TotalSeconds);
            stopwatch.Restart();
            Update(0);
        }

        public override float Value
        {
            get { return val; }
            set { val = Math.Min(Math.Max(value, min), max); }
        }

        public override void Update(float frameTime)
        {
            if (stopwatch != null)
            {
                if (stopwatch.Elapsed.Seconds > max)
                    return;

                Value = stopwatch.Elapsed.Seconds;
                Text.Text =
                    DateTime.Now.AddSeconds(max - stopwatch.Elapsed.Seconds).Subtract(DateTime.Now).ToString(@"mm\:ss");
            }

            Text.Position = new Vector2i(Position.X + (int)(Size.X/2f - Text.Width/2f),
                                         Position.Y + (int)(Size.Y/2f - Text.Height/2f));
            ClientArea = new IntRect(Position, Size);
        }

        public override void Dispose()
        {
            stopwatch.Stop();
            stopwatch = null;
            Text = null;
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        public override bool MouseDown(MouseButtonEventArgs e)
        {
            return false;
        }

        public override bool MouseUp(MouseButtonEventArgs e)
        {
            return false;
        }
    }
}