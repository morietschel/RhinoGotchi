namespace RhinoGotchi.Levels
{
    /// <summary>Decides whether a level was solved and scores the attempt.</summary>
    public interface ILevelEvaluator
    {
        LevelResult Evaluate(LevelAttempt attempt);
    }
}
