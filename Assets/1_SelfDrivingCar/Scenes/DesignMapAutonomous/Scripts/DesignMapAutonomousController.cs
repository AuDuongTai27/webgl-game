using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DesignMapAutonomousController : MonoBehaviour
{
    [SerializeField] CarRemoteControl mCar;

    private void Start()
    {
        StartCoroutine(DoCountDownToStart());
        EnsureTrafficLightPresent();
    }

    IEnumerator DoCountDownToStart()
    {
        for (int i = 3; i >= 1; i--)
        {
            // 
            //yield return new WaitForSeconds(1);
        }
        yield return null;

        mCar.StartControl();
    }

    private void EnsureTrafficLightPresent()
    {
        WorldTrafficLight existingLight = FindObjectOfType<WorldTrafficLight>();
        if (existingLight == null && mCar != null)
        {
            GameObject trafficLightGo = new GameObject("Default_WorldTrafficLight");
            Vector3 spawnPos = mCar.transform.position + mCar.transform.forward * 16.0f + mCar.transform.right * 3.0f;
            trafficLightGo.transform.position = spawnPos;
            trafficLightGo.transform.rotation = Quaternion.LookRotation(-mCar.transform.forward);
            trafficLightGo.AddComponent<WorldTrafficLight>();
        }
    }
}
