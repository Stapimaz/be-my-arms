try
{
    BeMyArms.M7.EditorTools.M7PlayerBodyBuilder.EnsurePrefabs(out UnityEngine.GameObject body, out UnityEngine.GameObject director);
    return "body rebuilt: " + (body != null ? body.name : "null") + " / " + (director != null ? director.name : "null");
}
catch (System.Exception e)
{
    UnityEngine.Debug.LogError("[bodybuild] " + e);
    return "body build FAILED: " + e.Message;
}
