var results=new System.Collections.Generic.List<object>();
foreach(float yawWeight in new[]{.72f,.82f,.92f,1f}) foreach(float weight in new[]{.45f,.55f,.65f})
{
    float max=0;
    foreach(bool crouch in new[]{false,true}) foreach(float yaw in new[]{-85f,0f,85f}) foreach(float pitch in new[]{-80f,0f,80f})
    {
        var go=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(BeMyArms.M7.EditorTools.M7PlayerBodyBuilder.BodyPrefabPath));
        var driver=go.GetComponent<BeMyArms.M7.M7CharacterAnimator>();
        BeMyArms.M7.EditorTools.M7FighterBuilder.Clip("P2",crouch ? "Crouch_Idle_Loop" : "Idle_Loop").SampleAnimation(driver.P2Animator.gameObject,0);
        if(crouch) driver.AimPivot.localPosition+=UnityEngine.Vector3.down*driver.AnimatedChestDrop();
        driver.AimPivot.localRotation=UnityEngine.Quaternion.Euler(pitch,yaw,0);
        driver.SetUpperBodyAim(yaw*yawWeight/.72f,pitch*weight/.45f);
        var pose=go.GetComponent<BeMyArms.M7.M7RiflePose>(); pose.Pose(false,0);
        max=UnityEngine.Mathf.Max(max,pose.LeftGripError,pose.RightGripError);
        UnityEngine.Object.DestroyImmediate(go);
    }
    results.Add(new {yawWeight,weight,max});
}
return results;
