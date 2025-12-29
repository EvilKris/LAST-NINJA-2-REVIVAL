using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject inGameUIOverlay; //the main in-game UI overlay


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(inGameUIOverlay != null)
            inGameUIOverlay.SetActive(true);    
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
