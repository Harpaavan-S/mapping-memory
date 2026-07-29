using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class SimpleGameFlow : MonoBehaviour
{
    // Singleton instance
    public static SimpleGameFlow Instance;
    
    // Game scene names (set these in Inspector)
    public string[] gameScenes = {
        "Game_Stroop",
        "Game_NBack",
        "Game_Pattern",
        "Game_Spatial"
    };
    
    // Data storage
    public List<GameResult> allResults = new List<GameResult>();
    
    // Settings
    public float delayBetweenGames = 1.0f;
    public bool showTransitionMessages = true;
    
    // Private
    private int currentGameIndex = 0;
    private bool transitioning = false;
    
    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            
            // Start the first game automatically
            StartCoroutine(StartFirstGame());
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    IEnumerator StartFirstGame()
    {
        // Wait a moment for everything to initialize
        yield return new WaitForSeconds(0.5f);
        
        // Load first game
        LoadGame(0);
    }
    
    // Call this when a game is completed
    public void OnGameCompleted(GameResult result)
    {
        if (transitioning) return;
        
        // Store the result
        allResults.Add(result);
        Debug.Log($"Game completed: {result.gameName}, Accuracy: {result.accuracy}%");
        
        // Load next game
        currentGameIndex++;
        StartCoroutine(TransitionToNextGame());
    }
    
    IEnumerator TransitionToNextGame()
    {
        transitioning = true;
        
        // Brief pause before next game
        yield return new WaitForSeconds(delayBetweenGames);
        
        if (currentGameIndex < gameScenes.Length)
        {
            LoadGame(currentGameIndex);
        }
        else
        {
            // All games done - show final report
            LoadFinalReport();
        }
        
        transitioning = false;
    }
    
    void LoadGame(int index)
    {
        if (index >= 0 && index < gameScenes.Length)
        {
            string sceneName = gameScenes[index];
            Debug.Log($"Loading game: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }
    }
    
    void LoadFinalReport()
    {
        Debug.Log("All games completed. Loading final report...");
        SceneManager.LoadScene("FinalReport");
    }
    
    // Helper method to get current game name
    public string GetCurrentGameName()
    {
        if (currentGameIndex < gameScenes.Length)
            return gameScenes[currentGameIndex];
        return "Final Report";
    }
    
    // For debugging
    void OnGUI()
    {
        if (Debug.isDebugBuild)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Game Flow Manager");
            GUILayout.Label($"Current Game: {GetCurrentGameName()}");
            GUILayout.Label($"Games Completed: {allResults.Count}/4");
            GUILayout.Label($"Transitioning: {transitioning}");
            
            if (GUILayout.Button("Skip to Report"))
            {
                // Create dummy data for testing
                allResults.Clear();
                allResults.Add(new GameResult("Stroop Test", 85.5f, 0.45f, 100));
                allResults.Add(new GameResult("N-Back Game", 72.3f, 0.78f, 150));
                allResults.Add(new GameResult("Pattern Recognition", 68.9f, 1.23f, 200));
                allResults.Add(new GameResult("Spatial Memory", 90.1f, 2.34f, 250));
                LoadFinalReport();
            }
            
            GUILayout.EndArea();
        }
    }
}