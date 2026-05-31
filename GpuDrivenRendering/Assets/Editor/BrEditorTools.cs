using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class BrEditorTools 
{
    [MenuItem("BrTool/DebugAndroidBuild")]
    private static void AttachProfilerToAndroid()
    {
        Debug.Log($"adb forward tcp:34999 localabstract:Unity-{PlayerSettings.applicationIdentifier}");
    }
}
