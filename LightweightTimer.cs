namespace XenkoCommunity.ImGuiDebug
{
    using System;
    using System.Diagnostics;

    public struct LightweightTimer
    {
        long _ts;
        
        public TimeSpan InitTime => Xenko.Core.Utilities.ConvertRawToTimestamp( _ts );
        public TimeSpan Elapsed => Xenko.Core.Utilities.ConvertRawToTimestamp( Stopwatch.GetTimestamp() - _ts );

        public void Start()
        {
            _ts = Stopwatch.GetTimestamp();
        }
        
        /// <summary>
        /// Use this function and its return value when inside a loop instead of <see cref="Elapsed"/>
        /// as it guarantees that no time will be discarded
        /// </summary>
        public TimeSpan Restart()
        {
            long now = Stopwatch.GetTimestamp();
            var delta = Xenko.Core.Utilities.ConvertRawToTimestamp( now - _ts );
            _ts = now;
            return delta;
        }

        public static LightweightTimer StartNew()
        {
            LightweightTimer lt = new LightweightTimer();
            lt.Start();
            return lt;
        }
    }
}