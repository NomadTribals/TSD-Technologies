using System;

namespace TsdTechnologies.Overlay.Animation
{
    internal enum DrawReadiness { Waiting, Ready, TimedOut }
    internal sealed class TabletTransitionTiming
    {
        private readonly float started, minimum, steady, timeout;
        private float quietSince = -1f;
        internal TabletTransitionTiming(float started, float minimum, float steady, float timeout)
        {
            this.started=started;this.minimum=minimum;this.steady=steady;
            this.timeout=Math.Max(timeout,minimum+steady+1f);
        }
        internal DrawReadiness Observe(float now,bool modelReady,bool nativeBusy,bool moving)
        {
            if(now-started>=timeout)return DrawReadiness.TimedOut;
            if(now-started<minimum||!modelReady||nativeBusy||moving){quietSince=-1f;return DrawReadiness.Waiting;}
            if(quietSince<0f)quietSince=now;
            return now-quietSince>=steady?DrawReadiness.Ready:DrawReadiness.Waiting;
        }
    }
}
