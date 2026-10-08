BeMyArms.Client.EditorTools.GameBuild.BuildWindowsPlayer();
var report=UnityEditor.Build.Reporting.BuildReport.GetLatestReport();
if(report==null || report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
    throw new System.Exception("Windows player build failed; inspect the Unity build report.");
return new {Output=report.summary.outputPath,Result=report.summary.result.ToString(),Errors=report.summary.totalErrors,Warnings=report.summary.totalWarnings,Bytes=report.summary.totalSize};
