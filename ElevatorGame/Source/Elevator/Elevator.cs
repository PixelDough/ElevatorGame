using System;
using System.Collections;
using System.Diagnostics;
using AsepriteDotNet.Aseprite;
using AsepriteDotNet.Aseprite.Types;
using Engine;
using FmodForFoxes.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Aseprite;
using MonoGame.Aseprite.Utils;

namespace ElevatorGame.Source.Elevator;

public class Elevator(Action<int> onChangeFloorNumber, Func<IEnumerator> endOfTurnSequence)
{
    public const int ParallaxDoors = 35;
    public const int ParallaxWalls = 25;
    public static int MaxFloors = 40;

    public enum ElevatorStates
    {
        Stopped,
        Closing,
        Moving,
        Stopping,
        Opening,
        Waiting,
        Other,
    }
    public ElevatorStates State { get; private set; } = ElevatorStates.Stopped;
    public void SetState(ElevatorStates state) => State = state;

    private Sprite _elevatorInteriorSprite;

    private float _floorNumber = 1;
    private int _targetFloorNumber = 1;
    private float _acceleration = 0.0005f;
    private float _velocity;
    private float _lastMaxUnsignedVelocity;
    private float _maxSpeed = 0.16f;

    private int _turns;
    private int _comboDirection = 1;
    private int _dir;

    private bool _stopping;
    private float _velocityParallax;

    public bool CanMove { get; set; } = true;

    private Doors _doors;
    private FloorNumberDisplay _floorNumbers;

    // FMOD
    private EventInstance _audioElevatorMove;
    private EventDescription _audioWhooshEventDescription;
    private EventInstance _audioBellUpEvent;
    private EventInstance _audioBellDownEvent;

    public void LoadContent()
    {
        // Load the elevator interior sprite
        var elevatorInteriorFile = ContentLoader.Load<AsepriteFile>("graphics/ElevatorInterior");
        _elevatorInteriorSprite = elevatorInteriorFile!.CreateSprite(MainGame.Graphics.GraphicsDevice, 0, true);

        // Initialize the doors
        _doors = new Doors(this, elevatorInteriorFile);

        _floorNumbers = new FloorNumberDisplay();
        _floorNumbers.LoadContent(this, elevatorInteriorFile);

        _audioElevatorMove = StudioSystem.GetEvent("event:/SFX/Elevator/Move").CreateInstance();
        _audioElevatorMove.Start();
        _audioWhooshEventDescription = StudioSystem.GetEvent("event:/SFX/Elevator/Whoosh");
        
        _audioBellUpEvent = StudioSystem.GetEvent("event:/SFX/Elevator/Bell/Up").CreateInstance();
        _audioBellDownEvent = StudioSystem.GetEvent("event:/SFX/Elevator/Bell/Down").CreateInstance();
    }

    public void UnloadContent()
    {
        _audioElevatorMove.Dispose();
        _doors.UnloadContent();
        _audioBellUpEvent.Dispose();
        _audioBellDownEvent.Dispose();
    }

    public void Update(GameTime gameTime)
    {
        switch (State)
        {
            case ElevatorStates.Stopped:
                UpdateStateStopped(gameTime);
                break;
            case ElevatorStates.Closing:
                // UpdateStateClosing(gameTime);
                break;
            case ElevatorStates.Moving:
                UpdateStateMoving(gameTime);
                break;
            case ElevatorStates.Stopping:
                UpdateStateStopping(gameTime);
                break;
            case ElevatorStates.Opening:
                // UpdateStateOpening(gameTime);
                break;
            case ElevatorStates.Waiting:
                UpdateStateWaiting(gameTime);
                break;
            case ElevatorStates.Other:
                break;
        }

        _audioElevatorMove.SetParameterValue("Velocity", Math.Abs(_velocity) / _maxSpeed);

        float targetParallax = 4 * MathUtil.InverseLerp01(_maxSpeed * 0.6f, _maxSpeed, Math.Abs(_velocity)) * _dir;
        _velocityParallax = MathUtil.ExpDecay(_velocityParallax, targetParallax, 8,
            (float)gameTime.ElapsedGameTime.TotalSeconds);

        MainGame.CameraPosition = new(MainGame.CameraPosition.X, _velocityParallax);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        int floorTop = ((int)(_floorNumber * 140) % 140) - 5 + 8;

        _doors.Draw(spriteBatch, floorTop);

        Vector2 fillPos = MainGame.Camera.GetParallaxPosition(new Vector2(240 + 16, -8), ParallaxWalls);
        spriteBatch.Draw(MainGame.PixelTexture, new Rectangle((int)fillPos.X, (int)fillPos.Y, 100, 135 + 16),
            new Color(120, 105, 196, 255));
        _elevatorInteriorSprite.Draw(spriteBatch, MainGame.Camera.GetParallaxPosition(Vector2.Zero, ParallaxWalls));

        _floorNumbers.Draw(spriteBatch, _floorNumber, _comboDirection);
    }

