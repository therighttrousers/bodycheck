using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck;

/// <summary>
/// Plays an entity's sounds for its <see cref="EntityCue"/>s. It goes in the scene that draws the entity, with an audio
/// player under it for each cue that has a sound, named after the cue, such as "PhaseBegan". A cue with no player
/// named after it plays nothing. It listens to the entity it is under (decision record 021).
/// </summary>
[GlobalClass]
public partial class CueSounds : Node3D
{
    private Entity? _entity;

    public override void _EnterTree()
    {
        for (Node? node = GetParent(); node is not null; node = node.GetParent())
        {
            if (node is Entity entity)
            {
                _entity = entity;
                _entity.CueRaised += Play;
                return;
            }
        }
    }

    public override void _ExitTree()
    {
        if (_entity is not null)
        {
            _entity.CueRaised -= Play;
            _entity = null;
        }
    }

    private void Play(EntityCue cue)
    {
        switch (GetNodeOrNull(cue.ToString()))
        {
            case AudioStreamPlayer player:
                player.Play();
                break;
            case AudioStreamPlayer3D player:
                player.Play();
                break;
        }
    }
}
