// Runtime visual-QA helper: build the normal playable game from the running Editor.
//
//   unity command eval_file tools/pipeline/qa/build-player.cs 3600000 --timeout 3600
//
// The trailing positional number is the eval command's own millisecond wait budget; the CLI's
// --timeout is the HTTP timeout in seconds. Both must exceed the build time. Equivalent to the
// editor menu item "Be My Arms/M7/Build Playable Game".
BeMyArms.M7.EditorTools.M7GameBuild.BuildWindowsPlayer();
return "M7 build complete";
