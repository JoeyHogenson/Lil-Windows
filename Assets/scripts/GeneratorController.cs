using System.Collections;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Windows;

public class GeneratorController : MonoBehaviour
{
    private Canvas canvas;
   public HUDManager HUDManager;
    public MovementController MovementController;
    public float fuel;
    public float maxfuel;
  
    // Start is called before the first frame update
    void Start()
    {
        ///// get current fuel from save
        ///
   
        MovementController = FindObjectOfType<MovementController>();
        HUDManager = FindObjectOfType<HUDManager>();
        
    }

    // Update is called once per frame
    void Update()
    {
        fuel -= Time.deltaTime;
       // Debug.Log("Fuel is " + fuel);
        if (fuel < maxfuel)
        {
            fuel = maxfuel;
        }
        HUDManager.fuellevel.text=fuel.ToString();

    }
    void OnMouseOver()
    {
        if (Vector3.Distance(MovementController.transform.position, transform.position) <= 3) 
        { 
            Debug.Log("The player is looking at " + transform.name);
        HUDManager.drawGen();
        // pop up generator hud
        }
    }
    private void OnMouseExit()
    {
       
        HUDManager.killGen();
    }
}
