using UnityEngine;

// Add this component to each game's manager object
// It listens for game completion and notifies the SimpleGameFlow
public class GameEndTrigger : MonoBehaviour
{
    // Set these in Inspector for each game
    public string gameName = "Unnamed Game";
    
    // This function should be called from each game's EndGame() method
    public void TriggerGameEnd(float accuracy, float avgReactionTime, float score = 0)
    {
        // Create result
        GameResult result = new GameResult(gameName, accuracy, avgReactionTime, score);
        
        // Add custom metrics based on game type
        if (TryGetComponent<StroopGame>(out var stroopGame))
        {
            // Add Stroop-specific metrics
            result.customMetrics.Add("stroop_effect", 0.0f); // You'll calculate this
        }
        else if (TryGetComponent<PatternGame>(out var patternGame))
        {
            // Add Pattern-specific metrics
            result.customMetrics.Add("learning_improvement", 0.0f); // You'll calculate this
        }
        // Add for other game types...
        
        // Notify the flow manager
        if (SimpleGameFlow.Instance != null)
        {
            SimpleGameFlow.Instance.OnGameCompleted(result);
        }
        else
        {
            Debug.LogError("SimpleGameFlow instance not found!");
            // Fallback: load next scene manually
            LoadNextSceneFallback();
        }
    }
    
    void LoadNextSceneFallback()
    {
        // Simple fallback - just load next scene in build order
        int currentIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentIndex + 1);
    }
}