// https://discussions.unity.com/t/matching-scene-view-with-camera-view/697621/4

using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class AlignCam : MonoBehaviour
{
    public Camera gameCam;
    public bool updateView = true;

    private void LateUpdate()
    {
        if (updateView)
        {
            SceneView sceneCam = SceneView.lastActiveSceneView;
            gameCam.transform.SetPositionAndRotation(sceneCam.camera.transform.position, sceneCam.camera.transform.rotation);
        }
    }
}