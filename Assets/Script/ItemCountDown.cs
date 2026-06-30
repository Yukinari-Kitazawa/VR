using UnityEngine;

public class ItemCountDown : MonoBehaviour
{
    public GameObject gameObjectRestore;
    private bool isGrabbed = false;
    private bool isTouching = false;
    public float grabItemTimeLimit =3.0f;
    float timer = 0.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        timer = 0;
    }
    public void OnGetGrab()
    {
        isGrabbed = true;
        isTouching = true;
    }
    public void OnExitGrab()
    {
        isTouching = false;
    }
    // Update is called once per frame
    void Update()
    {

        if(grabItemTimeLimit>0.0f)
        {
            if(isGrabbed== true)
            {
                if(isTouching == false)
                {
                    timer += Time.deltaTime;
                    if(timer >= grabItemTimeLimit)
                    {
                        var rb = GetComponent<Rigidbody>();
                        rb.linearVelocity = Vector3.zero;
                        rb.transform.position = gameObjectRestore.transform.position;
                        rb.transform.rotation = gameObjectRestore.transform.rotation;
                        isGrabbed = false;

                    }
                }
                else
                {
                       timer = 0.0f;
                }
            }
        }

    }
}
