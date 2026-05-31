using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private Transform _playerTr;

    [SerializeField]
    private float _playerSpeed;

    [SerializeField]
    private bool _autoWalk;
    
    private void Update()
    {
        var moveDir = Vector3.zero;
        if(_autoWalk)
        {
            moveDir += Vector3.forward;
        }
        else
        {
            if(Input.GetKey(KeyCode.W))
                moveDir += Vector3.forward;
            if(Input.GetKey(KeyCode.S))
                moveDir += Vector3.back;
            if(Input.GetKey(KeyCode.D))
                moveDir += Vector3.right;
            if(Input.GetKey(KeyCode.A))
                moveDir += Vector3.left;
            moveDir = moveDir.normalized;
        }
        _playerTr.position += moveDir * _playerSpeed * Time.deltaTime;
    }
}
