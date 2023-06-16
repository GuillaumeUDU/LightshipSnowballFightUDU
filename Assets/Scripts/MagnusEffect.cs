using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MagnusEffect : MonoBehaviour
{
    public float radius = .5f;
    //Real life value 1.2, base script value is 0.1
    public float airDensity = .1f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        var direction = Vector3.Cross(rb.angularVelocity, rb.velocity);
        var magnitude = 4 / 3f * Mathf.PI * airDensity * Mathf.Pow(radius, 3);
        rb.AddForce(magnitude * direction);
    }
}
