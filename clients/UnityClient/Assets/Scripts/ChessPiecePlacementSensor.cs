using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChessPiecePlacementSensor : MonoBehaviour
{
    public string TargetedCellName = string.Empty;

    private void OnTriggerEnter(Collider other)
    {
        TargetedCellName = other.gameObject.name;
    }

    private void OnTriggerExit(Collider other)
    {
        if (TargetedCellName == other.gameObject.name)
        {
            TargetedCellName = string.Empty;
        }
    }
}
