using UnityEngine;

public class LobbyController : MonoBehaviour
{
    public GameObject Shelter;
    public GameObject Map;

    public GameObject gamemanager;
    public void StartGame()
    {
        Shelter.SetActive(true);
        gamemanager.SetActive(true);
        gameObject.SetActive(false);
    }

    public void GoToMap()
    {
        Map.SetActive(true);
    }
}
