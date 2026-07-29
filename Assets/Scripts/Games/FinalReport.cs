using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class SimpleFinalReport : MonoBehaviour
{
    public TextMeshProUGUI reportText;
    
    void Start()
    {
        reportText.text = "All cognitive tests completed!\n\n" +
                         "Thank you for participating.\n\n" +
                         "Your brainwave data has been recorded.";
    }
    
    // Optional: Press space to restart
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SceneManager.LoadScene(0); // Back to first game
        }
    }
}