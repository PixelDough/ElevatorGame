﻿using System;
using System.Runtime.InteropServices;
using AsepriteDotNet.Aseprite;
using AsepriteDotNet.Processors;
using ElevatorGame.Source.Elevator;
using Engine;
using Engine.Display;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Aseprite;

namespace ElevatorGame;

public class MainGame : Game
{
    public static GraphicsDeviceManager Graphics { get; set; }
    public static SpriteBatch SpriteBatch { get; set; }
    
    public static long Step { get; private set; }
    public static long Frame { get; private set; }

    public static Texture2D PixelTexture { get; private set; }

    public static Camera Camera { get; } = new()
    {
        RootOffset = Vector2.One * 8
    };

    private static Point _actualWindowSize;
    private static bool _isFullscreen;

    private RenderTarget2D _renderTarget;

    private Elevator _elevator;

    public MainGame()
    {
        Graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        // TODO: Add your initialization logic here

        Window.AllowUserResizing = true;

        Graphics.PreferredBackBufferWidth = 1920;
        Graphics.PreferredBackBufferHeight = 1080;

        Window.Position = new((GraphicsDevice.DisplayMode.Width - Graphics.PreferredBackBufferWidth) / 2, (GraphicsDevice.DisplayMode.Height - Graphics.PreferredBackBufferHeight) / 2);

        Graphics.ApplyChanges();

        _actualWindowSize = new(
            Graphics.PreferredBackBufferWidth,
            Graphics.PreferredBackBufferHeight
        );

        ContentLoader.Initialize(Content);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        SpriteBatch = new SpriteBatch(GraphicsDevice);

        PixelTexture = new(GraphicsDevice, 1, 1);
        PixelTexture.SetData([Color.White]);

        _renderTarget = new RenderTarget2D(GraphicsDevice, 240, 135);
        
        _elevator = new Elevator();
        _elevator.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        InputManager.InputDisabled = !IsActive;

        InputManager.RefreshKeyboardState();
        InputManager.RefreshMouseState();
        InputManager.RefreshGamePadState();

        InputManager.UpdateTypingInput(gameTime);

        if(InputManager.GetPressed(Buttons.Start) || InputManager.GetPressed(Keys.Escape))
            Exit();

        if(InputManager.GetPressed(Keys.F11) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if(_isFullscreen)
            {
                Graphics.PreferredBackBufferWidth = _actualWindowSize.X;
                Graphics.PreferredBackBufferHeight = _actualWindowSize.Y;
                Window.Position = new((GraphicsDevice.DisplayMode.Width - Graphics.PreferredBackBufferWidth) / 2, (GraphicsDevice.DisplayMode.Height - Graphics.PreferredBackBufferHeight) / 2);
                Window.IsBorderless = false;
                Graphics.ApplyChanges();
            }
            else
            {
                _actualWindowSize.X = Graphics.PreferredBackBufferWidth;
                _actualWindowSize.Y = Graphics.PreferredBackBufferHeight;

                Graphics.PreferredBackBufferWidth = GraphicsDevice.DisplayMode.Width;
                Graphics.PreferredBackBufferHeight = GraphicsDevice.DisplayMode.Height;
                Window.IsBorderless = true;
                Graphics.ApplyChanges();
            }

            _isFullscreen = !_isFullscreen;
        }

        Camera.Update();

        _elevator.Update(gameTime);

        // TODO: Add your update logic here

        base.Update(gameTime);
        
        Step++;
    }

    protected override void Draw(GameTime gameTime)
    {

        RtScreen.DrawWithRtOnScreen(_renderTarget, Graphics, SpriteBatch, () =>
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            SpriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Camera.Transform);
            {
                _elevator.Draw(SpriteBatch);
            }
            SpriteBatch.End();
        });


        base.Draw(gameTime);
        
        Frame++;
    }
}
