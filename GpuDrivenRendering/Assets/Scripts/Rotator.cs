using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotator : MonoBehaviour
{
    [SerializeField]
    private Transform _transform;
    [SerializeField]
    private float _speed;
    
    private void Update()
    {
        _transform.Rotate(Vector3.up, Time.deltaTime * _speed);
    }
}
