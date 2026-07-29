using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class NBackGame : MonoBehaviour
{
#if !UNITY_WEBGL
    private UDPManager udp;
#endif

    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI instructionsText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI feedbackText;
    public Slider progressBar;
    public Button matchButton;
    public Button noMatchButton;
    
    [Header("Grid Squares")]
    public Image[] gridSquares = new Image[9];
    
    [Header("Game Settings")]
    public float highlightDuration = 2.5f;
    public float interTrialDelay = 2.0f;
    public float responseWindow = 5.0f;
    public int totalTrials = 25;
    public float matchProbability = 0.33f;
    
    [Header("Colors")]
    public Color idleColor = Color.gray;
    public Color highlightColor = Color.cyan;
    public Color correctColor = Color.green;
    public Color wrongColor = Color.red;
    public Color startButtonColor = new Color(0.2f, 0.6f, 1f);
    
    private int currentTrial = 0;
    private int score = 0;
    private int matchesCorrect = 0;
    private int matchesTotal = 0;
    private bool gameActive = false;
    private bool acceptingResponses = false;
    private float trialStartTime;
    private List<int> sequenceHistory = new List<int>();
    private int currentSquareIndex = -1;
    private bool isMatchTrial = false;
    private bool respondedThisTrial = false;
    private List<string> gameLog = new List<string>();
    
    void Start()
    {
#if !UNITY_WEBGL
        udp = FindObjectOfType<UDPManager>();
        if (udp != null) udp.SendMarker("NBack_Start");
#endif
        InitializeGame();
        SetupStartButtons();
    }
    
    void InitializeGame()
    {
        foreach (Image square in gridSquares) if (square != null) square.color = idleColor;
        if (titleText != null) titleText.text = "MEMORY MATRIX";
        if (instructionsText != null) { instructionsText.text = "Press MATCH if current square matches 2 steps back"; instructionsText.color = Color.white; instructionsText.fontSize = 28; }
        if (scoreText != null) scoreText.text = "Score: 0/0";
        if (timerText != null) timerText.text = "Ready";
        if (feedbackText != null) { feedbackText.text = "Click START to begin"; feedbackText.color = Color.yellow; }
        if (progressBar != null) { progressBar.minValue = 0; progressBar.maxValue = totalTrials; progressBar.value = 0; }
    }
    
    void SetupStartButtons()
    {
        if (matchButton != null)
        {
            matchButton.interactable = true;
            matchButton.onClick.RemoveAllListeners();
            matchButton.onClick.AddListener(StartGameViaButton);
            ColorBlock colors = matchButton.colors;
            colors.normalColor = startButtonColor;
            matchButton.colors = colors;
            TextMeshProUGUI text = matchButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = "START";
        }
        if (noMatchButton != null)
        {
            noMatchButton.interactable = true;
            noMatchButton.onClick.RemoveAllListeners();
            noMatchButton.onClick.AddListener(StartGameViaButton);
            ColorBlock colors = noMatchButton.colors;
            colors.normalColor = startButtonColor;
            noMatchButton.colors = colors;
            TextMeshProUGUI text = noMatchButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = "START";
        }
    }
    
    void SetupGameButtons()
    {
        if (matchButton != null)
        {
            matchButton.onClick.RemoveAllListeners();
            matchButton.onClick.AddListener(() => OnResponse(true));
            ColorBlock colors = matchButton.colors;
            colors.normalColor = new Color(0, 0.8f, 0);
            matchButton.colors = colors;
            TextMeshProUGUI text = matchButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = "MATCH";
        }
        if (noMatchButton != null)
        {
            noMatchButton.onClick.RemoveAllListeners();
            noMatchButton.onClick.AddListener(() => OnResponse(false));
            ColorBlock colors = noMatchButton.colors;
            colors.normalColor = new Color(0.8f, 0, 0);
            noMatchButton.colors = colors;
            TextMeshProUGUI text = noMatchButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = "NO MATCH";
        }
    }
    
    void SetResponseButtons(bool active)
    {
        if (matchButton != null) matchButton.interactable = active;
        if (noMatchButton != null) noMatchButton.interactable = active;
    }
    
    public void StartGameViaButton()
    {
        if (gameActive) return;
        SetupGameButtons();
        if (feedbackText != null) { feedbackText.text = ""; feedbackText.color = Color.white; }
        StartCoroutine(GameRoutine());
    }
    
    IEnumerator GameRoutine()
    {
        if (feedbackText != null) { feedbackText.text = "Starting in 3..."; feedbackText.color = Color.yellow; }
        yield return new WaitForSeconds(1f);
        if (feedbackText != null) feedbackText.text = "Starting in 2...";
        yield return new WaitForSeconds(1f);
        if (feedbackText != null) feedbackText.text = "Starting in 1...";
        yield return new WaitForSeconds(1f);
        if (feedbackText != null) feedbackText.text = "";
        gameActive = true;
        sequenceHistory.Clear();
        gameLog.Add("N-Back Game Started: " + System.DateTime.Now.ToString());
#if !UNITY_WEBGL
        if (udp != null) udp.SendMarker("NBack_Start");
#endif
        if (instructionsText != null) { instructionsText.text = "Press MATCH if current square matches 2 steps back"; instructionsText.color = Color.white; }
        for (currentTrial = 0; currentTrial < totalTrials; currentTrial++)
            yield return StartCoroutine(RunTrial(currentTrial));
        EndGame();
    }
    
    IEnumerator RunTrial(int trialNumber)
    {
        GenerateTrial(trialNumber);
        if (feedbackText != null) { feedbackText.text = $"Trial {trialNumber + 1} of {totalTrials}"; feedbackText.color = Color.white; }
        yield return new WaitForSeconds(1.0f);
        foreach (Image square in gridSquares) if (square != null) square.color = idleColor;
        if (currentSquareIndex >= 0 && currentSquareIndex < gridSquares.Length && gridSquares[currentSquareIndex] != null)
            gridSquares[currentSquareIndex].color = highlightColor;
        
#if !UNITY_WEBGL
        if (udp != null)
        {
            udp.SendMarker("NBack_TrialStart");
            udp.SendMarker(isMatchTrial ? "NBack_Match" : "NBack_NonMatch");
        }
#endif
        trialStartTime = Time.time;
        acceptingResponses = true;
        respondedThisTrial = false;
        SetResponseButtons(true);
        yield return new WaitForSeconds(highlightDuration);
        if (currentSquareIndex >= 0 && currentSquareIndex < gridSquares.Length && gridSquares[currentSquareIndex] != null)
            gridSquares[currentSquareIndex].color = idleColor;
        float responseEndTime = trialStartTime + highlightDuration + responseWindow;
        while (Time.time < responseEndTime && !respondedThisTrial)
        {
            float timeLeft = responseEndTime - Time.time;
            if (timerText != null) timerText.text = $"Time: {timeLeft.ToString("F1")}";
            yield return null;
        }
        if (!respondedThisTrial)
        {
#if !UNITY_WEBGL
            if (udp != null)
            {
                udp.SendMarker("NBack_Incorrect");
                udp.SendMarker("NBack_RT:0");
                udp.SendMarker("NBack_TrialEnd");
            }
#endif
            if (feedbackText != null) { feedbackText.text = "No response"; feedbackText.color = Color.yellow; }
            gameLog.Add($"Trial {trialNumber}: Timeout");
        }
        acceptingResponses = false;
        SetResponseButtons(false);
        if (progressBar != null) progressBar.value = trialNumber + 1;
        if (scoreText != null) scoreText.text = $"Score: {score}/{trialNumber + 1}";
        yield return new WaitForSeconds(interTrialDelay);
        if (feedbackText != null) feedbackText.text = "";
    }
    
    void GenerateTrial(int trialNumber)
    {
        if (trialNumber < 2)
        {
            isMatchTrial = false;
            currentSquareIndex = Random.Range(0, gridSquares.Length);
        }
        else
        {
            isMatchTrial = (Random.value < matchProbability);
            if (isMatchTrial)
                currentSquareIndex = sequenceHistory[trialNumber - 2];
            else
            {
                int twoBackIndex = sequenceHistory[trialNumber - 2];
                do { currentSquareIndex = Random.Range(0, gridSquares.Length); } while (currentSquareIndex == twoBackIndex);
            }
        }
        sequenceHistory.Add(currentSquareIndex);
        gameLog.Add($"Trial {trialNumber}: Square {currentSquareIndex}, Match: {isMatchTrial}");
    }
    
    void OnResponse(bool isMatchResponse)
    {
        if (!gameActive || !acceptingResponses || respondedThisTrial) return;
        respondedThisTrial = true;
        float reactionTime = Time.time - trialStartTime;
        bool isCorrect = (isMatchResponse == isMatchTrial);
#if !UNITY_WEBGL
        if (udp != null)
        {
            udp.SendMarker(isCorrect ? "NBack_Correct" : "NBack_Incorrect");
            udp.SendMarker($"NBack_RT:{Mathf.RoundToInt(reactionTime * 1000f)}");
            udp.SendMarker("NBack_TrialEnd");
        }
#endif
        if (isCorrect)
        {
            score++;
            if (isMatchTrial) matchesCorrect++;
            if (feedbackText != null)
            {
                feedbackText.text = isMatchTrial ? "Correct! That was a match" : "Correct! Not a match";
                feedbackText.color = correctColor;
            }
        }
        else
        {
            if (feedbackText != null)
            {
                feedbackText.text = isMatchTrial ? "Missed! That was a match" : "Wrong! That wasn't a match";
                feedbackText.color = wrongColor;
            }
        }
        if (isMatchTrial) matchesTotal++;
        gameLog.Add($"Response: {(isMatchResponse ? "Match" : "No Match")}, Correct: {isCorrect}, RT: {reactionTime:F2}s");
    }

    void EndGame()
    {
        gameActive = false;
        acceptingResponses = false;
        SetResponseButtons(false);
        float accuracy = (float)score / totalTrials * 100f;
        float matchAccuracy = matchesTotal > 0 ? (float)matchesCorrect / matchesTotal * 100f : 0f;
#if !UNITY_WEBGL
        if (udp != null)
        {
            udp.SendMarker("NBack_End");
            udp.SendMarker($"NBack_Score:{accuracy:F1}");
            udp.SendMarker($"NBack_MatchAccuracy:{matchAccuracy:F1}");
        }
#endif

        // Hide all game UI
        if (matchButton != null) matchButton.gameObject.SetActive(false);
        if (noMatchButton != null) noMatchButton.gameObject.SetActive(false);
        foreach (Image square in gridSquares) if (square != null) square.gameObject.SetActive(false);
        if (progressBar != null) progressBar.gameObject.SetActive(false);
        if (instructionsText != null) instructionsText.gameObject.SetActive(false);
        timerText.gameObject.SetActive(false);
        scoreText.gameObject.SetActive(false);

        // Set end screen text
        feedbackText.text =
            $"TEST COMPLETE\n\n" +
            $"Overall Accuracy: {accuracy:F1}%\n\n" +
            $"Match Detection: {matchAccuracy:F1}%\n\n" +
            $"Final Score: {score}/{totalTrials}";
        feedbackText.fontSize = 48;
        feedbackText.color = Color.cyan;
        feedbackText.alignment = TextAlignmentOptions.Center;

        // Force center alignment of the RectTransform
        RectTransform rect = feedbackText.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        if (titleText != null) titleText.gameObject.SetActive(false);

        SaveGameData();
        StartCoroutine(LoadNextGameAfterDelay(3f));
    }
    
    IEnumerator LoadNextGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        int nextIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextIndex);
    }
    
    void SaveGameData()
    {
        string filePath = Application.persistentDataPath + "/NBackData.txt";
        string data = "=== N-BACK TEST RESULTS ===\n";
        data += "Date: " + System.DateTime.Now + "\n";
        data += "Total Trials: " + totalTrials + "\n";
        data += "Score: " + score + "/" + totalTrials + "\n";
        data += "Overall Accuracy: " + ((float)score / totalTrials * 100f).ToString("F1") + "%\n";
        data += "Match Detection Rate: " + (matchesTotal > 0 ? ((float)matchesCorrect / matchesTotal * 100f).ToString("F1") : "0") + "%\n\n";
        data += "Trial Log:\n";
        foreach (string log in gameLog) data += log + "\n";
        System.IO.File.WriteAllText(filePath, data);
        Debug.Log("N-Back data saved to: " + filePath);
    }
}