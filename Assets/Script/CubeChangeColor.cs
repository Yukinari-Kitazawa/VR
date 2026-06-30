using UnityEngine;

public class CubeChangeColor : MonoBehaviour
{
    Material material;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        material = GetComponent<Renderer>().material;
    }
    public void OnChangeColorStart()
    {
        material.color = Color.red;
    }
    public void OnChangeColorEnd()
    {
        material.color = Color.white;
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
