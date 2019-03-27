namespace XenkoCommunity.ImGuiDebug
{
    using Xenko.Core;
    using Xenko.Core.Diagnostics;
    
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Numerics;
    using Thread = System.Threading.Thread;
    using TimeSpan = System.TimeSpan;
    
    using ImGuiNET;
    using static ImGuiNET.ImGui;
    using static ImGuiExtension;
    
    public class PerfMonitor : BaseWindow
    {
        public float GraphHeight = 48;
        public float FrameHeight = 128;
        
        // Work agnostic data
        Dictionary<Thread, ThreadSampleCollection> _cpuSamples = new Dictionary<Thread, ThreadSampleCollection>();
        LightweightTimer _timer = LightweightTimer.StartNew();
        (TimeSpan start, double duration) _cpuFrame;
        
        // Xenko-specific data
        Dictionary<ProfilingKey, TemporaryXenkoSample> _bufferedEvents = new Dictionary<ProfilingKey, TemporaryXenkoSample>();
        List<SampleInstance> _gpuSamples = new List<SampleInstance>();
        List<SampleInstance> _xenkoSamples = new List<SampleInstance>();
        (TimeSpan start, double duration) _gpuFrame, _xenkoFrame;
        uint _gpuDepth, _xenkoDepth;
        
        GraphPoint _graphAggregated;
        GraphPoint[] _graph = new GraphPoint[256];
        
        bool _pauseEval;

        static readonly ProfilingEventType[] PROFILING_EVENT_TYPES = (ProfilingEventType[])System.Enum.GetValues( typeof(ProfilingEventType) );
        static ProfilingKey _dummyKey = new ProfilingKey("dummy");
        List<ProfilingKey> _tempList = new List<ProfilingKey>();
        
        
        
        
        GraphPoint Average => _graphAggregated / _graph.Length;
        protected override ImGuiWindowFlags WindowFlags => ImGuiWindowFlags.NoMove |
                                                           ImGuiWindowFlags.NoSavedSettings |
                                                           ImGuiWindowFlags.NoFocusOnAppearing;
        protected override Vector2? WindowPos => new Vector2( 1f, 1f );
        
        
        
        
        
        
        
        
        
        
        /// <summary> Place within a using statement to monitor the code within it </summary>
        public PerfSampler Sample(
            bool sample = true,
            [ CallerLineNumber ] int    line     = 0,
            [ CallerMemberName ] string member   = null,
            [ CallerFilePath ]   string filePath = null ) => Sample($"{filePath} . {member}:{S(line)}", sample );

        /// <summary> Place within a using statement to monitor the code within it </summary>
        public PerfSampler Sample( string id, bool sample = true )
        {
            if( sample == false )
                return new PerfSampler();
            return new PerfSampler( id, this );
        }






        public PerfMonitor( IServiceRegistry services ) : base( services ){ }
        
        
        
        
        
        
        
        
        
        
        
        
        Vector2 GetGraphSize() => new Vector2(MaxWidth(), GraphHeight);
        float MaxWidth() => GetContentRegionAvailWidth();
        protected override void OnDraw( bool collapsed )
        {
            if( collapsed )
                return;
            
            Checkbox( "Pause", ref _pauseEval );
            
            { // Draw and update frame-time graph
                float min = float.MaxValue, max = float.MinValue;
                for( int i = 0; i < _graph.Length; i++ )
                {
                    float v = _graph[ i ].FrameTime;
                    if( v > max )
                        max = v;
                    if( v < min )
                        min = v;
                }
    
                PlotLines( "", ref _graph[0].FrameTime, _graph.Length, 
                    overlay:$"~{S(Average.FrameTime)}ms({S( 1f / ( Average.FrameTime / 1000f ) )}fps)",
                    // Reduce the size somewhat to make space for the text
                    size:GetGraphSize() - new Vector2(48f, 0f),
                    stride:GraphPoint.SizeOf );
                SameLine();
                TextUnformatted($"-{S(max)}\n({S(max-min)})\n-{S(min)}");
            }

            using( UColumns( 2 ) )
            {
                if( CollapsingHeader( "Managed Memory" ) )
                {
                    float min = float.MaxValue, max = float.MinValue;
                    for( int i = 0; i < _graph.Length; i++ )
                    {
                        float v = _graph[ i ].TotalManagedMB;
                        if( v > max )
                            max = v;
                        if( v < min )
                            min = v;
                    }
                    
                    PlotLines( "", ref _graph[0].TotalManagedMB, _graph.Length,  
                        overlay:$"Total: ~{S(Average.TotalManagedMB)}MB ({S(max-min)}d)", 
                        size:GetGraphSize(), 
                        stride:GraphPoint.SizeOf );
                }
                
                NextColumn();
                
                if( CollapsingHeader( "Draw calls" ) )
                {
                    PlotLines( "", ref _graph[0].DrawCalls, _graph.Length, 
                        valueMin:0f, 
                        overlay:$"~{S(Average.DrawCalls)}", 
                        size:GetGraphSize(), 
                        stride:GraphPoint.SizeOf );
                }
            }
            
            using( UColumns( 2 ) )
            {
                if( CollapsingHeader( "Buffer Memory" ) )
                {
                    PlotLines( "", ref _graph[ 0 ].BufferMemMB, _graph.Length,
                        valueMin: 0f,
                        overlay: $"~{S( Average.BufferMemMB )}MB",
                        size: GetGraphSize(),
                        stride: GraphPoint.SizeOf );
                }

                NextColumn();

                if( CollapsingHeader( "Texture Memory" ) )
                {
                    PlotLines( "", ref _graph[ 0 ].TexMemMB, _graph.Length,
                        valueMin: 0f,
                        overlay: $"~{S( Average.TexMemMB )}MB",
                        size: GetGraphSize(),
                        stride: GraphPoint.SizeOf );
                }
            }

            if( CollapsingHeader( "Frame", ImGuiTreeNodeFlags.DefaultOpen ) )
            {
                using( Child() )
                {
                    // CPU
                    foreach( var data in _cpuSamples )
                    {
                        var thread = data.Key;
                        var samples = data.Value.Displayed;
                        if( CollapsingHeader( thread.Name ) == false )
                            continue;
                        // Child() to properly align GetCursorPos
                        using( Child(size:new Vector2(0f, FrameHeight)) )
                        {
                            var corner = GetCursorPos();
                            for( int i = 0; i < samples.Count; i++ )
                                DrawSample( corner, MaxWidth(), samples[ i ], _cpuFrame.start, _cpuFrame.duration );
                        }
                    }
    
                    var buttonSize = new Vector2( GetContentRegionAvailWidth(), GetTextLineHeightWithSpacing() );
                    bool profiling = IsXenkoProfilingAll();
                    if( Button( profiling ? "Stop Profiling" : "Profile Xenko", buttonSize ) )
                    {
                        if( profiling )
                            Profiler.DisableAll();
                        else
                            Profiler.EnableAll();
                    }
                        
        
                    // GPU
                    if( CollapsingHeader( _gpuSamples.Count != 0 ? "GPU" : "GPU (profiling is off)" ) )
                    {
                        // Child() to properly align GetCursorPos
                        using( Child(size:new Vector2(0f, FrameHeight)) )
                        {
                            var corner = GetCursorPos();
                            foreach( var data in _gpuSamples )
                                DrawSample( corner, MaxWidth(), data, _gpuFrame.start, _gpuFrame.duration );
                        }
                    }
                    // Xenko Systems
                    if( CollapsingHeader( _xenkoSamples.Count != 0 ? "Xenko Systems" : "Xenko Systems (profiling is off)" ) )
                    {
                        // Child() to properly align GetCursorPos
                        using( Child(size:new Vector2(0f, FrameHeight)) )
                        {
                            var corner = GetCursorPos();
                            foreach( var data in _xenkoSamples )
                                DrawSample( corner, MaxWidth(), data, _xenkoFrame.start, _xenkoFrame.duration );
                        }
                    }
                }
            }
        }


        static void DrawSample( Vector2 corner, float maxWidth, SampleInstance sample, TimeSpan start, double duration )
        {
            const float MIN_SIZE = 5f;
            float height = GetTextLineHeightWithSpacing();
            // Get ratio of this sample compared to total frame duration
            float size = (float) ( sample.Duration / duration );
            size *= maxWidth; // Fit ratio to window
            size = size < MIN_SIZE ? MIN_SIZE : size;
            // Compute offset from the window's edge
            float pos = (float) ( sample.Start - start ).TotalMilliseconds;
            pos /= (float) duration;
            pos *= maxWidth;
            
            SetCursorPos( corner + new Vector2(pos, sample.Depth * height) );
            Button( sample.Id, new Vector2( size, height ) );
            if( IsItemHovered() )
                using( Tooltip() )
                    TextUnformatted($"{sample.Id}:\n{S(sample.Duration)}ms");
        }
        
        
        
        
        
        

        
        public void EndFrame()
        {
            if( _pauseEval )
            {
                foreach( var kvp in _cpuSamples )
                    kvp.Value.ClearBuffered();
                _timer = LightweightTimer.StartNew();
                // CONSUME ALL PENDING EVENTS WHEN PROFILING IS ON TO AVOID XENKO TRIPPING ON ITSELF
                Profiler.GetEvents( ProfilingEventType.CpuProfilingEvent, true );
                return;
            }

            { // Manage xenko-specific profiler events
                // aggregate any buffered xenko events
                foreach( var eType in PROFILING_EVENT_TYPES )
                {
                    bool receivedData = false;
                    foreach( var perfEvent in Profiler.GetEvents( eType, false ) )
                    {
                        var v = Guaranteed( _bufferedEvents, perfEvent.Key );
                        switch( perfEvent.Type )
                        {
                            case ProfilingMessageType.Begin:
                                v.Start = ComputeAccurateTimespan( perfEvent.TimeStamp, eType );
                                v.Type = eType;
                                v.Depth = Depth( eType )++;
                                break;
                            case ProfilingMessageType.End:
                                Depth( eType )--;
                                v.Duration = ComputeAccurateTimespan( perfEvent.ElapsedTime, eType );
                                break;
                            default:
                                continue;
                        }

                        receivedData = true;
    
                        _bufferedEvents[ perfEvent.Key ] = v;
                    }
                    
                    // Check if events finished and push them to display if they are
                    // <= 1: the cpu profiler seems to have a never-ending event created, perhaps application lifetime ?
                    // I haven't really investigated it
                    if( receivedData && Depth( eType ) <= 1 )
                    {
                        var receiver = Receiver( eType );
                        // Clear past frames
                        receiver.Clear();
                        
                        foreach( var kvp in _bufferedEvents )
                        {
                            var tempSample = kvp.Value;
                            if( tempSample.Type != eType )
                                continue;
                            
                            var id = kvp.Key.Name;
                            var sample = new SampleInstance( id, tempSample.Depth, tempSample.Start, tempSample.Duration.TotalMilliseconds );
                            receiver.Add( sample );
                            // Mark this key for removal
                            _tempList.Add( kvp.Key );
                        }
                        
                        // Find min-max
                        if( receiver.Count > 0 )
                        {
                            TimeSpan min = TimeSpan.MaxValue, max = TimeSpan.MinValue;
                            foreach( var data in receiver )
                            {
                                TimeSpan cMin = data.Start;
                                TimeSpan cMax = cMin + TimeSpan.FromMilliseconds( data.Duration );
                                if( cMin < min )
                                    min = cMin;
                                if( cMax > max )
                                    max = cMax;
                            }
                            Frames( eType ) = ( min, ( max - min ).TotalMilliseconds );
                        }
                    }

                }
                // Remove finished events
                foreach( var key in _tempList )
                    _bufferedEvents.Remove( key );
                _tempList.Clear();
            }

            foreach( var threadSample in _cpuSamples )
                threadSample.Value.SetReady();

            _cpuFrame = ( _timer.InitTime, _timer.Restart().TotalMilliseconds );

            const double MB = ( 1 << 20 );
            GraphPoint newPoint = new GraphPoint
            {
                FrameTime = (float) _cpuFrame.duration,
                TotalManagedMB = (float) ( System.GC.GetTotalMemory( false ) / MB ),
                DrawCalls = GraphicsDevice.FrameDrawCalls,
                BufferMemMB = (float) ( GraphicsDevice.BuffersMemory / MB ),
                TexMemMB = (float) ( GraphicsDevice.TextureMemory / MB )
            };
            
            // Use simple aggregate to avoid having to loop through the array to get the average
            _graphAggregated -= _graph[ 0 ];
            _graphAggregated += newPoint;

            // Move each value to a lower position in the array, could be replaced by a mem copy ?
            for( int i = 0; i < _graph.Length - 1; i++ )
                _graph[ i ] = _graph[ i + 1 ];
            // Push latest onto our plot
            _graph[ _graph.Length - 1 ] = newPoint;
        }

        protected override void OnDestroy()
        {
            if( IsXenkoProfilingAll() )
                Profiler.DisableAll();
        }
        
        
        
        
        
        
        
        
        
        
        

        TimeSpan ComputeAccurateTimespan(long ticks, ProfilingEventType pet)
        {
            switch( pet )
            {
                case ProfilingEventType.CpuProfilingEvent:
                    return Xenko.Core.Utilities.ConvertRawToTimestamp( ticks );
                case ProfilingEventType.GpuProfilingEvent: // Lifted from xenko's code base
                    return new TimeSpan((ticks * 10000000) / GraphicsDevice.TimestampFrequency);
                default:
                    throw new System.InvalidOperationException();
            }
        }
        
        ref uint Depth( ProfilingEventType type )
        {
            switch( type )
            {
                case ProfilingEventType.CpuProfilingEvent: return ref _xenkoDepth;
                case ProfilingEventType.GpuProfilingEvent: return ref _gpuDepth;
                default: throw new System.InvalidOperationException();
            }
        }
        ref ( TimeSpan start, double ms ) Frames( ProfilingEventType type )
        {
            switch( type )
            {
                case ProfilingEventType.CpuProfilingEvent: return ref _xenkoFrame;
                case ProfilingEventType.GpuProfilingEvent: return ref _gpuFrame;
                default: throw new System.InvalidOperationException();
            }
        }

        List<SampleInstance> Receiver( ProfilingEventType type )
        {
            switch( type )
            {
                case ProfilingEventType.CpuProfilingEvent: return _xenkoSamples;
                case ProfilingEventType.GpuProfilingEvent: return _gpuSamples;
                default: throw new System.InvalidOperationException();
            }
        }
        
        /// <summary>
        /// Format the given generic value to a string,
        /// this is a shortcut to avoid implicit casting to object when string interpolation is used.
        /// By default float-like values are formatted to a display friendly string.
        /// </summary>
        static string S<T>(T val, string format = null)
        {
            if( val is float f )
                return f.ToString( format ?? "F1", System.Globalization.CultureInfo.CurrentCulture );
            if( val is double d )
                return d.ToString( format ?? "F1", System.Globalization.CultureInfo.CurrentCulture );
            return val.ToString();
        }

        static bool IsXenkoProfilingAll()
        {
            Profiler.Disable( _dummyKey );
            // With the given disabled key this function will return true if EnableAll is set
            return Profiler.IsEnabled( _dummyKey );
        }
        
        /// <summary> Guarantees that this key exist and returns at least a default new() value </summary>
        static TValue Guaranteed<TKey, TValue>( IDictionary<TKey, TValue> dictionary, TKey key ) where TValue : new()
        {
            if( dictionary.TryGetValue( key, out TValue value ) == false )
            {
                value = new TValue();
                dictionary.Add( key, value );
            }

            return value;
        }
        
        
        
        
        /// <summary>
        /// Put this within a using statement to log performance of the code within it.
        /// Creates a new <see cref="SampleInstance"/> for the attached <see cref="PerfMonitor"/>
        /// once <see cref="PerfSampler(string, PerfMonitor)"/> and <see cref="Dispose()"/> have been called.
        /// The duration sent to the <see cref="PerfMonitor"/> will be the one between those calls.
        /// </summary>
        public readonly struct PerfSampler : System.IDisposable
        {
            readonly LightweightTimer _timer;
            readonly string _id;
            readonly uint _depth;
            readonly bool _valid;
            readonly ThreadSampleCollection _target;
            
            
            public PerfSampler(string id, PerfMonitor monitor)
            {
                _id = id;
                _target = Guaranteed( monitor._cpuSamples, Thread.CurrentThread );
                _depth = _target.Depth;
                _target.Depth++;
                
                _timer = LightweightTimer.StartNew();
                _valid = true;
            }
            /// <summary> Dispose of it to log it to the PerfMonitor </summary>
            public void Dispose()
            {
                if( _valid == false )
                    return;
                
                TimeSpan start = _timer.InitTime;
                double ms = _timer.Elapsed.TotalMilliseconds;
                        
                var sampleInstance = new SampleInstance( _id, _depth, start, ms );
                _target.Depth--;
                _target.Add( sampleInstance );
            }
        }
        
        
        
        
        /// <summary>
        /// This struct's operation expects it to only contain instance fields of <see cref="float"/> type.
        /// </summary>
        [ System.Runtime.InteropServices.StructLayout( System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4 ) ]
        struct GraphPoint
        {
            public static readonly unsafe int SizeOf = sizeof(GraphPoint);
            public static readonly int Count = SizeOf / 4;
            
            public float FrameTime, TotalManagedMB, DrawCalls, BufferMemMB, TexMemMB;
            
            // Math operations are made in bulk, the struct is read as an array of floats, applies the operation
            // over each elements and outputs the result of those operations as a new instance.
            #region OPERATOR
            [ MethodImpl( MethodImplOptions.AggressiveInlining ) ]
            public static GraphPoint operator +( in GraphPoint a, in GraphPoint b )
            {
                unsafe
                {
                    GraphPoint dest = a;
                    float* pDest = (float*)&dest;
                    fixed( GraphPoint* pSrcTmp = &b )
                    {
                        float* pSrc = (float*)pSrcTmp;
                        var remaining = Count;
                        while(remaining-- > 0)
                        {
                            *pDest += *pSrc;
                            pSrc++;
                            pDest++;
                        }
                    }

                    return dest;
                }
            }

            [ MethodImpl( MethodImplOptions.AggressiveInlining ) ]
            public static GraphPoint operator -( in GraphPoint a, in GraphPoint b )
            {
                unsafe
                {
                    GraphPoint dest = a;
                    float* pDest = (float*)&dest;
                    fixed( GraphPoint* pSrcTmp = &b )
                    {
                        float* pSrc = (float*)pSrcTmp;
                        var remaining = Count;
                        while(remaining-- > 0)
                        {
                            *pDest -= *pSrc;
                            pSrc++;
                            pDest++;
                        }
                    }

                    return dest;
                }
            }

            [ MethodImpl( MethodImplOptions.AggressiveInlining ) ]
            public static GraphPoint operator /( in GraphPoint a, in float v )
            {
                unsafe
                {
                    GraphPoint dest = a;
                    float* pDest = (float*)&dest;
                    var remaining = Count;
                    while(remaining-- > 0)
                    {
                        *pDest /= v;
                        pDest++;
                    }
                    
                    return dest;
                }
            }

            [ MethodImpl( MethodImplOptions.AggressiveInlining ) ]
            public static GraphPoint operator *( in GraphPoint a, in float v )
            {
                unsafe
                {
                    GraphPoint dest = a;
                    float* pDest = (float*)&dest;
                    var remaining = Count;
                    while(remaining-- > 0)
                    {
                        *pDest *= v;
                        pDest++;
                    }
                    
                    return dest;
                }
            }
            #endregion
        }
        
        
        
        
        /// <summary> Object containing a sample's data </summary>
        readonly struct SampleInstance
        {
            public readonly string Id;
            public readonly uint Depth;
            public readonly TimeSpan Start;
            public readonly double Duration;

            public SampleInstance(string id, uint depth, TimeSpan start, double duration)
            {
                Id = id;
                Depth = depth;
                Start = start;
                Duration = duration;
            }
        }
        
        
        
        /// <summary> Object to hold Xenko's profiler samples until they are marked as done </summary>
        struct TemporaryXenkoSample
        {
            public ProfilingEventType Type;
            public TimeSpan Start;
            public TimeSpan Duration;
            public uint Depth;
        }



        
        /// <summary>
        /// A collection of <see cref="SampleInstance"/> for a specific thread,
        /// samples are aggregated and once <see cref="SetReady"/> called,
        /// will be pushed to <see cref="Displayed"/>.
        /// </summary>
        class ThreadSampleCollection
        {
            /// <summary>
            /// Current depth (sample within sample) of this thread,
            /// should be incremented when starting and decremented
            /// when ending a sample by the <see cref="PerfSampler"/>.
            /// </summary>
            public uint Depth;
            /// <summary>
            /// Display-ready samples: samples that have ended before the end of the frame.
            /// </summary>
            public IReadOnlyList<SampleInstance> Displayed => _displayed;

            object _bufferLock = new object();
            List<SampleInstance> _buffered = new List<SampleInstance>();
            List<SampleInstance> _displayed = new List<SampleInstance>();

            /// <summary> We received all of the data for this frame, set it has ready </summary>
            public void SetReady()
            {
                lock( _bufferLock )
                {
                    var temp = _buffered;
                    _buffered = _displayed;
                    _displayed = temp;
                    _buffered.Clear();
                }
            }
            
            /// <summary> The given sample has finished and is ready to be displayed </summary>
            public void Add( SampleInstance sampleInstance )
            {
                lock( _bufferLock )
                    _buffered.Add(sampleInstance);
            }
            
            /// <summary> Clear any samples buffered </summary>
            public void ClearBuffered()
            {
                lock( _bufferLock )
                    _buffered.Clear();
            }
        }
    }
}