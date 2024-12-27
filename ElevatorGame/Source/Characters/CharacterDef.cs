using ElevatorGame.Source.Dialog;
using Microsoft.Xna.Framework;

namespace ElevatorGame.Source.Characters;

public struct CharacterDef()
{
    public string Name { get; set; }
    public string SpritePath { get; set; }
    public int WalkSpeed { get; set; } = 8;
    public DialogDef[] EnterPhrases { get; set; } = [];
    public DialogDef[] ExitPhrases { get; set; } = [];
    public DialogDef[] AngryPhrases { get; set; } = [];
    public Vector2 AngryIconPosition { get; set; }
}
