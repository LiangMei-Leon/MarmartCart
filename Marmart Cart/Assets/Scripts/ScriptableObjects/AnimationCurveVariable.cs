using UnityEngine;

[CreateAssetMenu(fileName = "AnimationCurveVariable", menuName = "Scriptable Objects/AnimationCurveVariable", order = 1)]
public class AnimationCurveVariable : ScriptableObject
{
#if UNITY_EDITOR
    [Multiline]
    public string DeveloperDescription = "";
#endif

    public AnimationCurve curve;
}
