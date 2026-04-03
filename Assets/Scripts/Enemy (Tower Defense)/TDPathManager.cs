using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class TDPathManager : MonoBehaviour
{
    public static TDPathManager instance;
    public GameObject[] pathObjects;
    private List<Transform> path;

    private void Awake()
    {
        //Simpleton stuff
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        //DontDestroyOnLoad(gameObject); //Allows for objects to persist between scenes
    }

    public GameObject GetPathObject(int index)
    {
        return pathObjects[index];
    }
}
