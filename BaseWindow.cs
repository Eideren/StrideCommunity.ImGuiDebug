namespace XenkoCommunity.ImGuiDebug
{
    using System.Collections.Generic;
    using Xenko.Core;
    using Xenko.Engine;
    using Xenko.Games;
    
    using System.Numerics;
    
    using ImGuiNET;
    using static ImGuiExtension;
    
    public abstract class BaseWindow : GameSystem
    {
        static Dictionary<string, uint> _windowId = new Dictionary<string, uint>();
        
        protected bool Open = true;
        protected uint Id;
        protected virtual ImGuiWindowFlags WindowFlags => ImGuiWindowFlags.None;
        protected virtual Vector2? WindowPos => null;
        protected virtual Vector2? WindowSize => null;
        string _uniqueName;
        ImGuiSystem _imgui;

        protected BaseWindow( IServiceRegistry services ) : base( services )
        {
            Game.GameSystems.Add(this);
            Enabled = true;
            var n = GetType().Name;
            lock( _windowId )
            {
                if( _windowId.TryGetValue( n, out Id ) == false )
                {
                    Id = 1;
                    _windowId.Add( n, Id );
                }

                _windowId[ n ] = Id + 1;
            }
            _uniqueName = Id == 1 ? n : $"{n}({Id})";
        }

        public override void Update( GameTime gameTime )
        {
            // Allow for some leeway to avoid throwing
            // if imgui as not been set yet
            _imgui = _imgui ?? Services.GetService<ImGuiSystem>();
            if( _imgui is null )
                return;
            
            // This component must run after imgui to
            // avoid throwing and single frame lag
            if( UpdateOrder <= _imgui.UpdateOrder )
            {
                UpdateOrder = _imgui.UpdateOrder + 1;
                return;
            }
            
            if( WindowPos != null ) 
                ImGui.SetNextWindowPos( WindowPos.Value );
            if( WindowSize != null )
                ImGui.SetNextWindowSize( WindowSize.Value );
            using( Window( _uniqueName, ref Open, out bool collapsed, WindowFlags ) )
            {
                OnDraw( collapsed );
            }

            if( Open == false )
            {
                Enabled = false;
                Dispose();
            }
        }
        protected abstract void OnDraw( bool collapsed );
        protected abstract void OnDestroy();

        protected override void Destroy()
        {
            Game.GameSystems.Remove( this );
            OnDestroy();
            base.Destroy();
        }
    }
}