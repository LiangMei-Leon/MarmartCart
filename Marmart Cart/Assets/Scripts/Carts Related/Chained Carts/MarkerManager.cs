using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarkerManager : MonoBehaviour
{
    public class Marker
    {
        public Vector3 position;
        public Quaternion rotation;

        public Marker (Vector3 pos, Quaternion rot)
        {
            position = pos;
            rotation = rot;
        }
    }

    public List<Marker> markerList = new List<Marker>();
    [SerializeField] AnimationCurveVariable offsetValueCurve;
    [SerializeField] float cartMaxSpeed = 30f;
    [SerializeField] float offsetValue = 0.0f;

    private Rigidbody cartRigidbody;

    void Start()
    {
        offsetValueCurve = Resources.Load<AnimationCurveVariable>("SO_Variables/Offset Value Curve");
        cartRigidbody = this.gameObject.transform.parent.GetChild(0).GetComponent<Rigidbody>(); //refer to the leading cart's rigidbody
    }

    // Update is called once per frame
    void Update()
    {
        if(cartRigidbody != null)
        {
            float cartSpeed = Vector3.Dot(cartRigidbody.gameObject.transform.forward, cartRigidbody.linearVelocity);
            offsetValue = offsetValueCurve.curve.Evaluate(Mathf.Abs(cartSpeed) / cartMaxSpeed);
            // offsetValue = 0.5f;
        }
    }

    void FixedUpdate()
    {
        UpdateMarkerList();
    }

    public void UpdateMarkerList()
    {
        markerList.Add(new Marker(transform.position + -1 * offsetValue * transform.forward, transform.rotation));
    }

    public void ClearMarkerList()
    {
        markerList.Clear();
        markerList.Add(new Marker(transform.position + -1 * offsetValue * transform.forward, transform.rotation));
    }
}
