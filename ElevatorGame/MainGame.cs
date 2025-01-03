﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using AsepriteDotNet.Aseprite;
using AsepriteDotNet.Processors;
using ElevatorGame.Source;
using ElevatorGame.Source.Characters;
using ElevatorGame.Source.Days;
using ElevatorGame.Source.Pause;
using ElevatorGame.Source.Rooms;
using ElevatorGame.Source.Tickets;
using Engine;
using Engine.Display;
using FMOD;
using FmodForFoxes;
using FmodForFoxes.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Aseprite;
using Elevator = ElevatorGame.Source.Elevator;
using Phone = ElevatorGame.Source.Phone;
using Dialog = ElevatorGame.Source.Dialog;

namespace ElevatorGame;

public class MainGame : Game
{
    public static GraphicsDeviceManager Graphics { get; set; }
    public static SpriteBatch SpriteBatch { get; set; }
    public static readonly Point RenderBufferSize = new Point(240, 135);

    public static long Step { get; private set; }
    public static long Frame { get; private set; }

    public static Texture2D PixelTexture { get; private set; }

    public static Camera Camera { get; } = new()
    {
        RootOffset = Vector2.One * 8
    };

    public static Vector2 CameraPosition { get; set; }
    public static Vector2 CameraPositionTarget { get; set; }

    public static Vector2 ScreenPosition => Vector2.Round(Camera.Position) + Vector2.One * 8;

    public static CoroutineRunner Coroutines { get; set; } = new();

    public static int CurrentDay { get; private set; } = 0;
    public static int FloorCount => DayRegistry.Days[CurrentDay].FloorCount;
    public static int CompletionRequirement => DayRegistry.Days[CurrentDay].CompletionRequirement;
    public static float SpawnChance => DayRegistry.Days[CurrentDay].OrderSpawnChancePerTurn;
    public static string[] CharacterIdsPool => DayRegistry.Days[CurrentDay].CharacterIds;
    public static int StartCharacterCount => DayRegistry.Days[CurrentDay].StartCharacterCount;
    public static int MaxCountPerSpawn => DayRegistry.Days[CurrentDay].MaxCountPerSpawn;
    public static int CurrentFloor { get; set; } = 1;
    public static int CurrentHealth { get; set; } = 8;

    public static float GrayscaleCoeff { get; set; } = 1;
    public static float GrayscaleCoeffTarget { get; set; } = 1;

    public static Rectangle ScreenBounds { get; private set; }
    public static Cursor Cursor { get; private set; }

    public static SpriteFont Font { get; private set; }
    public static SpriteFont FontBold { get; private set; }
    public static SpriteFont FontItalic { get; private set; }

    public enum Menus
    {
        None,
        Phone,
        Dialog,
        Tickets,
        DayTransition,
        TurnTransition,
    }

    public static Menus CurrentMenu { get; set; }

    public bool EndOfDaySequence { get; private set; }

    private static Point _actualWindowSize;
    private static bool _isFullscreen;

    public bool UseSteamworks { get; }

    private Elevator.Elevator _elevator;
    private Phone.Phone _phone;
    private Dialog.Dialog _dialog;

    private TicketManager _ticketManager;

    private List<RoomDef> _roomDefs = [];
    private RoomRenderer _roomRenderer;

    private Sprite _yetiIdle;
    private Sprite _yetiPeace;

    private Sprite _darkOverlaySprite;
    private float _darkOverlayOpacity;

    private AnimatedSprite _buttonHint;
    private float _buttonHintOpacity;
    private bool _buttonHintVisible;

    public static CharacterManager CharacterManager { get; private set; }

    private Effect _elevatorEffects;
    private Effect _postProcessingEffects;
    private EffectParameter _elevatorGameTime;
    private EffectParameter _elevatorGrayscaleIntensity;
    private EffectParameter _ppGameTime;
    private EffectParameter _ppWobbleInfluence;

    public static readonly Rectangle GameBounds = new(8, 8, 240, 135);

    private static readonly PauseManager _pauseManager = new();

    private readonly DayTransition _dayTransition = new DayTransition();
    private float _fadeoutProgress;

    private static Vector2 _lastMouseViewPos;
    private static Vector2 _lastMouseWorldPos;

    public MainGame(bool useSteamworks)
    {
        Graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = false;

        UseSteamworks = useSteamworks;
    }

