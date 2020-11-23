using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateAxis : MonoBehaviour
{
    public Vector3 axis;
    public float speed;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 eular = transform.localRotation.eulerAngles;
        eular += axis * Time.smoothDeltaTime * speed;
        transform.localRotation = Quaternion.Euler(eular);
    }
}
