namespace BodyCheck.Core.Tasks;

/// <summary>
/// What a wait is cancelled with when its tick's scope ends, so that the scheduler can tell what a scope cut short,
/// which it reports, from what its stopping did, which it doesn't.
/// </summary>
internal sealed class ScopeEndedException() : OperationCanceledException("The scope of the tick waited on has ended.");