    protected override void Initialize()
    {
        RenderPipeline.Init(RenderBufferSize);

        Window.AllowUserResizing = true;

        Graphics.PreferredBackBufferWidth = 1920;
        Graphics.PreferredBackBufferHeight = 1080;

        Window.Position = new((GraphicsDevice.DisplayMode.Width - Graphics.PreferredBackBufferWidth) / 2, (GraphicsDevice.DisplayMode.Height - Graphics.PreferredBackBufferHeight) / 2);

        Graphics.ApplyChanges();

        _actualWindowSize = new(
            Graphics.PreferredBackBufferWidth,
            Graphics.PreferredBackBufferHeight
        );

        SaveManager.OnLoad += OnSaveDataLoad;
        SaveManager.OnSave += OnSaveDataSave;

        SaveManager.Load();

        if (UseSteamworks)
        {
            SteamManager.Initialize(3429210u);
        }

        Exiting += Game_Exiting;

        ContentLoader.Initialize(Content);

        FmodController.Init();

        DayRegistry.Init();

        CharacterRegistry.Init();

        base.Initialize();
    }

    private void OnSaveDataSave(ref SaveData data)
    {
        data.Day = CurrentDay;
        data.Rooms = [.. _roomDefs];

        data.LanguagePreference = LocalizationManager.CurrentLanguage ?? "en-us";
    }

    private void OnSaveDataLoad(ref SaveData data)
    {
        CurrentDay = data.Day;
        _roomDefs = [.. data.Rooms];

        LocalizationManager.CurrentLanguage = data.LanguagePreference;

        DayRegistry.Init();

        CharacterRegistry.Init();

        Coroutines.TryRun("load_day_start", StartDay(data.Day), out _);
    }

    protected override void LoadContent()
    {
        SpriteBatch = new SpriteBatch(GraphicsDevice);

        // NOTE: You HAVE TO init fmod in the Initialize().
        // Otherwise, it may not work on some platforms.
        FmodController.LoadContent("audio/banks/Desktop", true, ["Master", "SFX", "Music"], ["Master"]);

        RenderPipeline.LoadContent(GraphicsDevice);

        PixelTexture = new(GraphicsDevice, 1, 1);
        PixelTexture.SetData([Color.White]);

        _elevator = new(OnChangeFloorNumber, EndOfTurnSequence);
        _elevator.LoadContent();

        _phone = new(_elevator);
        _phone.LoadContent();

        _ticketManager = new TicketManager(_elevator);
        _ticketManager.LoadContent();

        _dialog = new();
        _dialog.LoadContent();

        CharacterManager = new CharacterManager(_phone, _ticketManager, _dialog);
        // CharacterManager.Init();
        CharacterManager.LoadContent();

        var yetiSpriteFile = ContentLoader.Load<AsepriteFile>("graphics/characters/Yeti")!;
        _yetiIdle = yetiSpriteFile.CreateSprite(GraphicsDevice, 0, true);
        _yetiPeace = yetiSpriteFile.CreateSprite(GraphicsDevice, 1, true);

        var darkOverlayFile = ContentLoader.Load<AsepriteFile>("graphics/ElevatorDarkOverlay");
        _darkOverlaySprite = darkOverlayFile!.CreateSprite(GraphicsDevice, 0, true);
        _darkOverlaySprite.OriginX = 64;

        _buttonHint = ContentLoader.Load<AsepriteFile>("graphics/ButtonHint")
            .CreateSpriteSheet(GraphicsDevice, false)
            .CreateAnimatedSprite("Tag");
        _buttonHint.Origin = new(19, 5);

        Cursor = new();
        Cursor.LoadContent();

        _elevatorEffects =
            Content.Load<Effect>("shaders/elevatoreffects")!;
        _elevatorGrayscaleIntensity = _elevatorEffects.Parameters["GrayscaleIntensity"];
        _elevatorGameTime = _elevatorEffects.Parameters["GameTime"];

        _postProcessingEffects =
            Content.Load<Effect>("shaders/postprocessing")!;
        _ppWobbleInfluence = _postProcessingEffects.Parameters["WobbleInfluence"];
        _ppGameTime = _postProcessingEffects.Parameters["GameTime"];

        _dayTransition.LoadContent();

        _pauseManager.ExitGame = Exit;
        _pauseManager.LoadContent();

        Font = ContentLoader.Load<SpriteFont>("fonts/default");
        FontBold = ContentLoader.Load<SpriteFont>("fonts/defaultBold");
        FontItalic = ContentLoader.Load<SpriteFont>("fonts/defaultItalic");

        if (SaveManager.SaveData.Rooms.Count == 0)
        {
            for (int i = 0; i < 99; i++)
            {
                var newRoomDef = RoomDef.MakeRandom("graphics/RoomsGeneric");
                _roomDefs.Add(newRoomDef);
            }
        }
        _roomRenderer = new RoomRenderer();
        _roomRenderer.LoadContent();
        _roomRenderer.SetDefinition(_roomDefs[0]);
    }

