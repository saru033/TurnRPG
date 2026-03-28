using UnityEngine;

public class LobbyController : MonoBehaviour
{
    public GameObject Shelter;
    public GameObject Map;

    public void StartGame()
    {
        Shelter.SetActive(true);
        Map.SetActive(true);
        gameObject.SetActive(false);
    }
}
