using System;
using Mono.CSharp;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Game.Visual
{
    public class GameManager : MonoBehaviour
    {
        public CameraManager CameraManager;
        public InputSystem_Actions inputActions;

        private void Awake()
        {
            inputActions = new InputSystem_Actions();
            inputActions.Player.Jump.performed += SpacePressed;
        }
        
        private void OnEnable()
        {
            inputActions.Player.Enable();
        }

        private void SpacePressed(InputAction.CallbackContext obj)
        {
            Debug.Log("SpacePressed");
            CameraManager.StartCameraTransition();
        }

       
    }
}
