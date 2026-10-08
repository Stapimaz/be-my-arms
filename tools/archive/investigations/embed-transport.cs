// Embed the currently resolved package before applying the tracked UDP buffer-release fix.
// Unity maintains the package registration and lock file; do not modify Library/PackageCache.
var request = UnityEditor.PackageManager.Client.Embed("com.unity.transport");
UnityEditor.EditorApplication.CallbackFunction poll = null;
poll = () =>
{
    if (!request.IsCompleted) return;
    UnityEditor.EditorApplication.update -= poll;
    if (request.Error != null) UnityEngine.Debug.LogError("[Pass1] Transport embed: " + request.Error.message);
    else UnityEngine.Debug.Log("[Pass1] Transport embedded.");
};
UnityEditor.EditorApplication.update += poll;
return new { request.Status, request.IsCompleted };
