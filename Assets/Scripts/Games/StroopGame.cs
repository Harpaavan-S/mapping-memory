using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class StroopGame : MonoBehaviour
{
#if !UNITY_WEBGL
    private UDPManager udp;
#endif

    [Header("UI References")]
    public TextMeshProUGUI wordDisplay;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI feedbackText;
    public TextMeshProUGUI instructionsText;
    public Button redButton, blueButton, greenButton, yellowButton;
    public Slider progressBar;
    
    [Header("Elements to Hide at End")]
    public GameObject buttonContainer;
    public GameObject progressBarObject;
    
    [Header("Game Settings")]
    public float trialDuration = 2f;
    public float interTrialDelay = 1f;
    public int totalTrials = 40;
    
    [Header("Text Sizes")]
    public int titleSize = 175;
    public int gameWordSize = 125;
    public int feedbackSize = 48;
    
    private Button[] colorButtons;
    private int currentTrial = 0;
    private int score = 0;
    private bool gameActive = false;
    private float trialStartTime;
    private bool trialInProgress = false;
    
    private int congruentCorrect = 0;
    private int incongruentCorrect = 0;
    private int congruentTotal = 0;
    private int incongruentTotal = 0;
    private bool currentIsCongruent = false;
    
    private Color[] colors = { Color.red, Color.blue, new Color(0, 0.8f, 0), Color.yellow };
    private string[] colorNames = { "RED", "BLUE", "GREEN", "YELLOW" };
    
    private string currentWord;
    private Color currentColor;
    private int correctButtonIndex;
    private List<string> gameLog = new List<string>();
    
    void Start()
    {
#if !UNITY_WEBGL
        udp = FindObjectOfType<UDPManager>();
        if (udp != null) udp.SendMarker("Stroop_Start");
#endif

        colorButtons = new Button[] { redButton, blueButton, greenButton, yellowButton };
        
        wordDisplay.fontSize = titleSize;
        wordDisplay.text = "COLOR COMMAND";
        scoreText.text = "Score: 0/0";
        timerText.text = "Time: 2.0";
        feedbackText.fontSize = feedbackSize;
        feedbackText.text = "Press ANY color button to start";
        
        if (progressBar != null)
        {
            progressBar.minValue = 0;
            progressBar.maxValue = totalTrials;
            progressBar.value = 0;
        }
        
        for (int i = 0; i < colorButtons.Length; i++)
            colorButtons[i].onClick.AddListener(StartGameViaButton);
    }
    
    void StartGameViaButton()
    {
        for (int i = 0; i < colorButtons.Length; i++)
        {
            colorButtons[i].onClick.RemoveAllListeners();
            int index = i;
            colorButtons[i].onClick.AddListener(() => OnButtonClicked(index));
        }
        StartCoroutine(CountdownToStart());
    }
    
    IEnumerator CountdownToStart()
    {
        wordDisplay.fontSize = titleSize;
        wordDisplay.text = "Starting in 3...";
        yield return new WaitForSeconds(1f);
        wordDisplay.text = "Starting in 2...";
        yield return new WaitForSeconds(1f);
        wordDisplay.text = "Starting in 1...";
        yield return new WaitForSeconds(1f);
        
        wordDisplay.text = "";
        float animationTime = 0.3f;
        float elapsed = 0f;
        while (elapsed < animationTime)
        {
            elapsed += Time.deltaTime;
            wordDisplay.fontSize = Mathf.Lerp(titleSize, gameWordSize, elapsed / animationTime);
            yield return null;
        }
        
        gameActive = true;
        gameLog.Add("Game Started at: " + Time.time);
        StartCoroutine(RunGame());
    }
    
    IEnumerator RunGame()
    {
        for (int trial = 0; trial < totalTrials; trial++)
        {
            currentTrial = trial;
            GenerateTrial();
            wordDisplay.text = currentWord;
            wordDisplay.color = currentColor;

#if !UNITY_WEBGL
            if (udp != null)
            {
                udp.SendMarker("Stroop_TrialStart");
                udp.SendMarker(currentIsCongruent ? "Stroop_Congruent" : "Stroop_Incongruent");
            }
#endif
            
            trialStartTime = Time.time;
            trialInProgress = true;
            
            float elapsedTime = 0f;
            while (elapsedTime < trialDuration && trialInProgress)
            {
                elapsedTime = Time.time - trialStartTime;
                float timeLeft = trialDuration - elapsedTime;
                timerText.text = "Time: " + timeLeft.ToString("F1");
                if (timeLeft < 0.5f) timerText.color = Color.red;
                else if (timeLeft < 1.0f) timerText.color = Color.yellow;
                else timerText.color = Color.white;
                yield return null;
            }
            
            if (trialInProgress)
            {
#if !UNITY_WEBGL
                if (udp != null)
                {
                    udp.SendMarker("Stroop_Incorrect");
                    udp.SendMarker("Stroop_RT:0");
                    udp.SendMarker("Stroop_TrialEnd");
                }
#endif
                feedbackText.text = "Too slow!";
                feedbackText.color = Color.red;
                gameLog.Add($"Trial {trial}: Timeout");
                if (currentIsCongruent) congruentTotal++;
                else incongruentTotal++;
            }
            
            if (progressBar != null) progressBar.value = trial + 1;
            scoreText.text = "Score: " + score + "/" + (trial + 1);
            wordDisplay.text = "";
            yield return new WaitForSeconds(interTrialDelay);
            feedbackText.text = "";
        }
        EndGame();
    }
    
    void GenerateTrial()
    {
        currentIsCongruent = (currentTrial % 2 == 0);
        int wordIndex = Random.Range(0, colorNames.Length);
        currentWord = colorNames[wordIndex];
        if (currentIsCongruent)
        {
            currentColor = colors[wordIndex];
            correctButtonIndex = wordIndex;
        }
        else
        {
            int colorIndex;
            do { colorIndex = Random.Range(0, colors.Length); } while (colorIndex == wordIndex);
            currentColor = colors[colorIndex];
            correctButtonIndex = colorIndex;
        }
    }
    
    void OnButtonClicked(int buttonIndex)
    {
        if (!gameActive || !trialInProgress) return;
        trialInProgress = false;
        float reactionTime = Time.time - trialStartTime;
        bool isCorrect = (buttonIndex == correctButtonIndex);
        
#if !UNITY_WEBGL
        if (udp != null)
        {
            udp.SendMarker(isCorrect ? "Stroop_Correct" : "Stroop_Incorrect");
            udp.SendMarker($"Stroop_RT:{Mathf.RoundToInt(reactionTime * 1000f)}");
            udp.SendMarker("Stroop_TrialEnd");
        }
#endif
        
        if (currentIsCongruent) { congruentTotal++; if (isCorrect) congruentCorrect++; }
        else { incongruentTotal++; if (isCorrect) incongruentCorrect++; }
        
        if (isCorrect) { score++; feedbackText.text = "Correct!"; feedbackText.color = Color.green; }
        else { feedbackText.text = "Wrong!"; feedbackText.color = Color.red; }
        
        gameLog.Add($"Trial {currentTrial}: {currentWord} in {colorNames[correctButtonIndex]} - {(isCorrect ? "Correct" : "Wrong")}, RT: {reactionTime:F2}s");
    }

    void EndGame()
    {
        gameActive = false;
        trialInProgress = false;
        float accuracy = (float)score / totalTrials * 100f;
        float congruentAccuracy = congruentTotal > 0 ? (float)congruentCorrect / congruentTotal * 100f : 0f;
        float incongruentAccuracy = incongruentTotal > 0 ? (float)incongruentCorrect / incongruentTotal * 100f : 0f;

#if !UNITY_WEBGL
        if (udp != null)
        {
            udp.SendMarker("Stroop_End");
            udp.SendMarker($"Stroop_Score:{accuracy:F1}");
        }
#endif

        if (instructionsText != null) instructionsText.gameObject.SetActive(false);
        if (buttonContainer != null) buttonContainer.SetActive(false);
        if (progressBarObject != null) progressBarObject.SetActive(false);
        timerText.gameObject.SetActive(false);
        scoreText.gameObject.SetActive(false);
        feedbackText.gameObject.SetActive(false);

        wordDisplay.text =
            "TEST COMPLETE\n\n" +
            $"Overall Accuracy: {accuracy:F1}%\n\n" +
            $"Congruent: {congruentAccuracy:F1}%\n" +
            $"Incongruent: {incongruentAccuracy:F1}%\n\n" +
            $"Final Score: {score}/{totalTrials}";
        wordDisplay.fontSize = 48;
        wordDisplay.color = Color.cyan;
        wordDisplay.alignment = TextAlignmentOptions.Center;

        RectTransform rect = wordDisplay.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        SaveGameData();
        StartCoroutine(LoadNextGameAfterDelay(3f));
    }
    
    IEnumerator LoadNextGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
    
    void SaveGameData()
    {
        string filePath = Application.persistentDataPath + "/StroopData.txt";
        string data = "=== COLOR COMMAND TEST RESULTS ===\n";
        data += "Date: " + System.DateTime.Now + "\n";
        data += "Total Trials: " + totalTrials + "\n";
        data += "Score: " + score + "/" + totalTrials + "\n";
        data += "Overall Accuracy: " + ((float)score / totalTrials * 100f).ToString("F1") + "%\n";
        data += "Congruent Accuracy: " + (congruentTotal > 0 ? ((float)congruentCorrect / congruentTotal * 100f).ToString("F1") : "0") + "%\n";
        data += "Incongruent Accuracy: " + (incongruentTotal > 0 ? ((float)incongruentCorrect / incongruentTotal * 100f).ToString("F1") : "0") + "%\n\n";
        data += "Trial Log:\n";
        foreach (string log in gameLog) data += log + "\n";
        System.IO.File.WriteAllText(filePath, data);
        Debug.Log("Data saved to: " + filePath);
    }
}