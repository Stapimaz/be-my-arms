// Runtime visual-QA helper: reproduce the original main-menu layout bug on a live player by
// restoring the non-normalized anchor (0.5, 430) that the old M7MenuController.Stack() produced.
// Purely transient: navigate away and back (or click any menu button) to rebuild the corrected
// layout. Used to capture a "before" frame next to the fixed one.
//
//   unity command eval_file tools/pipeline/qa/break-menu.cs --runtime-path Builds/M7/.qa-client
//   unity command qa_capture_frame --output QA/00_before_menu_bug.png --runtime-path Builds/M7/.qa-client
var menu = UnityEngine.GameObject.Find("M7Menu");
if (menu == null) return "no M7Menu canvas";
var root = menu.transform.Find("Root");
if (root == null) return "no Root panel";
int mutated = 0;
foreach (var n in new[] { "PLAY", "SETTINGS", "QUIT" })
{
    var t = root.Find(n) as UnityEngine.RectTransform;
    if (t == null) continue;
    t.anchorMin = new UnityEngine.Vector2(0.5f, 430f);
    t.anchorMax = new UnityEngine.Vector2(0.5f, 430f);
    t.pivot = new UnityEngine.Vector2(0.5f, 430f);
    mutated++;
}
return "mutated " + mutated;
