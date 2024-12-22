using System;
using System.Diagnostics;
using AsepriteDotNet.Aseprite;
using AsepriteDotNet.Aseprite.Types;
using Engine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Aseprite;
using MonoGame.Aseprite.Utils;

namespace ElevatorGame.Source.Elevator;

public class Elevator
{
    private const int ParallaxDoors = 25;
    private const int ParallaxWalls = 15;
    
    private Sprite _elevatorInteriorSprite;
    private Sprite _elevatorLeftDoorSprite;
    private Sprite _elevatorRightDoorSprite;
    private AnimatedSprite _elevatorNumbersAnimSprite;
    
    private AsepriteSliceKey _elevatorDoorLeftSlice;
    private AsepriteSliceKey _elevatorDoorRightSlice;
    private AsepriteSliceKey _elevatorNumberTensSlice;
    private AsepriteSliceKey _elevatorNumberOnesSlice;

    private Vector2 _doorLeftOrigin;
    private Vector2 _doorRightOrigin;

    private float _floorNumber = 1;
    private int _targetFloorNumber = 1;
    private float _acceleration = 0.0005f;
    private float _velocity;
    private float _lastMaxUnsignedVelocity;
    private float _maxSpeed = 0.16f;
    private float _distToStopTarget;

    private int _turns;
    private int _dir;

    private bool _stopping;
    private float _velocityParallax;

    public void LoadContent()
    {
        // Load the elevator interior sprite
        var elevatorInteriorFile = ContentLoader.Load<AsepriteFile>("graphics/ElevatorInterior");
        _elevatorInteriorSprite = elevatorInteriorFile!.CreateSprite(MainGame.Graphics.GraphicsDevice, 0, true);
        
        // Get the slices
        _elevatorDoorLeftSlice = elevatorInteriorFile.GetSlice("DoorL").Keys[0];
        _elevatorDoorRightSlice = elevatorInteriorFile.GetSlice("DoorR").Keys[0];
        _elevatorNumberTensSlice = elevatorInteriorFile.GetSlice("DigitTens").Keys[0];
        _elevatorNumberOnesSlice = elevatorInteriorFile.GetSlice("DigitOnes").Keys[0];
        
        // Set the target positions for the doors when closed (based on slices)
        var leftDoorSliceBounds = _elevatorDoorLeftSlice.Bounds.ToXnaRectangle();
        var leftDoorTopRight = new Vector2(leftDoorSliceBounds.Right - 1, leftDoorSliceBounds.Y);
        _doorLeftOrigin = leftDoorTopRight;
        var rightDoorSliceBounds = _elevatorDoorRightSlice.Bounds.ToXnaRectangle();
        var rightDoorTopLeft = rightDoorSliceBounds.Location.ToVector2();
        _doorRightOrigin = rightDoorTopLeft;
        
        // Load the door sprites, and set their properties
        var elevatorDoorFile = ContentLoader.Load<AsepriteFile>("graphics/ElevatorDoor");
        _elevatorLeftDoorSprite = elevatorDoorFile!.CreateSprite(MainGame.Graphics.GraphicsDevice, 0, true);
        _elevatorRightDoorSprite = elevatorDoorFile!.CreateSprite(MainGame.Graphics.GraphicsDevice, 0, true);
        _elevatorLeftDoorSprite.Origin = new Vector2(_elevatorLeftDoorSprite.Width - 1, 0);
        _elevatorRightDoorSprite.FlipHorizontally = true;
        
        // Load the animated numbers sprite
        var elevatorNumbersAnimFile = ContentLoader.Load<AsepriteFile>("graphics/ElevatorNumbers");
        var elevatorNumbersSpriteSheet = elevatorNumbersAnimFile!.CreateSpriteSheet(MainGame.Graphics.GraphicsDevice, false);
        _elevatorNumbersAnimSprite = elevatorNumbersSpriteSheet.CreateAnimatedSprite("Tag");
        _elevatorNumbersAnimSprite.Speed = 0;
    }

