using System.Collections;
using AsepriteDotNet.Aseprite;
using AsepriteDotNet.Aseprite.Types;
using Engine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Aseprite;
using MonoGame.Aseprite.Utils;

namespace ElevatorGame.Source.Elevator;

public class Doors
{
    private readonly Elevator _elevator;
    
    private Sprite _elevatorLeftDoorSprite;
    private Sprite _elevatorRightDoorSprite;
    private AsepriteSliceKey _elevatorDoorLeftSlice;
    private AsepriteSliceKey _elevatorDoorRightSlice;
    private Vector2 _doorLeftOrigin;
    private Vector2 _doorRightOrigin;
    
    private float _doorOpenedness;

    public Doors(Elevator elevator, AsepriteFile elevatorInteriorFile)
    {
        _elevator = elevator;
        
        _elevatorDoorLeftSlice = elevatorInteriorFile.GetSlice("DoorL").Keys[0];
        _elevatorDoorRightSlice = elevatorInteriorFile.GetSlice("DoorR").Keys[0];
        
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
    }

    public void Draw(SpriteBatch spriteBatch, int floorTop)
    {
        _elevatorLeftDoorSprite.Draw(spriteBatch, MainGame.Camera.GetParallaxPosition(_doorLeftOrigin + Vector2.UnitX * -_doorOpenedness, Elevator.ParallaxDoors));
        _elevatorRightDoorSprite.Draw(spriteBatch, MainGame.Camera.GetParallaxPosition(_doorRightOrigin + Vector2.UnitX * _doorOpenedness, Elevator.ParallaxDoors));
        
        DrawLight(spriteBatch, floorTop);
    }
    
    private void DrawLight(SpriteBatch spriteBatch, int floorTop)
    {
        int lightTop = floorTop + 40;

        Vector2 barOnePosition = MainGame.Camera.GetParallaxPosition(new(0, lightTop), Elevator.ParallaxDoors);
        Vector2 barTwoPosition = MainGame.Camera.GetParallaxPosition(new(0, lightTop - 140), Elevator.ParallaxDoors);
        Vector2 blackBarPosition = MainGame.Camera.GetParallaxPosition(new(0, lightTop - 40), Elevator.ParallaxDoors);
        spriteBatch.Draw(MainGame.PixelTexture,
            new Rectangle((int)barOnePosition.X, (int)barOnePosition.Y, 240, 100), Color.White * (1 - (_doorOpenedness / 47f)));
        spriteBatch.Draw(MainGame.PixelTexture,
            new Rectangle((int)barTwoPosition.X, (int)barTwoPosition.Y, 240, 100), Color.White * (1 - (_doorOpenedness / 47f)));
        spriteBatch.Draw(MainGame.PixelTexture,
            new Rectangle((int)blackBarPosition.X, (int)blackBarPosition.Y, 240, 40), Color.Black * (1 - (_doorOpenedness / 47f)));
    }

    public void Open()
    {
        MainGame.Coroutines.Stop("elevator_door_close");
        MainGame.Coroutines.TryRun("elevator_door_open", OpenDoors(), 0, out _);
    }

    public void Close()
    {
        MainGame.Coroutines.Stop("elevator_door_open");
        MainGame.Coroutines.TryRun("elevator_door_close", CloseDoors(), 0, out _);
    }
    
    private IEnumerator OpenDoors()
    {
        while(_doorOpenedness < 46)
        {
            _doorOpenedness = MathUtil.ExpDecay(
                _doorOpenedness,
                47f,
                8,
                1/60f
            );
            yield return null;
        }
        _doorOpenedness = 47;
    }

    private IEnumerator CloseDoors()
    {
        while(_doorOpenedness > 1)
        {
            _doorOpenedness = MathUtil.ExpDecay(
                _doorOpenedness,
                0,
                10,
                1/60f
            );
            yield return null;
        }
        _doorOpenedness = 0;
    }
}