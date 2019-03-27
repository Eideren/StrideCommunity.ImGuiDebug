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
        public static DisposableImGui<Empty> Tooltip()
        {
            BeginTooltip();
            return new DisposableImGui<Empty>( true, DisposableTypes.Tooltip );
        }
        public static DisposableImGui<float> UIndent( float size = 0f )
        {
            Indent(size);
            return new DisposableImGui<float>( true, DisposableTypes.Indentation, size );
        }
        public static DisposableImGui<Empty> UColumns( int count, string id = null, bool border = false )
        {
            Columns( count, id, border );
            return new DisposableImGui<Empty>( true, DisposableTypes.Columns );
        }
        public static DisposableImGui<Empty> Window( string name, ref bool open, out bool collapsed, ImGuiWindowFlags flags = ImGuiWindowFlags.None )
        {
            collapsed = ! Begin( name, ref open, flags );
            return new DisposableImGui<Empty>( true, DisposableTypes.Window );
        }
        
        public static DisposableImGui<Empty> Child( [ CallerLineNumber ] int cln = 0, Vector2 size = default,
            bool border = false, ImGuiWindowFlags flags = ImGuiWindowFlags.None )
        {
            return new DisposableImGui<Empty>(BeginChild((uint)cln, size, border, flags), DisposableTypes.Child );
        }
        public static DisposableImGui<Empty> MenuBar() => new DisposableImGui<Empty>(BeginMenuBar(), DisposableTypes.MenuBar );
        public static DisposableImGui<Empty> Menu(string label, bool enabled = true) => new DisposableImGui<Empty>(BeginMenu(label, enabled), DisposableTypes.Menu );
        
        
        public struct DisposableImGui<T> : IDisposable
        {
            T _parameters;
            bool _dispose;
            DisposableTypes _type;

            public DisposableImGui( bool dispose, DisposableTypes type, T parameters = default )
            {
                _dispose = dispose;
                _type = type;
                _parameters = parameters;
            }
            public void Dispose()
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
            Columns
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