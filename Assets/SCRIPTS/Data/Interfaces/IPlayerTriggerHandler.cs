using UnityEngine;

public interface IPlayerTriggerHandler
{
    void OnPlayerEnter(GameObject player);
    void OnPlayerExit(GameObject player);
    void OnPlayerStay(GameObject player);
}