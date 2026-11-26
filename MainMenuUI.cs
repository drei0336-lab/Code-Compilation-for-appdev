
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public GameObject settingsPanel;
    public GameObject instructionsPanel;

    // Play button
    public void PlayGame()
    {
        SceneManager.LoadScene("GameScene");
    }

    // Settings button
    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
    }

    // How to Play button
    public void OpenInstructions()
    {
        instructionsPanel.SetActive(true);
    }

    // Back button
    public void BackToMenu()
    {
        settingsPanel.SetActive(false);
        instructionsPanel.SetActive(false);
    }

    // Quit button
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit button clicked!");
    }
}

