using UnityEngine;
using UnityEngine.Events;
using Unity.Cinemachine;

namespace Game.Visual
{
    public class CameraManager : MonoBehaviour
    {
        [Header("Virtual Cameras")]
        [SerializeField] private CinemachineCamera playerCamera;
        [SerializeField] private CinemachineCamera enemyCamera;
        
        [Header("Cinemachine Brain")]
        [SerializeField] private CinemachineBrain cinemachineBrain;
        
        [Header("Events")]
        public UnityEvent OnCameraTransitionStarted;
        public UnityEvent OnPlayerCameraActive;
        public UnityEvent OnEnemyCameraActive;

        private bool isTransitioning = false;
        private bool isPlayerTurn = true;

        
        private void SwitchToPlayerCamera()
        {
            if (isPlayerTurn) return;
            
            isPlayerTurn = true;
            
            playerCamera.Priority = 20;
            enemyCamera.Priority = 10;
            
            OnPlayerCameraActive.Invoke();
        }
        
        private void SwitchToEnemyCamera()
        {
            if (!isPlayerTurn) return;

            isPlayerTurn = false;

            enemyCamera.Priority = 20;
            playerCamera.Priority = 10;
            
            OnEnemyCameraActive.Invoke();
        }

        public void StartCameraTransition()
        {
            OnCameraTransitionStarted.Invoke();
            
            if (isPlayerTurn)
            {
                SwitchToEnemyCamera();
            }
            else
            {
                SwitchToPlayerCamera();
            }
        }
        
        
    }
}
