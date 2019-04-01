namespace XenkoCommunity.ImGuiDebug
{
    
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using IDisposable = System.IDisposable;
    using ArgumentOutOfRangeException = System.ArgumentOutOfRangeException;
    
    using ImGuiNET;
    using static ImGuiNET.ImGui;
    
    public static class ImGuiExtension
    {
        public static DisposableImGui ID( string id )
        {
            PushID( id );
            return new DisposableImGui( true, DisposableTypes.ID );
        }
        public static DisposableImGui ID( int id )
        {
            PushID( id );
            return new DisposableImGui( true, DisposableTypes.ID );
        }
        public static DisposableImGui UCombo( string label, string previewValue, out bool open, ImGuiComboFlags flags = ImGuiComboFlags.None )
        {
            return new DisposableImGui( open = BeginCombo( label, previewValue, flags ), DisposableTypes.Combo );
        }
        public static DisposableImGui Tooltip()
        {
            BeginTooltip();
            return new DisposableImGui( true, DisposableTypes.Tooltip );
        }
        public static DisposableImGui<float> UIndent( float size = 0f )
        {
            Indent(size);
            return new DisposableImGui<float>( true, DisposableTypes.Indentation, size );
        }
        public static DisposableImGui UColumns( int count, string id = null, bool border = false )
        {
            Columns( count, id, border );
            return new DisposableImGui( true, DisposableTypes.Columns );
        }
        public static DisposableImGui Window( string name, ref bool open, out bool collapsed, ImGuiWindowFlags flags = ImGuiWindowFlags.None )
        {
            collapsed = ! Begin( name, ref open, flags );
            return new DisposableImGui( true, DisposableTypes.Window );
        }
        
        public static DisposableImGui Child( [ CallerLineNumber ] int cln = 0, Vector2 size = default,
            bool border = false, ImGuiWindowFlags flags = ImGuiWindowFlags.None )
        {
            BeginChild( (uint) cln, size, border, flags );
            return new DisposableImGui(true, DisposableTypes.Child );
        }
        public static DisposableImGui MenuBar(out bool open) => new DisposableImGui(open = BeginMenuBar(), DisposableTypes.MenuBar );
        public static DisposableImGui Menu(string label, out bool open, bool enabled = true) => new DisposableImGui(open = BeginMenu(label, enabled), DisposableTypes.Menu );
        
        
        public struct DisposableImGui<T> : IDisposable
        {
            T _parameters;
            DisposableImGui _innerObj;

            public DisposableImGui( bool dispose, DisposableTypes type, T parameters = default )
            {
                _parameters = parameters;
                _innerObj = new DisposableImGui( dispose, type );
            }
            public void Dispose() => _innerObj.Dispose( _parameters );
        }
        
        public struct DisposableImGui : IDisposable
        {
            bool _dispose;
            DisposableTypes _type;

            public DisposableImGui( bool dispose, DisposableTypes type )
            {
                _dispose = dispose;
                _type = type;
            }

            public void Dispose()
            {
                // Dummy object
                Dispose(true);
            }
            public void Dispose<T>(T _parameters)
            {
                if( ! _dispose )
                    return;
                
                switch( _type )
                {
                    case DisposableTypes.Menu: EndMenu(); return;
                    case DisposableTypes.MenuBar: EndMenuBar(); return;
                    case DisposableTypes.Child: EndChild(); return;
                    case DisposableTypes.Window: End(); return;
                    case DisposableTypes.Tooltip: EndTooltip(); return;
                    case DisposableTypes.Columns: Columns(1); return;
                    case DisposableTypes.Combo: EndCombo(); return;
                    case DisposableTypes.ID: PopID(); return;
                    case DisposableTypes.Indentation:
                        if( _parameters is float f )
                            Unindent(f);
                        else
                            Unindent();
                        return;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        public enum DisposableTypes
        {
            Menu,
            MenuBar,
            Child,
            Window,
            Indentation,
            Tooltip,
            Columns,
            Combo,
            ID
        }
        
        public static void PlotLines
        (
            string label,
            ref float values,
            int count,
            int offset = 0,
            string overlay = null,
            float valueMin = float.MaxValue,
            float valueMax = float.MaxValue,
            Vector2 size = default,
            int stride = 4)
        {
            ImGui.PlotLines(label, ref values, count, offset, overlay, valueMin, valueMax, size, stride);
        }
    }
}