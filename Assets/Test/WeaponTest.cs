using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class WeaponTest : MonoBehaviour, ILiteTriggerHandler
{
    public void OnLiteTriggerEnter(LiteCollider other)
    {
        if (other.gameObject.CompareTag("Enemy"))
        {
            other.gameObject.GetComponent<LiteMob>().Kill();
        }
    }
    public void OnLiteTriggerExit(LiteCollider other)
    {
    }
    public void OnLiteTriggerStay(LiteCollider other)
    {
    }
}