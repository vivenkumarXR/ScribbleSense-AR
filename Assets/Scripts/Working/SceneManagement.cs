using UnityEngine;
using UnityEngine.SceneManagement;
public class SceneManagement : MonoBehaviour
{
    public void GotoMainScreen()
    {
        SceneManager.LoadScene(0);
    }
}