    protected override void UnloadContent()
    {
        FmodManager.Unload();

        _elevator.UnloadContent();
        _phone.UnloadContent();
    }

    private void Game_Exiting(object sender, ExitingEventArgs e)
    {
        SaveManager.Save();
        SteamManager.Cleanup();
    }

    protected override void Update(GameTime gameTime)
    {
        // Static Properties
        ScreenBounds = GraphicsDevice.PresentationParameters.Bounds;

        // Update Systems
        FmodManager.Update();

        // Update Input
        UpdateInput(gameTime);

        if (Keybindings.Pause.Pressed)
        {
            _pauseManager.Pause();
        }
        _pauseManager.Update(gameTime);

        HandleToggleFullscreen();

        DebugUpdate();

        SteamManager.Update();

        if (_pauseManager.IsPaused)
        {
            base.Update(gameTime);
            return;
        }

        Coroutines.Update();

        // Tilt camera towards cursor (should be an option to disable)
        Camera.Position =
            CameraPosition + Cursor.TiltOffset;

        _elevator.Update(gameTime);
        _ticketManager.Update(gameTime);
        _phone.Update(gameTime);

        CharacterManager.Update(gameTime);

        _dayTransition.Update(gameTime);

        CameraPosition = MathUtil.ExpDecay(CameraPosition, CameraPositionTarget, 8f, 1f / 60f);
        Camera.Update();

        float darkOverlayOpacityTarget = 0;
        if(EndOfDaySequence)
        {
            darkOverlayOpacityTarget = 1f;
        }
        else if ((float)(CharacterManager.CharactersFinished + CharacterManager.CharactersInPlay.Count) / CompletionRequirement >= 0.8f)
        {
            darkOverlayOpacityTarget = 0.5f;
        }
        _darkOverlayOpacity = MathUtil.ExpDecay(_darkOverlayOpacity, darkOverlayOpacityTarget, 2f, 1f/60f);
        _darkOverlaySprite.Color = Color.White * _darkOverlayOpacity;

        if (CurrentDay == 0 && CurrentFloor == 1 && _buttonHintVisible)
        {
            _buttonHintOpacity = MathUtil.Approach(_buttonHintOpacity, 1, 0.1f);
        }
        else
        {
            _buttonHintOpacity = MathUtil.Approach(_buttonHintOpacity, 0, 0.1f);
            if (CurrentFloor != 1)
                _buttonHintVisible = false;
        }
        _buttonHint.Color = Color.White * _buttonHintOpacity;
        _buttonHint.Update(1f / 60f);

        base.Update(gameTime);

        Step++;
    }

    protected override void Draw(GameTime gameTime)
    {
        UpdateShaderProperties();

        _roomRenderer.PreRender(SpriteBatch);
        _phone.PreRenderScreen(SpriteBatch);
        _dayTransition.PreDraw(SpriteBatch);
        _pauseManager.PreDraw(SpriteBatch);

        RenderPipeline.DrawBeforeUI(SpriteBatch, GraphicsDevice, _elevatorEffects, () =>
        {
            GraphicsDevice.Clear(new Color(new Vector3(120, 105, 196)));
            SpriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Camera.Transform);
            {
                DrawScene(SpriteBatch);
            }
            SpriteBatch.End();
        });

        RenderPipeline.DrawUI(SpriteBatch, GraphicsDevice, () =>
        {
            GraphicsDevice.Clear(Color.Transparent);
            SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
            {
                DrawUI(SpriteBatch);
            }
            SpriteBatch.End();
        });

        RenderPipeline.DrawPostProcess(SpriteBatch, GraphicsDevice, _postProcessingEffects);
        RenderPipeline.DrawFinish(SpriteBatch, Graphics);

        base.Draw(gameTime);

