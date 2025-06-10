using Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Resetter : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        if (SceneTransitionManager.Instance != null)
        {
            Debug.Log("[Resetter]SceneReset");
            SceneTransitionManager.ResetTransitionState();
        }
    }
}
