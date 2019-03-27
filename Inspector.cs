namespace XenkoCommunity.ImGuiDebug
{
    using Xenko.Core;
    
    using System.Numerics;
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Runtime.CompilerServices;
    
    using ImGuiDir = ImGuiNET.ImGuiDir;
    using static ImGuiNET.ImGui;
    using static ImGuiExtension;
    
    public class Inspector : BaseWindow
    {
        public delegate bool ValueHandler( string label, ref object value );
        /// <summary> Add your drawing functions to explicitly override drawing for objects of the given type </summary>
        public static ConcurrentDictionary<Type, ValueHandler> ValueDrawingHandlers = new ConcurrentDictionary<Type, ValueHandler>();
        
        public object Target
        {
            get
            {
                if( _target.TryGetTarget( out object target ) )
                    return target;
                return null;
            }
            set
            {
                if( _target == value )
                    return;
                
                _target.SetTarget( value );
                _openedId.Clear();
            }
        }
        
        
        
        
        
        
        
        
        
        
        
        
        static List<Inspector> _inspectors = new List<Inspector>();
        static readonly Filter[] _filterValues = (Filter[]) Enum.GetValues( typeof(Filter) );


        public bool Locked = false;
        
        /// <summary> Lets not keep reference alive even if we end up sharing codes </summary>
        Dictionary<Type, MemberInfo[]> _cachedMembers = new Dictionary<Type, MemberInfo[]>();
        HashSet<int> _openedId = new HashSet<int>();
        WeakReference<object> _target = new WeakReference<object>(null);
        bool _iEnumerableViewer = true;
        Filter _filter = Filter.Public | Filter.Inherited | Filter.Properties | Filter.Fields;
        
        const float DUMMY_WIDTH = 19;
        const float INDENTATION2 = DUMMY_WIDTH+8;
        
        [ Flags ]
        enum Filter : uint
        {
            Fields = 1,
            Properties = Fields << 1,
            PublicStatic = Properties << 1,
            NonPublicStatic = PublicStatic << 1,
            Public = NonPublicStatic << 1,
            NonPublic = Public << 1,
            Inherited = NonPublic << 1,
        }
        
        public Inspector( IServiceRegistry services ) : base( services )
        {
            _inspectors.Add( this );
        }
        
        public static Inspector FindFreeInspector( IServiceRegistry services )
        {
            foreach( Inspector inspector in _inspectors )
            {
                if( ! inspector.Locked )
                    return inspector;
            }

            return new Inspector( services );
        }
        protected override void OnDestroy()
        {
            _inspectors.Remove( this );
        }




        protected override void OnDraw( bool collapsed )
        {
            if( collapsed )
                return;
            uint filterUI = (uint)_filter;

            int align = 0;
            foreach( Filter def in _filterValues )
            {
                CheckboxFlags( def.ToString(), ref filterUI, (uint)def );
                // Alignment breaks every 2 lines
                if( ++align % 2 != 0 && align != _filterValues.Length )
                    SameLine();
            }
            
            // On change, clear members to force re-filter
            if( filterUI != (uint) _filter )
            {
                _filter = (Filter) filterUI;
                _cachedMembers.Clear();
            }

            Checkbox( "Locked", ref Locked );
            Checkbox( "IEnumerable viewer", ref _iEnumerableViewer );

            using( Child() )
            {
                if( Target != null )
                {
                    TextUnformatted( $"Inspecting [{Target}]" );
                    RecursiveDraw( Target, Target.GetType().GetHashCode() );
                }
            }
        }

        bool RecursiveDraw( object target, int hashcodeSource )
        {
            if(target == null)
                return false;

            MemberInfo[] members = GetMembers( target.GetType() );

            bool hasChanged = false;
            using( UIndent( INDENTATION2 ) )
            {
                if( _iEnumerableViewer && target is IEnumerable ienum )
                {
                    SetNextWindowBgAlpha( 0.2f );
                    
                    int index = 0;
                    foreach( object o in ienum )
                    {
                        object o2 = o;
                        bool changed = RecursiveDrawValue( $"[{index++.ToString()}]:{o2}", ref o2, true, hashcodeSource );
                    }

                    if( index != 0 )
                        Spacing();
                }
                foreach( var member in members )
                {
                    object value;
                    bool readOnly;
                    { // Get value
                        try
                        {
                            if( member is FieldInfo fi )
                            {
                                value = fi.GetValue( target );
                                readOnly = false;
                            }
                            else if( member is PropertyInfo pi && pi.CanRead && pi.GetMethod.GetParameters().Length == 0 )
                            {
                                value = pi.GetValue( target );
                                readOnly = ! pi.CanWrite;
                            }
                            else 
                                continue;
                        }
                        catch( Exception e )
                        {
                            value = $"x Exception: {e.Message}";
                            readOnly = true;
                        }
                    }

                    bool changed = RecursiveDrawValue(member.Name, ref value, readOnly, hashcodeSource);
                    if( changed && !readOnly )
                    {
                        hasChanged = true;
                        try
                        {
                            if( member is FieldInfo fi )
                                fi.SetValue( target, value );
                            else if( member is PropertyInfo pi )
                                pi?.SetValue(target, value);
                            else
                                throw new NotImplementedException();
                        }
                        catch( Exception e )
                        {
                            System.Console.Out?.WriteLine( e );
                        }
                    }
                }
            }
            
            // structs have to bubble up their changes since the object
            // we get is not pointing to the source but is a copy of it instead
            return hasChanged && target.GetType().IsValueType;
        }

        bool RecursiveDrawValue(string name, ref object value, bool readOnly, int hashcodeSource)
        {
            // Deterministic way to provide a hashcode in a hierarchic/recursive manner
            // The hashcode created here, properly create one specific code for this object at this place in the hierarchy
            // of course hashcodes still aren't unique but this should work well enough for now
            int memberInHierarchyId = (hashcodeSource, name).GetHashCode();
            PushID( memberInHierarchyId );
            
            if( value == null )
            {
                Dummy( new Vector2( DUMMY_WIDTH, 1 ) );
                SameLine();
                TextUnformatted( name );
                SameLine();
                TextUnformatted( "null" );
                return false;
            }
            
            bool recursable = Type.GetTypeCode( value.GetType() ) == TypeCode.Object;
            recursable = recursable && ( GetMembers( value.GetType() ).Length > 0 || ReadableIEnumerable(value) );
            
            bool recurse = recursable && _openedId.Contains( memberInHierarchyId );
            
            // Present button to recurse through value
            if( recursable )
            {
                if( ArrowButton( "", recurse ? ImGuiDir.Down : ImGuiDir.Right ) )
                {
                    if( recurse )
                        _openedId.Remove( memberInHierarchyId );
                    else
                        _openedId.Add( memberInHierarchyId );
                }
            }
            else
                Dummy( new Vector2( DUMMY_WIDTH, 1 ) );
            SameLine();
            
            // Complex object: present button to swap inspect target to this object ?
            bool valueChanged = false;
            if( ValueDrawingHandlers.TryGetValue( value.GetType(), out var handler ) )
            {
                valueChanged = handler( name, ref value );
                goto HANDLED_TEXT;
            }
            else if( Type.GetTypeCode( value.GetType() ) == TypeCode.Object && value.GetType().IsClass )
            {
                if( Button( name ) )
                    Target = value;
                goto HANDLED_TEXT;
            }
            // Basic value type: Present UI handler for values
            else if( readOnly == false )
            {
                switch( value )
                {
                    // if(valueChanged) => to cast / generate garbage only when the value changed
                    case bool v: valueChanged = Checkbox( name, ref v ); if(valueChanged){ value = v; } goto HANDLED_TEXT;
                    case string v: valueChanged = InputText( name, ref v, 99 ); if(valueChanged){ value = v; } goto HANDLED_TEXT;
                    case float v: valueChanged = InputFloat( name, ref v ); if(valueChanged){ value = v; } goto HANDLED_TEXT;
                    case double v: valueChanged = InputDouble( name, ref v ); if(valueChanged){ value = v; } goto HANDLED_TEXT;
                    case int v: valueChanged = InputInt( name, ref v ); if(valueChanged){ value = v; } goto HANDLED_TEXT;
                    // c = closest type that ImGui implements natively, manually cast it to the right type afterward
                    case uint v: { int c = (int)v; valueChanged = InputInt( name, ref c ); if(valueChanged){ value = (uint)c; } goto HANDLED_TEXT; }
                    case long v: { int c = (int)v; valueChanged = InputInt( name, ref c ); if(valueChanged){ value = (long)c; } goto HANDLED_TEXT; }
                    case ulong v: { int c = (int)v; valueChanged = InputInt( name, ref c ); if(valueChanged){ value = (ulong)c; } goto HANDLED_TEXT; }
                    case short v: { int c = (int)v; valueChanged = InputInt( name, ref c ); if(valueChanged){ value = (short)c; } goto HANDLED_TEXT; }
                    case ushort v: { int c = (int)v; valueChanged = InputInt( name, ref c ); if(valueChanged){ value = (ushort)c; } goto HANDLED_TEXT; }
                    case byte v: { int c = (int)v; valueChanged = InputInt( name, ref c ); if(valueChanged){ value = (byte)c; } goto HANDLED_TEXT; }
                    case sbyte v: { int c = (int)v; valueChanged = InputInt( name, ref c ); if(valueChanged){ value = (sbyte)c; } goto HANDLED_TEXT; }
                }
            }
            
            // Otherwise, present basic read-only text
            TextUnformatted( $"{name}: {value}" );
            
            HANDLED_TEXT:
            
            if( recurse ) // Pass in this member's id to properly offset sub-members' hash
                valueChanged = valueChanged || RecursiveDraw( value, memberInHierarchyId );
            
            return valueChanged;
        }

        /// <summary> Lazy init of unmapped MemberInfo </summary>
        MemberInfo[] GetMembers( Type t )
        {
            MemberInfo[] members;
            if( _cachedMembers.TryGetValue( t, out members ) )
                return members;
            
            members = GetAllMembers(t).Where( m => PassesFilter( t, m ) ).ToArray();
            _cachedMembers.Add( t, members );
            return members;
        }

        bool PassesFilter( Type classType, MemberInfo m )
        {
            if( ! ( m is FieldInfo || m is PropertyInfo ) )
                return false;

            Filter memberFilter = 0;

            if( classType != m.DeclaringType )
                memberFilter |= Filter.Inherited;

            if( m is FieldInfo fi )
            {
                if( IsBackingField( fi ) )
                    return false;
                    
                memberFilter |= Filter.Fields;
                if( fi.IsPublic )
                {
                    if( fi.IsStatic )
                        memberFilter |= Filter.PublicStatic;
                    else
                        memberFilter |= Filter.Public;
                }
                else
                {
                    if( fi.IsStatic )
                        memberFilter |= Filter.NonPublicStatic;
                    else
                        memberFilter |= Filter.NonPublic;
                }
            }

            if( m is PropertyInfo pi )
            {
                memberFilter |= Filter.Properties;
                var method = pi.GetMethod;
                if( method == null )
                    return false;
                    
                if( method.IsPublic )
                {
                    if( method.IsStatic )
                        memberFilter |= Filter.PublicStatic;
                    else
                        memberFilter |= Filter.Public;
                }
                else
                {
                    if( method.IsStatic )
                        memberFilter |= Filter.NonPublicStatic;
                    else
                        memberFilter |= Filter.NonPublic;
                }
            }

            return (memberFilter & _filter) == memberFilter;
        }
        
        static bool IsBackingField(FieldInfo fi)
        {
            if( ! fi.IsPrivate )
                return false;

            if( fi.Name[ 0 ] != '<' || ! fi.Name.EndsWith( ">k__BackingField" ) )
                return false;

            return fi.IsDefined(typeof(CompilerGeneratedAttribute), true );
        }

        static bool ReadableIEnumerable( object source )
        {
            if( source is IEnumerable ienum )
            {
                foreach( object o in ienum )
                    return true;
            }

            return false;
        }
        
        
        /// <summary> Reflection doesn't provide private inherited fields for some reason, this resolves that issue </summary>
        static IEnumerable<MemberInfo> GetAllMembers( Type t )
        {
            while( t != null )
            {
                foreach( MemberInfo member in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly) )
                {
                    yield return member;
                }

                t = t.BaseType;
            }
        }
    }
}