        Frame++;
    }

    private void DrawScene(SpriteBatch spriteBatch)
    {
        _roomRenderer.Draw(spriteBatch);
        // _yetiIdle.Draw(spriteBatch, Camera.GetParallaxPosition(new(80, 40), 50));

        CharacterManager.DrawWaiting(spriteBatch);

        _elevator.Draw(spriteBatch);

        CharacterManager.DrawMain(spriteBatch);

        _darkOverlaySprite.Draw(spriteBatch, Camera.GetParallaxPosition(Vector2.Zero, 0));
    }

    private void DrawUI(SpriteBatch spriteBatch)
    {
        _phone.Draw(spriteBatch);

        _ticketManager.Draw(spriteBatch);

        _dialog.Draw(spriteBatch);

        _buttonHint.Draw(spriteBatch, new(GameBounds.Width / 2, GameBounds.Height - 20 - (_buttonHintOpacity * 4)));

        _pauseManager.Draw(spriteBatch);

        DrawScreenTransition(spriteBatch);

        Cursor.Draw(spriteBatch);
    }

    private void DrawScreenTransition(SpriteBatch spriteBatch)
    {
        // left
        spriteBatch.Draw(
            PixelTexture,
            GameBounds with {
                X = 0,
                Y = 0,
                Width = MathUtil.CeilToInt(_fadeoutProgress * GameBounds.Width / 2)
            },
            Color.Black
        );

        // right
        spriteBatch.Draw(
            PixelTexture,
            GameBounds with {
                X = GameBounds.Width - MathUtil.CeilToInt(_fadeoutProgress * GameBounds.Width / 2),
                Y = 0,
                Width = MathUtil.CeilToInt(_fadeoutProgress * GameBounds.Width / 2)
            },
            Color.Black
        );

        _dayTransition.Draw(spriteBatch);
    }

    private void OnChangeFloorNumber(int floorNumber)
    {
        CurrentFloor = floorNumber;
        _roomRenderer.SetDefinition(_roomDefs[floorNumber - 1]);
    }

    private IEnumerator EndOfTurnSequence()
    {
        // If anyone is going to this floor, they leave one at a time
        // Subtract patience from remaining passengers
        // Any passengers with patience <= 0 leave
        // Any passengers getting on this floor get on

        var waitingDir = CharacterManager.WaitingDirectionOnFloor(CurrentFloor);
        if (waitingDir != 0) _elevator.SetComboDirection(waitingDir);
        CurrentMenu = Menus.TurnTransition;
        Cursor.CursorSprite = Cursor.CursorSprites.Wait;

        yield return CharacterManager.EndOfTurnSequence();

        _phone.CanOpen = true;
        Coroutines.Stop("phone_show");
        Coroutines.TryRun("phone_hide", _phone.Close(false), out _);

        yield return null;

        if (CharacterManager.CharactersFinished >= DayRegistry.Days[CurrentDay].CompletionRequirement)
        {
            // Advance to the next day
            // Coroutines.TryRun("main_day_advance", AdvanceDay(), out _);

            if(!EndOfDaySequence)
            {
                EndOfDaySequence = true;

                _phone.PlayJingle();

                if (CurrentDay == 0)
                {
                    yield return _phone.Open(false, false);
                    _phone.StartTalking();

                    yield return _dialog.Display(
                        new Dialog.DialogDef(
                            LocalizationManager.Get("dialog.phone.tutorial.end_of_day")
                        ).Pages,
                        Dialog.Dialog.DisplayMethod.Human
                    );

                    _phone.StopTalking();
                    yield return _phone.Close(false, true);
                }
            }

            if (CurrentFloor == 1)
            {
                Coroutines.TryRun("main_day_advance", AdvanceDay(), out _);
            }
        }
        CurrentMenu = Menus.None;
        Cursor.CursorSprite = Cursor.CursorSprites.Default;

        if (CharacterManager.CharactersInPlay.Count == 0)
            CharacterManager.SpawnMultipleRandomCharacters(MaxCountPerSpawn);
    }

    public void SimulateBatteryChange(int newValue)
    {
        _phone.SimulateBatteryChange(newValue);
    }

    public static void ChangeHealth(int change)
    {
        CurrentHealth = Math.Clamp(CurrentHealth + change, 0, 8);
    }

    private IEnumerator AdvanceDay()
    {
        yield return SetDay(CurrentDay + 1);
    }

    private IEnumerator SetDay(int day)
    {
        CurrentMenu = Menus.DayTransition;

        yield return FadeToBlack();

        EndOfDaySequence = false;
        _darkOverlayOpacity = 0;

        yield return 60;

        yield return _dayTransition.TransitionToNextDay(day + 1);

        if (day >= DayRegistry.Days.Length || day < 0)
        {
            // End of the game
            yield return FadeFromBlack();
            yield break;
        }

        // the rest of the cleanup process
        CurrentDay = day;

        SaveManager.Save();

        _elevator.Dispose();
        _phone.Dispose();
        // _dialog.UnloadContent();
        // _ticketManager.UnloadContent();
        // CharacterManager.UnloadContent();

        _dialog = new();
        _elevator = new(OnChangeFloorNumber, EndOfTurnSequence);
        _phone = new(_elevator);
        _ticketManager = new(_elevator);
        CharacterManager = new(_phone, _ticketManager, _dialog);

        CurrentFloor = 1;
        _dialog.LoadContent();
        _elevator.LoadContent();
        _phone.LoadContent();
        _ticketManager.LoadContent();
        CharacterManager.LoadContent();

        CameraPosition = Vector2.Zero;
        CameraPositionTarget = Vector2.Zero;
        Camera.Position = Vector2.Zero;
        ResetShaderProperties();

        _roomRenderer.SetDefinition(_roomDefs[0]);
        _roomRenderer.PreRender(SpriteBatch);

        GC.Collect();

        yield return FadeFromBlack();
        CurrentMenu = Menus.None;
        Coroutines.StopAll();

        yield return StartDay(day);
    }

    private IEnumerator StartDay(int dayIndex)
    {
        bool displayDialog = DayRegistry.Days[dayIndex].StartDialog.Pages.Length != 0;
        if (displayDialog)
        {
            yield return _phone.Open(false, false);
            _phone.StartTalking();
            yield return _dialog.Display(DayRegistry.Days[dayIndex].StartDialog.Pages, Dialog.Dialog.DisplayMethod.Human);
            _phone.StopTalking();
        }

        if (dayIndex == 0)
        {
            _buttonHint.Play();
            _buttonHintVisible = true;
        }

        if (displayDialog)
            yield return _phone.Close(false, true);

        // calling Init here prevents characters from spawning during start of day dialog
        CharacterManager.Init();
    }

    private IEnumerator FadeToBlack()
    {
        _fadeoutProgress = 0;
        while(!MathUtil.Approximately(_fadeoutProgress, 1, 0.01f))
        {
            _fadeoutProgress = MathUtil.ExpDecay(_fadeoutProgress, 1, 8, 1f/60f);
            yield return null;
        }
        _fadeoutProgress = 1;
    }

    private IEnumerator FadeFromBlack()
    {
        _fadeoutProgress = 1;
        while(!MathUtil.Approximately(_fadeoutProgress, 0, 0.01f))
        {
            _fadeoutProgress = MathUtil.ExpDecay(_fadeoutProgress, 0, 5, 1f/60f);
            yield return null;
        }
        _fadeoutProgress = 0;
    }

    public void UpdateInput(GameTime gameTime)
    {
        InputManager.InputDisabled = !IsActive;
        InputManager.RefreshKeyboardState();
        InputManager.RefreshMouseState();
        InputManager.RefreshGamePadState();
        InputManager.UpdateTypingInput(gameTime);
        Cursor.Update();
        _lastMouseViewPos = Cursor.ViewPosition;
        _lastMouseWorldPos = Cursor.WorldPosition;
    }

    public static Vector2 GetCursorParallaxValue(Vector2 position, float distance)
    {
        Vector2 checkPos = (
            Vector2.Clamp(
                _pauseManager.IsPaused
                    ? _lastMouseViewPos
                    : Cursor.ViewPosition,
                Vector2.Zero,
                new(240, 135)
            ) - new Vector2(240, 135) / 2f
        ) * (8 / 120f);
        return position + Vector2.Round(checkPos * MathUtil.InverseLerp01(0, 100, distance));
    }

    private void ResetShaderProperties()
    {
        GrayscaleCoeffTarget = 1;
        GrayscaleCoeff = 1;
    }

    private void UpdateShaderProperties()
    {
        GrayscaleCoeff = MathUtil.ExpDecay(GrayscaleCoeff, GrayscaleCoeffTarget, 8, 1f / 60f);
        _elevatorGrayscaleIntensity.SetValue(GrayscaleCoeff);
        _ppWobbleInfluence.SetValue(0);
        _ppGameTime.SetValue(Frame / 60f);
    }

    private void HandleToggleFullscreen()
    {
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
    }

    [Conditional("DEBUG")]
    private void DebugUpdate()
    {
        if (InputManager.GetPressed(Keys.Y))
        {
            Coroutines.Stop("main_day_advance");
            Coroutines.TryRun("main_day_advance", AdvanceDay(), out _);
        }

        if (InputManager.GetPressed(Keys.T))
        {
            CharacterManager.ForceCompleteDay();
        }
    }
}