    private void UpdateStateStopped(GameTime gameTime)
    {
        int inputDir = 0;
        if(InputManager.GetPressed(Keys.Up))
            inputDir += 1;
        if(InputManager.GetPressed(Keys.Down))
            inputDir -= 1;

        if (inputDir > 0 && (int)Math.Round(_floorNumber) >= MaxFloors || inputDir < 0 && (int)Math.Round(_floorNumber) <= 1)
        {
            MainGame.Camera.SetShake(2, 15);
            return;
        }

        _dir = inputDir;
        _comboDirection = _dir;

        if (_dir == 0) return;
        _doors.Close();
        State = ElevatorStates.Closing;
    }

    private void UpdateStateMoving(GameTime gameTime)
    {
        int lastFloor = MathUtil.RoundToInt(_floorNumber);
        _floorNumber += _velocity;
        if (MathUtil.RoundToInt(_floorNumber) != lastFloor)
        {
            PlayWhoosh();
        }

        bool didSoftCrash = false;
        if(_floorNumber < 1 || _floorNumber > MaxFloors)
        {
            _floorNumber = MathHelper.Clamp(_floorNumber, 1, MaxFloors);

            _velocityParallax *= 0.25f;
            _dir = 0;

            _targetFloorNumber = (int)_floorNumber;

            if (Math.Abs(_velocity) < _maxSpeed * 0.5)
            {
                didSoftCrash = true;
            }
            else
            {
                MainGame.Camera.SetShake(4, 60);
                _velocity = 0;

                SetState(ElevatorStates.Other);
                MainGame.Coroutines.TryRun("elevator_crash", CrashSequence(), 0, out _);
                return;
            }
            _velocity = 0;
        }

        if((_dir == 1 && !InputManager.GetDown(Keys.Up)) || (_dir == -1 && !InputManager.GetDown(Keys.Down)) || didSoftCrash)
        {
            _targetFloorNumber = (int)Math.Round(_floorNumber);

            if (Math.Abs(_velocity) < _acceleration)
            {
                _audioElevatorMove.SetParameterValue("Velocity", 0.9f);
            }

            if(Math.Abs(_velocity) > _maxSpeed * 0.5f || Math.Sign(_targetFloorNumber - _floorNumber) != _dir)
            {
                int rolloverAmount = Math.Sign(_velocity);
                if (rolloverAmount == 0)
                    rolloverAmount = _dir;
                _targetFloorNumber += rolloverAmount;
            }

            _targetFloorNumber = MathHelper.Clamp(_targetFloorNumber, 1, MaxFloors);

            State = ElevatorStates.Stopping;
            return;
        }

        _velocity = MathUtil.Approach(_velocity, _dir * _maxSpeed, _acceleration);
        _lastMaxUnsignedVelocity = Math.Max(_lastMaxUnsignedVelocity, Math.Abs(_velocity));
    }

    private void UpdateStateStopping(GameTime gameTime)
    {
        _velocity = 0;
        _floorNumber = MathUtil.ExpDecay(
            _floorNumber,
            _targetFloorNumber,
            8,
            (float)gameTime.ElapsedGameTime.TotalSeconds
        );
        if(Math.Abs(_targetFloorNumber - _floorNumber) < 1/140f)
        {
            _floorNumber = _targetFloorNumber;
            _dir = 0;

            MainGame.Coroutines.TryRun("elevator_open", OpenSequence(), 0, out _);
        }
    }

    private IEnumerator OpenSequence()
    {
        PlayBell(_comboDirection);
        onChangeFloorNumber?.Invoke(_targetFloorNumber);
        State = ElevatorStates.Opening;
        var openHandle = _doors.Open();
        yield return openHandle.Wait();
        State = ElevatorStates.Waiting;
        yield return endOfTurnSequence();
        State = ElevatorStates.Stopped;
    }

    private void UpdateStateWaiting(GameTime gameTime)
    {

    }

    private IEnumerator CrashSequence()
    {
        yield return 60;
        SetState(Elevator.ElevatorStates.Stopping);
    }

    private void PlayWhoosh()
    {
        var whooshInstance = _audioWhooshEventDescription.CreateInstance();
        whooshInstance.Start();
        whooshInstance.SetParameterValue("Velocity", Math.Abs(_velocity) / _maxSpeed);
        whooshInstance.Dispose();
    }
    
    private void PlayBell(int direction)
    {
        if (direction == 1)
            _audioBellUpEvent.Start();
        else
            _audioBellDownEvent.Start();
    }
}
