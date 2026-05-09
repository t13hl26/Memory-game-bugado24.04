using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    private static MusicPlayer instance;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // mantém entre as cenas
        }
        else
        {
            Destroy(gameObject); // evita duplicar música
        }
    }
}
