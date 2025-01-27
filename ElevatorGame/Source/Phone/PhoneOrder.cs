using System;
using System.Collections;
using AsepriteDotNet.Aseprite;
using Engine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Aseprite;

namespace ElevatorGame.Source.Phone;

public class PhoneOrder
{
    public Guid CharacterId { get; set; }
    public int FloorNumber { get; set; }
    public int DestinationNumber { get; set; }
    public int Mood { get; set; }
    public Vector2 TargetPosition { get; set; }
    public bool Highlighted { get; set; }
    public Vector2 Position => _position;
    public bool Viewed => _viewed;

    private Vector2 _position;
    
    private AnimatedSprite _digitsSpriteAnim4x5;
    private Sprite _arrowSprite;
    private AnimatedSprite _moodsSpriteAnim;
    private Sprite _starSprite;

    private bool _isDeleting;

    private bool _viewed;

    private Color _mainColor = ColorUtil.CreateFromHex(0x40318d);
    private Color _bgColor = ColorUtil.CreateFromHex(0x67b6bd);
    private Color _currentColor;

    public PhoneOrder(Guid characterId)
    {
        CharacterId = characterId;

        // Digits
        _digitsSpriteAnim4x5 = ContentLoader.Load<AsepriteFile>("graphics/Digits4x5")!
            .CreateSpriteSheet(
                MainGame.Graphics.GraphicsDevice,
                true
            )
            .CreateAnimatedSprite("Tag");
        
        // Arrow
        _arrowSprite = ContentLoader.Load<AsepriteFile>("graphics/phone/Arrow")!
            .CreateSprite(MainGame.Graphics.GraphicsDevice, 0, true);

        // Star
        _starSprite = ContentLoader.Load<AsepriteFile>("graphics/phone/Star")!
            .CreateSprite(MainGame.Graphics.GraphicsDevice, 0, true);

        // Moods
        _moodsSpriteAnim = ContentLoader.Load<AsepriteFile>("graphics/phone/Moods")!
            .CreateSpriteSheet(
                MainGame.Graphics.GraphicsDevice,
                true
            )
            .CreateAnimatedSprite("Tag");

        _currentColor = _mainColor;
    }

    public void Update(GameTime gameTime)
    {
        _position = MathUtil.ExpDecay(_position, TargetPosition, 5f, 1f / 60f);

        _currentColor = _mainColor;
        if (Highlighted)
        {
            _currentColor = MainGame.Step % 20 < 10 ? _mainColor : _bgColor;
        }
    }
    
    public void Draw(SpriteBatch spriteBatch)
    {
        Vector2 orderPos = Vector2.Round(Vector2.One + _position);

        if (_currentColor == _bgColor)
        {
            spriteBatch.Draw(MainGame.PixelTexture, new Rectangle((int)orderPos.X, (int)orderPos.Y, 26, 5), _mainColor);
        }
        
        _digitsSpriteAnim4x5.Color = _currentColor;
        if ((int)MathF.Round(FloorNumber) >= 10)
        {
            _digitsSpriteAnim4x5.SetFrame(FloorNumber / 10 % 10);
            _digitsSpriteAnim4x5.Draw(spriteBatch, orderPos);
        }
        _digitsSpriteAnim4x5.SetFrame(FloorNumber % 10);
        _digitsSpriteAnim4x5.Draw(spriteBatch, orderPos + Vector2.UnitX * 4);

        _arrowSprite.Color = _currentColor;
        _arrowSprite.FlipVertically = DestinationNumber < FloorNumber;
        _arrowSprite.Draw(spriteBatch, orderPos + new Vector2(8, 1));

        if (!Viewed && (MainGame.Step - orderPos.Y * 0.2f) % 30 < 15)
        {
            _starSprite.Color = _currentColor;
            _starSprite.Draw(spriteBatch, orderPos + new Vector2(14, 1));
        }

        if (Mood < 3 || MainGame.Step % 30 < 15)
        {
            _moodsSpriteAnim.Color = _currentColor;
            _moodsSpriteAnim.SetFrame(Mood);
            _moodsSpriteAnim.Draw(spriteBatch, orderPos + Vector2.UnitX * 18);
        }

        if (_isDeleting && MainGame.Step % 20 < 10)
        {
            spriteBatch.Draw(MainGame.PixelTexture, new Rectangle((int)orderPos.X, (int)orderPos.Y + 1, 26, 1), _currentColor);
        }
    }

    public void SnapToTarget()
    {
        _position = TargetPosition;
    }

    public void MarkAsViewed()
    {
        _viewed = true;
    }

    public IEnumerator DeleteSequence()
    {
        _isDeleting = true;
        yield return 90;
    }
}
