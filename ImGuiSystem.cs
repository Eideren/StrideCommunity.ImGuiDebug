namespace StrideCommunity.ImGuiDebug
{
    using Stride.Core;
    using Stride.Games;
    using Stride.Graphics;
    using Stride.Rendering;
    using Stride.Core.Mathematics;
    using Stride.Core.Annotations;
    using Stride.Input;

    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using ImGuiNET;

    public class ImGuiSystem : GameSystemBase 
    {
        const int INITIAL_VERTEX_BUFFER_SIZE = 128;
        const int INITIAL_INDEX_BUFFER_SIZE = 128;

        // dependencies
        InputManager input;
        GraphicsDevice device;
        GraphicsDeviceManager deviceManager;
        GraphicsContext context;
        EffectSystem effectSystem;
        CommandList commandList;

        // device objects
        PipelineState imPipeline;
        VertexDeclaration imVertLayout;
        VertexBufferBinding vertexBinding;
        IndexBufferBinding indexBinding;
        EffectInstance imShader;
        Texture fontTexture;

        public ImGuiSystem([NotNull] IServiceRegistry registry, [NotNull] GraphicsDeviceManager graphicsDeviceManager, InputManager inputManager = null) : base(registry) 
        {
            input = inputManager ?? Services.GetService<InputManager>();
            Debug.Assert(input != null, "ImGuiSystem: InputManager must be available!");

            deviceManager = graphicsDeviceManager;
            Debug.Assert(deviceManager != null, "ImGuiSystem: GraphicsDeviceManager must be available!");

            device = deviceManager.GraphicsDevice;
            Debug.Assert(device != null, "ImGuiSystem: GraphicsDevice must be available!");

            context = Services.GetService<GraphicsContext>();
            Debug.Assert(context != null, "ImGuiSystem: GraphicsContext must be available!");

            effectSystem = Services.GetService<EffectSystem>();
            Debug.Assert(effectSystem != null, "ImGuiSystem: EffectSystem must be available!");

            IntPtr c = ImGui.CreateContext();
            Debug.Assert(c != IntPtr.Zero, "ImGuiSystem: Failed Creating ImGui Context!");
            ImGui.SetCurrentContext(c);

            // SETTO
            SetupInput();

            // vbos etc
            CreateDeviceObjects();

            // font stuff
            CreateFontTexture();
            
            Enabled = true; // Force Update functions to be run
            Visible = true; // Force Draw related functions to be run
            UpdateOrder = 1; // Update should occur after Stride's InputManager

            // Include this new instance into our services and systems so that stride fires our functions automatically
            Services.AddService(this);
            Game.GameSystems.Add(this);
        }

        void SetupInput() 
        {
            var io = ImGui.GetIO();

            // keyboard nav yes
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;

            var keymap = io.KeyMap;
            keymap[(int)ImGuiKey.Tab] = (int)Keys.Tab;
            keymap[(int)ImGuiKey.LeftArrow] = (int)Keys.Left;
            keymap[(int)ImGuiKey.RightArrow] = (int)Keys.Right;
            keymap[(int)ImGuiKey.UpArrow] = (int)Keys.Up;
            keymap[(int)ImGuiKey.DownArrow] = (int)Keys.Down;
            keymap[(int)ImGuiKey.PageUp] = (int)Keys.PageUp;
            keymap[(int)ImGuiKey.PageDown] = (int)Keys.PageDown;
            keymap[(int)ImGuiKey.Home] = (int)Keys.Home;
            keymap[(int)ImGuiKey.End] = (int)Keys.End;
            keymap[(int)ImGuiKey.Delete] = (int)Keys.Delete;
            keymap[(int)ImGuiKey.Backspace] = (int)Keys.Back;
            keymap[(int)ImGuiKey.Enter] = (int)Keys.Enter;
            keymap[(int)ImGuiKey.Escape] = (int)Keys.Escape;
            keymap[(int)ImGuiKey.Space] = (int)Keys.Space;
            keymap[(int)ImGuiKey.A] = (int)Keys.A;
            keymap[(int)ImGuiKey.C] = (int)Keys.C;
            keymap[(int)ImGuiKey.V] = (int)Keys.V;
            keymap[(int)ImGuiKey.X] = (int)Keys.X;
            keymap[(int)ImGuiKey.Y] = (int)Keys.Y;
            keymap[(int)ImGuiKey.Z] = (int)Keys.Z;

            setClipboardFn = SetClipboard;
            getClipboardFn = GetClipboard;

            io.SetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(setClipboardFn);
            io.GetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(getClipboardFn);
        }

        [FixedAddressValueType]
        static SetClipboardDelegate setClipboardFn;

        [FixedAddressValueType]
        static GetClipboardDelegate getClipboardFn;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void SetClipboardDelegate(IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr GetClipboardDelegate();

        void SetClipboard(IntPtr data) 
        {
        }

        IntPtr GetClipboard() 
        {
            var io = ImGui.GetIO();
            return io.ClipboardUserData;
        }

        void CreateDeviceObjects() 
        {
            // set up a commandlist
            commandList = context.CommandList;

            // compile de shader
            imShader = new EffectInstance(effectSystem.LoadEffect("ImGuiShader").WaitForResult());
            imShader.UpdateEffect(device);

            var layout = new VertexDeclaration(
                VertexElement.Position<Vector2>(),
                VertexElement.TextureCoordinate<Vector2>(),
                VertexElement.Color(PixelFormat.R8G8B8A8_UNorm)
            );

            imVertLayout = layout;

            // de pipeline desc
            var pipeline = new PipelineStateDescription() 
            {
                BlendState = BlendStates.NonPremultiplied,

                RasterizerState = new RasterizerStateDescription() 
                {
                    CullMode = CullMode.None,
                    DepthBias = 0,
                    FillMode = FillMode.Solid,
                    MultisampleAntiAliasLine = false,
                    ScissorTestEnable = true,
                    SlopeScaleDepthBias = 0,
                },

                PrimitiveType = PrimitiveType.TriangleList,
                InputElements = imVertLayout.CreateInputElements(),
                DepthStencilState = DepthStencilStates.Default,

                EffectBytecode = imShader.Effect.Bytecode,
                RootSignature = imShader.RootSignature,

                Output = new RenderOutputDescription(PixelFormat.R8G8B8A8_UNorm)
            };

            // finally set up the pipeline
            var pipelineState = PipelineState.New(device, ref pipeline);
            imPipeline = pipelineState;

            var is32Bits = false;
            var indexBuffer = Stride.Graphics.Buffer.Index.New(device, INITIAL_INDEX_BUFFER_SIZE * sizeof(ushort), GraphicsResourceUsage.Dynamic);
            var indexBufferBinding = new IndexBufferBinding(indexBuffer, is32Bits, 0);
            indexBinding = indexBufferBinding;

            var vertexBuffer = Stride.Graphics.Buffer.Vertex.New(device, INITIAL_VERTEX_BUFFER_SIZE * imVertLayout.CalculateSize(), GraphicsResourceUsage.Dynamic);
            var vertexBufferBinding = new VertexBufferBinding(vertexBuffer, layout, 0);
            vertexBinding = vertexBufferBinding;
        }

        unsafe void CreateFontTexture() 
        {
            // font data, important
            ImGui.GetIO().Fonts.AddFontDefault();

            ImGuiIOPtr io = ImGui.GetIO();
            io.Fonts.GetTexDataAsRGBA32(out byte* pixelData, out int width, out int height, out int bytesPerPixel);

            var newFontTexture = Texture.New2D(device, width, height, PixelFormat.R8G8B8A8_UNorm, TextureFlags.ShaderResource);
            newFontTexture.SetData(commandList, new DataPointer(pixelData, (width * height) * bytesPerPixel));

            fontTexture = newFontTexture;
        }

        public override void Update( GameTime gameTime )
        {
            ImGuiIOPtr io = ImGui.GetIO();

            var surfaceSize = input.Mouse.SurfaceSize;
            io.DisplaySize = new System.Numerics.Vector2(surfaceSize.X, surfaceSize.Y);
            io.DisplayFramebufferScale = new System.Numerics.Vector2(1.0f, 1.0f);
            io.DeltaTime = (float)gameTime.TimePerFrame.TotalSeconds;

            var mousePos = input.AbsoluteMousePosition;
            io.MousePos = new System.Numerics.Vector2(mousePos.X, mousePos.Y);

            if (io.WantTextInput) 
            {
                input.TextInput.EnabledTextInput();
            } 
            else 
            {
                input.TextInput.DisableTextInput();
            }

            // handle input events
            foreach (InputEvent ev in input.Events) 
            {
                switch (ev) 
                {
                    case TextInputEvent tev:
                        if (tev.Text == "\t") continue;
                        io.AddInputCharactersUTF8(tev.Text);
                        break;
                    case KeyEvent kev:
                        var keysDown = io.KeysDown;
                        keysDown[(int)kev.Key] = kev.IsDown;
                        break;
                    case MouseWheelEvent mw:
                        io.MouseWheel += mw.WheelDelta;
                        break;
                }
            }

            var mouseDown = io.MouseDown;
            mouseDown[0] = input.IsMouseButtonDown(MouseButton.Left);
            mouseDown[1] = input.IsMouseButtonDown(MouseButton.Right);
            mouseDown[2] = input.IsMouseButtonDown(MouseButton.Middle);

            io.KeyAlt = input.IsKeyDown(Keys.LeftAlt) || input.IsKeyDown(Keys.LeftAlt);
            io.KeyShift = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);
            io.KeyCtrl = input.IsKeyDown(Keys.LeftCtrl) || input.IsKeyDown(Keys.RightCtrl);
            io.KeySuper = input.IsKeyDown(Keys.LeftWin) || input.IsKeyDown(Keys.RightWin);

            ImGui.NewFrame();
            
        }
        
        public override bool BeginDraw() => true; // Tell stride to execute EndDraw

        public override void EndDraw()
        {
            ImGui.Render();
            RenderDrawLists(ImGui.GetDrawData());
        }

        void CheckBuffers(ImDrawDataPtr drawData) 
        {
            uint totalVBOSize = (uint)(drawData.TotalVtxCount * Unsafe.SizeOf<ImDrawVert>());
            if (totalVBOSize > vertexBinding.Buffer.SizeInBytes) 
            {
                var vertexBuffer = Stride.Graphics.Buffer.Vertex.New(device, (int)(totalVBOSize * 1.5f));
                vertexBinding = new VertexBufferBinding(vertexBuffer, imVertLayout, 0);
            }

            uint totalIBOSize = (uint)(drawData.TotalIdxCount * sizeof(ushort));
            if (totalIBOSize > indexBinding.Buffer.SizeInBytes) 
            {
                var is32Bits = false;
                var indexBuffer = Stride.Graphics.Buffer.Index.New(device, (int)(totalIBOSize * 1.5f));
                indexBinding = new IndexBufferBinding(indexBuffer, is32Bits, 0);
            }
        }

        void UpdateBuffers(ImDrawDataPtr drawData) 
        {
            // copy de dators
            int vtxOffsetBytes = 0;
            int idxOffsetBytes = 0;

            for (int n = 0; n < drawData.CmdListsCount; n++) 
            {
                ImDrawListPtr cmdList = drawData.CmdListsRange[n];
                vertexBinding.Buffer.SetData(commandList, new DataPointer(cmdList.VtxBuffer.Data, cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>()), vtxOffsetBytes);
                indexBinding.Buffer.SetData(commandList, new DataPointer(cmdList.IdxBuffer.Data, cmdList.IdxBuffer.Size * sizeof(ushort)), idxOffsetBytes);
                vtxOffsetBytes += cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>();
                idxOffsetBytes += cmdList.IdxBuffer.Size * sizeof(ushort);
            }
        }

        void RenderDrawLists(ImDrawDataPtr drawData) 
        {
            // view proj
            var surfaceSize = input.Mouse.SurfaceSize;
            var projMatrix = Matrix.OrthoRH(surfaceSize.X, -surfaceSize.Y, -1, 1);

            CheckBuffers(drawData); // potentially resize buffers first if needed
            UpdateBuffers(drawData); // updeet em now

            // set pipeline stuff
            var is32Bits = false;
            commandList.SetPipelineState(imPipeline);
            commandList.SetVertexBuffer(0, vertexBinding.Buffer, 0, Unsafe.SizeOf<ImDrawVert>());
            commandList.SetIndexBuffer(indexBinding.Buffer, 0, is32Bits);
            imShader.Parameters.Set(ImGuiShaderKeys.tex, fontTexture);

            int vtxOffset = 0;
            int idxOffset = 0;
            for (int n = 0; n < drawData.CmdListsCount; n++) 
            {
                ImDrawListPtr cmdList = drawData.CmdListsRange[n];

                for (int i = 0; i < cmdList.CmdBuffer.Size; i++) 
                {
                    ImDrawCmdPtr cmd = cmdList.CmdBuffer[i];

                    if (cmd.TextureId != IntPtr.Zero) 
                    {
                        // imShader.Parameters.Set(ImGuiShaderKeys.tex, fontTexture);
                    }
                    else 
                    {
                        commandList.SetScissorRectangle(
                            new Rectangle(
                                (int)cmd.ClipRect.X,
                                (int)cmd.ClipRect.Y,
                                (int)(cmd.ClipRect.Z - cmd.ClipRect.X),
                                (int)(cmd.ClipRect.W - cmd.ClipRect.Y)
                            )
                        );

                        imShader.Parameters.Set(ImGuiShaderKeys.tex, fontTexture);
                        imShader.Parameters.Set(ImGuiShaderKeys.proj, ref projMatrix);
                        imShader.Apply(context);

                        commandList.DrawIndexed((int)cmd.ElemCount, idxOffset, vtxOffset);
                    }

                    idxOffset += (int)cmd.ElemCount;
                }

                vtxOffset += cmdList.VtxBuffer.Size;
            }
        }
    }
}
