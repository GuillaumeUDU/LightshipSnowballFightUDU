using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MagnusEffect : MonoBehaviour
{
    // 0.5 very high value huge angle
    // 0.2 very low value barely noticable
    // 0.275 good low value but light angle
    // 0.333 good value but average angle
    private float radius = .333f;
    //Real life value 1.2, base script value is 0.1
    private float airDensity = .1f;

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
