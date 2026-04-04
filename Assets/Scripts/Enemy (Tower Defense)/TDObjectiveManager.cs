using System;
using TMPro;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.InputSystem.Android;

public class TDObjectiveManager : MonoBehaviour
{
    public static TDObjectiveManager instance;
    [SerializeField] public GameObject[] healthCounters;
    
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        foreach(GameObject ctr in healthCounters)
        {
            ctr.GetComponent<TDObjectiveHealth>().UpdateDisplay();
        }
    }

    public GameObject GetCounter(int element)
    {
        if (element > healthCounters.Length - 1)
        {
            //Default to first counter; Fix by adding approriate number
            return healthCounters[0];
        }
        else
        {
            return healthCounters[element];
        }
    }
}
