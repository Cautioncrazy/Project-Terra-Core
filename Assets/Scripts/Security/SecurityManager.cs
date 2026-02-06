using UnityEngine;

namespace Security
{
    public class SecurityManager : MonoBehaviour
    {
        public static SecurityManager Instance { get; private set; }

        [Header("Settings")]
        public int maxClicksPerSecond = 15;
        public float fatigueCheckInterval = 300f;

        [Header("Recalibration")]
        public float minRecalibrationInterval = 1800f; // 30 mins
        public float maxRecalibrationInterval = 2700f; // 45 mins
        private float recalibrationTimer;

        private int clickCount;
        private float clickTimer;
        private float sessionTimer;

        private bool isInputLocked = false;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        void Start()
        {
            ResetRecalibrationTimer();
        }

        void ResetRecalibrationTimer()
        {
            recalibrationTimer = Random.Range(minRecalibrationInterval, maxRecalibrationInterval);
        }

        void Update()
        {
            sessionTimer += Time.deltaTime;

            // Recalibration Logic
            if (!isInputLocked)
            {
                recalibrationTimer -= Time.deltaTime;
                if (recalibrationTimer <= 0)
                {
                    TriggerRecalibration();
                    ResetRecalibrationTimer();
                }
            }

            clickTimer += Time.deltaTime;
            if (clickTimer >= 1.0f)
            {
                clickTimer = 0;
                clickCount = 0;
            }
        }

        public bool ValidateInput()
        {
            if (isInputLocked)
            {
                Debug.Log("Input Locked: Recalibrate.");
                return false;
            }

            clickCount++;
            if (clickCount > maxClicksPerSecond)
            {
                Debug.LogWarning("Rate Limit Exceeded!");
                return false;
            }

            return true;
        }

        public void TriggerRecalibration()
        {
            isInputLocked = true;
            Debug.Log("Tool Overheated. Recalibrate.");
            Invoke("UnlockInput", 3.0f);
        }

        void UnlockInput()
        {
            isInputLocked = false;
            Debug.Log("Recalibration Complete.");
        }
    }
}
