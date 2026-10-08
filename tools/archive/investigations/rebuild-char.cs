try
{
    BeMyArms.M7.EditorTools.M7CharacterBodyBuilder.BuildAll();
    return "character build ok";
}
catch (System.Exception e)
{
    UnityEngine.Debug.LogError("[charbuild] " + e);
    return "character build FAILED: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace;
}
