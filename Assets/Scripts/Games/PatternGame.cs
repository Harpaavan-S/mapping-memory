using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class PatternGame : MonoBehaviour
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
    public Button stormButton;
    public Button clearButton;
    
    [Header("Symbol Cards")]
    public Image[] symbolCards = new Image[3];
    public Sprite[] symbols;
    
    [Header("Game Settings")]
    public float symbolDisplayTime = 3.0f;
    public float feedbackTime = 2.0f;
    public float interTrialDelay = 1.5f;
    public int totalTrials = 50;
    public float learningProbability = 0.8f;
    
    [Header("Colors")]
    public Color stormColor = new Color(0.8f, 0.2f, 0.2f);
    public Color clearColor = new Color(0.2f, 0.2f, 0.8f);
    public Color correctColor = Color.green;
    public Color wrongColor = Color.red;
    public Color startButtonColor = new Color(0.2f, 0.6f, 1f);
    public Color symbolColor = Color.white;
    
    private int currentTrial = 0;
    private int score = 0;
    private bool gameActive = false;
    private bool acceptingResponses = false;
    private int[] currentSymbols = new int[3];
    private bool correctPrediction;
    private bool playerPrediction;
    private float symbolAppearanceTime;
    private Dictionary<int, float> symbolRules;
    private List<float> accuracyOverTime = new List<float>();
    private int[] symbolTypeCount = new int[3];
    private int[] correctBySymbol = new int[3];
    private List<string> gameLog = new List<string>();
    
    void Start()
    {
#if !UNITY_WEBGL
        udp = FindObjectOfType<UDPManager>();
        if (udp != null) udp.SendMarker("Pattern_Start");
#endif
        InitializeGame();
        SetupStartButtons();
        InitializeRules();
    }
    
    void InitializeGame()
    {
        if (titleText != null) titleText.text = "PLANETARY FORECAST";
        if (instructionsText != null) { instructionsText.text = "Predict STORM or CLEAR based on the symbols"; instructionsText.color = Color.white; }
        if (scoreText != null) scoreText.text = "Score: 0/0";
        if (timerText != null) timerText.text = "Ready";
        if (feedbackText != null) { feedbackText.text = "Click START to begin"; feedbackText.color = Color.yellow; }
        if (progressBar != null) { progressBar.minValue = 0; progressBar.maxValue = totalTrials; progressBar.value = 0; }
        foreach (Image card in symbolCards) if (card != null) { card.gameObject.SetActive(false); card.color = symbolColor; }
    }
    
    void InitializeRules()
    {
        symbolRules = new Dictionary<int, float> { {0, 0.8f}, {1, 0.2f}, {2, 0.5f} };
    }
    
    void SetupStartButtons()
    {
        if (stormButton != null)
        {
            stormButton.interactable = true;
            stormButton.onClick.RemoveAllListeners();
            stormButton.onClick.AddListener(StartGameViaButton);
            ColorBlock colors = stormButton.colors;
            colors.normalColor = startButtonColor;
            stormButton.colors = colors;
            TextMeshProUGUI text = stormButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = "START";
        }
        if (clearButton != null)
        {
            clearButton.interactable = true;
            clearButton.onClick.RemoveAllListeners();
            clearButton.onClick.AddListener(StartGameViaButton);
            ColorBlock colors = clearButton.colors;
            colors.normalColor = startButtonColor;
            clearButton.colors = colors;
            TextMeshProUGUI text = clearButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = "START";
        }
    }
    
    void SetupGameButtons()
    {
        if (stormButton != null)
        {
            stormButton.onClick.RemoveAllListeners();
            stormButton.onClick.AddListener(() => OnPrediction(true));
            ColorBlock colors = stormButton.colors;
            colors.normalColor = stormColor;
            stormButton.colors = colors;
            TextMeshProUGUI text = stormButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = "STORM";
        }
        if (clearButton != null)
        {
            clearButton.onClick.RemoveAllListeners();
            clearButton.onClick.AddListener(() => OnPrediction(false));
            ColorBlock colors = clearButton.colors;
            colors.normalColor = clearColor;
            clearButton.colors = colors;
            TextMeshProUGUI text = clearButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = "CLEAR";
        }
    }
    
    void SetResponseButtons(bool active)
    {
        if (stormButton != null) stormButton.interactable = active;
        if (clearButton != null) clearButton.interactable = active;
    }
    
    public void StartGameViaButton()
    {
        if (gameActive) return;
        SetupGameButtons();
        if (feedbackText != null) feedbackText.text = "";
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
        gameLog.Add("Pattern Game Started: " + System.DateTime.Now.ToString());
#if !UNITY_WEBGL
        if (udp != null) udp.SendMarker("Pattern_Start");
#endif
        if (instructionsText != null) { instructionsText.text = "Predict STORM or CLEAR based on the symbols"; instructionsText.color = Color.white; }
        for (currentTrial = 0; currentTrial < totalTrials; currentTrial++)
            yield return StartCoroutine(RunTrial(currentTrial));
        EndGame();
    }
    
    IEnumerator RunTrial(int trialNumber)
    {
        GenerateSymbols();
        if (feedbackText != null) { feedbackText.text = $"Forecast {trialNumber + 1} of {totalTrials}"; feedbackText.color = Color.white; }
        yield return new WaitForSeconds(1.0f);
        if (feedbackText != null) feedbackText.text = "";
        symbolAppearanceTime = Time.time;
#if !UNITY_WEBGL
        if (udp != null) udp.SendMarker("Pattern_TrialStart");
#endif
        yield return StartCoroutine(ShowSymbols());
        acceptingResponses = true;
        SetResponseButtons(true);
        if (feedbackText != null) { feedbackText.text = "Make your prediction..."; feedbackText.color = Color.yellow; }
        while (acceptingResponses) yield return null;
        if (progressBar != null) progressBar.value = trialNumber + 1;
        if (scoreText != null) scoreText.text = $"Score: {score}/{trialNumber + 1}";
        accuracyOverTime.Add((float)score / (trialNumber + 1) * 100f);
        yield return new WaitForSeconds(interTrialDelay);
    }
    
    void GenerateSymbols()
    {
        for (int i = 0; i < 3; i++) currentSymbols[i] = Random.Range(0, symbols.Length);
        int firstSymbol = currentSymbols[0];
        float clearProbability = symbolRules[firstSymbol];
        correctPrediction = (Random.value > clearProbability);
        symbolTypeCount[firstSymbol]++;
        gameLog.Add($"Trial {currentTrial}: Symbols [{currentSymbols[0]},{currentSymbols[1]},{currentSymbols[2]}], Correct: {(correctPrediction ? "STORM" : "CLEAR")}");
    }
    
    IEnumerator ShowSymbols()
    {
        foreach (Image card in symbolCards) if (card != null) card.gameObject.SetActive(true);
        for (int i = 0; i < symbolCards.Length; i++)
            if (symbolCards[i] != null && currentSymbols[i] < symbols.Length)
                symbolCards[i].sprite = symbols[currentSymbols[i]];
        yield return new WaitForSeconds(symbolDisplayTime);
        foreach (Image card in symbolCards) if (card != null) card.gameObject.SetActive(false);
    }
    
    void OnPrediction(bool predictedStorm)
    {
        if (!gameActive || !acceptingResponses) return;
        float reactionTime = Time.time - symbolAppearanceTime;
        acceptingResponses = false;
        SetResponseButtons(false);
        playerPrediction = predictedStorm;
        bool isCorrect = (playerPrediction == correctPrediction);
#if !UNITY_WEBGL
        if (udp != null)
        {
            udp.SendMarker(isCorrect ? "Pattern_Correct" : "Pattern_Incorrect");
            udp.SendMarker($"Pattern_RT:{Mathf.RoundToInt(reactionTime * 1000f)}");
            udp.SendMarker("Pattern_TrialEnd");
        }
#endif
        if (isCorrect)
        {
            score++;
            correctBySymbol[currentSymbols[0]]++;
            if (feedbackText != null) { feedbackText.text = $"Correct! Outcome was {(correctPrediction ? "STORM" : "CLEAR")}"; feedbackText.color = correctColor; }
        }
        else
        {
            if (feedbackText != null) { feedbackText.text = $"Wrong! Outcome was {(correctPrediction ? "STORM" : "CLEAR")}"; feedbackText.color = wrongColor; }
        }
        gameLog.Add($"Prediction: {(predictedStorm ? "STORM" : "CLEAR")}, Correct: {isCorrect}, RT: {reactionTime:F2}s");
        StartCoroutine(ShowButtonFeedback(isCorrect));
    }
    
    IEnumerator ShowButtonFeedback(bool isCorrect)
    {
        Button correctButton = correctPrediction ? stormButton : clearButton;
        if (correctButton != null)
        {
            ColorBlock colors = correctButton.colors;
            Color original = colors.normalColor;
            colors.normalColor = isCorrect ? correctColor : wrongColor;
            correctButton.colors = colors;
            yield return new WaitForSeconds(feedbackTime);
            colors.normalColor = original;
            correctButton.colors = colors;
        }
    }

    void EndGame()
    {
        gameActive = false;
        acceptingResponses = false;
        SetResponseButtons(false);
        float finalAccuracy = (float)score / totalTrials * 100f;
        float firstHalfAccuracy = 0f, secondHalfAccuracy = 0f;
        int halfPoint = totalTrials / 2;
        if (accuracyOverTime.Count >= totalTrials)
        {
            firstHalfAccuracy = accuracyOverTime[halfPoint - 1];
            secondHalfAccuracy = accuracyOverTime[totalTrials - 1];
        }
        float learningImprovement = secondHalfAccuracy - firstHalfAccuracy;
        string symbolAccuracy = "";
        for (int i = 0; i < 3; i++)
            if (symbolTypeCount[i] > 0)
                symbolAccuracy += $"\nSymbol {i + 1}: {((float)correctBySymbol[i] / symbolTypeCount[i] * 100f):F1}%";
#if !UNITY_WEBGL
        if (udp != null)
        {
            udp.SendMarker("Pattern_End");
            udp.SendMarker($"Pattern_Score:{finalAccuracy:F1}");
            udp.SendMarker($"Pattern_Learning:{learningImprovement:F1}");
        }
#endif

        // Hide all game UI
        if (stormButton != null) stormButton.gameObject.SetActive(false);
        if (clearButton != null) clearButton.gameObject.SetActive(false);
        foreach (Image card in symbolCards) if (card != null) card.gameObject.SetActive(false);
        if (progressBar != null) progressBar.gameObject.SetActive(false);
        if (instructionsText != null) instructionsText.gameObject.SetActive(false);
        timerText.gameObject.SetActive(false);
        scoreText.gameObject.SetActive(false);

        string learningText = "";
        if (learningImprovement > 10f) learningText = " (Excellent learning!)";
        else if (learningImprovement > 5f) learningText = " (Good learning!)";
        else if (learningImprovement > 0f) learningText = " (Some learning)";
        else learningText = " (Pattern was challenging)";
        
        feedbackText.text =
            $"TEST COMPLETE\n\n" +
            $"Overall Accuracy: {finalAccuracy:F1}%\n" +
            $"Learning Improvement: +{learningImprovement:F1}%{learningText}\n\n" +
            $"Pattern Detection:{symbolAccuracy}";
        feedbackText.fontSize = 48;
        feedbackText.color = Color.cyan;
        feedbackText.alignment = TextAlignmentOptions.Center;

        // Force center alignment
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
        string filePath = Application.persistentDataPath + "/PatternData.txt";
        string data = "=== PATTERN RECOGNITION RESULTS ===\n";
        data += "Date: " + System.DateTime.Now + "\n";
        data += "Total Trials: " + totalTrials + "\n";
        data += "Score: " + score + "/" + totalTrials + "\n";
        data += "Overall Accuracy: " + ((float)score / totalTrials * 100f).ToString("F1") + "%\n";
        data += "\nLearning Curve (Accuracy over time):\n";
        for (int i = 0; i < accuracyOverTime.Count; i += 5)
            if (i < accuracyOverTime.Count) data += $"Trial {i+1}: {accuracyOverTime[i]:F1}%\n";
        data += "\nSymbol-Specific Accuracy:\n";
        for (int i = 0; i < 3; i++)
            if (symbolTypeCount[i] > 0)
                data += $"Symbol {i+1}: {((float)correctBySymbol[i] / symbolTypeCount[i] * 100f).ToString("F1")}% ({correctBySymbol[i]}/{symbolTypeCount[i]})\n";
        data += "\nTrial Log:\n";
        foreach (string log in gameLog) data += log + "\n";
        System.IO.File.WriteAllText(filePath, data);
        Debug.Log("Pattern data saved to: " + filePath);
    }
}