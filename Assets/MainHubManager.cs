using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainHubManager : MonoBehaviour
{
    [Header("UI References")]
    public Button scene1Button;
    public Button scene2Button;
    public Button scene3Button;

    [Header("Scene Names")]
    public string scene1Name = "ObjectDetectionScene";
    public string scene2Name = "ARScene";
    public string scene3Name = "TrainingScene";

    [Header("Main Hub Scene")]
    public string mainHubSceneName = "MainHub";

    void Start()
    {
        SetupButtons();

        // Store main hub scene name for other scenes to return to
        PlayerPrefs.SetString("MainHubScene", mainHubSceneName);
    }

    void SetupButtons()
    {
        if (scene1Button != null)
            scene1Button.onClick.AddListener(() => LoadScene(scene1Name));

        if (scene2Button != null)
            scene2Button.onClick.AddListener(() => LoadScene(scene2Name));

        if (scene3Button != null)
            scene3Button.onClick.AddListener(() => LoadScene(scene3Name));
    }

    public void LoadScene(string sceneName)
    {
        Debug.Log($"Loading scene: {sceneName}");

        // Add loading transition if needed
        StartCoroutine(LoadSceneWithTransition(sceneName));
    }

    System.Collections.IEnumerator LoadSceneWithTransition(string sceneName)
    {
        // Optional: Add fade out effect here
        yield return new WaitForSeconds(0.1f);

        SceneManager.LoadScene(sceneName);
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // Save any necessary data when app is paused
        }
    }
}
