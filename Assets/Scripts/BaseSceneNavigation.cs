using UnityEngine.SceneManagement;

public static class BaseSceneNavigation
{
    // The project currently has Assets/baseSecne.unity. Keep both spellings accepted.
    public const string SceneToLoad = "baseSecne";

    public static bool IsBaseSceneName(string sceneName)
    {
        return string.Equals(sceneName, "baseScene", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sceneName, "baseSecne", System.StringComparison.OrdinalIgnoreCase);
    }

    public static void ReturnToBaseScene()
    {
        UnityEngine.Time.timeScale = 1f;
        SceneManager.LoadScene(SceneToLoad);
    }
}
