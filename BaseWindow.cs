using System.Diagnostics;

namespace XenkoCommunity.ImGuiDebug
{
    using Xenko.Core;
    using Xenko.Engine;
    using Xenko.Games;
    
    using System.Numerics;
    
    using ImGuiNET;
    using static ImGuiExtension;
    
    public abstract class BaseWindow : GameSystem
    {
        static object _idLock = new object();
        static uint _windowId;
        
        protected bool Open = true;
        protected uint Id;
        protected virtual ImGuiWindowFlags WindowFlags => ImGuiWindowFlags.None;
        protected virtual Vector2? WindowPos => null;
        string _uniqueName;
        ImGuiSystem _imgui;

        protected BaseWindow( IServiceRegistry services ) : base( services )
        {
            Game.GameSystems.Add(this);
            Enabled = true;
            lock( _idLock )
            {
                Id = _windowId++;
            }
            // IDs could be per type / name collision instead
            _uniqueName = $"{GetType().Name}({Id})";
        }

        public override void Update( GameTime gameTime )
        {
            // Allow for some leeway to avoid throwing if imgui
            // as not been set or this runs before imgui is ready
            if( _imgui is null )
            {
                _imgui = Services.GetService<ImGuiSystem>();
                if( _imgui is null )
                    return;
                if( UpdateOrder != _imgui.UpdateOrder + 1 )
                {
                    UpdateOrder = _imgui.UpdateOrder + 1;
                    return;
                }
            }
            
            if( WindowPos != null ) 
                ImGui.SetNextWindowPos( WindowPos.Value );
            using( Window( _uniqueName, ref Open, out bool collapsed, WindowFlags ) )
            {
                OnDraw( collapsed );
            }

            if( Open == false )
            {
                Game.GameSystems.Remove( this );
                Dispose();
                #warning Doesn't properly get removed from the list of update-executing systems, need to investigate, forced to do this for now
                Enabled = false;
            }
        }
        protected abstract void OnDraw( bool collapsed );
        protected abstract void OnDestroy();

        protected override void Destroy()
        {
            OnDestroy();
            base.Destroy();
        }
    }
}