    public void Update(GameTime gameTime)
    {
        if (_dir == 0 && !_stopping)
        {
            int inputDir = 0;
            if(InputManager.GetPressed(Keys.Up))
                inputDir += 1;
            if(InputManager.GetPressed(Keys.Down))
                inputDir -= 1;
            _dir = inputDir;
        }

        if((_dir == 1 && InputManager.GetReleased(Keys.Up)) || (_dir == -1 && InputManager.GetReleased(Keys.Down)) && !_stopping)
        {
            _stopping = true;

            int lastFloor = _targetFloorNumber;
            _targetFloorNumber = (int)Math.Round(_floorNumber);
            _distToStopTarget = Math.Abs(_targetFloorNumber - _floorNumber);

            if(Math.Abs(_velocity) > _maxSpeed * 0.5f || Math.Sign(_targetFloorNumber - _floorNumber) != _dir)
            {
                _targetFloorNumber += Math.Sign(_velocity);
            }

            if(_targetFloorNumber != lastFloor)
            {
                _turns++;
                Console.WriteLine($"_turns: {_turns}");
            }
        }

        if(!_stopping)
        {
            _velocity = MathUtil.Approach(_velocity, _dir * _maxSpeed, _acceleration);
            _lastMaxUnsignedVelocity = Math.Max(_lastMaxUnsignedVelocity, Math.Abs(_velocity));
        }
        else
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
                _stopping = false;
                _dir = 0;
            }
        }

        _floorNumber += _velocity;

        if(_floorNumber < 1 || _floorNumber > 40)
        {
            _floorNumber = MathHelper.Clamp(_floorNumber, 1, 40);

            if(_velocity != 0)
                MainGame.Camera.SetShake(10 * _velocity, (int)(90 * _velocity));

            _velocity = 0;
            _velocityParallax *= 0.25f;
        }

        float targetParallax = 4 * MathUtil.InverseLerp01(_maxSpeed * 0.5f, _maxSpeed, Math.Abs(_velocity)) * _dir;
        _velocityParallax = MathUtil.ExpDecay(_velocityParallax, targetParallax, 8,
            (float)gameTime.ElapsedGameTime.TotalSeconds);
        
        MainGame.Camera.Position = new(MainGame.Camera.Position.X, _velocityParallax);
    }
    
    public void Draw(SpriteBatch spriteBatch)
    {
        int floorTop = ((int)(_floorNumber * 140) % 140) - 5 + 8;

        DrawLight(spriteBatch, floorTop);

        DrawDoors(spriteBatch);

        _elevatorInteriorSprite.Draw(spriteBatch, MainGame.Camera.GetParallaxPosition(Vector2.Zero, ParallaxWalls));

        DrawNumbers(spriteBatch);
    }

    private void DrawNumbers(SpriteBatch spriteBatch)
    {
        if ((int)MathF.Round(_floorNumber) < 10)
            _elevatorNumbersAnimSprite.SetFrame(10);
        else
            _elevatorNumbersAnimSprite.SetFrame((int)MathF.Round(_floorNumber) / 10 % 10);
        _elevatorNumbersAnimSprite.Draw(spriteBatch,
            MainGame.Camera.GetParallaxPosition(_elevatorNumberTensSlice.GetLocation(), ParallaxWalls));

        if((int)MathF.Round(_floorNumber) == 1)
            _elevatorNumbersAnimSprite.SetFrame(11);
        else
            _elevatorNumbersAnimSprite.SetFrame((int)MathF.Round(_floorNumber) % 10);
        _elevatorNumbersAnimSprite.Draw(spriteBatch,
            MainGame.Camera.GetParallaxPosition(_elevatorNumberOnesSlice.GetLocation(), ParallaxWalls));
    }

    private void DrawDoors(SpriteBatch spriteBatch)
    {
        _elevatorLeftDoorSprite.Draw(spriteBatch, MainGame.Camera.GetParallaxPosition(_doorLeftOrigin, ParallaxDoors));
        _elevatorRightDoorSprite.Draw(spriteBatch, MainGame.Camera.GetParallaxPosition(_doorRightOrigin, ParallaxDoors));
    }

    private static void DrawLight(SpriteBatch spriteBatch, int floorTop)
    {
        int lightTop = floorTop + 40;
        
        Vector2 barOnePosition = MainGame.Camera.GetParallaxPosition(new(0, lightTop), ParallaxDoors);
        Vector2 barTwoPosition = MainGame.Camera.GetParallaxPosition(new(0, lightTop - 140), ParallaxDoors);
        spriteBatch.Draw(MainGame.PixelTexture,
            new Rectangle((int)barOnePosition.X, (int)barOnePosition.Y, 240, 100), Color.White);
        spriteBatch.Draw(MainGame.PixelTexture,
            new Rectangle((int)barTwoPosition.X, (int)barTwoPosition.Y, 240, 100), Color.White);
    }
}
