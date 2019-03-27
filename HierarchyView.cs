namespace XenkoCommunity.ImGuiDebug
{
    using System.Numerics;
    using System.Collections.Generic;
    using Guid = System.Guid;
    
    using Xenko.Core;
    using Xenko.Engine;
    
    using ImGuiNET;
    using static ImGuiNET.ImGui;
    using static ImGuiExtension;
    public class HierarchyView : BaseWindow
    {
        /// <summary>
        /// Based on hashcodes, it doesn't have to be exact, we just don't want to keep references from being collected
        /// </summary>
        HashSet<Guid> _recursingThrough = new HashSet<Guid>();

        const float DUMMY_WIDTH = 19;
        const float INDENTATION2 = DUMMY_WIDTH+8;

        public HierarchyView( IServiceRegistry service ) : base( service ) { }
        
        protected override void OnDraw( bool collapsed )
        {
            if( collapsed )
                return;
                    
            using( Child() )
            {
                RecursiveDrawing( Game.SceneSystem.SceneInstance.RootScene );
            }
        }

        protected override void OnDestroy(){}
        
        void RecursiveDrawing( IIdentifiable source, int d = 0 )
        {
            foreach( var child in EnumerateChildren( source ) )
            {
                string label;
                bool canRecurse;
                {
                    if( child is Entity entity )
                    {
                        label = entity.Name;
                        canRecurse = entity.Transform.Children.Count > 0;
                    }
                    else if( child is Scene scene )
                    {
                        label = scene.Name;
                        canRecurse = scene.Children.Count > 0 || scene.Entities.Count > 0;
                    }
                    else return;
                }
    
                PushID( child.Id.GetHashCode() );
                
                bool recurse = canRecurse && _recursingThrough.Contains( child.Id );
                if( canRecurse )
                {
                    if( ArrowButton( "", recurse ? ImGuiDir.Down : ImGuiDir.Right ) )
                    {
                        if( recurse )
                            _recursingThrough.Remove( child.Id );
                        else
                            _recursingThrough.Add( child.Id );
                    }
                }
                else
                    Dummy( new Vector2( DUMMY_WIDTH, 1 ) );
                SameLine();
                
                if( Button( label ) )
                    Inspector.FindFreeInspector( Services ).Target = child;

                using( UIndent( INDENTATION2 ) )
                {
                    if( recurse )
                        RecursiveDrawing( child, d+1 );
                }
            }
        }
        
        static IEnumerable<IIdentifiable> EnumerateChildren( IIdentifiable source )
        {
            if( source is Entity entity )
            {
                foreach( var child in entity.Transform.Children )
                    yield return child.Entity;
            }
            else if( source is Scene scene )
            {
                foreach( var childEntity in scene.Entities )
                    yield return childEntity;
                foreach( var childScene in scene.Children )
                    yield return childScene;
            }
        }
    }
}