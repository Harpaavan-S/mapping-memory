using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;   // Already used, but ensure it's present

public class Beginning : MonoBehaviour
{
    public TextMeshProUGUI reportText;
    
    void Start()
    {
        reportText.text = "Welcome to the Muse Memory Test!!!\n\n" +
                         "You will play 4 games which will assess your memory.\n\n" +
                         "I hope you enjoy it!!!";
    }

    IEnumerator LoadNextGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;
        
        SceneManager.LoadScene(nextSceneIndex);
    }
    
}
