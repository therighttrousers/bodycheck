using Godot;

namespace BodyCheck.Decoration;

/// <summary>
/// The decorators an environment adds to each of its rooms, as a node named <c>Decorations</c> in the environment's
/// scene. It only holds them: nothing runs while it is in the tree.
/// </summary>
[GlobalClass]
public partial class RoomDecorations : Node
{
    [Export] public Godot.Collections.Array<RoomDecorator> Decorators { get; set; } = [];
}
